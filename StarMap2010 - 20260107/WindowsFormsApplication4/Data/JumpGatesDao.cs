using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using StarMap2010.Models;

namespace StarMap2010.Data
{
    public sealed class JumpGatesDao
    {
        private readonly string _dbPath;

        public JumpGatesDao(string dbPath)
        {
            _dbPath = dbPath;
        }

        // ---------------------------
        // Map loads (existing)
        // ---------------------------

        public List<JumpGate> LoadGatesMinimalForMap()
        {
            var list = new List<JumpGate>();

            using (var conn = Db.Open(_dbPath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT gate_id, system_id, owner_government_id, gate_type, notes, " +
                    "       gate_name, gate_class, gate_role, commissioned_date, decommissioned_date, is_operational " +
                    "FROM jump_gates;";

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var g = new JumpGate();

                        g.GateId = Db.GetString(r, "gate_id");
                        g.SystemId = Db.GetString(r, "system_id");
                        g.OwnerGovernmentId = Db.GetString(r, "owner_government_id");
                        g.GateType = Db.GetString(r, "gate_type");
                        g.Notes = Db.GetStringNullOk(r, "notes") ?? "";

                        g.GateName = Db.GetStringNullOk(r, "gate_name") ?? "";
                        g.GateClass = Db.GetStringNullOk(r, "gate_class") ?? "standard";
                        g.GateRole = Db.GetStringNullOk(r, "gate_role") ?? "standard";
                        g.CommissionedDate = Db.GetStringNullOk(r, "commissioned_date");
                        g.DecommissionedDate = Db.GetStringNullOk(r, "decommissioned_date");
                        g.IsOperational = Db.GetInt32(r, "is_operational");

                        list.Add(g);
                    }
                }
            }

            return list;
        }

        public List<JumpGateRenderableLink> LoadRenderableLinksForMap()
        {
            var list = new List<JumpGateRenderableLink>();

            using (var conn = Db.Open(_dbPath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT l.link_id, l.gate_a_id, l.gate_b_id, l.status, l.notes, " +
                    "       l.active_from, l.active_until, l.is_bidirectional, l.transit_hours, l.toll_credits, " +
                    "       ga.system_id AS system_a_id, gb.system_id AS system_b_id, " +
                    "       ga.gate_type AS gate_type_a, gb.gate_type AS gate_type_b " +
                    "FROM jump_gate_links l " +
                    "JOIN jump_gates ga ON ga.gate_id = l.gate_a_id " +
                    "JOIN jump_gates gb ON gb.gate_id = l.gate_b_id;";

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var link = new JumpGateRenderableLink();

                        link.LinkId = Db.GetString(r, "link_id");
                        link.GateAId = Db.GetString(r, "gate_a_id");
                        link.GateBId = Db.GetString(r, "gate_b_id");

                        link.SystemAId = Db.GetString(r, "system_a_id");
                        link.SystemBId = Db.GetString(r, "system_b_id");

                        link.Status = Db.GetStringNullOk(r, "status") ?? "open";
                        link.Notes = Db.GetStringNullOk(r, "notes") ?? "";

                        link.GateTypeA = Db.GetStringNullOk(r, "gate_type_a") ?? "";
                        link.GateTypeB = Db.GetStringNullOk(r, "gate_type_b") ?? "";

                        link.ActiveFrom = Db.GetStringNullOk(r, "active_from");
                        link.ActiveUntil = Db.GetStringNullOk(r, "active_until");
                        link.IsBidirectional = Db.GetInt32(r, "is_bidirectional");

                        link.TransitHours = ReadNullableDouble(r, "transit_hours");
                        link.TollCredits = ReadNullableInt(r, "toll_credits");

                        list.Add(link);
                    }
                }
            }

