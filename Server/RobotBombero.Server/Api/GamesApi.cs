using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using RobotBombero.Server.Data;
using RobotBombero.Server.Leaderboard;
using RobotBombero.Simulation;

namespace RobotBombero.Server.Api
{
    public class StartGameRequest
    {
        public long? PackId { get; set; }
        public Rule[] Rules { get; set; }
    }

    /// <summary>
    /// Partidas validadas: el servidor fija reglas y semilla al empezar y, al terminar, simula él mismo.
    /// Lo que el cliente diga del resultado no se lee nunca.
    /// </summary>
    public static class GamesApi
    {
        private const int MaxGamesPerHour = 60;
        private static readonly TimeSpan GameLifetime = TimeSpan.FromHours(24);

        private const string InProgress = "EN_CURSO";
        private const string Finished = "TERMINADA";

        // Rule usa campos públicos: sin IncludeFields las reglas se leerían vacías. Se usan las mismas
        // opciones para guardar y para repetir la partida, de modo que se lee exactamente lo guardado.
        private static readonly JsonSerializerOptions RulesJson = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        };

        public static void Map(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/games", Start);
            app.MapPost("/api/games/{id:long}/finish", Finish);
        }

        private static async Task<IResult> Start(HttpRequest request, Database db)
        {
            using (var connection = db.Open())
            {
                long? player = PlayerAuth.Authenticate(request, connection);
                if (player == null) return Results.Unauthorized();

                byte[] json = await ReadBodyWithinLimit(request);
                if (json == null)
                    return Results.Json(new { error = "El cuerpo supera el tamaño máximo." }, statusCode: StatusCodes.Status413PayloadTooLarge);

                StartGameRequest body;
                try
                {
                    body = JsonSerializer.Deserialize<StartGameRequest>(json, RulesJson);
                }
                catch (JsonException)
                {
                    return Results.BadRequest(new { error = "JSON no válido." });
                }
                if (body == null || body.PackId == null)
                    return Results.BadRequest(new { error = "Falta el pack." });
                long packId = body.PackId.Value;

                using (var tx = connection.BeginTransaction(deferred: false))
                {
                    string packName, packHash;
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "SELECT ID_JUGADOR, NOMBRE, REGLAS_HASH FROM PACK WHERE ID_PACK = $id";
                        cmd.Parameters.AddWithValue("$id", packId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read()) return Results.NotFound();
                            if (reader.GetInt64(0) != player.Value) return Results.StatusCode(StatusCodes.Status403Forbidden);
                            packName = reader.GetString(1);
                            packHash = reader.GetString(2);
                        }
                    }

                    // Antes de la huella o de la simulación: la simulación confía en su entrada.
                    ValidationResult validation = PackValidator.Validate(new RulePackFile
                    {
                        formatVersion = PackLimits.CurrentFormatVersion,
                        name = packName,
                        rules = body.Rules
                    });
                    if (!validation.IsValid) return Results.BadRequest(new { error = validation.Error });

                    DateTime now = DateTime.UtcNow;
                    using (var cmd = connection.CreateCommand())
                    {
                        // INICIO_PARTIDA, no PARTIDA: borrar un pack no debe devolver partidas al límite.
                        // Las fechas son UTC en formato "o": de ancho fijo, comparables como texto.
                        cmd.Transaction = tx;
                        cmd.CommandText = "DELETE FROM INICIO_PARTIDA WHERE FECHA <= $since; SELECT COUNT(*) FROM INICIO_PARTIDA WHERE ID_JUGADOR = $j";
                        cmd.Parameters.AddWithValue("$j", player.Value);
                        cmd.Parameters.AddWithValue("$since", now.AddHours(-1).ToString("o"));
                        if ((long)cmd.ExecuteScalar() >= MaxGamesPerHour)
                            return Results.Json(new { error = "Demasiadas partidas en la última hora." }, statusCode: StatusCodes.Status429TooManyRequests);
                    }
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "INSERT INTO INICIO_PARTIDA (ID_JUGADOR, FECHA) VALUES ($j, $f)";
                        cmd.Parameters.AddWithValue("$j", player.Value);
                        cmd.Parameters.AddWithValue("$f", now.ToString("o"));
                        cmd.ExecuteNonQuery();
                    }

