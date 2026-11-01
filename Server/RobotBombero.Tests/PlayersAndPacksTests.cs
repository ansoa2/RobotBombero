using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using RobotBombero.Server.Data;
using Xunit;

namespace RobotBombero.Tests
{
    public class PlayersAndPacksTests : IDisposable
    {
        private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string HashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private readonly string dbPath;
        private readonly string connectionString;
        private readonly WebApplicationFactory<Program> factory;
        private readonly HttpClient client;

        public PlayersAndPacksTests()
        {
            dbPath = Path.Combine(Path.GetTempPath(), "rb-test-" + Guid.NewGuid().ToString("N") + ".db");
            connectionString = "Data Source=" + dbPath + ";Pooling=False";
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
                b.UseSetting("ConnectionStrings:Default", connectionString));
            client = factory.CreateClient();
        }

        public void Dispose()
        {
            client.Dispose();
            factory.Dispose();
            SqliteConnection.ClearAllPools();
            foreach (var f in new[] { dbPath, dbPath + "-wal", dbPath + "-shm", dbPath + "-journal" })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch (IOException) { }
            }
        }

        private SqliteConnection OpenDb()
        {
            return new Database(connectionString).Open();
        }

        private long Scalar(string sql)
        {
            using (var c = OpenDb())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = sql;
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        private void Exec(string sql)
        {
            using (var c = OpenDb())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private async Task<(long id, string key)> NewPlayer(string name)
        {
            var r = await client.PostAsJsonAsync("/api/players", new { name });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            return (j.GetProperty("playerId").GetInt64(), j.GetProperty("key").GetString());
        }

        private async Task<HttpResponseMessage> Send(HttpMethod m, string url, string key, object body = null)
        {
            var req = new HttpRequestMessage(m, url);
            if (key != null) req.Headers.Add("X-Player-Key", key);
            if (body != null) req.Content = JsonContent.Create(body);
            return await client.SendAsync(req);
        }

        private async Task<long> NewPack(string key, string name = "Pack", string hash = HashA)
        {
            var r = await Send(HttpMethod.Post, "/api/packs", key, new { name, rulesHash = hash });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            return j.GetProperty("packId").GetInt64();
        }

        // Inserta directamente una partida terminada y su fila de clasificación (la API de partidas llega después).
        private void InsertLeaderboardRow(long playerId, long packId)
        {
            Exec("INSERT INTO PARTIDA (ID_PACK,SEMILLA,REGLAS_JSON,REGLAS_HASH,ESTADO,CADUCA,PASOS,FUEGOS,PUNTOS,FECHA) " +
                 "VALUES (" + packId + ",1,'[]','" + HashA + "','TERMINADA','2999-01-01',5,1,10,'2026-01-01')");
            long partida = Scalar("SELECT MAX(ID_PARTIDA) FROM PARTIDA");
            Exec("INSERT INTO CLASIFICACION (ID_JUGADOR,ID_PACK,ID_PARTIDA,PUNTOS,PASOS,FUEGOS,FECHA) VALUES (" +
                 playerId + "," + packId + "," + partida + ",10,5,1,'2026-01-01')");
        }

        [Fact]
        public async Task CreatePlayer_ReturnsKeyOnce_AndStoresOnlyHash()
        {
            var r = await client.PostAsJsonAsync("/api/players", new { name = "Ana_01" });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            string key = j.GetProperty("key").GetString();
            Assert.Equal("Ana_01", j.GetProperty("name").GetString());
            Assert.True(j.GetProperty("playerId").GetInt64() > 0);
            Assert.Equal(32, Convert.FromBase64String(key).Length);

            using (var c = OpenDb())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT HUELLA_CLAVE FROM JUGADOR";
                string stored = (string)cmd.ExecuteScalar();
                Assert.NotEqual(key, stored);
                Assert.Equal(64, stored.Length);
                string expected = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
                Assert.Equal(expected, stored);
            }
        }

        [Fact]
        public async Task DuplicateName_409()
        {
            await NewPlayer("Ana");
            var r = await client.PostAsJsonAsync("/api/players", new { name = "Ana" });
            Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        }

        [Fact]
        public async Task DuplicateName_DifferentCase_409()
        {
            await NewPlayer("Ana");
            var r = await client.PostAsJsonAsync("/api/players", new { name = "ANA" });
            Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        }

        [Fact]
        public async Task Concurrent_SameName_OnlyOneWins()
        {
            var tasks = Enumerable.Range(0, 12)
                .Select(_ => Task.Run(() => client.PostAsJsonAsync("/api/players", new { name = "Carrera" })))
                .ToArray();
            var results = await Task.WhenAll(tasks);
            Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
            Assert.Equal(11, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM JUGADOR"));
        }

        [Theory]
        [InlineData("ab")]
        [InlineData("abcdefghijklmnopqrstu")]
        [InlineData("con espacio")]
        [InlineData("ñandú")]
        [InlineData("abc\n")]
        [InlineData("")]
        [InlineData(null)]
        public async Task InvalidName_400(string name)
        {
            var r = await client.PostAsJsonAsync("/api/players", new { name });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM JUGADOR"));
        }

        [Fact]
        public async Task InvalidName_NoBody_400()
        {
            var r = await client.PostAsync("/api/players", new StringContent("", System.Text.Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        [Fact]
        public async Task Pack_WithoutKey_401()
        {
            var r = await Send(HttpMethod.Post, "/api/packs", null, new { name = "P", rulesHash = HashA });
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
            r = await Send(HttpMethod.Post, "/api/packs", "clave-falsa", new { name = "P", rulesHash = HashA });
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        }

        [Fact]
        public async Task Pack_InvalidBody_400()
        {
            var (_, key) = await NewPlayer("Ana");
            var r = await Send(HttpMethod.Post, "/api/packs", key, new { name = "P", rulesHash = "xyz" });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            r = await Send(HttpMethod.Post, "/api/packs", key, new { name = "P", rulesHash = HashA.ToUpperInvariant() });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            r = await Send(HttpMethod.Post, "/api/packs", key, new { name = "", rulesHash = HashA });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            r = await Send(HttpMethod.Post, "/api/packs", key, new { name = new string('x', 41), rulesHash = HashA });
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }

        [Fact]
        public async Task Pack_OfAnotherPlayer_403()
        {
            var (_, keyA) = await NewPlayer("Ana");
            var (_, keyB) = await NewPlayer("Beto");
            long pack = await NewPack(keyA);

            var put = await Send(HttpMethod.Put, "/api/packs/" + pack, keyB, new { name = "X", rulesHash = HashA });
            Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
            var del = await Send(HttpMethod.Delete, "/api/packs/" + pack, keyB);
            Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);
            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM PACK"));
        }

        [Fact]
        public async Task Pack_Missing_404()
        {
            var (_, key) = await NewPlayer("Ana");
            var r = await Send(HttpMethod.Delete, "/api/packs/999", key);
            Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        }

        [Fact]
        public async Task Rename_KeepsLeaderboardRow()
        {
            var (id, key) = await NewPlayer("Ana");
            long pack = await NewPack(key);
            InsertLeaderboardRow(id, pack);

            var r = await Send(HttpMethod.Put, "/api/packs/" + pack, key, new { name = "Nuevo", rulesHash = HashA });
            Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
            using (var c = OpenDb())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "SELECT NOMBRE FROM PACK WHERE ID_PACK=" + pack;
                Assert.Equal("Nuevo", (string)cmd.ExecuteScalar());
            }
        }

        [Fact]
        public async Task ChangeRules_RemovesLeaderboardRow()
        {
            var (id, key) = await NewPlayer("Ana");
            long pack = await NewPack(key);
            InsertLeaderboardRow(id, pack);

            var r = await Send(HttpMethod.Put, "/api/packs/" + pack, key, new { name = "Pack", rulesHash = HashB });
            Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM PACK WHERE REGLAS_HASH='" + HashB + "'"));
        }

        [Fact]
        public async Task Delete_RemovesLeaderboardRow()
        {
            var (id, key) = await NewPlayer("Ana");
            long pack = await NewPack(key);
            long other = await NewPack(key, "Otro");
            InsertLeaderboardRow(id, pack);

            var r = await Send(HttpMethod.Delete, "/api/packs/" + pack, key);
            Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM CLASIFICACION"));
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM PARTIDA"));
            Assert.Equal(0, Scalar("SELECT COUNT(*) FROM PACK WHERE ID_PACK=" + pack));
            Assert.Equal(1, Scalar("SELECT COUNT(*) FROM PACK WHERE ID_PACK=" + other));
        }
    }
}
