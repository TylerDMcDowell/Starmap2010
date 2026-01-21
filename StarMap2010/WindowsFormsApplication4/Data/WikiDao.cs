// ============================================================
// File: WikiDao.cs
// Project: StarMap2010
//
// Purpose:
//   Data access for wiki pages/images stored in SQLite.
//   - No external libraries
//   - VS2013 / .NET 4.x compatible
//   - Avoids newer SQLite UPSERT syntax for max compatibility
//
// Tables:
//   wiki_pages(page_id, slug, title, body_markdown, tags, created_utc, updated_utc, sort_order)
//   wiki_images(image_id, page_id, image_path, caption, sort_order)
// ============================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using StarMap2010.Models;

namespace StarMap2010.Dao
{
    public sealed class WikiDao
    {
        private readonly string _dbPath;

        public WikiDao(string dbPath)
        {
            _dbPath = dbPath ?? "";
        }

        // ------------------------------------------------------------
        // Connection
        // ------------------------------------------------------------
        private SQLiteConnection Open()
        {
            if (string.IsNullOrWhiteSpace(_dbPath))
                throw new InvalidOperationException("WikiDao DB path is empty.");

            if (!File.Exists(_dbPath))
                throw new FileNotFoundException("SQLite DB not found: " + _dbPath);

            var conn = new SQLiteConnection("Data Source=" + _dbPath + ";Version=3;");
            conn.Open();
            return conn;
        }

