using System;

namespace StarMap2010.Models
{
    public sealed class NotesTarget
    {
        public string RelatedTable;   // e.g. "star_systems", "system_objects"
        public string RelatedId;      // e.g. system_id or object_id (TEXT)
        public string Title;          // UI title

        public NotesTarget(string relatedTable, string relatedId, string title)
        {
            RelatedTable = relatedTable;
            RelatedId = relatedId;
            Title = title;
        }

        public override string ToString()
        {
            return string.Format("{0} ({1}:{2})", Title ?? "Notes", RelatedTable ?? "?", RelatedId ?? "?");
        }
    }
}
