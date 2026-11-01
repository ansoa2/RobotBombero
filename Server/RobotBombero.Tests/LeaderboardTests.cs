using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using RobotBombero.Simulation;
using Xunit;

namespace RobotBombero.Tests
{
    public class LeaderboardTests : GameApiTestBase
    {
        private const string Older = "2026-01-01T00:00:00.0000000Z";
        private const string Newer = "2026-02-01T00:00:00.0000000Z";

        private async Task<JsonElement[]> Board()
        {
            var r = await Client.GetAsync("/api/leaderboard");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            return j.EnumerateArray().ToArray();
        }

        private static long[] Points(JsonElement[] board)
        {
            return board.Select(e => e.GetProperty("points").GetInt64()).ToArray();
        }

        /// <summary>Llena el top con jugadores distintos, un pack cada uno: 1000, 1010, 1020... puntos.</summary>
        private void Fill(int count = 100, string date = Older)
        {
            using (var c = OpenDb())
            using (var tx = c.BeginTransaction(deferred: false))
            {
                for (int i = 0; i < count; i++)
                {
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "INSERT INTO JUGADOR (NOMBRE,HUELLA_CLAVE,FECHA_CREACION) VALUES ($n,$n,$f);" +
                            "INSERT INTO PACK (ID_JUGADOR,NOMBRE,REGLAS_HASH,FECHA_CREACION) VALUES (last_insert_rowid(),$n,$h,$f);" +
                            "INSERT INTO PARTIDA (ID_PACK,SEMILLA,REGLAS_JSON,REGLAS_HASH,ESTADO,CADUCA,PASOS,FUEGOS,PUNTOS,CAUSA_MUERTE,FECHA) " +
                            "VALUES (last_insert_rowid(),1,'[]',$h,'TERMINADA','2999-01-01',5,1,$p,'Battery',$f);" +
                            "INSERT INTO CLASIFICACION (ID_JUGADOR,ID_PACK,ID_PARTIDA,PUNTOS,PASOS,FUEGOS,FECHA) " +
                            "SELECT k.ID_JUGADOR,p.ID_PACK,p.ID_PARTIDA,$p,5,1,$f FROM PARTIDA p JOIN PACK k ON k.ID_PACK=p.ID_PACK WHERE p.ID_PARTIDA=last_insert_rowid();";
                        cmd.Parameters.AddWithValue("$n", "Relleno" + i);
                        cmd.Parameters.AddWithValue("$h", Hash(100000 + i));
                        cmd.Parameters.AddWithValue("$p", 1000L + i * 10);
                        cmd.Parameters.AddWithValue("$f", date);
                        cmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
        }

        private async Task EditRules(string key, long pack, string newHash)
        {
            var r = await Send(HttpMethod.Put, "/api/packs/" + pack, key, new { name = "Editado", rulesHash = newHash });
            Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        }

        private async Task DeletePack(string key, long pack)
        {
            var r = await Send(HttpMethod.Delete, "/api/packs/" + pack, key);
            Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        }

        [Fact]
        public async Task Leaderboard_Empty_ReturnsEmptyList()
        {
            Assert.Empty(await Board());
        }

        [Fact]
        public async Task Leaderboard_SamePack_KeepsBest()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", RulesHash.Compute(Basicas()));

            var low = await PlayWithSeed(key, pack, Basicas(), LowSeed);
            var high = await PlayWithSeed(key, pack, Basicas(), HighSeed);

            Assert.Equal(LowPoints, low.GetProperty("points").GetInt64());
            Assert.Equal(HighPoints, high.GetProperty("points").GetInt64());
            Assert.Equal(1, high.GetProperty("rank").GetInt32());
            Assert.Equal(new[] { HighPoints }, Points(await Board()));
        }

        [Fact]
        public async Task Leaderboard_WorseGame_NoChange()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", RulesHash.Compute(Basicas()));
            await PlayWithSeed(key, pack, Basicas(), HighSeed);
            long partida = Scalar("SELECT ID_PARTIDA FROM CLASIFICACION");
            long fila = Scalar("SELECT ID_CLASIFICACION FROM CLASIFICACION");

            var worse = await PlayWithSeed(key, pack, Basicas(), LowSeed);

            Assert.Equal(LowPoints, worse.GetProperty("points").GetInt64());
            Assert.Equal(JsonValueKind.Null, worse.GetProperty("rank").ValueKind);
            Assert.Equal(new[] { HighPoints }, Points(await Board()));
            Assert.Equal(partida, Scalar("SELECT ID_PARTIDA FROM CLASIFICACION"));
            Assert.Equal(fila, Scalar("SELECT ID_CLASIFICACION FROM CLASIFICACION"));
        }

