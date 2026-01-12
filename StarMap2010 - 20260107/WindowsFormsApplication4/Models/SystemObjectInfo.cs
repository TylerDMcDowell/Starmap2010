using System;

namespace StarMap2010.Models
{
    public sealed class SystemObjectInfo
    {
        public string ObjectId;
        public string SystemId;

        public string ObjectKind;          // system_root, star, planet, moon, belt, installation, gate_facility, etc.
        public string ParentObjectId;      // UI hierarchy
        public string OrbitHostObjectId;   // physical orbit host
        public int RadialOrder;

        public string DisplayName;
        public string Notes;

        public string RelatedTable;        // e.g. system_stars, system_bodies, jump_gates, system_installations
        public string RelatedId;           // TEXT (even if numeric in related table)
        public string Flags;               // comma list: 'habitable,terraforming_active,capital'

        public string CreatedUtc;          // text datetime in sqlite
        public string UpdatedUtc;

        public override string ToString()
        {
            return string.Format("{0} [{1}]", DisplayName ?? "(unnamed)", ObjectKind ?? "?");
        }
    }
}
