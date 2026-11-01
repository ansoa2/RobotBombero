using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RobotBombero.Server.Data;
using RobotBombero.Server.Leaderboard;

namespace RobotBombero.Server.Api
{
    public static class LeaderboardApi
    {
        public static void Map(IEndpointRouteBuilder app)
        {
            app.MapGet("/api/leaderboard", (Database db) =>
            {
                var rows = new List<object>();
                using (var connection = db.Open())
                using (var cmd = connection.CreateCommand())
                {
                    // Pública: solo nombres y marcas, nunca claves ni huellas.
                    cmd.CommandText =
                        "SELECT j.NOMBRE, k.NOMBRE, c.PUNTOS, c.PASOS, c.FUEGOS, c.FECHA FROM CLASIFICACION c " +
                        "JOIN JUGADOR j ON j.ID_JUGADOR = c.ID_JUGADOR " +
                        "JOIN PACK k ON k.ID_PACK = c.ID_PACK " +
                        "ORDER BY " + LeaderboardUpdater.Ordering;
                    using (var reader = cmd.ExecuteReader())
                    {
                        int rank = 0;
                        while (reader.Read())
                        {
                            rank++;
                            rows.Add(new
                            {
                                rank,
                                playerName = reader.GetString(0),
                                packName = reader.GetString(1),
                                points = reader.GetInt64(2),
                                steps = reader.GetInt32(3),
                                fires = reader.GetInt32(4),
                                date = reader.GetString(5)
                            });
                        }
                    }
                }
                return Results.Ok(rows);
            });
        }
    }
}
