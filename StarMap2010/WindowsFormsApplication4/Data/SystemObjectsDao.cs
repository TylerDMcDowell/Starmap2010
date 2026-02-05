using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using StarMap2010.Models;

namespace StarMap2010.Data
{
    public sealed class SystemObjectsDao
    {
        private readonly string _dbPath;

        public SystemObjectsDao(string dbPath)
        {
            if (string.IsNullOrEmpty(dbPath))
                throw new ArgumentException("dbPath is null/empty", "dbPath");

            _dbPath = dbPath;
        }

        private SQLiteConnection Open()
        {
            // Keep consistent with your existing DAOs
            var cs = "Data Source=" + _dbPath + ";Version=3;";
            var conn = new SQLiteConnection(cs);
            conn.Open();
            return conn;
        }

        private static string Norm(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static int ReadInt(IDataRecord r, int ord, int def)
        {
            if (ord < 0) return def;
            if (r.IsDBNull(ord)) return def;

            try { return Convert.ToInt32(r.GetValue(ord)); }
            catch { return def; }
        }

        private static string ReadString(IDataRecord r, int ord)
        {
            if (ord < 0) return null;
            if (r.IsDBNull(ord)) return null;
            return Convert.ToString(r.GetValue(ord));
        }

        /// <summary>
        /// Loads ALL system_objects for a system_id.
        /// Caller builds a tree by parent_object_id.
        /// </summary>
        public List<SystemObjectInfo> LoadObjectsForSystem(string systemId)
        {
            systemId = Norm(systemId);
            if (string.IsNullOrEmpty(systemId))
                throw new ArgumentException("systemId is null/empty", "systemId");

            var list = new List<SystemObjectInfo>();

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT " +
                    " object_id, system_id, object_kind, parent_object_id, orbit_host_object_id, radial_order, " +
                    " display_name, notes, related_table, related_id, flags, created_utc, updated_utc " +
                    "FROM system_objects " +
                    "WHERE system_id = @sid " +
                    // Stable order helps deterministic tree builds
                    "ORDER BY COALESCE(parent_object_id,''), radial_order, object_kind, display_name COLLATE NOCASE;";

                cmd.Parameters.AddWithValue("@sid", systemId);

                using (var rd = cmd.ExecuteReader())
                {
                    int o_object_id = rd.GetOrdinal("object_id");
                    int o_system_id = rd.GetOrdinal("system_id");
                    int o_kind = rd.GetOrdinal("object_kind");
                    int o_parent = rd.GetOrdinal("parent_object_id");
                    int o_orbit = rd.GetOrdinal("orbit_host_object_id");
                    int o_radial = rd.GetOrdinal("radial_order");
                    int o_name = rd.GetOrdinal("display_name");
                    int o_notes = rd.GetOrdinal("notes");
                    int o_rtable = rd.GetOrdinal("related_table");
                    int o_rid = rd.GetOrdinal("related_id");
                    int o_flags = rd.GetOrdinal("flags");
                    int o_created = rd.GetOrdinal("created_utc");
                    int o_updated = rd.GetOrdinal("updated_utc");

                    while (rd.Read())
                    {
                        var x = new SystemObjectInfo();

                        x.ObjectId = ReadString(rd, o_object_id);
                        x.SystemId = ReadString(rd, o_system_id);
                        x.ObjectKind = ReadString(rd, o_kind);
                        x.ParentObjectId = ReadString(rd, o_parent);
                        x.OrbitHostObjectId = ReadString(rd, o_orbit);
                        x.RadialOrder = ReadInt(rd, o_radial, 0);

                        x.DisplayName = ReadString(rd, o_name);
                        x.Notes = ReadString(rd, o_notes);

                        x.RelatedTable = ReadString(rd, o_rtable);
                        x.RelatedId = ReadString(rd, o_rid);
                        x.Flags = ReadString(rd, o_flags);

                        x.CreatedUtc = ReadString(rd, o_created);
                        x.UpdatedUtc = ReadString(rd, o_updated);

                        list.Add(x);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Upsert by object_id.
        /// If exists -> update; else insert.
        /// </summary>
        public void Upsert(SystemObjectInfo o)
        {
            if (o == null) throw new ArgumentNullException("o");

            o.ObjectId = Norm(o.ObjectId);
            o.SystemId = Norm(o.SystemId);
            o.ObjectKind = Norm(o.ObjectKind);
            o.ParentObjectId = Norm(o.ParentObjectId);
            o.OrbitHostObjectId = Norm(o.OrbitHostObjectId);
            o.DisplayName = Norm(o.DisplayName);
            o.Notes = Norm(o.Notes);
            o.RelatedTable = Norm(o.RelatedTable);
            o.RelatedId = Norm(o.RelatedId);
            o.Flags = Norm(o.Flags);

            if (string.IsNullOrEmpty(o.ObjectId)) throw new ArgumentException("ObjectId required", "o.ObjectId");
            if (string.IsNullOrEmpty(o.SystemId)) throw new ArgumentException("SystemId required", "o.SystemId");
            if (string.IsNullOrEmpty(o.ObjectKind)) throw new ArgumentException("ObjectKind required", "o.ObjectKind");
            if (string.IsNullOrEmpty(o.DisplayName)) throw new ArgumentException("DisplayName required", "o.DisplayName");

            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                bool exists;

                using (var chk = conn.CreateCommand())
                {
                    chk.Transaction = tx;
                    chk.CommandText = "SELECT 1 FROM system_objects WHERE object_id=@id LIMIT 1";
                    chk.Parameters.AddWithValue("@id", o.ObjectId);

                    object v = chk.ExecuteScalar();
                    exists = (v != null && v != DBNull.Value);
                }

                if (!exists)
                {
                    using (var ins = conn.CreateCommand())
                    {
                        ins.Transaction = tx;
                        ins.CommandText =
                            "INSERT INTO system_objects (" +
                            " object_id, system_id, object_kind, parent_object_id, orbit_host_object_id, radial_order," +
                            " display_name, notes, related_table, related_id, flags, created_utc, updated_utc" +
                            ") VALUES (" +
                            " @object_id, @system_id, @object_kind, @parent_object_id, @orbit_host_object_id, @radial_order," +
                            " @display_name, @notes, @related_table, @related_id, @flags, datetime('now'), datetime('now')" +
                            ")";

                        ins.Parameters.AddWithValue("@object_id", o.ObjectId);
                        ins.Parameters.AddWithValue("@system_id", o.SystemId);
                        ins.Parameters.AddWithValue("@object_kind", o.ObjectKind);
                        ins.Parameters.AddWithValue("@parent_object_id", (object)o.ParentObjectId ?? DBNull.Value);
                        ins.Parameters.AddWithValue("@orbit_host_object_id", (object)o.OrbitHostObjectId ?? DBNull.Value);
                        ins.Parameters.AddWithValue("@radial_order", o.RadialOrder);

                        ins.Parameters.AddWithValue("@display_name", o.DisplayName);
                        ins.Parameters.AddWithValue("@notes", (object)o.Notes ?? DBNull.Value);

                        ins.Parameters.AddWithValue("@related_table", (object)o.RelatedTable ?? DBNull.Value);
                        ins.Parameters.AddWithValue("@related_id", (object)o.RelatedId ?? DBNull.Value);
                        ins.Parameters.AddWithValue("@flags", (object)o.Flags ?? DBNull.Value);

                        ins.ExecuteNonQuery();
                    }
                }
                else
                {
                    using (var upd = conn.CreateCommand())
                    {
                        upd.Transaction = tx;
                        upd.CommandText =
                            "UPDATE system_objects SET " +
                            " system_id=@system_id," +
                            " object_kind=@object_kind," +
                            " parent_object_id=@parent_object_id," +
                            " orbit_host_object_id=@orbit_host_object_id," +
                            " radial_order=@radial_order," +
                            " display_name=@display_name," +
                            " notes=@notes," +
                            " related_table=@related_table," +
                            " related_id=@related_id," +
                            " flags=@flags," +
                            " updated_utc=datetime('now')" +
                            " WHERE object_id=@object_id";

                        upd.Parameters.AddWithValue("@system_id", o.SystemId);
                        upd.Parameters.AddWithValue("@object_kind", o.ObjectKind);
                        upd.Parameters.AddWithValue("@parent_object_id", (object)o.ParentObjectId ?? DBNull.Value);
                        upd.Parameters.AddWithValue("@orbit_host_object_id", (object)o.OrbitHostObjectId ?? DBNull.Value);
                        upd.Parameters.AddWithValue("@radial_order", o.RadialOrder);

                        upd.Parameters.AddWithValue("@display_name", o.DisplayName);
                        upd.Parameters.AddWithValue("@notes", (object)o.Notes ?? DBNull.Value);

                        upd.Parameters.AddWithValue("@related_table", (object)o.RelatedTable ?? DBNull.Value);
                        upd.Parameters.AddWithValue("@related_id", (object)o.RelatedId ?? DBNull.Value);
                        upd.Parameters.AddWithValue("@flags", (object)o.Flags ?? DBNull.Value);

                        upd.Parameters.AddWithValue("@object_id", o.ObjectId);

                        upd.ExecuteNonQuery();
                    }
                }

                tx.Commit();
            }
        }

        /// <summary>
        /// Deletes a node by object_id.
        /// Because of your FK ON DELETE CASCADE on parent_object_id,
        /// deleting a parent will delete its subtree automatically.
        /// </summary>
        public void DeleteByObjectId(string objectId)
        {
            objectId = Norm(objectId);
            if (string.IsNullOrEmpty(objectId))
                throw new ArgumentException("objectId is null/empty", "objectId");

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM system_objects WHERE object_id=@id";
                cmd.Parameters.AddWithValue("@id", objectId);
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Returns the system_root object_id for a system, if present.
        /// </summary>
        public string GetRootObjectId(string systemId)
        {
            systemId = Norm(systemId);
            if (string.IsNullOrEmpty(systemId))
                throw new ArgumentException("systemId is null/empty", "systemId");

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT object_id FROM system_objects " +
                    "WHERE system_id=@sid AND object_kind='system_root' " +
                    "ORDER BY radial_order LIMIT 1";

                cmd.Parameters.AddWithValue("@sid", systemId);

                object v = cmd.ExecuteScalar();
                if (v == null || v == DBNull.Value) return null;

                return Convert.ToString(v);
            }
        }
    }
}