        // ------------------------------------------------------------
        // Pages - Index
        // ------------------------------------------------------------
        public List<WikiPageIndexVO> GetPageIndex()
        {
            var list = new List<WikiPageIndexVO>();

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT page_id, slug, title, tags, IFNULL(sort_order,0) AS sort_order " +
                    "FROM wiki_pages " +
                    "ORDER BY IFNULL(sort_order,0) ASC, title ASC;";

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new WikiPageIndexVO
                        {
                            PageId = SafeGetString(r, "page_id"),
                            Slug = SafeGetString(r, "slug"),
                            Title = SafeGetString(r, "title"),
                            Tags = SafeGetString(r, "tags"),
                            SortOrder = SafeGetInt(r, "sort_order")
                        });
                    }
                }
            }

            return list;
        }

        // ------------------------------------------------------------
        // Pages - Load full
        // ------------------------------------------------------------
        public WikiPageVO GetPageById(string pageId)
        {
            if (string.IsNullOrWhiteSpace(pageId))
                return null;

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT page_id, slug, title, body_markdown, tags, IFNULL(sort_order,0) AS sort_order, created_utc, updated_utc " +
                    "FROM wiki_pages WHERE page_id=@id LIMIT 1;";
                cmd.Parameters.AddWithValue("@id", pageId);

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;

                    return new WikiPageVO
                    {
                        PageId = SafeGetString(r, "page_id"),
                        Slug = SafeGetString(r, "slug"),
                        Title = SafeGetString(r, "title"),
                        BodyMarkdown = SafeGetString(r, "body_markdown"),
                        Tags = SafeGetString(r, "tags"),
                        SortOrder = SafeGetInt(r, "sort_order"),
                        CreatedUtc = SafeGetDateTimeUtc(r, "created_utc"),
                        UpdatedUtc = SafeGetDateTimeUtc(r, "updated_utc")
                    };
                }
            }
        }

        // Resolve by slug or title (case-insensitive lookup is better done in-memory,
        // but this helps for direct DB lookups when needed).
        public WikiPageVO GetPageBySlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return null;

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT page_id, slug, title, body_markdown, tags, IFNULL(sort_order,0) AS sort_order, created_utc, updated_utc " +
                    "FROM wiki_pages WHERE slug=@slug LIMIT 1;";
                cmd.Parameters.AddWithValue("@slug", slug);

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return null;

                    return new WikiPageVO
                    {
                        PageId = SafeGetString(r, "page_id"),
                        Slug = SafeGetString(r, "slug"),
                        Title = SafeGetString(r, "title"),
                        BodyMarkdown = SafeGetString(r, "body_markdown"),
                        Tags = SafeGetString(r, "tags"),
                        SortOrder = SafeGetInt(r, "sort_order"),
                        CreatedUtc = SafeGetDateTimeUtc(r, "created_utc"),
                        UpdatedUtc = SafeGetDateTimeUtc(r, "updated_utc")
                    };
                }
            }
        }

        // ------------------------------------------------------------
        // Pages - Insert/Update (Upsert without modern UPSERT syntax)
        //   - Preserves created_utc on update.
        //   - Sets updated_utc to datetime('now') on update.
        // ------------------------------------------------------------
        public void UpsertPage(WikiPageVO p)
        {
            if (p == null) throw new ArgumentNullException("p");
            if (string.IsNullOrWhiteSpace(p.PageId)) throw new ArgumentException("PageId is required.");
            if (string.IsNullOrWhiteSpace(p.Slug)) throw new ArgumentException("Slug is required.");
            if (string.IsNullOrWhiteSpace(p.Title)) throw new ArgumentException("Title is required.");

            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                int updated = 0;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText =
                        "UPDATE wiki_pages SET " +
                        "slug=@slug, title=@title, body_markdown=@body, tags=@tags, sort_order=@sort, updated_utc=datetime('now') " +
                        "WHERE page_id=@id;";
                    cmd.Parameters.AddWithValue("@id", p.PageId);
                    cmd.Parameters.AddWithValue("@slug", p.Slug);
                    cmd.Parameters.AddWithValue("@title", p.Title);
                    cmd.Parameters.AddWithValue("@body", p.BodyMarkdown ?? "");
                    cmd.Parameters.AddWithValue("@tags", p.Tags ?? "");
                    cmd.Parameters.AddWithValue("@sort", p.SortOrder);

                    updated = cmd.ExecuteNonQuery();
                }

                if (updated == 0)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "INSERT INTO wiki_pages (page_id, slug, title, body_markdown, tags, sort_order, created_utc, updated_utc) " +
                            "VALUES (@id, @slug, @title, @body, @tags, @sort, datetime('now'), datetime('now'));";
                        cmd.Parameters.AddWithValue("@id", p.PageId);
                        cmd.Parameters.AddWithValue("@slug", p.Slug);
                        cmd.Parameters.AddWithValue("@title", p.Title);
                        cmd.Parameters.AddWithValue("@body", p.BodyMarkdown ?? "");
                        cmd.Parameters.AddWithValue("@tags", p.Tags ?? "");
                        cmd.Parameters.AddWithValue("@sort", p.SortOrder);

                        cmd.ExecuteNonQuery();
                    }
                }

                tx.Commit();
            }
        }

        public void DeletePage(string pageId, bool deleteImagesToo)
        {
            if (string.IsNullOrWhiteSpace(pageId)) return;

            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                if (deleteImagesToo)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = "DELETE FROM wiki_images WHERE page_id=@pid;";
                        cmd.Parameters.AddWithValue("@pid", pageId);
                        cmd.ExecuteNonQuery();
                    }
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM wiki_pages WHERE page_id=@id;";
                    cmd.Parameters.AddWithValue("@id", pageId);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
        }

        // ------------------------------------------------------------
        // Images
        // ------------------------------------------------------------
        public List<WikiImageVO> GetImagesForPage(string pageId)
        {
            var list = new List<WikiImageVO>();
            if (string.IsNullOrWhiteSpace(pageId))
                return list;

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT image_id, page_id, image_path, caption, IFNULL(sort_order,0) AS sort_order " +
                    "FROM wiki_images WHERE page_id=@pid " +
                    "ORDER BY IFNULL(sort_order,0) ASC, image_path ASC;";
                cmd.Parameters.AddWithValue("@pid", pageId);

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new WikiImageVO
                        {
                            ImageId = SafeGetString(r, "image_id"),
                            PageId = SafeGetString(r, "page_id"),
                            ImagePath = SafeGetString(r, "image_path"),
                            Caption = SafeGetString(r, "caption"),
                            SortOrder = SafeGetInt(r, "sort_order")
                        });
                    }
                }
            }

            return list;
        }

        // For editor later: add/update an image row (no modern UPSERT).
        public void UpsertImage(WikiImageVO img)
        {
            if (img == null) throw new ArgumentNullException("img");
            if (string.IsNullOrWhiteSpace(img.ImageId)) throw new ArgumentException("ImageId is required.");
            if (string.IsNullOrWhiteSpace(img.PageId)) throw new ArgumentException("PageId is required.");
            if (string.IsNullOrWhiteSpace(img.ImagePath)) throw new ArgumentException("ImagePath is required.");

            using (var conn = Open())
            using (var tx = conn.BeginTransaction())
            {
                int updated = 0;

                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText =
                        "UPDATE wiki_images SET " +
                        "page_id=@pid, image_path=@path, caption=@cap, sort_order=@sort " +
                        "WHERE image_id=@id;";
                    cmd.Parameters.AddWithValue("@id", img.ImageId);
                    cmd.Parameters.AddWithValue("@pid", img.PageId);
                    cmd.Parameters.AddWithValue("@path", img.ImagePath);
                    cmd.Parameters.AddWithValue("@cap", img.Caption ?? "");
                    cmd.Parameters.AddWithValue("@sort", img.SortOrder);

                    updated = cmd.ExecuteNonQuery();
                }

                if (updated == 0)
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "INSERT INTO wiki_images (image_id, page_id, image_path, caption, sort_order) " +
                            "VALUES (@id, @pid, @path, @cap, @sort);";
                        cmd.Parameters.AddWithValue("@id", img.ImageId);
                        cmd.Parameters.AddWithValue("@pid", img.PageId);
                        cmd.Parameters.AddWithValue("@path", img.ImagePath);
                        cmd.Parameters.AddWithValue("@cap", img.Caption ?? "");
                        cmd.Parameters.AddWithValue("@sort", img.SortOrder);

                        cmd.ExecuteNonQuery();
                    }
                }

                tx.Commit();
            }
        }

        public void DeleteImage(string imageId)
        {
            if (string.IsNullOrWhiteSpace(imageId)) return;

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM wiki_images WHERE image_id=@id;";
                cmd.Parameters.AddWithValue("@id", imageId);
                cmd.ExecuteNonQuery();
            }
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------
        private static string SafeGetString(IDataRecord r, string col)
        {
            int i = r.GetOrdinal(col);
            if (r.IsDBNull(i)) return "";
            return Convert.ToString(r.GetValue(i)) ?? "";
        }

        private static int SafeGetInt(IDataRecord r, string col)
        {
            int i = r.GetOrdinal(col);
            if (r.IsDBNull(i)) return 0;
            try { return Convert.ToInt32(r.GetValue(i)); }
            catch { return 0; }
        }

        private static DateTime SafeGetDateTimeUtc(IDataRecord r, string col)
        {
            int i = r.GetOrdinal(col);
            if (r.IsDBNull(i)) return DateTime.MinValue;

            var s = Convert.ToString(r.GetValue(i));
            if (string.IsNullOrWhiteSpace(s)) return DateTime.MinValue;

            // SQLite datetime('now') returns "YYYY-MM-DD HH:MM:SS"
            DateTime dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            if (DateTime.TryParse(s, out dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            return DateTime.MinValue;
        }

        public bool SlugExists(string slug)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT 1 FROM wiki_pages WHERE slug=@s LIMIT 1;";
                cmd.Parameters.AddWithValue("@s", slug);
                var o = cmd.ExecuteScalar();
                return (o != null && o != DBNull.Value);
            }
        }

        public int GetMaxPageSortOrder()
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT IFNULL(MAX(sort_order),0) FROM wiki_pages;";
                var o = cmd.ExecuteScalar();
                return Convert.ToInt32(o);
            }
        }



    }
}
