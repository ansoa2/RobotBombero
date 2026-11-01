using System;
using Microsoft.Data.Sqlite;

namespace RobotBombero.Server.Leaderboard
{
    /// <summary>
    /// Mantiene CLASIFICACION: una fila por jugador, con su mejor partida vigente, y 100 como máximo.
    /// "Superar" significa quedar por encima en el orden de la clasificación: más puntos o, con los mismos, una
    /// fecha anterior. Así el resultado no depende del orden en que se terminen las partidas.
    /// </summary>
    public static class LeaderboardUpdater
    {
        public const int MaxRows = 100;

        /// <summary>El orden público de la clasificación; lo comparten la API y este actualizador.</summary>
        public const string Ordering = "PUNTOS DESC, FECHA ASC, ID_CLASIFICACION ASC";

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
            Place(connection, transaction, partidaId, boardWasFull: false);
        }

        /// <summary>
        /// Llamar en la misma transacción cuando cambian las reglas de un pack (ya con su nueva huella guardada)
        /// o antes de borrarlo. Si el pack da su puesto al jugador, retira la fila y la pasa a su mejor partida
        /// vigente, si entra en el top. Si no, no cambia nada.
        /// </summary>
        public static void OnPackInvalidated(SqliteConnection connection, SqliteTransaction transaction, long packId, bool packBeingDeleted)
        {
            long playerId;
            using (var cmd = Command(connection, transaction, "SELECT ID_JUGADOR FROM CLASIFICACION WHERE ID_PACK = $k"))
            {
                cmd.Parameters.AddWithValue("$k", packId);
                object holder = cmd.ExecuteScalar();
                if (holder == null) return;
                playerId = Convert.ToInt64(holder);
            }

            // Con el top lleno, retirar la fila no abre un hueco para cualquiera: partidas que quedaron
            // fuera pueden valer más que esta, así que debe superar a la última igual que siempre.
            bool boardWasFull = CountRows(connection, transaction) >= MaxRows;

            using (var cmd = Command(connection, transaction, "DELETE FROM CLASIFICACION WHERE ID_JUGADOR = $j"))
            {
                cmd.Parameters.AddWithValue("$j", playerId);
                cmd.ExecuteNonQuery();
            }

            object best;
            using (var cmd = Command(connection, transaction,
                "SELECT p.ID_PARTIDA FROM PARTIDA p JOIN PACK k ON k.ID_PACK = p.ID_PACK " +
                "WHERE k.ID_JUGADOR = $j AND p.ESTADO = 'TERMINADA' AND p.REGLAS_HASH = k.REGLAS_HASH " +
                "AND (p.ID_PACK <> $k OR $deleted = 0) " +
                "ORDER BY p.PUNTOS DESC, p.FECHA ASC, p.ID_PARTIDA ASC LIMIT 1"))
            {
                cmd.Parameters.AddWithValue("$j", playerId);
                cmd.Parameters.AddWithValue("$k", packId);
                cmd.Parameters.AddWithValue("$deleted", packBeingDeleted ? 1 : 0);
                best = cmd.ExecuteScalar();
            }
            if (best != null) Place(connection, transaction, Convert.ToInt64(best), boardWasFull);
        }

        /// <summary>Posición (desde 1) de la partida, o null si no es la que tiene la fila de su jugador.</summary>
        public static long? RankOfGame(SqliteConnection connection, SqliteTransaction transaction, long partidaId)
        {
            using (var cmd = Command(connection, transaction,
                "SELECT POSICION FROM (SELECT ID_PARTIDA, ROW_NUMBER() OVER (ORDER BY " + Ordering + ") AS POSICION FROM CLASIFICACION) WHERE ID_PARTIDA = $id"))
            {
                cmd.Parameters.AddWithValue("$id", partidaId);
                object rank = cmd.ExecuteScalar();
                return rank == null ? (long?)null : Convert.ToInt64(rank);
            }
        }

        private static void Place(SqliteConnection connection, SqliteTransaction transaction, long partidaId, bool boardWasFull)
        {
            long packId, playerId;
            int steps, fires;
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
                    if (reader.GetString(6) != reader.GetString(7)) return;

                    packId = reader.GetInt64(0);
                    playerId = reader.GetInt64(1);
                    game = new Mark(reader.GetInt64(2), reader.GetString(5));
                    steps = reader.GetInt32(3);
                    fires = reader.GetInt32(4);
                }
            }

            // Una partida de 0 puntos no entra, aunque haya sitio.
            if (game.Points <= 0) return;

            // 1. Si el jugador ya tiene fila, sigue solo si la partida la supera, venga del pack que venga.
            using (var cmd = Command(connection, transaction, "SELECT PUNTOS, FECHA FROM CLASIFICACION WHERE ID_JUGADOR = $j"))
            {
                cmd.Parameters.AddWithValue("$j", playerId);
                Mark? own = ReadMark(cmd);
                if (own != null && !game.Beats(own.Value)) return;
            }

            // 2. Entra si hay hueco o si supera a la última. Si el jugador ya tenía fila y la ha superado,
            // también supera a la última, así que aquí no se pierde ninguna fila.
            if (boardWasFull || CountRows(connection, transaction) >= MaxRows)
            {
                using (var cmd = Command(connection, transaction, "SELECT PUNTOS, FECHA FROM CLASIFICACION ORDER BY PUNTOS ASC, FECHA DESC, ID_CLASIFICACION DESC LIMIT 1"))
                {
                    Mark? last = ReadMark(cmd);
                    if (last != null && !game.Beats(last.Value)) return;
                }
            }

            // La restricción UNIQUE de ID_JUGADOR hace que esto reemplace la fila anterior del jugador.
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

            // 3. Borra lo que sobre por encima de 100.
            using (var cmd = Command(connection, transaction,
                "DELETE FROM CLASIFICACION WHERE ID_CLASIFICACION NOT IN " +
                "(SELECT ID_CLASIFICACION FROM CLASIFICACION ORDER BY " + Ordering + " LIMIT $max)"))
            {
                cmd.Parameters.AddWithValue("$max", MaxRows);
                cmd.ExecuteNonQuery();
            }
        }

        private static long CountRows(SqliteConnection connection, SqliteTransaction transaction)
        {
            using (var cmd = Command(connection, transaction, "SELECT COUNT(*) FROM CLASIFICACION"))
            {
                return (long)cmd.ExecuteScalar();
            }
        }

        private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction transaction, string sql)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            return cmd;
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
