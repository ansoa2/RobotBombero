using System;
using Microsoft.Data.Sqlite;

namespace RobotBombero.Server.Leaderboard
{
    /// <summary>
    /// Mantiene CLASIFICACION: una fila por pack, una sola por reglas idénticas de un jugador y 100 como máximo.
    /// "Superar" significa quedar por encima en el orden de la clasificación: más puntos o, con los mismos, una
    /// fecha anterior. Así el resultado no depende del orden en que se terminen las partidas.
    /// </summary>
    public static class LeaderboardUpdater
    {
        public const int MaxRows = 100;

        /// <summary>El orden público de la clasificación; lo comparten la API y este actualizador.</summary>
        public const string Ordering = "PUNTOS DESC, FECHA ASC, ID_CLASIFICACION ASC";

        private const string OtherPacksSameRules =
            "SELECT c.ID_CLASIFICACION, c.PUNTOS, c.FECHA FROM CLASIFICACION c " +
            "JOIN PARTIDA p ON p.ID_PARTIDA = c.ID_PARTIDA " +
            "WHERE c.ID_JUGADOR = $j AND c.ID_PACK <> $k AND p.REGLAS_HASH = $h";

        private readonly struct Mark
        {
            public readonly long Points;
            public readonly string Date;

            public Mark(long points, string date)
            {
                Points = points;
                Date = date;
            }

            public bool Beats(Mark other)
            {
                return Points > other.Points
                    || (Points == other.Points && string.CompareOrdinal(Date, other.Date) < 0);
            }
        }

        public static void Update(SqliteConnection connection, SqliteTransaction transaction, long partidaId)
        {
            long packId, playerId;
            int steps, fires;
            string hash;
            Mark game;

            using (var cmd = Command(connection, transaction,
                "SELECT p.ID_PACK, k.ID_JUGADOR, p.PUNTOS, p.PASOS, p.FUEGOS, p.FECHA, p.REGLAS_HASH, k.REGLAS_HASH, p.ESTADO " +
                "FROM PARTIDA p JOIN PACK k ON k.ID_PACK = p.ID_PACK WHERE p.ID_PARTIDA = $id"))
            {
                cmd.Parameters.AddWithValue("$id", partidaId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read() || reader.GetString(8) != "TERMINADA") return;

                    // Si el pack cambió de reglas después de empezar, esta partida ya no lo representa.
                    hash = reader.GetString(6);
                    if (hash != reader.GetString(7)) return;

                    packId = reader.GetInt64(0);
                    playerId = reader.GetInt64(1);
                    game = new Mark(reader.GetInt64(2), reader.GetString(5));
                    steps = reader.GetInt32(3);
                    fires = reader.GetInt32(4);
                }
            }

            // 1. Si hay fila del mismo pack, sigue solo si la partida la supera.
            using (var cmd = Command(connection, transaction, "SELECT PUNTOS, FECHA FROM CLASIFICACION WHERE ID_PACK = $k"))
            {
                cmd.Parameters.AddWithValue("$k", packId);
                Mark? own = ReadMark(cmd);
                if (own != null && !game.Beats(own.Value)) return;
            }

            // 2. Si hay fila de otro pack del jugador con las mismas reglas, también debe superarla, y entonces se borra.
            using (var cmd = Command(connection, transaction, "SELECT PUNTOS, FECHA FROM (" + OtherPacksSameRules + ") ORDER BY " + Ordering + " LIMIT 1"))
            {
                AddSameRulesParameters(cmd, playerId, packId, hash);
                Mark? twin = ReadMark(cmd);
                if (twin != null && !game.Beats(twin.Value)) return;
            }
            using (var cmd = Command(connection, transaction, "DELETE FROM CLASIFICACION WHERE ID_CLASIFICACION IN (SELECT ID_CLASIFICACION FROM (" + OtherPacksSameRules + "))"))
            {
                AddSameRulesParameters(cmd, playerId, packId, hash);
                cmd.ExecuteNonQuery();
            }

            // 3. Entra si hay hueco o si supera a la última. Si el paso 2 borró algo, hay hueco; si el pack
            // ya tenía fila, la supera y por tanto también supera a la última: aquí no se pierde nada.
            long count;
            using (var cmd = Command(connection, transaction, "SELECT COUNT(*) FROM CLASIFICACION"))
            {
                count = (long)cmd.ExecuteScalar();
            }
            if (count >= MaxRows)
            {
                using (var cmd = Command(connection, transaction, "SELECT PUNTOS, FECHA FROM CLASIFICACION ORDER BY PUNTOS ASC, FECHA DESC, ID_CLASIFICACION DESC LIMIT 1"))
                {
                    Mark? last = ReadMark(cmd);
                    if (last != null && !game.Beats(last.Value)) return;
                }
            }

            using (var cmd = Command(connection, transaction,
                "INSERT OR REPLACE INTO CLASIFICACION (ID_JUGADOR, ID_PACK, ID_PARTIDA, PUNTOS, PASOS, FUEGOS, FECHA) " +
                "VALUES ($j, $k, $id, $pts, $s, $f, $d)"))
            {
                cmd.Parameters.AddWithValue("$j", playerId);
                cmd.Parameters.AddWithValue("$k", packId);
                cmd.Parameters.AddWithValue("$id", partidaId);
                cmd.Parameters.AddWithValue("$pts", game.Points);
                cmd.Parameters.AddWithValue("$s", steps);
                cmd.Parameters.AddWithValue("$f", fires);
                cmd.Parameters.AddWithValue("$d", game.Date);
                cmd.ExecuteNonQuery();
            }

            // 4. Borra lo que sobre por encima de 100.
            using (var cmd = Command(connection, transaction,
                "DELETE FROM CLASIFICACION WHERE ID_CLASIFICACION NOT IN " +
                "(SELECT ID_CLASIFICACION FROM CLASIFICACION ORDER BY " + Ordering + " LIMIT $max)"))
            {
                cmd.Parameters.AddWithValue("$max", MaxRows);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Posición (desde 1) del pack en la clasificación, o null si no está en el top.</summary>
        public static long? RankOfPack(SqliteConnection connection, SqliteTransaction transaction, long packId)
        {
            using (var cmd = Command(connection, transaction,
                "SELECT POSICION FROM (SELECT ID_PACK, ROW_NUMBER() OVER (ORDER BY " + Ordering + ") AS POSICION FROM CLASIFICACION) WHERE ID_PACK = $k"))
            {
                cmd.Parameters.AddWithValue("$k", packId);
                object rank = cmd.ExecuteScalar();
                return rank == null ? (long?)null : Convert.ToInt64(rank);
            }
        }

        private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction, string sql)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            return cmd;
        }

        private static void AddSameRulesParameters(SqliteCommand cmd, long playerId, long packId, string hash)
        {
            cmd.Parameters.AddWithValue("$j", playerId);
            cmd.Parameters.AddWithValue("$k", packId);
            cmd.Parameters.AddWithValue("$h", hash);
        }

        private static Mark? ReadMark(SqliteCommand cmd)
        {
            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.Read()) return null;
                return new Mark(reader.GetInt64(0), reader.GetString(1));
            }
        }
    }
}