        [Fact]
        public async Task Leaderboard_OnePlayerOnce_AcrossPacks()
        {
            var (_, key) = await NewPlayer("Ana");
            long a = await NewPack(key, "A", RulesHash.Compute(Basicas()));
            long b = await NewPack(key, "B", RulesHash.Compute(Variante()));
            long sameRules = await NewPack(key, "C", RulesHash.Compute(Basicas()));

            await PlayWithSeed(key, a, Basicas(), LowSeed);
            await PlayWithSeed(key, b, Variante(), LowSeed);
            await PlayWithSeed(key, sameRules, Basicas(), HighSeed);

            var board = await Board();
            var row = Assert.Single(board);
            Assert.Equal("Ana", row.GetProperty("playerName").GetString());
            Assert.Equal(HighPoints, row.GetProperty("points").GetInt64());
            Assert.Equal("C", row.GetProperty("packName").GetString());
        }

        [Fact]
        public async Task Leaderboard_OtherPackBetterGame_ReplacesRow()
        {
            var (_, key) = await NewPlayer("Ana");
            long a = await NewPack(key, "A", RulesHash.Compute(Basicas()));
            long b = await NewPack(key, "B", RulesHash.Compute(Variante()));
            await PlayWithSeed(key, a, Basicas(), LowSeed);

            var better = await PlayWithSeed(key, b, Variante(), HighSeed);

            Assert.Equal(1, better.GetProperty("rank").GetInt32());
            Assert.Equal(b, Scalar("SELECT ID_PACK FROM CLASIFICACION"));
            Assert.Equal(new[] { HighPoints }, Points(await Board()));
        }

        [Fact]
        public async Task Leaderboard_OtherPackWorseGame_NoChange()
        {
            var (_, key) = await NewPlayer("Ana");
            long a = await NewPack(key, "A", RulesHash.Compute(Basicas()));
            long b = await NewPack(key, "B", RulesHash.Compute(Variante()));
            await PlayWithSeed(key, a, Basicas(), HighSeed);
            long fila = Scalar("SELECT ID_CLASIFICACION FROM CLASIFICACION");

            var worse = await PlayWithSeed(key, b, Variante(), LowSeed);

            Assert.Equal(JsonValueKind.Null, worse.GetProperty("rank").ValueKind);
            Assert.Equal(a, Scalar("SELECT ID_PACK FROM CLASIFICACION"));
            Assert.Equal(fila, Scalar("SELECT ID_CLASIFICACION FROM CLASIFICACION"));
            Assert.Equal(new[] { HighPoints }, Points(await Board()));
        }

        [Fact]
        public async Task Leaderboard_IdenticalRules_OfOtherPlayer_BothCount()
        {
            var (_, keyA) = await NewPlayer("Ana");
            var (_, keyB) = await NewPlayer("Beto");
            string hash = RulesHash.Compute(Basicas());
            long packA = await NewPack(keyA, "A", hash);
            long packB = await NewPack(keyB, "B", hash);

            await PlayWithSeed(keyA, packA, Basicas(), HighSeed);
            await PlayWithSeed(keyB, packB, Basicas(), LowSeed);

            Assert.Equal(new[] { HighPoints, LowPoints }, Points(await Board()));
        }

