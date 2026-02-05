using System;

namespace StarMap2010.Models
{
    public sealed class JumpGate
    {
        // Core fields (jump_gates)
        public string GateId;
        public string SystemId;
        public string OwnerGovernmentId;
        public string GateType;    // legacy / standard / advanced / military
        public string Notes;

        // Optional extended fields (future-proof; safe to leave unused)
        public string GateName;                 // gate_name
        public string GateClass = "standard";   // gate_class
        public string GateRole = "standard";    // gate_role
        public string CommissionedDate;         // commissioned_date (YYYY-MM-DD or NULL)
        public string DecommissionedDate;       // decommissioned_date (YYYY-MM-DD or NULL)
        public int IsOperational = 1;           // is_operational (0/1)

        public JumpGate()
        {
        }

        public override string ToString()
        {
            return string.Format("{0} ({1})", GateId ?? "(no id)", GateType ?? "standard");
        }
    }
}
