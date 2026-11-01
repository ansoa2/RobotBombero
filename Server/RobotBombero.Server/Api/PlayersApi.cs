using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using RobotBombero.Server.Data;

namespace RobotBombero.Server.Api
{
    public class CreatePlayerRequest
    {
        public string Name { get; set; }
    }

    public static class PlayersApi
    {
        // \z (no $) para que un salto de línea final no pase la validación.
        private static readonly Regex NamePattern = new Regex(@"^[A-Za-z0-9_-]{3,20}\z", RegexOptions.CultureInvariant);

        private const int SqliteConstraint = 19;
        private const int SqliteConstraintUnique = 2067;

        public static void Map(IEndpointRouteBuilder app)
        {
            app.MapPost("/api/players", (CreatePlayerRequest body, Database db) =>
            {
                string name = body == null ? null : body.Name;
                if (name == null || !NamePattern.IsMatch(name))
                    return Results.BadRequest(new { error = "El nombre debe tener de 3 a 20 caracteres: letras, números, _ o -." });

                string key = PlayerAuth.NewKey();
                try
                {
                    using (var connection = db.Open())
                    using (var cmd = connection.CreateCommand())
                    {
                        // La unicidad la decide la restricción UNIQUE (NOCASE), no una consulta previa.
                        cmd.CommandText = "INSERT INTO JUGADOR (NOMBRE, HUELLA_CLAVE, FECHA_CREACION) VALUES ($n, $h, $f); SELECT last_insert_rowid();";
                        cmd.Parameters.AddWithValue("$n", name);
                        cmd.Parameters.AddWithValue("$h", PlayerAuth.Fingerprint(key));
                        cmd.Parameters.AddWithValue("$f", DateTime.UtcNow.ToString("o"));
                        long id = (long)cmd.ExecuteScalar();
                        return Results.Created("/api/players/" + id, new { playerId = id, name, key });
                    }
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraint && ex.SqliteExtendedErrorCode == SqliteConstraintUnique)
                {
                    return Results.Conflict(new { error = "Ese nombre ya existe." });
                }
            });
        }
    }
}
