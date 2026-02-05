using System;
using System.Data;
using System.Data.SQLite;


namespace StarMap2010.Data
{
    public static class Db
    {
        public static SQLiteConnection Open(string dbPath)
        {
            string cs = "Data Source=" + dbPath + ";Version=3;Busy Timeout=5000;";

            var conn = new SQLiteConnection(cs);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON;";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "PRAGMA journal_mode=WAL;";
                cmd.ExecuteNonQuery();

                cmd.CommandText = "PRAGMA busy_timeout=5000;";
                cmd.ExecuteNonQuery();
            }

            return conn;
        }

        public static string GetString(IDataRecord r, string col)
        {
            int i = r.GetOrdinal(col);
            if (r.IsDBNull(i)) return "";
            return Convert.ToString(r.GetValue(i)) ?? "";
        }

        public static string GetStringNullOk(IDataRecord r, string col)
        {
            int i = r.GetOrdinal(col);
            if (r.IsDBNull(i)) return null;
            return Convert.ToString(r.GetValue(i));
        }

        public static int GetInt32(IDataRecord r, string col)
        {
            int i = r.GetOrdinal(col);
            if (r.IsDBNull(i)) return 0;
            return Convert.ToInt32(r.GetValue(i));
        }

        public static long GetInt64(IDataRecord r, string col)
        {
            int i = r.GetOrdinal(col);
            if (r.IsDBNull(i)) return 0;
            return Convert.ToInt64(r.GetValue(i));
        }

        public static float GetFloat(IDataRecord r, string col)
        {
            int i = r.GetOrdinal(col);
            if (r.IsDBNull(i)) return 0f;
            return Convert.ToSingle(r.GetValue(i));
        }

        public static double GetDouble(IDataRecord r, string col)
        {
            int i = r.GetOrdinal(col);
            if (r.IsDBNull(i)) return 0.0;
            return Convert.ToDouble(r.GetValue(i));
        }

        public static void AddParam(SQLiteCommand cmd, string name, object value)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }
}
