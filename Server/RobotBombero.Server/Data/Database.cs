using Microsoft.Data.Sqlite;

namespace RobotBombero.Server.Data
{
    public class Database
    {
        private readonly string connectionString;

        public Database(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public SqliteConnection Open()
        {
            var connection = new SqliteConnection(connectionString);
            connection.Open();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys=ON";
                cmd.ExecuteNonQuery();
            }
            return connection;
        }

        public void Migrate()
        {
            using (var connection = Open())
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = Schema;
                cmd.ExecuteNonQuery();
            }
        }

        private const string Schema = @"
CREATE TABLE IF NOT EXISTS JUGADOR (
    ID_JUGADOR INTEGER PRIMARY KEY AUTOINCREMENT,
    NOMBRE TEXT NOT NULL UNIQUE COLLATE NOCASE,
    HUELLA_CLAVE TEXT NOT NULL,
    FECHA_CREACION TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS PACK (
    ID_PACK INTEGER PRIMARY KEY AUTOINCREMENT,
    ID_JUGADOR INTEGER NOT NULL REFERENCES JUGADOR(ID_JUGADOR),
    NOMBRE TEXT NOT NULL,
    REGLAS_HASH TEXT NOT NULL,
    FECHA_CREACION TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS PARTIDA (
    ID_PARTIDA INTEGER PRIMARY KEY AUTOINCREMENT,
    ID_PACK INTEGER NOT NULL REFERENCES PACK(ID_PACK),
    SEMILLA INTEGER NOT NULL,
    REGLAS_JSON TEXT NOT NULL,
    REGLAS_HASH TEXT NOT NULL,
    ESTADO TEXT NOT NULL CHECK (ESTADO IN ('EN_CURSO','TERMINADA')),
    CADUCA TEXT NOT NULL,
    PASOS INTEGER,
    FUEGOS INTEGER,
    PUNTOS INTEGER,
    CAUSA_MUERTE TEXT,
    FECHA TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS CLASIFICACION (
    ID_CLASIFICACION INTEGER PRIMARY KEY AUTOINCREMENT,
    ID_JUGADOR INTEGER NOT NULL REFERENCES JUGADOR(ID_JUGADOR),
    ID_PACK INTEGER NOT NULL UNIQUE REFERENCES PACK(ID_PACK),
    ID_PARTIDA INTEGER NOT NULL REFERENCES PARTIDA(ID_PARTIDA),
    PUNTOS INTEGER NOT NULL,
    PASOS INTEGER NOT NULL,
    FUEGOS INTEGER NOT NULL,
    FECHA TEXT NOT NULL
);";
    }
}
