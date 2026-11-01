using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using RobotBombero.Server.Data;
using RobotBombero.Server.Leaderboard;
using RobotBombero.Simulation;
using Xunit;

namespace RobotBombero.Tests
{
    /// <summary>Servidor con base de datos aislada y utilidades comunes a las pruebas de partidas y clasificación.</summary>
    public abstract class GameApiTestBase : IDisposable
    {
        // Rule usa campos públicos: sin IncludeFields se serializaría como {}.
        protected static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Con Basicas() estas semillas dan 46 y 220 puntos (comprobado en LowAndHighSeeds_GiveDifferentPoints).
        protected const uint LowSeed = 3;
        protected const uint HighSeed = 7;
        protected const long LowPoints = 46;
        protected const long HighPoints = 220;

        private readonly string dbPath;
        protected readonly string ConnectionString;
        private readonly WebApplicationFactory<Program> factory;
        protected readonly HttpClient Client;

        protected GameApiTestBase()
        {
            dbPath = Path.Combine(Path.GetTempPath(), "rb-test-" + Guid.NewGuid().ToString("N") + ".db");
            ConnectionString = "Data Source=" + dbPath + ";Pooling=False";
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
                b.UseSetting("ConnectionStrings:Default", ConnectionString));
            Client = factory.CreateClient();
        }

        public void Dispose()
        {
            Client.Dispose();
            factory.Dispose();
            SqliteConnection.ClearAllPools();
            foreach (var f in new[] { dbPath, dbPath + "-wal", dbPath + "-shm", dbPath + "-journal" })
            {
                try { if (File.Exists(f)) File.Delete(f); } catch (IOException) { }
            }
        }

        protected static Rule R(string battery, string temp, string light, string prox, string cell, params string[] actions)
        {
            return new Rule { name = "r", battery = battery, temp = temp, light = light, prox = prox, cell = cell, actions = actions };
        }

        /// <summary>Apaga fuegos cuando los tiene delante, se acerca si puede y si no gira.</summary>
        protected static Rule[] Basicas()
        {
            return new[]
            {
                R("Anyone", "Anyone", "Bright", "Anyone", "Anyone", "Put Out"),
                R("Anyone", "Up", "Anyone", "Obstacle No", "Anyone", "Fwd"),
                R("Anyone", "Anyone", "Anyone", "Anyone", "Anyone", "TurnL"),
            };
        }

        /// <summary>Otras reglas con el mismo juego en la práctica, pero con otra huella.</summary>
        protected static Rule[] Variante()
        {
            var rules = Basicas();
            Array.Resize(ref rules, rules.Length + 1);
            rules[rules.Length - 1] = R("Low", "Equal", "Dark", "Obstacle Yes", "Base", "TurnR");
            return rules;
        }

        protected static string Hash(long n)
        {
            return n.ToString("x64");
        }

        protected SqliteConnection OpenDb()
        {
            return new Database(ConnectionString).Open();
        }

        protected long Scalar(string sql)
        {
            using (var c = OpenDb())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = sql;
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        protected string ScalarText(string sql)
        {
            using (var c = OpenDb())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = sql;
                return (string)cmd.ExecuteScalar();
            }
        }

        protected void Exec(string sql)
        {
            using (var c = OpenDb())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        protected async Task<(long id, string key)> NewPlayer(string name)
        {
            var r = await Client.PostAsJsonAsync("/api/players", new { name });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            return (j.GetProperty("playerId").GetInt64(), j.GetProperty("key").GetString());
        }

        protected async Task<long> NewPack(string key, string name, string hash)
        {
            var r = await Send(HttpMethod.Post, "/api/packs", key, new { name, rulesHash = hash });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            return j.GetProperty("packId").GetInt64();
        }

        protected async Task<HttpResponseMessage> Send(HttpMethod method, string url, string key, object body = null)
        {
            var req = new HttpRequestMessage(method, url);
            if (key != null) req.Headers.Add("X-Player-Key", key);
            if (body != null) req.Content = JsonContent.Create(body, body.GetType(), options: Json);
            return await Client.SendAsync(req);
        }

        protected async Task<HttpResponseMessage> SendRaw(string url, string key, string json)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            if (key != null) req.Headers.Add("X-Player-Key", key);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return await Client.SendAsync(req);
        }

