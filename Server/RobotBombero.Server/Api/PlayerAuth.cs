using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace RobotBombero.Server.Api
{
    /// <summary>Clave de invitado: se genera una vez, solo se guarda su SHA-256 y se compara en tiempo constante.</summary>
    public static class PlayerAuth
    {
        public const string HeaderName = "X-Player-Key";

        public static string NewKey()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        }

        public static string Fingerprint(string key)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();
        }

        /// <summary>Devuelve el id del jugador dueño de la clave de la cabecera, o null si falta o no es válida.</summary>
        public static long? Authenticate(HttpRequest request, SqliteConnection connection)
        {
            string key = request.Headers[HeaderName].ToString();
            if (string.IsNullOrEmpty(key) || key.Length > 128) return null;

            byte[] presented = Encoding.ASCII.GetBytes(Fingerprint(key));
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT ID_JUGADOR, HUELLA_CLAVE FROM JUGADOR WHERE HUELLA_CLAVE = $h";
                cmd.Parameters.AddWithValue("$h", Encoding.ASCII.GetString(presented));
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read()) return null;
                    byte[] stored = Encoding.ASCII.GetBytes(reader.GetString(1));
                    if (stored.Length != presented.Length || !CryptographicOperations.FixedTimeEquals(stored, presented))
                        return null;
                    return reader.GetInt64(0);
                }
            }
        }
    }
}
