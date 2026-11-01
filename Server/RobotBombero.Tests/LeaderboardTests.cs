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

        /// <summary>Llena el top con 100 packs de reglas distintas: 1000, 1010, ... 1990 puntos.</summary>
        private void Fill100(long playerId, string date = Older)
        {
            using (var c = OpenDb())
            using (var tx = c.BeginTransaction(deferred: false))
            {
                for (int i = 0; i < 100; i++)
                {
                    long points = 1000 + i * 10;
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "INSERT INTO PACK (ID_JUGADOR,NOMBRE,REGLAS_HASH,FECHA_CREACION) VALUES ($j,$n,$h,$f);" +
                            "INSERT INTO PARTIDA (ID_PACK,SEMILLA,REGLAS_JSON,REGLAS_HASH,ESTADO,CADUCA,PASOS,FUEGOS,PUNTOS,CAUSA_MUERTE,FECHA) " +
                            "VALUES (last_insert_rowid(),1,'[]',$h,'TERMINADA','2999-01-01',5,1,$p,'Battery',$f);" +
                            "INSERT INTO CLASIFICACION (ID_JUGADOR,ID_PACK,ID_PARTIDA,PUNTOS,PASOS,FUEGOS,FECHA) " +
                            "SELECT $j,ID_PACK,ID_PARTIDA,$p,5,1,$f FROM PARTIDA WHERE ID_PARTIDA=last_insert_rowid();";
                        cmd.Parameters.AddWithValue("$j", playerId);
                        cmd.Parameters.AddWithValue("$n", "Relleno" + i);
                        cmd.Parameters.AddWithValue("$h", Hash(100000 + i));
                        cmd.Parameters.AddWithValue("$p", points);
                        cmd.Parameters.AddWithValue("$f", date);
                        cmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
        }

        [Fact]
        public async Task Leaderboard_Empty_ReturnsEmptyList()
        {
            Assert.Empty(await Board());
        }

        [Fact]
        public async Task Leaderboard_OneRowPerPack_KeepsBest()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", RulesHash.Compute(Basicas()));

            var low = await PlayWithSeed(key, pack, Basicas(), LowSeed);
            var high = await PlayWithSeed(key, pack, Basicas(), HighSeed);

            Assert.Equal(LowPoints, low.GetProperty("points").GetInt64());
            Assert.Equal(HighPoints, high.GetProperty("points").GetInt64());
            Assert.Equal(1, high.GetProperty("rank").GetInt32());
            var board = await Board();
            Assert.Equal(new[] { HighPoints }, Points(board));
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
            // El pack sigue en el top con su mejor marca, y rank es la posición del pack.
            Assert.Equal(1, worse.GetProperty("rank").GetInt32());
            Assert.Equal(new[] { HighPoints }, Points(await Board()));
            Assert.Equal(partida, Scalar("SELECT ID_PARTIDA FROM CLASIFICACION"));
            Assert.Equal(fila, Scalar("SELECT ID_CLASIFICACION FROM CLASIFICACION"));
        }

        [Fact]
        public async Task Leaderboard_ImportedIdenticalRules_OnlyBestCounts()
        {
            var (_, key) = await NewPlayer("Ana");
            string hash = RulesHash.Compute(Basicas());
            long original = await NewPack(key, "Original", hash);
            long imported = await NewPack(key, "Importado", hash);

            await PlayWithSeed(key, original, Basicas(), LowSeed);
            Assert.Equal(original, Scalar("SELECT ID_PACK FROM CLASIFICACION"));

            // La copia supera a la marca del original: lo sustituye.
            await PlayWithSeed(key, imported, Basicas(), HighSeed);
            Assert.Equal(imported, Scalar("SELECT ID_PACK FROM CLASIFICACION"));
            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));

            // Una marca peor con el original no entra ni desplaza a la copia.
            var worse = await PlayWithSeed(key, original, Basicas(), LowSeed);
            Assert.Equal(imported, Scalar("SELECT ID_PACK FROM CLASIFICACION"));
            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
            Assert.Equal(JsonValueKind.Null, worse.GetProperty("rank").ValueKind);
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
        public async Task Leaderboard_Keeps100_DropsLowest()
        {
            var (id, key) = await NewPlayer("Ana");
            Fill100(id);
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
            var (id, key) = await NewPlayer("Ana");
            Fill100(id);
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
            var (id, _) = await NewPlayer("Ana");
            Fill100(id);
            long pack = InsertPack(id, "Nuevo", Hash(1));

            Record(pack, Hash(1), 1000, Newer);

            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM CLASIFICACION WHERE ID_PACK=" + pack));
            Assert.Equal(100, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
        }

        [Fact]
        public async Task Leaderboard_TiedWith100th_OlderGame_TakesThePlace()
        {
            var (id, _) = await NewPlayer("Ana");
            Fill100(id, Newer);
            long pack = InsertPack(id, "Nuevo", Hash(1));

            Record(pack, Hash(1), 1000, Older);

            var board = await Board();
            Assert.Equal(100, board.Length);
            Assert.Equal("Nuevo", board[99].GetProperty("packName").GetString());
        }

        [Fact]
        public async Task Leaderboard_SamePackImproves_WhenFull_StaysAt100()
        {
            var (id, _) = await NewPlayer("Ana");
            Fill100(id);
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
            var (id, _) = await NewPlayer("Ana");
            long newerPack = InsertPack(id, "Reciente", Hash(1));
            long olderPack = InsertPack(id, "Antiguo", Hash(2));
            long sameDateA = InsertPack(id, "MismaFechaA", Hash(3));
            long sameDateB = InsertPack(id, "MismaFechaB", Hash(4));
            long topPack = InsertPack(id, "Mejor", Hash(5));

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
        public async Task Leaderboard_SamePack_TiedNewerGame_KeepsOlderRow()
        {
            var (id, _) = await NewPlayer("Ana");
            long pack = InsertPack(id, "Pack", Hash(1));
            long first = Record(pack, Hash(1), 100, Older);

            Record(pack, Hash(1), 100, Newer);

            Assert.Equal(first, Scalar("SELECT ID_PARTIDA FROM CLASIFICACION"));
        }

        [Fact]
        public async Task Leaderboard_PointsAboveInt32_NotTruncated()
        {
            var (id, key) = await NewPlayer("Ana");
            long big = InsertPack(id, "Grande", Hash(1));
            long small = InsertPack(id, "Pequeño", Hash(2));
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
