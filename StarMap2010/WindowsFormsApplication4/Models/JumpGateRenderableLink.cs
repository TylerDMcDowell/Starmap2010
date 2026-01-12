using System;

namespace StarMap2010.Models
{
    public sealed class JumpGateRenderableLink
    {
        // Core fields (jump_gate_links)
        public string LinkId;
        public string GateAId;
        public string GateBId;

        // Derived endpoints (systems) for drawing + UI
        public string SystemAId;
        public string SystemBId;

        public string Status;   // open / restricted / interdicted / closed
        public string Notes;

        // New: gate types at both endpoints (for coloring / legend / UI)
        public string GateTypeA;   // ga.gate_type
        public string GateTypeB;   // gb.gate_type

        // Optional extended fields (future-proof; safe to leave unused)
        public string ActiveFrom;           // active_from (YYYY-MM-DD or NULL)
        public string ActiveUntil;          // active_until (YYYY-MM-DD or NULL)
        public int IsBidirectional = 1;     // is_bidirectional (0/1)
        public double? TransitHours;        // transit_hours (NULL allowed)
        public int? TollCredits;            // toll_credits (NULL allowed)

        public bool TouchesSystem(string systemId)
        {
            return string.Equals(SystemAId, systemId, StringComparison.Ordinal) ||
                   string.Equals(SystemBId, systemId, StringComparison.Ordinal);
        }

        public string OtherSystem(string systemId)
        {
            if (string.Equals(SystemAId, systemId, StringComparison.Ordinal)) return SystemBId;
            if (string.Equals(SystemBId, systemId, StringComparison.Ordinal)) return SystemAId;
            return null;
        }

        public override string ToString()
        {
            string a = SystemAId ?? "?";
            string b = SystemBId ?? "?";
            string st = string.IsNullOrWhiteSpace(Status) ? "open" : Status.Trim();
            return string.Format("{0}: {1} <-> {2} [{3}]", LinkId ?? "(no id)", a, b, st);
        }
    }
}
