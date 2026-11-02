using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using RobotBombero.Server.Data;
using Xunit;

namespace RobotBombero.Tests
{
    public class DatabaseTests
    {
        private static readonly string[] Tables = { "JUGADOR", "PACK", "PARTIDA", "CLASIFICACION", "INICIO_PARTIDA" };

        // En memoria compartida: la base vive mientras haya una conexión abierta.
        private static Database NewDb(out SqliteConnection keepAlive)
        {
            string cs = "Data Source=file:" + System.Guid.NewGuid().ToString("N") + "?mode=memory&cache=shared";
            keepAlive = new SqliteConnection(cs);
            keepAlive.Open();
            var db = new Database(cs);
            db.Migrate();
            return db;
        }

        private static long Scalar(SqliteConnection c, string sql)
        {
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = sql;
                return (long)cmd.ExecuteScalar();
            }
        }

        private static void Exec(SqliteConnection c, string sql)
        {
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        [Fact]
        public void Migrate_IsIdempotent()
        {
            var db = NewDb(out var keep);
            using (keep)
            {
                db.Migrate();
                db.Migrate();
                using (var c = db.Open())
                    Assert.Equal(5, Scalar(c, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"));
            }
        }

        [Fact]
        public void Every_Table_Has_Autoincrement_Id_Named_After_It()
        {
            var db = NewDb(out var keep);
            using (keep)
            using (var c = db.Open())
            {
                foreach (string table in Tables)
                {
                    var pks = new List<string>();
                    using (var cmd = c.CreateCommand())
                    {
                        cmd.CommandText = "PRAGMA table_info(" + table + ")";
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                                if (r.GetInt32(5) > 0) pks.Add(r.GetString(1));
                    }
                    Assert.Equal(new[] { "ID_" + table }, pks);

                    using (var cmd = c.CreateCommand())
                    {
                        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE name=$n";
                        cmd.Parameters.AddWithValue("$n", table);
                        string sql = (string)cmd.ExecuteScalar();
                        Assert.Contains("AUTOINCREMENT", sql.ToUpperInvariant());
                    }
                }
            }
        }

        [Fact]
        public void Duplicate_Name_DifferentCase_Rejected()
        {
            var db = NewDb(out var keep);
            using (keep)
            using (var c = db.Open())
            {
                Exec(c, "INSERT INTO JUGADOR(NOMBRE,HUELLA_CLAVE,FECHA_CREACION) VALUES('Ana','k','2026-01-01')");
                Assert.Throws<SqliteException>(() =>
                    Exec(c, "INSERT INTO JUGADOR(NOMBRE,HUELLA_CLAVE,FECHA_CREACION) VALUES('ANA','k2','2026-01-01')"));
            }
        }

        [Fact]
        public void Clasificacion_UniquePerPlayer_NotPerPack()
        {
            var db = NewDb(out var keep);
            using (keep)
            using (var c = db.Open())
            {
                Exec(c, "INSERT INTO JUGADOR(NOMBRE,HUELLA_CLAVE,FECHA_CREACION) VALUES('Ana','k','2026-01-01'),('Beto','k2','2026-01-01')");
                Exec(c, "INSERT INTO PACK(ID_JUGADOR,NOMBRE,REGLAS_HASH,FECHA_CREACION) VALUES(1,'p','h','2026-01-01')");
                Exec(c, "INSERT INTO PARTIDA(ID_PACK,SEMILLA,REGLAS_JSON,REGLAS_HASH,ESTADO,CADUCA,FECHA) VALUES(1,1,'[]','h','EN_CURSO','2999','2026-01-01')");
                Exec(c, "INSERT INTO CLASIFICACION(ID_JUGADOR,ID_PACK,ID_PARTIDA,PUNTOS,PASOS,FUEGOS,FECHA) VALUES(1,1,1,1,1,1,'2026-01-01')");

                Assert.Throws<SqliteException>(() =>
                    Exec(c, "INSERT INTO CLASIFICACION(ID_JUGADOR,ID_PACK,ID_PARTIDA,PUNTOS,PASOS,FUEGOS,FECHA) VALUES(1,1,1,2,1,1,'2026-01-01')"));
                Exec(c, "INSERT INTO CLASIFICACION(ID_JUGADOR,ID_PACK,ID_PARTIDA,PUNTOS,PASOS,FUEGOS,FECHA) VALUES(2,1,1,2,1,1,'2026-01-01')");
            }
        }

        [Fact]
        public void ForeignKeys_Enforced()
        {
            var db = NewDb(out var keep);
            using (keep)
            using (var c = db.Open())
            {
                Assert.Throws<SqliteException>(() =>
                    Exec(c, "INSERT INTO PACK(ID_JUGADOR,NOMBRE,REGLAS_HASH,FECHA_CREACION) VALUES(999,'p','h','2026-01-01')"));
            }
        }
    }
}