            return list;
        }

        // ---------------------------
        // Gate editor loads
        // ---------------------------

        public JumpGate LoadGateBySystemId(string systemId)
        {
            if (string.IsNullOrWhiteSpace(systemId)) return null;

            using (var conn = Db.Open(_dbPath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT gate_id, system_id, owner_government_id, gate_type, notes, " +
                    "       gate_name, gate_class, gate_role, commissioned_date, decommissioned_date, is_operational " +
                    "FROM jump_gates " +
                    "WHERE system_id=@sid " +
                    "LIMIT 1;";

                Db.AddParam(cmd, "@sid", systemId.Trim());

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;

                    var g = new JumpGate();
                    g.GateId = Db.GetString(r, "gate_id");
                    g.SystemId = Db.GetString(r, "system_id");
                    g.OwnerGovernmentId = Db.GetString(r, "owner_government_id");
                    g.GateType = Db.GetString(r, "gate_type");
                    g.Notes = Db.GetStringNullOk(r, "notes") ?? "";

                    g.GateName = Db.GetStringNullOk(r, "gate_name") ?? "";
                    g.GateClass = Db.GetStringNullOk(r, "gate_class") ?? "standard";
                    g.GateRole = Db.GetStringNullOk(r, "gate_role") ?? "standard";
                    g.CommissionedDate = Db.GetStringNullOk(r, "commissioned_date");
                    g.DecommissionedDate = Db.GetStringNullOk(r, "decommissioned_date");
                    g.IsOperational = Db.GetInt32(r, "is_operational");

                    return g;
                }
            }
        }

        public List<JumpGateRenderableLink> LoadLinksByGateId(string gateId)
        {
            var list = new List<JumpGateRenderableLink>();
            if (string.IsNullOrWhiteSpace(gateId)) return list;

            using (var conn = Db.Open(_dbPath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT l.link_id, l.gate_a_id, l.gate_b_id, l.status, l.notes, " +
                    "       l.active_from, l.active_until, l.is_bidirectional, l.transit_hours, l.toll_credits, " +
                    "       ga.system_id AS system_a_id, gb.system_id AS system_b_id " +
                    "FROM jump_gate_links l " +
                    "JOIN jump_gates ga ON ga.gate_id = l.gate_a_id " +
                    "JOIN jump_gates gb ON gb.gate_id = l.gate_b_id " +
                    "WHERE l.gate_a_id=@gid OR l.gate_b_id=@gid;";

                Db.AddParam(cmd, "@gid", gateId.Trim());

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var link = new JumpGateRenderableLink();

                        link.LinkId = Db.GetString(r, "link_id");
                        link.GateAId = Db.GetString(r, "gate_a_id");
                        link.GateBId = Db.GetString(r, "gate_b_id");

                        link.SystemAId = Db.GetString(r, "system_a_id");
                        link.SystemBId = Db.GetString(r, "system_b_id");

                        link.Status = Db.GetStringNullOk(r, "status") ?? "open";
                        link.Notes = Db.GetStringNullOk(r, "notes") ?? "";

                        link.ActiveFrom = Db.GetStringNullOk(r, "active_from");
                        link.ActiveUntil = Db.GetStringNullOk(r, "active_until");
                        link.IsBidirectional = Db.GetInt32(r, "is_bidirectional");

                        link.TransitHours = ReadNullableDouble(r, "transit_hours");
                        link.TollCredits = ReadNullableInt(r, "toll_credits");

                        list.Add(link);
                    }
                }
            }

            return list;
        }

        public List<GateTargetItem> LoadGateLinkTargets(string selfSystemId)
        {
            var items = new List<GateTargetItem>();

            using (var conn = Db.Open(_dbPath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT system_id, system_name, real_system_name " +
                    "FROM star_systems " +
                    "WHERE system_id <> @self " +
                    "  AND government_id NOT IN ('GOV_UNALIGNED', 'GOV_HABITABLE') " +
                    "ORDER BY COALESCE(NULLIF(system_name,''), real_system_name) COLLATE NOCASE;";

                Db.AddParam(cmd, "@self", (selfSystemId ?? "").Trim());

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string id = Db.GetStringNullOk(r, "system_id");
                        if (string.IsNullOrWhiteSpace(id)) continue;

                        string name = Db.GetStringNullOk(r, "system_name");
                        string real = Db.GetStringNullOk(r, "real_system_name");

                        string display = !string.IsNullOrWhiteSpace(name) ? name : real;
                        if (string.IsNullOrWhiteSpace(display)) display = id;

                        var it = new GateTargetItem();
                        it.SystemId = id.Trim();
                        it.Name = display.Trim();
                        items.Add(it);
                    }
                }
            }

            return items;
        }

        // ---------------------------
        // Gate editor saves / helpers
        // ---------------------------

        public void UpsertGate(JumpGate gate)
        {
            if (gate == null) throw new ArgumentNullException("gate");
            if (string.IsNullOrWhiteSpace(gate.SystemId)) throw new ArgumentException("gate.SystemId required");
            if (string.IsNullOrWhiteSpace(gate.GateId)) throw new ArgumentException("gate.GateId required");
            if (string.IsNullOrWhiteSpace(gate.OwnerGovernmentId)) throw new ArgumentException("gate.OwnerGovernmentId required");

            using (var conn = Db.Open(_dbPath))
            using (var tx = conn.BeginTransaction())
            {
                UpsertGate(conn, tx, gate);
                MarkSystemHasGates(conn, tx, gate.SystemId.Trim());
                tx.Commit();
            }
        }

        public void ReplaceLinksForGate(string gateId, List<JumpGateRenderableLink> links)
        {
            if (string.IsNullOrWhiteSpace(gateId))
                throw new ArgumentException("gateId required");

            using (var conn = Db.Open(_dbPath))
            using (var tx = conn.BeginTransaction())
            {
                DeleteLinksTouchingGate(conn, tx, gateId.Trim());

                if (links != null)
                {
                    for (int i = 0; i < links.Count; i++)
                    {
                        var l = links[i];
                        if (l == null) continue;

                        string a = (l.GateAId ?? "").Trim();
                        string b = (l.GateBId ?? "").Trim();
                        if (a.Length == 0 || b.Length == 0) continue;

                        NormalizePair(ref a, ref b);

                        string linkId = MakeLinkId(a, b);
                        string status = string.IsNullOrWhiteSpace(l.Status) ? "open" : l.Status.Trim();

                        object notes = string.IsNullOrWhiteSpace(l.Notes) ? (object)DBNull.Value : l.Notes.Trim();
                        object activeFrom = string.IsNullOrWhiteSpace(l.ActiveFrom) ? (object)DBNull.Value : l.ActiveFrom.Trim();
                        object activeUntil = string.IsNullOrWhiteSpace(l.ActiveUntil) ? (object)DBNull.Value : l.ActiveUntil.Trim();
                        int isBi = (l.IsBidirectional != 0) ? 1 : 0;
                        object transit = l.TransitHours.HasValue ? (object)l.TransitHours.Value : DBNull.Value;
                        object toll = l.TollCredits.HasValue ? (object)l.TollCredits.Value : DBNull.Value;

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText =
                                "INSERT OR REPLACE INTO jump_gate_links " +
                                "(link_id, gate_a_id, gate_b_id, status, notes, active_from, active_until, is_bidirectional, transit_hours, toll_credits) " +
                                "VALUES " +
                                "(@lid, @a, @b, @st, @no, @af, @au, @bi, @th, @toll);";

                            Db.AddParam(cmd, "@lid", linkId);
                            Db.AddParam(cmd, "@a", a);
                            Db.AddParam(cmd, "@b", b);
                            Db.AddParam(cmd, "@st", status);
                            Db.AddParam(cmd, "@no", notes);
                            Db.AddParam(cmd, "@af", activeFrom);
                            Db.AddParam(cmd, "@au", activeUntil);
                            Db.AddParam(cmd, "@bi", isBi);
                            Db.AddParam(cmd, "@th", transit);
                            Db.AddParam(cmd, "@toll", toll);

                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                tx.Commit();
            }
        }

        /// <summary>
        /// Creates a minimal gate row for the given system if missing. Returns gate_id.
        /// Safe to call repeatedly.
        /// </summary>
        public string EnsureGateForSystemExists(string systemId)
        {
            if (string.IsNullOrWhiteSpace(systemId))
                throw new ArgumentException("systemId required");

            systemId = systemId.Trim();

            using (var conn = Db.Open(_dbPath))
            using (var tx = conn.BeginTransaction())
            {
                string gateId = GetGateIdForSystem(conn, tx, systemId);
                if (!string.IsNullOrWhiteSpace(gateId))
                {
                    tx.Commit();
                    return gateId.Trim();
                }

                string ownerGov = GetSystemGovernmentId(conn, tx, systemId);
                if (string.IsNullOrWhiteSpace(ownerGov))
                    throw new Exception("Cannot create gate: star_systems.government_id not found for system_id=" + systemId);

                gateId = "JG-" + systemId;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText =
                        "INSERT INTO jump_gates (gate_id, system_id, owner_government_id, gate_type, notes) " +
                        "VALUES (@gid, @sid, @own, @type, NULL);";

                    Db.AddParam(cmd, "@gid", gateId);
                    Db.AddParam(cmd, "@sid", systemId);
                    Db.AddParam(cmd, "@own", ownerGov.Trim());
                    Db.AddParam(cmd, "@type", "standard");
                    cmd.ExecuteNonQuery();
                }

                MarkSystemHasGates(conn, tx, systemId);

                tx.Commit();
                return gateId;
            }
        }

        public string GetSystemGovernmentId(string systemId)
        {
            if (string.IsNullOrWhiteSpace(systemId)) return null;

            using (var conn = Db.Open(_dbPath))
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT government_id FROM star_systems WHERE system_id=@sid LIMIT 1;";
                Db.AddParam(cmd, "@sid", systemId.Trim());
                object o = cmd.ExecuteScalar();
                return (o == null || o == DBNull.Value) ? null : Convert.ToString(o);
            }
        }

        // ---------------------------
        // Internal helpers
        // ---------------------------

        private static double? ReadNullableDouble(IDataRecord r, string col)
        {
            int i = r.GetOrdinal(col);
            if (r.IsDBNull(i)) return null;
            return Convert.ToDouble(r.GetValue(i), CultureInfo.InvariantCulture);
        }

        private static int? ReadNullableInt(IDataRecord r, string col)
        {
            int i = r.GetOrdinal(col);
            if (r.IsDBNull(i)) return null;
            return Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture);
        }

        private static void NormalizePair(ref string a, ref string b)
        {
            if (string.CompareOrdinal(a, b) > 0)
            {
                string t = a; a = b; b = t;
            }
        }

        private static string MakeLinkId(string gateA, string gateB)
        {
            return "JGL-" + gateA + "-" + gateB;
        }

        private static void DeleteLinksTouchingGate(SQLiteConnection conn, SQLiteTransaction tx, string gateId)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM jump_gate_links WHERE gate_a_id=@gid OR gate_b_id=@gid;";
                Db.AddParam(cmd, "@gid", gateId);
                cmd.ExecuteNonQuery();
            }
        }

        private static void MarkSystemHasGates(SQLiteConnection conn, SQLiteTransaction tx, string systemId)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "UPDATE star_systems SET has_gates=1 WHERE system_id=@sid;";
                Db.AddParam(cmd, "@sid", systemId);
                cmd.ExecuteNonQuery();
            }
        }

        private static string GetGateIdForSystem(SQLiteConnection conn, SQLiteTransaction tx, string systemId)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT gate_id FROM jump_gates WHERE system_id=@sid LIMIT 1;";
                Db.AddParam(cmd, "@sid", systemId);
                object o = cmd.ExecuteScalar();
                return (o == null || o == DBNull.Value) ? null : Convert.ToString(o);
            }
        }

        private static string GetSystemGovernmentId(SQLiteConnection conn, SQLiteTransaction tx, string systemId)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "SELECT government_id FROM star_systems WHERE system_id=@sid LIMIT 1;";
                Db.AddParam(cmd, "@sid", systemId);
                object o = cmd.ExecuteScalar();
                return (o == null || o == DBNull.Value) ? null : Convert.ToString(o);
            }
        }

        private static void UpsertGate(SQLiteConnection conn, SQLiteTransaction tx, JumpGate gate)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    "INSERT INTO jump_gates " +
                    "(gate_id, system_id, owner_government_id, gate_type, notes, gate_name, gate_class, gate_role, commissioned_date, decommissioned_date, is_operational) " +
                    "VALUES " +
                    "(@gid, @sid, @own, @type, @notes, @gname, @gclass, @grole, @com, @decom, @op) " +
                    "ON CONFLICT(gate_id) DO UPDATE SET " +
                    "system_id=excluded.system_id, " +
                    "owner_government_id=excluded.owner_government_id, " +
                    "gate_type=excluded.gate_type, " +
                    "notes=excluded.notes, " +
                    "gate_name=excluded.gate_name, " +
                    "gate_class=excluded.gate_class, " +
                    "gate_role=excluded.gate_role, " +
                    "commissioned_date=excluded.commissioned_date, " +
                    "decommissioned_date=excluded.decommissioned_date, " +
                    "is_operational=excluded.is_operational;";

                Db.AddParam(cmd, "@gid", (gate.GateId ?? "").Trim());
                Db.AddParam(cmd, "@sid", (gate.SystemId ?? "").Trim());
                Db.AddParam(cmd, "@own", (gate.OwnerGovernmentId ?? "").Trim());
                Db.AddParam(cmd, "@type", string.IsNullOrWhiteSpace(gate.GateType) ? "standard" : gate.GateType.Trim());

                object notes = string.IsNullOrWhiteSpace(gate.Notes) ? (object)DBNull.Value : gate.Notes.Trim();
                object gname = string.IsNullOrWhiteSpace(gate.GateName) ? (object)DBNull.Value : gate.GateName.Trim();
                object gclass = string.IsNullOrWhiteSpace(gate.GateClass) ? (object)"standard" : gate.GateClass.Trim();
                object grole = string.IsNullOrWhiteSpace(gate.GateRole) ? (object)"standard" : gate.GateRole.Trim();

                object com = string.IsNullOrWhiteSpace(gate.CommissionedDate) ? (object)DBNull.Value : gate.CommissionedDate.Trim();
                object decom = string.IsNullOrWhiteSpace(gate.DecommissionedDate) ? (object)DBNull.Value : gate.DecommissionedDate.Trim();

                int op = (gate.IsOperational != 0) ? 1 : 0;

                Db.AddParam(cmd, "@notes", notes);
                Db.AddParam(cmd, "@gname", gname);
                Db.AddParam(cmd, "@gclass", gclass);
                Db.AddParam(cmd, "@grole", grole);
                Db.AddParam(cmd, "@com", com);
                Db.AddParam(cmd, "@decom", decom);
                Db.AddParam(cmd, "@op", op);

                cmd.ExecuteNonQuery();
            }
        }

        // Simple DTO for ComboBox binding
        public sealed class GateTargetItem
        {
            public string SystemId { get; set; }
            public string Name { get; set; }
            public override string ToString() { return Name; }
        }
    }
}
