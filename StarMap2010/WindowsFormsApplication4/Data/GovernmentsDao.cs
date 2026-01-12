using System.Data;
using System.Data.SQLite;

namespace StarMap2010.Data
{
    public sealed class GovernmentsDao
    {
        private readonly string _dbPath;

        public GovernmentsDao(string dbPath)
        {
            _dbPath = dbPath;
        }

        public DataTable GetGovernmentLookup()
        {
            using (var conn = Db.Open(_dbPath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT government_id, government_name FROM governments ORDER BY government_name;";

                var dt = new DataTable();
                using (var da = new SQLiteDataAdapter(cmd))
                {
                    da.Fill(dt);
                }
                return dt;
            }
        }
    }
}
