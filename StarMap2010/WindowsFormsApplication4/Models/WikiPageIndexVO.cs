// ============================================================
// File: WikiPageIndexVO.cs
// Project: StarMap2010
//
// Purpose:
//   Lightweight wiki page record for lists/search.
//   Avoids loading body_markdown for the left page list.
// ============================================================

namespace StarMap2010.Models
{
    public sealed class WikiPageIndexVO
    {
        public string PageId;
        public string Slug;
        public string Title;
        public string Tags;
        public int SortOrder;
    }
}
