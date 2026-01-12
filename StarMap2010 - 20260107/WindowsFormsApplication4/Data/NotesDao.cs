using System;
using System.Data.SQLite;

namespace StarMap2010.Data
{
    public sealed class NotesDao
    {
        private readonly string _dbPath;

        public NotesDao(string dbPath)
        {
            if (string.IsNullOrEmpty(dbPath))
                throw new ArgumentException("dbPath is null/empty", "dbPath");

            _dbPath = dbPath;
        }

        private SQLiteConnection Open()
        {
            var cs = "Data Source=" + _dbPath + ";Version=3;";
            var conn = new SQLiteConnection(cs);
            conn.Open();
            return conn;
        }

        private static string Norm(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        public string LoadNotes(string relatedTable, string relatedId)
        {
            relatedTable = Norm(relatedTable);
            relatedId = Norm(relatedId);

            if (string.IsNullOrEmpty(relatedTable)) throw new ArgumentException("relatedTable required", "relatedTable");
            if (string.IsNullOrEmpty(relatedId)) throw new ArgumentException("relatedId required", "relatedId");

            string sql;

            if (string.Equals(relatedTable, "star_systems", StringComparison.OrdinalIgnoreCase))
            {
                sql = "SELECT notes FROM star_systems WHERE system_id=@id LIMIT 1";
            }
            else if (string.Equals(relatedTable, "system_objects", StringComparison.OrdinalIgnoreCase))
            {
                sql = "SELECT notes FROM system_objects WHERE object_id=@id LIMIT 1";
            }
            else if (string.Equals(relatedTable, "jump_gates", StringComparison.OrdinalIgnoreCase))
            {
                sql = "SELECT notes FROM jump_gates WHERE gate_id=@id LIMIT 1";
            }
            else
            {
                throw new NotSupportedException("Unsupported notes table: " + relatedTable);
            }

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@id", relatedId);

                object v = cmd.ExecuteScalar();
                if (v == null || v == DBNull.Value) return "";
                return Convert.ToString(v);
            }
        }

        public void SaveNotes(string relatedTable, string relatedId, string notes)
        {
            relatedTable = Norm(relatedTable);
            relatedId = Norm(relatedId);

            if (string.IsNullOrEmpty(relatedTable)) throw new ArgumentException("relatedTable required", "relatedTable");
            if (string.IsNullOrEmpty(relatedId)) throw new ArgumentException("relatedId required", "relatedId");

            // We store NULL for empty notes (keeps DB tidy)
            object notesVal = string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : (object)notes;

            string sql;

            if (string.Equals(relatedTable, "star_systems", StringComparison.OrdinalIgnoreCase))
            {
                sql = "UPDATE star_systems SET notes=@n WHERE system_id=@id";
            }
            else if (string.Equals(relatedTable, "system_objects", StringComparison.OrdinalIgnoreCase))
            {
                sql = "UPDATE system_objects SET notes=@n, updated_utc=datetime('now') WHERE object_id=@id";
            }
            else if (string.Equals(relatedTable, "jump_gates", StringComparison.OrdinalIgnoreCase))
            {
                sql = "UPDATE jump_gates SET notes=@n WHERE gate_id=@id";
            }
            else
            {
                throw new NotSupportedException("Unsupported notes table: " + relatedTable);
            }

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@n", notesVal);
                cmd.Parameters.AddWithValue("@id", relatedId);

                cmd.ExecuteNonQuery();
            }
        }
    }
}