        [Fact]
        public async Task EditingRowPack_FallsBackToBestOtherPack()
        {
            var (id, key) = await NewPlayer("Ana");
            long worst = InsertPack(id, "Peor", Hash(1));
            long second = InsertPack(id, "Segundo", Hash(2));
            long top = InsertPack(id, "Mejor", Hash(3));
            Record(worst, Hash(1), 100);
            long expected = Record(second, Hash(2), 200);
            Record(top, Hash(3), 300);
            Assert.Equal(top, Scalar("SELECT ID_PACK FROM CLASIFICACION"));

            await EditRules(key, top, Hash(4));

            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
            Assert.Equal(expected, Scalar("SELECT ID_PARTIDA FROM CLASIFICACION"));
            Assert.Equal(new[] { 200L }, Points(await Board()));
        }

        [Fact]
        public async Task StartingWithNewRules_OnRowPack_FallsBackToBestOtherPack()
        {
            var (_, key) = await NewPlayer("Ana");
            long a = await NewPack(key, "A", RulesHash.Compute(Basicas()));
            long b = await NewPack(key, "B", RulesHash.Compute(Basicas()));
            await PlayWithSeed(key, a, Basicas(), LowSeed);
            await PlayWithSeed(key, b, Basicas(), HighSeed);
            Assert.Equal(b, Scalar("SELECT ID_PACK FROM CLASIFICACION"));

            await StartOk(key, b, Variante());

            Assert.Equal(a, Scalar("SELECT ID_PACK FROM CLASIFICACION"));
            Assert.Equal(new[] { LowPoints }, Points(await Board()));
        }

        [Fact]
        public async Task DeletingRowPack_FallsBackToBestOtherPack()
        {
            var (id, key) = await NewPlayer("Ana");
            long a = InsertPack(id, "A", Hash(1));
            long b = InsertPack(id, "B", Hash(2));
            long expected = Record(a, Hash(1), 100);
            Record(b, Hash(2), 300);

            await DeletePack(key, b);

            Assert.Equal(expected, Scalar("SELECT ID_PARTIDA FROM CLASIFICACION"));
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM PACK WHERE ID_PACK=" + b));
            Assert.Equal(new[] { 100L }, Points(await Board()));
        }

        [Fact]
        public async Task DeletingRowPack_OwnGamesAreNotTheFallback()
        {
            var (id, key) = await NewPlayer("Ana");
            long pack = InsertPack(id, "A", Hash(1));
            Record(pack, Hash(1), 100);
            Record(pack, Hash(1), 300);

            await DeletePack(key, pack);

            Assert.Empty(await Board());
        }

        [Fact]
        public async Task EditingRowPack_NoOtherGames_RemovesRow()
        {
            var (id, key) = await NewPlayer("Ana");
            long pack = InsertPack(id, "A", Hash(1));
            Record(pack, Hash(1), 100);

            await EditRules(key, pack, Hash(2));

            Assert.Empty(await Board());
        }

        [Fact]
        public async Task EditingRowPack_RulesBackToEarlierOnes_ItsOwnGameCountsAgain()
        {
            var (id, key) = await NewPlayer("Ana");
            long pack = InsertPack(id, "A", Hash(1));
            long old = Record(pack, Hash(1), 100);
            await EditRules(key, pack, Hash(2));
            Record(pack, Hash(2), 300);

            await EditRules(key, pack, Hash(1));

            Assert.Equal(old, Scalar("SELECT ID_PARTIDA FROM CLASIFICACION"));
        }

        [Fact]
        public async Task EditingRowPack_FallbackIgnoresStaleGames()
        {
            var (id, key) = await NewPlayer("Ana");
            long a = InsertPack(id, "A", Hash(1));
            long b = InsertPack(id, "B", Hash(2));
            Record(a, Hash(1), 100);
            Record(b, Hash(2), 300);
            await EditRules(key, a, Hash(3)); // la partida de A queda obsoleta, sin tocar la fila de B

            await EditRules(key, b, Hash(4));

            Assert.Empty(await Board());
        }

        [Fact]
        public async Task EditingOtherPack_KeepsRow()
        {
            var (id, key) = await NewPlayer("Ana");
            long a = InsertPack(id, "A", Hash(1));
            long b = InsertPack(id, "B", Hash(2));
            long c = InsertPack(id, "C", Hash(3));
            Record(a, Hash(1), 100);
            Record(c, Hash(3), 50);
            long kept = Record(b, Hash(2), 300);
            long fila = Scalar("SELECT ID_CLASIFICACION FROM CLASIFICACION");

            await EditRules(key, a, Hash(4));
            await DeletePack(key, c);

            Assert.Equal(kept, Scalar("SELECT ID_PARTIDA FROM CLASIFICACION"));
            Assert.Equal(fila, Scalar("SELECT ID_CLASIFICACION FROM CLASIFICACION"));
        }

