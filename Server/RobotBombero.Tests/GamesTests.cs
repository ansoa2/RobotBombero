using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RobotBombero.Simulation;
using Xunit;

namespace RobotBombero.Tests
{
    public class GamesTests : GameApiTestBase
    {
        private const string ValidRule = "{\"battery\":\"Anyone\",\"temp\":\"Anyone\",\"light\":\"Anyone\",\"prox\":\"Anyone\",\"cell\":\"Anyone\",\"actions\":[\"TurnL\"]}";

        private static string ValidRules(int count)
        {
            return "[" + string.Join(",", Enumerable.Repeat(ValidRule, count)) + "]";
        }

        private static GameSimulation Simulate(Rule[] rules, uint seed)
        {
            var sim = new GameSimulation(rules, seed);
            sim.RunToEnd();
            return sim;
        }

        private static void AssertResult(JsonElement j, GameSimulation sim)
        {
            Assert.Equal(sim.Steps, j.GetProperty("steps").GetInt32());
            Assert.Equal(sim.Fires, j.GetProperty("fires").GetInt32());
            Assert.Equal(Score.Points(sim.Fires, sim.Steps), j.GetProperty("points").GetInt64());
            Assert.Equal(sim.EndReason.ToString(), j.GetProperty("endReason").GetString());
        }

        [Fact]
        public void LowAndHighSeeds_GiveDifferentPoints()
        {
            var low = Simulate(Basicas(), LowSeed);
            var high = Simulate(Basicas(), HighSeed);
            Assert.Equal(LowPoints, Score.Points(low.Fires, low.Steps));
            Assert.Equal(HighPoints, Score.Points(high.Fires, high.Steps));
        }

        [Theory]
        [InlineData("{}")] // sin packId ni reglas
        [InlineData("{\"packId\":PACK}")] // sin reglas
        [InlineData("{\"packId\":PACK,\"rules\":null}")]
        [InlineData("{\"packId\":PACK,\"rules\":[]}")]
        [InlineData("{\"packId\":PACK,\"rules\":[null]}")]
        [InlineData("{\"packId\":PACK,\"rules\":[{}]}")] // sin condiciones ni acciones
        [InlineData("{\"packId\":PACK,\"rules\":\"no\"}")]
        [InlineData("{\"packId\":PACK,\"rules\":[{\"battery\":\"Mucha\",\"temp\":\"Anyone\",\"light\":\"Anyone\",\"prox\":\"Anyone\",\"cell\":\"Anyone\",\"actions\":[\"Fwd\"]}]}")]
        [InlineData("{\"packId\":PACK,\"rules\":[{\"battery\":\"Anyone\",\"temp\":\"Anyone\",\"light\":\"Anyone\",\"prox\":\"Anyone\",\"cell\":\"Anyone\",\"actions\":[]}]}")]
        [InlineData("{\"packId\":PACK,\"rules\":[{\"battery\":\"Anyone\",\"temp\":\"Anyone\",\"light\":\"Anyone\",\"prox\":\"Anyone\",\"cell\":\"Anyone\",\"actions\":[\"Volar\"]}]}")]
        [InlineData("{\"packId\":PACK,\"rules\":[{\"battery\":\"Anyone\",\"temp\":\"Anyone\",\"light\":\"Anyone\",\"prox\":\"Anyone\",\"cell\":\"Anyone\",\"actions\":[\"Fwd\",\"Fwd\",\"Fwd\",\"Fwd\",\"Fwd\",\"Fwd\"]}]}")] // 6 acciones
        [InlineData("{no es json")]
        [InlineData("")]
        [InlineData("null")]
        public async Task Start_InvalidRules_400(string template)
        {
            var (id, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            InsertRowForPack(id, pack, Hash(1));

            var r = await SendRaw("/api/games", key, template.Replace("PACK", pack.ToString(CultureInfo.InvariantCulture)));

            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            AssertUntouched(pack, Hash(1));
        }

        [Fact]
        public async Task Start_TooManyRules_400()
        {
            var (id, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            InsertRowForPack(id, pack, Hash(1));

            var r = await SendRaw("/api/games", key, "{\"packId\":" + pack + ",\"rules\":" + ValidRules(PackLimits.MaxRules + 1) + "}");

            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            AssertUntouched(pack, Hash(1));
        }

        [Fact]
        public async Task Start_MaxRules_Accepted()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));

