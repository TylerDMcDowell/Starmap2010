using System.Collections.Generic;
using System.Data.SQLite;
using StarMap2010.Models;

namespace StarMap2010.Data
{
    public sealed class StarSystemsDao
    {
        private readonly string _dbPath;

        public StarSystemsDao(string dbPath)
        {
            _dbPath = dbPath;
        }

        public List<StarSystemInfo> LoadSystemsForMap()
        {
            var systems = new List<StarSystemInfo>();

            using (var conn = Db.Open(_dbPath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT s.system_id, s.system_name, s.real_system_name, s.primary_star_name, s.primary_star_type, s.system_type," +
                    " g.government_name, g.faction_color, st.color_hex," +
                    " s.real_x_ly, s.real_y_ly, s.real_z_ly," +
                    " s.screen_x, s.screen_y," +
                    " s.notes" +
                    " FROM star_systems s" +
                    " JOIN governments g ON s.government_id = g.government_id" +
                    " LEFT JOIN star_types st ON st.star_key = s.primary_star_type" +
                    " ORDER BY g.government_name DESC, s.distance_ly;";

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var s = new StarSystemInfo();

                        s.SystemId = Db.GetString(r, "system_id");
                        s.SystemName = Db.GetString(r, "system_name");
                        s.RealSystemName = Db.GetString(r, "real_system_name");
                        s.PrimaryStarName = Db.GetString(r, "primary_star_name");
                        s.PrimaryStarType = Db.GetString(r, "primary_star_type");

                        s.primaryStarColor = Db.GetStringNullOk(r, "color_hex") ?? "";
                        s.SystemType = Db.GetString(r, "system_type");

                        s.GovernmentName = Db.GetString(r, "government_name");
                        s.FactionColor = Db.GetString(r, "faction_color");

                        s.ScreenX = (int)Db.GetInt64(r, "screen_x");
                        s.ScreenY = (int)Db.GetInt64(r, "screen_y");

                        s.XReal = Db.GetFloat(r, "real_x_ly");
                        s.YReal = Db.GetFloat(r, "real_y_ly");
                        s.ZReal = Db.GetFloat(r, "real_z_ly");

                        s.Notes = Db.GetStringNullOk(r, "notes") ?? "";

                        systems.Add(s);
                    }
                }
            }

            return systems;
        }

        public StarSystemInfo GetSystemById(string systemId)
        {
            if (string.IsNullOrWhiteSpace(systemId)) return null;

            using (var conn = Db.Open(_dbPath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT s.system_id, s.system_name, s.real_system_name, s.primary_star_name, s.primary_star_type, s.system_type," +
                    " g.government_name, g.faction_color, st.color_hex," +
                    " s.real_x_ly, s.real_y_ly, s.real_z_ly," +
                    " s.screen_x, s.screen_y," +
                    " s.notes" +
                    " FROM star_systems s" +
                    " JOIN governments g ON s.government_id = g.government_id" +
                    " LEFT JOIN star_types st ON st.star_key = s.primary_star_type" +
                    " WHERE s.system_id = @id;";

                Db.AddParam(cmd, "@id", systemId.Trim());

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;

                    var s = new StarSystemInfo();
                    s.SystemId = Db.GetString(r, "system_id");
                    s.SystemName = Db.GetString(r, "system_name");
                    s.RealSystemName = Db.GetString(r, "real_system_name");
                    s.PrimaryStarName = Db.GetString(r, "primary_star_name");
                    s.PrimaryStarType = Db.GetString(r, "primary_star_type");
                    s.primaryStarColor = Db.GetStringNullOk(r, "color_hex") ?? "";
                    s.SystemType = Db.GetString(r, "system_type");
                    s.GovernmentName = Db.GetString(r, "government_name");
                    s.FactionColor = Db.GetString(r, "faction_color");
                    s.ScreenX = (int)Db.GetInt64(r, "screen_x");
                    s.ScreenY = (int)Db.GetInt64(r, "screen_y");
                    s.XReal = Db.GetFloat(r, "real_x_ly");
                    s.YReal = Db.GetFloat(r, "real_y_ly");
                    s.ZReal = Db.GetFloat(r, "real_z_ly");
                    s.Notes = Db.GetStringNullOk(r, "notes") ?? "";
                    return s;
                }
            }
        }
    }
}