        [Fact]
        public async Task RenamingRowPack_KeepsRow()
        {
            var (id, key) = await NewPlayer("Ana");
            long a = InsertPack(id, "A", Hash(1));
            long b = InsertPack(id, "B", Hash(2));
            Record(a, Hash(1), 100);
            long kept = Record(b, Hash(2), 300);

            await EditRules(key, b, Hash(2));

            Assert.Equal(kept, Scalar("SELECT ID_PARTIDA FROM CLASIFICACION"));
            Assert.Equal("Editado", Assert.Single(await Board()).GetProperty("packName").GetString());
        }

        [Fact]
        public async Task Leaderboard_FullBoard_FallbackBelowLast_NotAdded()
        {
            Fill(99); // con Ana suman 100; el último de los demás tiene 1000
            var (id, key) = await NewPlayer("Ana");
            long a = InsertPack(id, "A", Hash(1));
            long b = InsertPack(id, "B", Hash(2));
            Record(a, Hash(1), 500);
            Record(b, Hash(2), 1500);
            Assert.Equal(100, (await Board()).Length);

            await EditRules(key, b, Hash(3));

            var board = await Board();
            Assert.Equal(99, board.Length);
            Assert.DoesNotContain(board, e => e.GetProperty("playerName").GetString() == "Ana");
        }

        [Fact]
        public async Task Leaderboard_FullBoard_FallbackAboveLast_Added()
        {
            Fill(99);
            var (id, key) = await NewPlayer("Ana");
            long a = InsertPack(id, "A", Hash(1));
            long b = InsertPack(id, "B", Hash(2));
            long expected = Record(a, Hash(1), 1005);
            Record(b, Hash(2), 1500);

            await DeletePack(key, b);

            Assert.Equal(100, (await Board()).Length);
            Assert.Equal(expected, Scalar("SELECT ID_PARTIDA FROM CLASIFICACION WHERE ID_JUGADOR=" + id));
        }

        [Fact]
        public async Task Leaderboard_FreePlaces_FallbackBelowLast_Added()
        {
            Fill(5);
            var (id, key) = await NewPlayer("Ana");
            long a = InsertPack(id, "A", Hash(1));
            long b = InsertPack(id, "B", Hash(2));
            long expected = Record(a, Hash(1), 500);
            Record(b, Hash(2), 1500);

            await EditRules(key, b, Hash(3));

            Assert.Equal(6, (await Board()).Length);
            Assert.Equal(expected, Scalar("SELECT ID_PARTIDA FROM CLASIFICACION WHERE ID_JUGADOR=" + id));
        }

        [Fact]
        public async Task Leaderboard_Keeps100_DropsLowest()
        {
            Fill();
            var (id, _) = await NewPlayer("Ana");
            long pack = InsertPack(id, "Nuevo", Hash(1));

            long partida = Record(pack, Hash(1), 1005, Newer);

            var board = await Board();
            Assert.Equal(100, board.Length);
            Assert.DoesNotContain(1000L, Points(board));
            Assert.Contains(1005L, Points(board));
            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM CLASIFICACION WHERE ID_PARTIDA=" + partida));
            Assert.Equal(100, board[99].GetProperty("rank").GetInt32());
            Assert.Equal(1005L, board[99].GetProperty("points").GetInt64());
        }

