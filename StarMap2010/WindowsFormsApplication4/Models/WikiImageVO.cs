// ============================================================
// File: WikiImageVO.cs
// Project: StarMap2010
//
// Purpose:
//   Value object representing a wiki image row (wiki_images).
// ============================================================

namespace StarMap2010.Models
{
    public sealed class WikiImageVO
    {
        public string ImageId;
        public string PageId;
        public string ImagePath;   // relative: Assets/Wiki/...
        public string Caption;
        public int SortOrder;
    }
}