        protected Task<HttpResponseMessage> Start(string key, long packId, Rule[] rules)
        {
            return Send(HttpMethod.Post, "/api/games", key, new { packId, rules });
        }

        protected async Task<(long gameId, uint seed)> StartOk(string key, long packId, Rule[] rules)
        {
            var r = await Start(key, packId, rules);
            Assert.True(r.IsSuccessStatusCode, "Start: " + r.StatusCode + " " + await r.Content.ReadAsStringAsync());
            var j = await r.Content.ReadFromJsonAsync<JsonElement>();
            return (j.GetProperty("gameId").GetInt64(), j.GetProperty("seed").GetUInt32());
        }

        protected Task<HttpResponseMessage> Finish(string key, long gameId)
        {
            return Send(HttpMethod.Post, "/api/games/" + gameId + "/finish", key);
        }

        protected async Task<JsonElement> FinishOk(string key, long gameId)
        {
            var r = await Finish(key, gameId);
            Assert.True(r.IsSuccessStatusCode, "Finish: " + r.StatusCode + " " + await r.Content.ReadAsStringAsync());
            return await r.Content.ReadFromJsonAsync<JsonElement>();
        }

        /// <summary>Juega una partida por la API, fijando la semilla en la base para que el resultado sea conocido.</summary>
        protected async Task<JsonElement> PlayWithSeed(string key, long packId, Rule[] rules, uint seed)
        {
            var (gameId, _) = await StartOk(key, packId, rules);
            Exec("UPDATE PARTIDA SET SEMILLA=" + seed + " WHERE ID_PARTIDA=" + gameId);
            return await FinishOk(key, gameId);
        }

        protected long InsertPlayer(string name)
        {
            Exec("INSERT INTO JUGADOR (NOMBRE,HUELLA_CLAVE,FECHA_CREACION) VALUES ('" + name + "','" + name + "','2026-01-01T00:00:00.0000000Z')");
            return Scalar("SELECT MAX(ID_JUGADOR) FROM JUGADOR");
        }

        protected long InsertPack(long playerId, string name, string hash)
        {
            Exec("INSERT INTO PACK (ID_JUGADOR,NOMBRE,REGLAS_HASH,FECHA_CREACION) VALUES (" +
                 playerId + ",'" + name + "','" + hash + "','2026-01-01T00:00:00.0000000Z')");
            return Scalar("SELECT MAX(ID_PACK) FROM PACK");
        }

        protected long InsertFinished(long packId, string hash, long points, string date)
        {
            Exec("INSERT INTO PARTIDA (ID_PACK,SEMILLA,REGLAS_JSON,REGLAS_HASH,ESTADO,CADUCA,PASOS,FUEGOS,PUNTOS,CAUSA_MUERTE,FECHA) VALUES (" +
                 packId + ",1,'[]','" + hash + "','TERMINADA','2999-01-01T00:00:00.0000000Z',5,1," + points + ",'Battery','" + date + "')");
            return Scalar("SELECT MAX(ID_PARTIDA) FROM PARTIDA");
        }

        protected void ApplyUpdate(long partidaId)
        {
            using (var c = OpenDb())
            using (var tx = c.BeginTransaction(deferred: false))
            {
                LeaderboardUpdater.Update(c, tx, partidaId);
                tx.Commit();
            }
        }

        /// <summary>Partida terminada insertada en la base y pasada por el actualizador. Devuelve el id de la partida.</summary>
        protected long Record(long packId, string hash, long points, string date = "2026-01-01T00:00:00.0000000Z")
        {
            long partida = InsertFinished(packId, hash, points, date);
            ApplyUpdate(partida);
            return partida;
        }
    }
}