        [Fact]
        public async Task Leaderboard_LowerThan100th_NotAdded()
        {
            Fill();
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Nuevo", RulesHash.Compute(Basicas()));

            var j = await PlayWithSeed(key, pack, Basicas(), HighSeed);

            Assert.Equal(HighPoints, j.GetProperty("points").GetInt64());
            Assert.Equal(JsonValueKind.Null, j.GetProperty("rank").ValueKind);
            Assert.Equal(100, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM CLASIFICACION WHERE ID_PACK=" + pack));
            Assert.Equal(1000, Scalar("SELECT MIN(PUNTOS) FROM CLASIFICACION"));
        }

        [Fact]
        public async Task Leaderboard_TiedWith100th_NewerGame_NotAdded()
        {
            Fill();
            var (id, _) = await NewPlayer("Ana");
            long pack = InsertPack(id, "Nuevo", Hash(1));

            Record(pack, Hash(1), 1000, Newer);

            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM CLASIFICACION WHERE ID_PACK=" + pack));
            Assert.Equal(100, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
        }

        [Fact]
        public async Task Leaderboard_TiedWith100th_OlderGame_TakesThePlace()
        {
            Fill(100, Newer);
            var (id, _) = await NewPlayer("Ana");
            long pack = InsertPack(id, "Nuevo", Hash(1));

            Record(pack, Hash(1), 1000, Older);

            var board = await Board();
            Assert.Equal(100, board.Length);
            Assert.Equal("Nuevo", board[99].GetProperty("packName").GetString());
        }

        [Fact]
        public async Task Leaderboard_SamePlayerImproves_WhenFull_StaysAt100()
        {
            Fill();
            long worstPack = Scalar("SELECT ID_PACK FROM CLASIFICACION WHERE PUNTOS=1000");

            Record(worstPack, Hash(100000), 5000, Newer);

            var board = await Board();
            Assert.Equal(100, board.Length);
            Assert.Equal(5000L, board[0].GetProperty("points").GetInt64());
            Assert.Equal(1010L, board[99].GetProperty("points").GetInt64());
        }

        [Fact]
        public async Task Leaderboard_Ties_OlderFirst()
        {
            long newerPack = InsertPack(InsertPlayer("Reciente"), "Reciente", Hash(1));
            long olderPack = InsertPack(InsertPlayer("Antiguo"), "Antiguo", Hash(2));
            long sameDateA = InsertPack(InsertPlayer("MismaFechaA"), "MismaFechaA", Hash(3));
            long sameDateB = InsertPack(InsertPlayer("MismaFechaB"), "MismaFechaB", Hash(4));
            long topPack = InsertPack(InsertPlayer("Mejor"), "Mejor", Hash(5));

            // Se inserta primero la más reciente: el orden no puede salir del orden de inserción.
            Record(newerPack, Hash(1), 100, Newer);
            Record(olderPack, Hash(2), 100, Older);
            Record(sameDateA, Hash(3), 100, Newer);
            Record(sameDateB, Hash(4), 100, Newer);
            Record(topPack, Hash(5), 200, Newer);

            var board = await Board();
            Assert.Equal(
                new[] { "Mejor", "Antiguo", "Reciente", "MismaFechaA", "MismaFechaB" },
                board.Select(e => e.GetProperty("packName").GetString()).ToArray());
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, board.Select(e => e.GetProperty("rank").GetInt32()).ToArray());
        }

        [Fact]
        public async Task Leaderboard_SamePlayer_TiedNewerGame_KeepsOlderRow()
        {
            var (id, _) = await NewPlayer("Ana");
            long pack = InsertPack(id, "Pack", Hash(1));
            long first = Record(pack, Hash(1), 100, Older);

            Record(pack, Hash(1), 100, Newer);

            Assert.Equal(first, Scalar("SELECT ID_PARTIDA FROM CLASIFICACION"));
            Assert.Single(await Board());
        }

        [Fact]
        public async Task Leaderboard_ZeroPoints_NotAdded_EvenWithFreePlaces()
        {
            var (_, key) = await NewPlayer("Ana");
            var rules = new[] { R("Anyone", "Anyone", "Anyone", "Anyone", "Anyone", "TurnL") };
            long pack = await NewPack(key, "Pack", RulesHash.Compute(rules));

            var (game, _) = await StartOk(key, pack, rules);
            var j = await FinishOk(key, game);

            Assert.Equal(0L, j.GetProperty("points").GetInt64());
            Assert.True(j.GetProperty("steps").GetInt32() > 0);
            Assert.Equal(JsonValueKind.Null, j.GetProperty("rank").ValueKind);
            Assert.Empty(await Board());
        }

        [Fact]
        public async Task Finish_Rank_OnlyWhenThisGameHoldsTheRow_ComputedLive()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", RulesHash.Compute(Basicas()));
            var (first, _) = await StartOk(key, pack, Basicas());
            Exec("UPDATE PARTIDA SET SEMILLA=" + LowSeed + " WHERE ID_PARTIDA=" + first);
            var original = await FinishOk(key, first);
            Assert.Equal(1, original.GetProperty("rank").GetInt32());

            // Otro jugador la adelanta: el puesto se calcula al momento.
            long rival = InsertPack(InsertPlayer("Rival"), "Rival", Hash(9));
            Record(rival, Hash(9), LowPoints + 1);
            Assert.Equal(2, (await FinishOk(key, first)).GetProperty("rank").GetInt32());

            // Una partida mejor del mismo jugador le quita la fila.
            await PlayWithSeed(key, pack, Basicas(), HighSeed);
            var repeated = await FinishOk(key, first);

            Assert.Equal(JsonValueKind.Null, repeated.GetProperty("rank").ValueKind);
            foreach (string field in new[] { "steps", "fires", "points", "endReason" })
            {
                Assert.Equal(original.GetProperty(field).GetRawText(), repeated.GetProperty(field).GetRawText());
            }
        }

