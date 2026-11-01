using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using RobotBombero.Server.Data;
using RobotBombero.Simulation;

namespace RobotBombero.Server.Api
{
    public class PackRequest
    {
        public string Name { get; set; }
        public string RulesHash { get; set; }
    }

    public static class PacksApi
    {
        private static readonly Regex HashPattern = new Regex(@"^[0-9a-f]{64}\z", RegexOptions.CultureInvariant);

        private static bool Valid(PackRequest body)
        {
            return body != null
                && !string.IsNullOrWhiteSpace(body.Name)
                && body.Name.Length <= PackLimits.MaxNameLength
                && body.RulesHash != null
                && HashPattern.IsMatch(body.RulesHash);
        }

        public static void Map(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/packs", (HttpRequest request, PackRequest body, Database db) =>
            {
                using (var connection = db.Open())
                {
                    long? player = PlayerAuth.Authenticate(request, connection);
                    if (player == null) return Results.Unauthorized();
                    if (!Valid(body)) return Results.BadRequest(new { error = "Nombre o huella de reglas no válidos." });

                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "INSERT INTO PACK (ID_JUGADOR, NOMBRE, REGLAS_HASH, FECHA_CREACION) VALUES ($j, $n, $h, $f); SELECT last_insert_rowid();";
                        cmd.Parameters.AddWithValue("$j", player.Value);
                        cmd.Parameters.AddWithValue("$n", body.Name);
                        cmd.Parameters.AddWithValue("$h", body.RulesHash);
                        cmd.Parameters.AddWithValue("$f", DateTime.UtcNow.ToString("o"));
                        long id = (long)cmd.ExecuteScalar();
                        return Results.Created("/api/packs/" + id, new { packId = id });
                    }
                }
            });

            app.MapPut("/api/packs/{id:long}", (long id, HttpRequest request, PackRequest body, Database db) =>
            {
                using (var connection = db.Open())
                {
                    long? player = PlayerAuth.Authenticate(request, connection);
                    if (player == null) return Results.Unauthorized();
                    if (!Valid(body)) return Results.BadRequest(new { error = "Nombre o huella de reglas no válidos." });

                    using (var tx = connection.BeginTransaction())
                    {
                        IResult denied = CheckOwner(connection, tx, id, player.Value, out string oldHash);
                        if (denied != null) return denied;

                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = "UPDATE PACK SET NOMBRE = $n, REGLAS_HASH = $h WHERE ID_PACK = $id";
                            cmd.Parameters.AddWithValue("$n", body.Name);
                            cmd.Parameters.AddWithValue("$h", body.RulesHash);
                            cmd.Parameters.AddWithValue("$id", id);
                            cmd.ExecuteNonQuery();
                        }
                        // Si cambian las reglas, la marca anterior ya no es comparable: se retira de la clasificación.
                        if (oldHash != body.RulesHash)
                        {
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.Transaction = tx;
                                cmd.CommandText = "DELETE FROM CLASIFICACION WHERE ID_PACK = $id";
                                cmd.Parameters.AddWithValue("$id", id);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        tx.Commit();
                        return Results.NoContent();
                    }
                }
            });

            app.MapDelete("/api/packs/{id:long}", (long id, HttpRequest request, Database db) =>
            {
                using (var connection = db.Open())
                {
                    long? player = PlayerAuth.Authenticate(request, connection);
                    if (player == null) return Results.Unauthorized();

                    using (var tx = connection.BeginTransaction())
                    {
                        IResult denied = CheckOwner(connection, tx, id, player.Value, out string _);
                        if (denied != null) return denied;

                        // Orden de claves foráneas: CLASIFICACION -> PARTIDA -> PACK.
                        foreach (string table in new[] { "CLASIFICACION", "PARTIDA", "PACK" })
                        {
                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.Transaction = tx;
                                cmd.CommandText = "DELETE FROM " + table + " WHERE ID_PACK = $id";
                                cmd.Parameters.AddWithValue("$id", id);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        tx.Commit();
                        return Results.NoContent();
                    }
                }
            });
        }

        // Devuelve null si el pack existe y es del jugador; si no, la respuesta de error.
        private static IResult CheckOwner(SqliteConnection connection, SqliteTransaction tx, long packId, long playerId, out string rulesHash)
        {
            rulesHash = null;
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT ID_JUGADOR, REGLAS_HASH FROM PACK WHERE ID_PACK = $id";
                cmd.Parameters.AddWithValue("$id", packId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return Results.NotFound();
                    if (reader.GetInt64(0) != playerId) return Results.StatusCode(StatusCodes.Status403Forbidden);
                    rulesHash = reader.GetString(1);
                    return null;
                }
            }
        }
    }
}