            var r = await SendRaw("/api/games", key, "{\"packId\":" + pack + ",\"rules\":" + ValidRules(PackLimits.MaxRules) + "}");

            Assert.True(r.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Start_BodyOverLimit_413()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            // Reglas válidas salvo por el tamaño: el nombre de una regla no se valida.
            string json = "{\"packId\":" + pack + ",\"rules\":[{\"name\":\"" + new string('x', PackLimits.MaxFileBytes) + "\",\"battery\":\"Anyone\",\"temp\":\"Anyone\",\"light\":\"Anyone\",\"prox\":\"Anyone\",\"cell\":\"Anyone\",\"actions\":[\"Fwd\"]}]}";

            var r = await SendRaw("/api/games", key, json);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.StatusCode);

            // Sin Content-Length: el límite tiene que aplicarse al leer, no solo a la cabecera.
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/games");
            req.Headers.Add("X-Player-Key", key);
            req.Content = new StreamContent(new System.IO.MemoryStream(Encoding.UTF8.GetBytes(json)));
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            r = await Client.SendAsync(req);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.StatusCode);

            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM PARTIDA"));
        }

        [Fact]
        public async Task Start_BodyExactlyAtLimit_Accepted()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            string prefix = "{\"packId\":" + pack + ",\"rules\":[{\"name\":\"";
            string suffix = "\",\"battery\":\"Anyone\",\"temp\":\"Anyone\",\"light\":\"Anyone\",\"prox\":\"Anyone\",\"cell\":\"Anyone\",\"actions\":[\"Fwd\"]}]}";
            string json = prefix + new string('x', PackLimits.MaxFileBytes - prefix.Length - suffix.Length) + suffix;

            var r = await SendRaw("/api/games", key, json);

            Assert.True(r.IsSuccessStatusCode, r.StatusCode.ToString());
        }

        [Fact]
        public async Task Start_WithoutKey_401()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));

            Assert.Equal(HttpStatusCode.Unauthorized, (await Start(null, pack, Basicas())).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Start("clave-falsa", pack, Basicas())).StatusCode);
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM PARTIDA"));
        }

        [Fact]
        public async Task Start_UnknownPack_404()
        {
            var (_, key) = await NewPlayer("Ana");
            Assert.Equal(HttpStatusCode.NotFound, (await Start(key, 999, Basicas())).StatusCode);
        }

        [Fact]
        public async Task Start_OtherPlayersPack_403()
        {
            var (_, keyA) = await NewPlayer("Ana");
            var (_, keyB) = await NewPlayer("Beto");
            long pack = await NewPack(keyA, "Pack", Hash(1));

            Assert.Equal(HttpStatusCode.Forbidden, (await Start(keyB, pack, Basicas())).StatusCode);
            AssertUntouched(pack, Hash(1), expectRow: false);
        }

        [Fact]
        public async Task Start_FixesRulesAndSeed()
        {
            var (_, key) = await NewPlayer("Ana");
            string hash = RulesHash.Compute(Basicas());
            long pack = await NewPack(key, "Pack", hash);

            var (game, seed) = await StartOk(key, pack, Basicas());
            var (other, otherSeed) = await StartOk(key, pack, Basicas());

            Assert.NotEqual(game, other);
            Assert.NotEqual(seed, otherSeed);
            Assert.Equal((long)seed, Scalar("SELECT SEMILLA FROM PARTIDA WHERE ID_PARTIDA=" + game));
            Assert.Equal(hash, ScalarText("SELECT REGLAS_HASH FROM PARTIDA WHERE ID_PARTIDA=" + game));
            Assert.Equal("EN_CURSO", ScalarText("SELECT ESTADO FROM PARTIDA WHERE ID_PARTIDA=" + game));

            // Lo guardado reproduce las reglas de verdad, no objetos vacíos.
            string stored = ScalarText("SELECT REGLAS_JSON FROM PARTIDA WHERE ID_PARTIDA=" + game);
            var back = JsonSerializer.Deserialize<Rule[]>(stored, Json);
            Assert.Equal(hash, RulesHash.Compute(back));
            Assert.Equal("Put Out", back[0].actions[0]);

            // Caduca 24 horas después, y los dos textos son UTC en formato comparable.
            DateTime start = DateTime.Parse(ScalarText("SELECT FECHA FROM PARTIDA WHERE ID_PARTIDA=" + game), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            DateTime expires = DateTime.Parse(ScalarText("SELECT CADUCA FROM PARTIDA WHERE ID_PARTIDA=" + game), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            Assert.Equal(DateTimeKind.Utc, start.Kind);
            Assert.Equal(DateTimeKind.Utc, expires.Kind);
            Assert.Equal(TimeSpan.FromHours(24), expires - start);
        }

        [Fact]
        public async Task Start_DoesNotStoreRuleNames()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            var rules = Basicas();
            foreach (Rule rule in rules) rule.name = "TextoLibreDelJugador";

            var (game, seed) = await StartOk(key, pack, rules);

            string stored = ScalarText("SELECT REGLAS_JSON FROM PARTIDA WHERE ID_PARTIDA=" + game);
            Assert.DoesNotContain("TextoLibreDelJugador", stored);
            Assert.All(JsonSerializer.Deserialize<Rule[]>(stored, Json), r => Assert.Null(r.name));
            // Los nombres no forman parte de la huella ni de la partida.
            Assert.Equal(RulesHash.Compute(Basicas()), ScalarText("SELECT REGLAS_HASH FROM PARTIDA WHERE ID_PARTIDA=" + game));
            AssertResult(await FinishOk(key, game), Simulate(Basicas(), seed));
        }

        [Fact]
        public async Task Start_RulesChanged_UpdatesHashAndRemovesRow()
        {
            var (id, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            InsertRowForPack(id, pack, Hash(1));

            await StartOk(key, pack, Basicas());

            Assert.Equal(RulesHash.Compute(Basicas()), ScalarText("SELECT REGLAS_HASH FROM PACK WHERE ID_PACK=" + pack));
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
        }

        [Fact]
        public async Task Start_SameRules_KeepsRow()
        {
            var (id, key) = await NewPlayer("Ana");
            string hash = RulesHash.Compute(Basicas());
            long pack = await NewPack(key, "Pack", hash);
            InsertRowForPack(id, pack, hash);

            await StartOk(key, pack, Basicas());

            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
        }

        [Fact]
        public async Task Finish_UsesServerSimulation_NotClientValues()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            var (game, seed) = await StartOk(key, pack, Basicas());

            var bogus = new
            {
                steps = 999999,
                fires = 999999,
                points = 999998000001L,
                endReason = "MissionComplete",
                seed = seed + 1,
                rules = Variante()
            };
            var r = await Send(HttpMethod.Post, "/api/games/" + game + "/finish", key, bogus);

            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            var expected = Simulate(Basicas(), seed);
            AssertResult(j, expected);
            Assert.True(expected.Fires > 0);
            Assert.Equal(Score.Points(expected.Fires, expected.Steps), Scalar("SELECT PUNTOS FROM CLASIFICACION"));
            Assert.Equal(expected.Steps, Scalar("SELECT PASOS FROM PARTIDA WHERE ID_PARTIDA=" + game));
            Assert.Equal(expected.Fires, Scalar("SELECT FUEGOS FROM PARTIDA WHERE ID_PARTIDA=" + game));
            Assert.Equal("TERMINADA", ScalarText("SELECT ESTADO FROM PARTIDA WHERE ID_PARTIDA=" + game));
            Assert.Equal(expected.EndReason.ToString(), ScalarText("SELECT CAUSA_MUERTE FROM PARTIDA WHERE ID_PARTIDA=" + game));
        }

        [Fact]
        public async Task Finish_MalformedBody_IsIgnored()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            var (game, seed) = await StartOk(key, pack, Basicas());

            var r = await SendRaw("/api/games/" + game + "/finish", key, "{no es json");

            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            AssertResult(await r.Content.ReadFromJsonAsync<JsonElement>(), Simulate(Basicas(), seed));
        }

        [Fact]
        public async Task Finish_PinnedGame_KnownResult()
        {
            // Valores calculados una vez y fijados: si cambian, la simulación ha derivado y las
            // puntuaciones guardadas dejan de ser comparables con las nuevas.
            const string body = "{\"packId\":PACK,\"rules\":[" +
                "{\"name\":\"apagar\",\"battery\":\"Anyone\",\"temp\":\"Anyone\",\"light\":\"Bright\",\"prox\":\"Anyone\",\"cell\":\"Anyone\",\"actions\":[\"Put Out\"]}," +
                "{\"name\":\"acercarse\",\"battery\":\"Anyone\",\"temp\":\"Up\",\"light\":\"Anyone\",\"prox\":\"Obstacle No\",\"cell\":\"Anyone\",\"actions\":[\"Fwd\"]}," +
                "{\"name\":\"girar\",\"battery\":\"Anyone\",\"temp\":\"Anyone\",\"light\":\"Anyone\",\"prox\":\"Anyone\",\"cell\":\"Anyone\",\"actions\":[\"TurnL\"]}]}";
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            var start = await SendRaw("/api/games", key, body.Replace("PACK", pack.ToString(CultureInfo.InvariantCulture)));
            Assert.True(start.IsSuccessStatusCode);
            long game = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("gameId").GetInt64();
            Exec("UPDATE PARTIDA SET SEMILLA=7 WHERE ID_PARTIDA=" + game);

            var j = await FinishOk(key, game);

            Assert.Equal(44, j.GetProperty("steps").GetInt32());
            Assert.Equal(5, j.GetProperty("fires").GetInt32());
            Assert.Equal(220L, j.GetProperty("points").GetInt64());
            Assert.Equal("Battery", j.GetProperty("endReason").GetString());
            Assert.Equal(1, j.GetProperty("rank").GetInt32());
        }

        [Theory]
        [InlineData(2147483648u)] // ya no cabe en int
        [InlineData(4294967295u)] // el máximo de uint
        public async Task Finish_SeedAboveInt32_ReplaysSameGame(uint seed)
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            var (game, _) = await StartOk(key, pack, Basicas());
            Exec("UPDATE PARTIDA SET SEMILLA=" + seed + " WHERE ID_PARTIDA=" + game);

            AssertResult(await FinishOk(key, game), Simulate(Basicas(), seed));
        }

        [Fact]
        public async Task Finish_Twice_SameResultNoChange()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            var (game, _) = await StartOk(key, pack, Basicas());
            var first = await FinishOk(key, game);
            string before = Snapshot();

            // Aunque la semilla guardada cambie, no se vuelve a simular.
            Exec("UPDATE PARTIDA SET SEMILLA=SEMILLA+1 WHERE ID_PARTIDA=" + game);
            string beforeWithSeed = Snapshot();
            var second = await FinishOk(key, game);

            Assert.NotEqual(before, beforeWithSeed);
            Assert.Equal(first.GetRawText(), second.GetRawText());
            Assert.Equal(beforeWithSeed, Snapshot());
        }

        [Fact]
        public async Task Finish_Expired_410()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            var (game, _) = await StartOk(key, pack, Basicas());
            Exec("UPDATE PARTIDA SET CADUCA='" + DateTime.UtcNow.AddSeconds(-1).ToString("o") + "' WHERE ID_PARTIDA=" + game);

            var r = await Finish(key, game);

            Assert.Equal(HttpStatusCode.Gone, r.StatusCode);
            Assert.Equal("EN_CURSO", ScalarText("SELECT ESTADO FROM PARTIDA WHERE ID_PARTIDA=" + game));
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
        }

        [Fact]
        public async Task Finish_FinishedThenPastExpiry_StillReturnsResult()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            var (game, _) = await StartOk(key, pack, Basicas());
            var first = await FinishOk(key, game);
            Exec("UPDATE PARTIDA SET CADUCA='2020-01-01T00:00:00.0000000Z' WHERE ID_PARTIDA=" + game);

            var second = await FinishOk(key, game);

            Assert.Equal(first.GetRawText(), second.GetRawText());
        }

        [Fact]
        public async Task Finish_WithoutKey_401()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            var (game, _) = await StartOk(key, pack, Basicas());

            Assert.Equal(HttpStatusCode.Unauthorized, (await Finish(null, game)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Finish("clave-falsa", game)).StatusCode);
            Assert.Equal("EN_CURSO", ScalarText("SELECT ESTADO FROM PARTIDA WHERE ID_PARTIDA=" + game));
        }

        [Fact]
        public async Task Finish_OtherPlayersGame_403()
        {
            var (_, keyA) = await NewPlayer("Ana");
            var (_, keyB) = await NewPlayer("Beto");
            long pack = await NewPack(keyA, "Pack", Hash(1));
            var (game, _) = await StartOk(keyA, pack, Basicas());

            var r = await Finish(keyB, game);

            Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
            Assert.Equal("EN_CURSO", ScalarText("SELECT ESTADO FROM PARTIDA WHERE ID_PARTIDA=" + game));
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
        }

        [Fact]
        public async Task Finish_Unknown_404()
        {
            var (_, key) = await NewPlayer("Ana");
            Assert.Equal(HttpStatusCode.NotFound, (await Finish(key, 999)).StatusCode);
        }

        [Fact]
        public async Task Finish_Concurrent_OnlyAppliesOnce()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            var (game, seed) = await StartOk(key, pack, Basicas());
            // Una segunda aplicación daría el mismo resultado y pasaría inadvertida: la cuentan estos disparadores.
            Exec("CREATE TABLE AUDITORIA (QUE TEXT)");
            Exec("CREATE TRIGGER AUD_PARTIDA AFTER UPDATE ON PARTIDA BEGIN INSERT INTO AUDITORIA VALUES ('PARTIDA'); END");
            Exec("CREATE TRIGGER AUD_CLASIFICACION AFTER INSERT ON CLASIFICACION BEGIN INSERT INTO AUDITORIA VALUES ('CLASIFICACION'); END");

            var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => Finish(key, game))));

            foreach (var r in results) Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var bodies = await Task.WhenAll(results.Select(r => r.Content.ReadAsStringAsync()));
            Assert.Single(bodies.Distinct());
            AssertResult(JsonSerializer.Deserialize<JsonElement>(bodies[0]), Simulate(Basicas(), seed));
            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM AUDITORIA WHERE QUE='PARTIDA'"));
            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM AUDITORIA WHERE QUE='CLASIFICACION'"));
            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
        }

        [Fact]
        public async Task Finish_GameFromBeforeRulesChanged_NotRanked()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            var (oldGame, seed) = await StartOk(key, pack, Basicas());
            await StartOk(key, pack, Variante()); // el pack ya no tiene esas reglas

            var j = await FinishOk(key, oldGame);

            // El resultado es válido, pero la fila del pack debe corresponder a sus reglas actuales.
            AssertResult(j, Simulate(Basicas(), seed));
            Assert.Equal(JsonValueKind.Null, j.GetProperty("rank").ValueKind);
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
        }

        [Fact]
        public async Task Finish_GameFromBeforePackEdit_NotRanked()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "Pack", Hash(1));
            var (game, _) = await StartOk(key, pack, Basicas());
            var put = await Send(HttpMethod.Put, "/api/packs/" + pack, key, new { name = "Pack", rulesHash = Hash(2) });
            Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

            await FinishOk(key, game);

            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
        }

        [Fact]
        public async Task RateLimit_61stGame_429()
        {
            var (_, key) = await NewPlayer("Ana");
            long packA = await NewPack(key, "A", Hash(1));
            long packB = await NewPack(key, "B", Hash(2));

            // El límite es por jugador: se reparte entre sus packs.
            for (int i = 0; i < 60; i++)
            {
                await StartOk(key, i % 2 == 0 ? packA : packB, Basicas());
            }
            var r = await Start(key, packA, Basicas());

            Assert.Equal((HttpStatusCode)429, r.StatusCode);
            Assert.Equal(60, Scalar("SELECT COUNT(*) FROM PARTIDA"));

            // Otro jugador no se ve afectado.
            var (_, otherKey) = await NewPlayer("Beto");
            long otherPack = await NewPack(otherKey, "C", Hash(3));
            await StartOk(otherKey, otherPack, Basicas());
        }

        [Fact]
        public async Task RateLimit_GamesOlderThanOneHour_DoNotCount_AndArePruned()
        {
            var (id, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "A", Hash(1));
            InsertStarts(id, 60, DateTime.UtcNow.AddMinutes(-59));
            Assert.Equal((HttpStatusCode)429, (await Start(key, pack, Basicas())).StatusCode);

            Exec("UPDATE INICIO_PARTIDA SET FECHA='" + DateTime.UtcNow.AddMinutes(-61).ToString("o") + "'");

            await StartOk(key, pack, Basicas());
            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM INICIO_PARTIDA"));
        }

        [Fact]
        public async Task RateLimit_FinishingDoesNotFreeSlots()
        {
            var (id, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "A", Hash(1));
            InsertStarts(id, 59, DateTime.UtcNow.AddMinutes(-1));
            var (game, _) = await StartOk(key, pack, Basicas());
            await FinishOk(key, game);

            Assert.Equal((HttpStatusCode)429, (await Start(key, pack, Basicas())).StatusCode);
        }

        [Fact]
        public async Task RateLimit_SurvivesPackDeletion()
        {
            var (_, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "A", Hash(1));
            for (int i = 0; i < 60; i++)
            {
                await StartOk(key, pack, Basicas());
            }

            var del = await Send(HttpMethod.Delete, "/api/packs/" + pack, key);
            Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM PARTIDA"));

            long another = await NewPack(key, "B", Hash(2));
            Assert.Equal((HttpStatusCode)429, (await Start(key, another, Basicas())).StatusCode);
        }

        [Fact]
        public async Task RateLimit_Concurrent_NeverExceeded()
        {
            var (id, key) = await NewPlayer("Ana");
            long pack = await NewPack(key, "A", Hash(1));
            InsertStarts(id, 55, DateTime.UtcNow.AddMinutes(-1));

            var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => Start(key, pack, Basicas()))));

            Assert.Equal(5, results.Count(r => r.IsSuccessStatusCode));
            Assert.Equal(11, results.Count(r => r.StatusCode == (HttpStatusCode)429));
            Assert.Equal(60, Scalar("SELECT COUNT(*) FROM INICIO_PARTIDA"));
            Assert.Equal(5, Scalar("SELECT COUNT(*) FROM PARTIDA"));
        }

        private void InsertRowForPack(long playerId, long packId, string hash)
        {
            long partida = InsertFinished(packId, hash, 10, "2026-01-01T00:00:00.0000000Z");
            Exec("INSERT INTO CLASIFICACION (ID_JUGADOR,ID_PACK,ID_PARTIDA,PUNTOS,PASOS,FUEGOS,FECHA) VALUES (" +
                 playerId + "," + packId + "," + partida + ",10,5,1,'2026-01-01T00:00:00.0000000Z')");
        }

        private void AssertUntouched(long pack, string hash, bool expectRow = true)
        {
            Assert.Equal(hash, ScalarText("SELECT REGLAS_HASH FROM PACK WHERE ID_PACK=" + pack));
            Assert.Equal(expectRow ? 1 : 0, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM PARTIDA WHERE ESTADO='EN_CURSO'"));
        }

        private void InsertStarts(long playerId, int count, DateTime when)
        {
            using (var c = OpenDb())
            using (var tx = c.BeginTransaction(deferred: false))
            {
                for (int i = 0; i < count; i++)
                {
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "INSERT INTO INICIO_PARTIDA (ID_JUGADOR,FECHA) VALUES ($j,$f)";
                        cmd.Parameters.AddWithValue("$j", playerId);
                        cmd.Parameters.AddWithValue("$f", when.ToString("o"));
                        cmd.ExecuteNonQuery();
                    }
                }
                tx.Commit();
            }
        }

        private string Snapshot()
        {
            var sb = new StringBuilder();
            using (var c = OpenDb())
            {
                foreach (string sql in new[]
                {
                    "SELECT ID_PARTIDA,ID_PACK,SEMILLA,REGLAS_HASH,ESTADO,CADUCA,PASOS,FUEGOS,PUNTOS,CAUSA_MUERTE,FECHA FROM PARTIDA ORDER BY 1",
                    "SELECT ID_CLASIFICACION,ID_JUGADOR,ID_PACK,ID_PARTIDA,PUNTOS,PASOS,FUEGOS,FECHA FROM CLASIFICACION ORDER BY 1",
                    "SELECT ID_PACK,REGLAS_HASH FROM PACK ORDER BY 1"
                })
                {
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                for (int i = 0; i < reader.FieldCount; i++) sb.Append(reader.GetValue(i)).Append('|');
                                sb.AppendLine();
                            }
                        }
                    }
                }
            }
            return sb.ToString();
        }
    }
}