        [Fact]
        public async Task Leaderboard_PointsAboveInt32_NotTruncated()
        {
            var (id, key) = await NewPlayer("Ana");
            long big = InsertPack(id, "Grande", Hash(1));
            long small = InsertPack(InsertPlayer("Beto"), "Chico", Hash(2));
            long huge = 5000000000L;

            Record(small, Hash(2), 2000000000L, Newer);
            long partida = Record(big, Hash(1), huge, Newer);

            var board = await Board();
            Assert.Equal(new[] { huge, 2000000000L }, Points(board));

            // También al repetir finish de una partida ya terminada.
            var r = await Finish(key, partida);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(huge, j.GetProperty("points").GetInt64());
            Assert.Equal(1, j.GetProperty("rank").GetInt32());
        }

        [Fact]
        public async Task Leaderboard_Exposes_NoKeyOrPersonalData()
        {
            var (id, key) = await NewPlayer("Ana");
            string hash = RulesHash.Compute(Basicas());
            long pack = await NewPack(key, "MiPack", hash);
            await PlayWithSeed(key, pack, Basicas(), HighSeed);

            var r = await Client.GetAsync("/api/leaderboard");
            string raw = await r.Content.ReadAsStringAsync();

            var row = JsonSerializer.Deserialize<JsonElement>(raw).EnumerateArray().Single();
            Assert.Equal(
                new[] { "date", "fires", "packName", "playerName", "points", "rank", "steps" },
                row.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
            Assert.Equal(1, row.GetProperty("rank").GetInt32());
            Assert.Equal("Ana", row.GetProperty("playerName").GetString());
            Assert.Equal("MiPack", row.GetProperty("packName").GetString());
            Assert.Equal(HighPoints, row.GetProperty("points").GetInt64());
            Assert.Equal(44, row.GetProperty("steps").GetInt32());
            Assert.Equal(5, row.GetProperty("fires").GetInt32());
            Assert.False(string.IsNullOrEmpty(row.GetProperty("date").GetString()));

            string fingerprint = ScalarText("SELECT HUELLA_CLAVE FROM JUGADOR WHERE ID_JUGADOR=" + id);
            Assert.DoesNotContain(key, raw);
            Assert.DoesNotContain(fingerprint, raw);
            Assert.DoesNotContain(hash, raw);
        }

        [Fact]
        public async Task Update_UnfinishedGame_DoesNothing()
        {
            var (id, _) = await NewPlayer("Ana");
            long pack = InsertPack(id, "Pack", Hash(1));
            long partida = InsertFinished(pack, Hash(1), 100, Older);
            Exec("UPDATE PARTIDA SET ESTADO='EN_CURSO', PUNTOS=NULL, PASOS=NULL, FUEGOS=NULL WHERE ID_PARTIDA=" + partida);

            ApplyUpdate(partida);

            Assert.Empty(await Board());
        }
    }
}
