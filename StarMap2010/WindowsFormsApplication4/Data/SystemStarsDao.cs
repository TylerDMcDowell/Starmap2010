using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using StarMap2010.Models;

namespace StarMap2010.Data
{
    public sealed class SystemStarsDao
    {
        private readonly string _dbPath;

        public SystemStarsDao(string dbPath)
        {
            if (string.IsNullOrEmpty(dbPath))
                throw new ArgumentException("dbPath is null/empty", "dbPath");

            _dbPath = dbPath;
        }

        private SQLiteConnection Open()
        {
            // Match your other DAOs
            string cs = "Data Source=" + _dbPath + ";Version=3;";
            var conn = new SQLiteConnection(cs);
            conn.Open();
            return conn;
        }

        private static string Norm(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static string ReadString(IDataRecord r, int ord)
        {
            if (ord < 0) return null;
            if (r.IsDBNull(ord)) return null;
            return Convert.ToString(r.GetValue(ord));
        }

        private static int ReadInt(IDataRecord r, int ord, int def)
        {
            if (ord < 0) return def;
            if (r.IsDBNull(ord)) return def;
            try { return Convert.ToInt32(r.GetValue(ord)); }
            catch { return def; }
        }

        private static double? ReadDoubleNullable(IDataRecord r, int ord)
        {
            if (ord < 0) return null;
            if (r.IsDBNull(ord)) return null;
            try { return Convert.ToDouble(r.GetValue(ord)); }
            catch { return null; }
        }

        /// <summary>
        /// Load stars for a system. Keeps ordering stable (primary -> secondary -> tertiary -> other).
        /// </summary>
        public List<SystemStarInfo> LoadStarsForSystem(string systemId)
        {
            systemId = Norm(systemId);
            if (string.IsNullOrEmpty(systemId))
                throw new ArgumentException("systemId is null/empty", "systemId");

            var list = new List<SystemStarInfo>();

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT " +
                    " star_id, system_id, star_name, real_star_name, star_type, spectral_class, orbital_role, " +
                    " semi_major_axis_au, eccentricity " +
                    "FROM system_stars " +
                    "WHERE system_id = @sid " +
                    "ORDER BY " +
                    " CASE lower(trim(orbital_role)) " +
                    "   WHEN 'primary' THEN 0 " +
                    "   WHEN 'secondary' THEN 1 " +
                    "   WHEN 'tertiary' THEN 2 " +
                    "   ELSE 9 " +
                    " END, " +
                    " star_id;";

                cmd.Parameters.AddWithValue("@sid", systemId);

                using (var rd = cmd.ExecuteReader())
                {
                    int o_star_id = rd.GetOrdinal("star_id");
                    int o_system_id = rd.GetOrdinal("system_id");
                    int o_star_name = rd.GetOrdinal("star_name");
                    int o_real_star_name = rd.GetOrdinal("real_star_name");
                    int o_star_type = rd.GetOrdinal("star_type");
                    int o_spec = rd.GetOrdinal("spectral_class");
                    int o_role = rd.GetOrdinal("orbital_role");
                    int o_sma = rd.GetOrdinal("semi_major_axis_au");
                    int o_ecc = rd.GetOrdinal("eccentricity");

                    while (rd.Read())
                    {
                        var s = new SystemStarInfo();
                        s.StarId = ReadInt(rd, o_star_id, 0);
                        s.SystemId = ReadString(rd, o_system_id);
                        s.StarName = ReadString(rd, o_star_name);
                        s.RealStarName = ReadString(rd, o_real_star_name);
                        s.StarType = ReadString(rd, o_star_type);
                        s.SpectralClass = ReadString(rd, o_spec);
                        s.OrbitalRole = ReadString(rd, o_role);
                        s.SemiMajorAxisAu = ReadDoubleNullable(rd, o_sma);
                        s.Eccentricity = ReadDoubleNullable(rd, o_ecc);

                        list.Add(s);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Update a star row by star_id.
        /// </summary>
        public void Update(SystemStarInfo s)
        {
            if (s == null) throw new ArgumentNullException("s");
            if (s.StarId <= 0) throw new ArgumentException("StarId must be > 0", "s.StarId");

            s.SystemId = Norm(s.SystemId);
            s.StarName = Norm(s.StarName);
            s.RealStarName = Norm(s.RealStarName);
            s.StarType = Norm(s.StarType);
            s.SpectralClass = Norm(s.SpectralClass);
            s.OrbitalRole = Norm(s.OrbitalRole);

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "UPDATE system_stars SET " +
                    " system_id=@system_id, " +
                    " star_name=@star_name, " +
                    " real_star_name=@real_star_name, " +
                    " star_type=@star_type, " +
                    " spectral_class=@spectral_class, " +
                    " orbital_role=@orbital_role, " +
                    " semi_major_axis_au=@semi_major_axis_au, " +
                    " eccentricity=@eccentricity " +
                    "WHERE star_id=@star_id;";

                cmd.Parameters.AddWithValue("@system_id", (object)s.SystemId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@star_name", (object)s.StarName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@real_star_name", (object)s.RealStarName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@star_type", (object)s.StarType ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@spectral_class", (object)s.SpectralClass ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@orbital_role", (object)s.OrbitalRole ?? DBNull.Value);

                if (s.SemiMajorAxisAu.HasValue) cmd.Parameters.AddWithValue("@semi_major_axis_au", s.SemiMajorAxisAu.Value);
                else cmd.Parameters.AddWithValue("@semi_major_axis_au", DBNull.Value);

                if (s.Eccentricity.HasValue) cmd.Parameters.AddWithValue("@eccentricity", s.Eccentricity.Value);
                else cmd.Parameters.AddWithValue("@eccentricity", DBNull.Value);

                cmd.Parameters.AddWithValue("@star_id", s.StarId);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Delete by star_id (careful: if system_objects references this star, you may want to delete that node too).
        /// </summary>
        public void DeleteByStarId(int starId)
        {
            if (starId <= 0) throw new ArgumentException("starId must be > 0", "starId");

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM system_stars WHERE star_id=@id;";
                cmd.Parameters.AddWithValue("@id", starId);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