                    string hash = RulesHash.Compute(body.Rules);
                    if (hash != packHash)
                    {
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = "UPDATE PACK SET REGLAS_HASH = $h WHERE ID_PACK = $id";
                            cmd.Parameters.AddWithValue("$h", hash);
                            cmd.Parameters.AddWithValue("$id", packId);
                            cmd.ExecuteNonQuery();
                        }
                        LeaderboardUpdater.OnPackInvalidated(connection, tx, packId, packBeingDeleted: false);
                    }

                    // Texto libre sin validar que la repetición no necesita: no se guarda.
                    foreach (Rule rule in body.Rules) rule.name = null;

                    uint seed = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(sizeof(uint)));
                    long gameId;
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "INSERT INTO PARTIDA (ID_PACK, SEMILLA, REGLAS_JSON, REGLAS_HASH, ESTADO, CADUCA, FECHA) " +
                            "VALUES ($p, $s, $r, $h, $e, $c, $f); SELECT last_insert_rowid();";
                        cmd.Parameters.AddWithValue("$p", packId);
                        cmd.Parameters.AddWithValue("$s", (long)seed);
                        cmd.Parameters.AddWithValue("$r", JsonSerializer.Serialize(body.Rules, RulesJson));
                        cmd.Parameters.AddWithValue("$h", hash);
                        cmd.Parameters.AddWithValue("$e", InProgress);
                        cmd.Parameters.AddWithValue("$c", now.Add(GameLifetime).ToString("o"));
                        cmd.Parameters.AddWithValue("$f", now.ToString("o"));
                        gameId = (long)cmd.ExecuteScalar();
                    }

                    tx.Commit();
                    return Results.Created("/api/games/" + gameId, new { gameId, seed });
                }
            }
        }

        private static IResult Finish(long id, HttpRequest request, Database db)
        {
            using (var connection = db.Open())
            {
                long? player = PlayerAuth.Authenticate(request, connection);
                if (player == null) return Results.Unauthorized();

                // Inmediata: dos finish a la vez se esperan, y el segundo ve la partida ya terminada.
                using (var tx = connection.BeginTransaction(deferred: false))
                {
                    uint seed;
                    string rulesJson, expires;
                    GameOutcome stored = null;

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "SELECT k.ID_JUGADOR, p.SEMILLA, p.REGLAS_JSON, p.ESTADO, p.CADUCA, p.PASOS, p.FUEGOS, p.PUNTOS, p.CAUSA_MUERTE " +
                            "FROM PARTIDA p JOIN PACK k ON k.ID_PACK = p.ID_PACK WHERE p.ID_PARTIDA = $id";
                        cmd.Parameters.AddWithValue("$id", id);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read()) return Results.NotFound();
                            if (reader.GetInt64(0) != player.Value) return Results.StatusCode(StatusCodes.Status403Forbidden);

                            seed = (uint)reader.GetInt64(1);
                            rulesJson = reader.GetString(2);
                            expires = reader.GetString(4);
                            if (reader.GetString(3) == Finished)
                            {
                                stored = new GameOutcome(reader.GetInt32(5), reader.GetInt32(6), reader.GetInt64(7), reader.GetString(8));
                            }
                        }
                    }

                    // Una partida terminada conserva su resultado aunque ya haya pasado su caducidad.
                    if (stored != null) return Response(stored, LeaderboardUpdater.RankOfGame(connection, tx, id));

                    if (string.CompareOrdinal(expires, DateTime.UtcNow.ToString("o")) <= 0)
                        return Results.Json(new { error = "La partida ha caducado." }, statusCode: StatusCodes.Status410Gone);

                    var simulation = new GameSimulation(JsonSerializer.Deserialize<Rule[]>(rulesJson, RulesJson), seed, SimConfig.DefaultMaxSteps);
                    simulation.RunToEnd();
                    var outcome = new GameOutcome(
                        simulation.Steps,
                        simulation.Fires,
                        Score.Points(simulation.Fires, simulation.Steps),
                        simulation.EndReason.ToString());

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "UPDATE PARTIDA SET ESTADO = $e, PASOS = $s, FUEGOS = $f, PUNTOS = $p, CAUSA_MUERTE = $c " +
                            "WHERE ID_PARTIDA = $id";
                        cmd.Parameters.AddWithValue("$e", Finished);
                        cmd.Parameters.AddWithValue("$s", outcome.Steps);
                        cmd.Parameters.AddWithValue("$f", outcome.Fires);
                        cmd.Parameters.AddWithValue("$p", outcome.Points);
                        cmd.Parameters.AddWithValue("$c", outcome.EndReason);
                        cmd.Parameters.AddWithValue("$id", id);
                        cmd.ExecuteNonQuery();
                    }

                    LeaderboardUpdater.Update(connection, tx, id);
                    long? rank = LeaderboardUpdater.RankOfGame(connection, tx, id);
                    tx.Commit();
                    return Response(outcome, rank);
                }
            }
        }

        // null si el cuerpo supera el límite. Se comprueba leyendo porque Content-Length puede no venir.
        private static async Task<byte[]> ReadBodyWithinLimit(HttpRequest request)
        {
            if (request.ContentLength > PackLimits.MaxFileBytes) return null;

            using (var buffer = new MemoryStream())
            {
                byte[] chunk = new byte[8192];
                int read;
                while ((read = await request.Body.ReadAsync(chunk, request.HttpContext.RequestAborted)) > 0)
                {
                    if (buffer.Length + read > PackLimits.MaxFileBytes) return null;
                    buffer.Write(chunk, 0, read);
                }
                return buffer.ToArray();
            }
        }

        private static IResult Response(GameOutcome outcome, long? rank)
        {
            return Results.Ok(new
            {
                steps = outcome.Steps,
                fires = outcome.Fires,
                points = outcome.Points,
                endReason = outcome.EndReason,
                rank
            });
        }

        private sealed class GameOutcome
        {
            public readonly int Steps;
            public readonly int Fires;
            public readonly long Points;
            public readonly string EndReason;

            public GameOutcome(int steps, int fires, long points, string endReason)
            {
                Steps = steps;
                Fires = fires;
                Points = points;
                EndReason = endReason;
            }
        }
    }
}
