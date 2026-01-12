// ============================================================
// File: Data/ObjectAttributesDao.cs
// Project: StarMap2010
//
// Simple DAO for object_attributes.
// Reads attribute key/value pairs for a given system_object.
// ============================================================

using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace StarMap2010.Data
{
    public sealed class ObjectAttributesDao
    {
        private readonly string _dbPath;

        public ObjectAttributesDao(string dbPath)
        {
            _dbPath = dbPath;
        }

        private SQLiteConnection Open()
        {
            var cs = "Data Source=" + _dbPath + ";Version=3;";
            var con = new SQLiteConnection(cs);
            con.Open();
            return con;
        }

        // Returns a dictionary of attr_key -> best-effort string value
        public Dictionary<string, string> LoadAttributesAsText(string objectId)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(objectId)) return map;

            using (var con = Open())
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT attr_key, value_text, value_num, value_int, value_bool " +
                    "FROM object_attributes " +
                    "WHERE object_id = @id " +
                    "ORDER BY attr_key;";

                cmd.Parameters.AddWithValue("@id", objectId.Trim());

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string key = Convert.ToString(r["attr_key"]);
                        if (string.IsNullOrWhiteSpace(key)) continue;
                        key = key.Trim();

                        object vt = r["value_text"];
                        object vn = r["value_num"];
                        object vi = r["value_int"];
                        object vb = r["value_bool"];

                        string s = null;

                        if (vt != DBNull.Value) s = Convert.ToString(vt);
                        else if (vn != DBNull.Value) s = Convert.ToString(vn);
                        else if (vi != DBNull.Value) s = Convert.ToString(vi);
                        else if (vb != DBNull.Value)
                        {
                            int b = Convert.ToInt32(vb);
                            s = (b != 0) ? "Yes" : "No";
                        }

                        if (s == null) s = "";
                        map[key] = s;
                    }
                }
            }

            return map;
        }
    }
}
