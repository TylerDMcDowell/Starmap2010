using System;
using System.Data;
using System.Data.SQLite;

namespace StarMap2010.Data
{
    public sealed class CompareSwapDao
    {
        private readonly string _dbPath;

        public CompareSwapDao(string dbPath)
        {
            _dbPath = dbPath;
        }

        public DataTable LoadTwoSystemsForCompare(string systemIdA, string systemIdB)
        {
            if (string.IsNullOrWhiteSpace(systemIdA) || string.IsNullOrWhiteSpace(systemIdB))
                throw new ArgumentException("Both system ids are required.");

            using (var conn = Db.Open(_dbPath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT system_id, system_name, real_system_name, primary_star_name, primary_star_type, system_type," +
                    " government_id, notes" +
                    " FROM star_systems" +
                    " WHERE system_id IN (@a, @b);";

                Db.AddParam(cmd, "@a", systemIdA.Trim());
                Db.AddParam(cmd, "@b", systemIdB.Trim());

                var dt = new DataTable();
                using (var da = new SQLiteDataAdapter(cmd))
                {
                    da.Fill(dt);
                }
                return dt;
            }
        }

        public void UpdateTwoSystems(DataRow rowA, DataRow rowB)
        {
            if (rowA == null || rowB == null)
                throw new ArgumentNullException("Both DataRows must be provided.");

            using (var conn = Db.Open(_dbPath))
            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    UpdateOne(conn, tx, rowA);
                    UpdateOne(conn, tx, rowB);
                    tx.Commit();
                }
                catch
                {
                    try { tx.Rollback(); }
                    catch { }
                    throw;
                }
            }
        }

        private static void UpdateOne(SQLiteConnection conn, SQLiteTransaction tx, DataRow r)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;

                cmd.CommandText =
                    "UPDATE star_systems SET " +
                    " system_name=@system_name," +
                    " real_system_name=@real_system_name," +
                    " primary_star_name=@primary_star_name," +
                    " primary_star_type=@primary_star_type," +
                    " system_type=@system_type," +
                    " government_id=@government_id," +
                    " notes=@notes" +
                    " WHERE system_id=@system_id;";

                Db.AddParam(cmd, "@system_name", Convert.ToString(r["system_name"]) ?? "");
                Db.AddParam(cmd, "@real_system_name", Convert.ToString(r["real_system_name"]) ?? "");
                Db.AddParam(cmd, "@primary_star_name", Convert.ToString(r["primary_star_name"]) ?? "");
                Db.AddParam(cmd, "@primary_star_type", Convert.ToString(r["primary_star_type"]) ?? "");
                Db.AddParam(cmd, "@system_type", Convert.ToString(r["system_type"]) ?? "");
                Db.AddParam(cmd, "@government_id", Convert.ToString(r["government_id"]) ?? "");
                Db.AddParam(cmd, "@notes", Convert.ToString(r["notes"]) ?? "");
                Db.AddParam(cmd, "@system_id", Convert.ToString(r["system_id"]) ?? "");

                int n = cmd.ExecuteNonQuery();
                if (n != 1)
                    throw new Exception("Update affected " + n + " rows for system_id=" + Convert.ToString(r["system_id"]));
            }
        }
    }
}
