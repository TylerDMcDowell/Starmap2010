// ============================================================
// File: WikiPageVO.cs
// Project: StarMap2010
//
// Purpose:
//   Value object representing a full wiki page row (wiki_pages).
//   Used by WikiDao and future editor logic.
// ============================================================

using System;

namespace StarMap2010.Models
{
    public sealed class WikiPageVO
    {
        public string PageId;
        public string Slug;
        public string Title;
        public string BodyMarkdown;
        public string Tags;
        public int SortOrder;

        // Stored as TEXT in SQLite (datetime('now')) but exposed as DateTime where possible.
        // If parsing fails, these may be DateTime.MinValue.
        public DateTime CreatedUtc;
        public DateTime UpdatedUtc;
    }
}
