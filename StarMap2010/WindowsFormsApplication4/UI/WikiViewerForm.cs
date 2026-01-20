// ============================================================
// File: WikiViewerForm.cs
// Project: StarMap2010
//
// Purpose:
//   Read-only viewer for StarMap Wiki content.
//   Displays wiki pages stored in SQLite (wiki_pages, wiki_images).
//
// Layout (matches "corner images" sketch):
//   - Left: search + page list
//   - Right:
//       * Title across top
//       * Below title: one content region that fills the form height
//           - Right side: a vertical strip of images (0..N)
//           - Left side: a single RichTextBox that fills to the bottom
//
// Notes:
//   - Text does NOT flow under images. The text column stays narrower all the way down.
//   - Multiple images require NO schema changes: wiki_images already supports many rows per page_id.
//   - Uses ImagePreviewForm for click-to-enlarge (must exist in project).
// ============================================================

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace StarMap2010.Ui
{
    public sealed class WikiViewerForm : Form
    {
        private readonly string _dbPath;

        private SplitContainer _split;

        // Left
        private TextBox _txtSearch;
        private ListBox _lstPages;

        // Right
        private Label _lblTitle;
        private RichTextBox _rtbBody;

        // Right image strip (upper-right corner area; scrolls if too many)
        private Panel _imagesHost;
        private FlowLayoutPanel _pnlImages;

        private string _currentPageId;

        // Page index for filtering + [[link]] nav
        private readonly List<WikiPageIndexItem> _allPages = new List<WikiPageIndexItem>();
        private readonly Dictionary<string, string> _lookupToPageId =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Clickable [[links]] ranges in body
        private readonly List<WikiLinkRange> _linkRanges = new List<WikiLinkRange>();

        // Split sizing
        private const int LEFT_DESIRED_WIDTH = 240;
        private const int LEFT_MIN_WIDTH = 200;
        private const int RIGHT_MIN_WIDTH = 500;

        // Image strip sizing (tweak to taste)
        private const int IMAGE_STRIP_WIDTH = 300;
        private const int IMAGE_HEIGHT = 220;
        private const int CAPTION_HEIGHT = 38;

        // Spacing tweaks to close title-to-content gap
        private static readonly Padding RIGHT_ROOT_PADDING = new Padding(8);
        private static readonly Padding TITLE_PADDING = new Padding(4, 2, 4, 0);

        public WikiViewerForm(string dbPath)
        {
            _dbPath = dbPath ?? "";

            Text = "StarMap Wiki";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1100;
            Height = 750;

            BuildUi();

            Shown += (s, e) => ApplySplitterSafe();

            LoadPageList();
        }

        private void BuildUi()
        {
            _split = new SplitContainer();
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Vertical;
            _split.FixedPanel = FixedPanel.Panel1;
            _split.SplitterWidth = 6;

            // ----------------------------
            // Left: Search + page list
            // ----------------------------
            var leftTop = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(8) };

            var lblPages = new Label
            {
                Text = "Pages",
                Dock = DockStyle.Top,
                Height = 18,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _txtSearch = new TextBox { Dock = DockStyle.Top, Height = 22 };
            _txtSearch.TextChanged += (s, e) => ApplyFilter();

            leftTop.Controls.Add(_txtSearch);
            leftTop.Controls.Add(lblPages);

            _lstPages = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            _lstPages.SelectedIndexChanged += LstPages_SelectedIndexChanged;

            var left = new Panel { Dock = DockStyle.Fill };
            left.Controls.Add(_lstPages);
            left.Controls.Add(leftTop);

            _split.Panel1.Controls.Add(left);

            // ----------------------------
            // Right: Title + Content region (fills)
            // ----------------------------
            var rightRoot = new TableLayoutPanel();
            rightRoot.Dock = DockStyle.Fill;
            rightRoot.ColumnCount = 1;
            rightRoot.RowCount = 2;
            rightRoot.Padding = RIGHT_ROOT_PADDING;
            rightRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // title
            rightRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // content fill

            _lblTitle = new Label();
            _lblTitle.Dock = DockStyle.Fill;
            _lblTitle.Font = new Font("Arial", 16f, FontStyle.Bold);
            _lblTitle.Height = 34;
            _lblTitle.Padding = TITLE_PADDING;

            // Content region: right image strip + body fill
            var content = new Panel();
            content.Dock = DockStyle.Fill;
            content.Margin = new Padding(0);

            _imagesHost = new Panel();
            _imagesHost.Dock = DockStyle.Right;
            _imagesHost.Width = IMAGE_STRIP_WIDTH;
            _imagesHost.Padding = new Padding(6, 0, 0, 0); // small gap between text and images
            _imagesHost.Margin = new Padding(0);
            _imagesHost.Visible = false;

            _pnlImages = new FlowLayoutPanel();
            _pnlImages.Dock = DockStyle.Fill;
            _pnlImages.AutoScroll = true;
            _pnlImages.WrapContents = false;
            _pnlImages.FlowDirection = FlowDirection.TopDown;
            _pnlImages.Padding = new Padding(0);
            _pnlImages.Margin = new Padding(0);

            _imagesHost.Controls.Add(_pnlImages);

            _rtbBody = new RichTextBox();
            _rtbBody.Dock = DockStyle.Fill;
            _rtbBody.ReadOnly = true;
            _rtbBody.BorderStyle = BorderStyle.None;
            _rtbBody.DetectUrls = false;
            _rtbBody.BackColor = SystemColors.Window;
            _rtbBody.Margin = new Padding(0);
            _rtbBody.MouseUp += RtbBody_MouseUp;

            // Important: add fill first, then right-docked host (Dock order matters)
            content.Controls.Add(_rtbBody);
            content.Controls.Add(_imagesHost);

            rightRoot.Controls.Add(_lblTitle, 0, 0);
            rightRoot.Controls.Add(content, 0, 1);

            _split.Panel2.Controls.Add(rightRoot);

            Controls.Add(_split);
        }

        private void ApplySplitterSafe()
        {
            if (_split == null) return;

            try
            {
                _split.Panel1MinSize = 0;
                _split.Panel2MinSize = 0;

                if (_split.Width <= 0) return;

                int minLeft = LEFT_MIN_WIDTH;
                int minRight = RIGHT_MIN_WIDTH;

                if (_split.Width < (minLeft + minRight + _split.SplitterWidth))
                    minRight = Math.Max(0, _split.Width - minLeft - _split.SplitterWidth);

                int desired = LEFT_DESIRED_WIDTH;

                int max = _split.Width - minRight - _split.SplitterWidth;
                if (max < minLeft) max = minLeft;

                if (desired < minLeft) desired = minLeft;
                if (desired > max) desired = max;

                _split.SplitterDistance = desired;

                _split.Panel1MinSize = minLeft;
                _split.Panel2MinSize = minRight;
            }
            catch
            {
                // ignore transient layout throws
            }
        }

        private void LstPages_SelectedIndexChanged(object sender, EventArgs e)
        {
            var it = _lstPages.SelectedItem as WikiListItem;
            if (it == null) return;
            LoadPage(it.PageId);
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection("Data Source=" + _dbPath + ";Version=3;");
            conn.Open();
            return conn;
        }

        private void LoadPageList()
        {
            _lstPages.Items.Clear();
            _allPages.Clear();
            _lookupToPageId.Clear();

            if (string.IsNullOrWhiteSpace(_dbPath) || !File.Exists(_dbPath))
            {
                MessageBox.Show(this, "Database not found:\n" + _dbPath, "Wiki", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var conn = Open())
            using (var cmd = new SQLiteCommand("SELECT page_id, slug, title, tags FROM wiki_pages ORDER BY title;", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    var p = new WikiPageIndexItem();
                    p.PageId = Convert.ToString(r["page_id"]);
                    p.Slug = Convert.ToString(r["slug"]);
                    p.Title = Convert.ToString(r["title"]);
                    p.Tags = Convert.ToString(r["tags"]);
                    _allPages.Add(p);

                    AddLookupKey(p.PageId, p.PageId);
                    AddLookupKey(p.Slug, p.PageId);
                    AddLookupKey(p.Title, p.PageId);
                }
            }

            ApplyFilter();

            if (_lstPages.Items.Count > 0)
                _lstPages.SelectedIndex = 0;
        }

        private void AddLookupKey(string key, string pageId)
        {
            key = (key ?? "").Trim();
            if (key.Length == 0) return;
            if (!_lookupToPageId.ContainsKey(key))
                _lookupToPageId[key] = pageId;
        }

        private void ApplyFilter()
        {
            string q = (_txtSearch == null ? "" : (_txtSearch.Text ?? "")).Trim();

            string keepPageId = null;
            var cur = _lstPages.SelectedItem as WikiListItem;
            if (cur != null) keepPageId = cur.PageId;

            _lstPages.BeginUpdate();
            try
            {
                _lstPages.Items.Clear();
                for (int i = 0; i < _allPages.Count; i++)
                {
                    var p = _allPages[i];
                    if (MatchesFilter(p, q))
                        _lstPages.Items.Add(new WikiListItem(p.PageId, p.Title));
                }
            }
            finally
            {
                _lstPages.EndUpdate();
            }

            if (!string.IsNullOrEmpty(keepPageId))
            {
                for (int i = 0; i < _lstPages.Items.Count; i++)
                {
                    var it = _lstPages.Items[i] as WikiListItem;
                    if (it != null && string.Equals(it.PageId, keepPageId, StringComparison.OrdinalIgnoreCase))
                    {
                        _lstPages.SelectedIndex = i;
                        return;
                    }
                }
            }

            if (_lstPages.Items.Count > 0 && _lstPages.SelectedIndex < 0)
                _lstPages.SelectedIndex = 0;
        }

        private bool MatchesFilter(WikiPageIndexItem p, string q)
        {
            if (p == null) return false;
            if (string.IsNullOrEmpty(q)) return true;

            q = q.ToLowerInvariant();

            if (!string.IsNullOrEmpty(p.Title) && p.Title.ToLowerInvariant().Contains(q)) return true;
            if (!string.IsNullOrEmpty(p.Tags) && p.Tags.ToLowerInvariant().Contains(q)) return true;
            if (!string.IsNullOrEmpty(p.PageId) && p.PageId.ToLowerInvariant().Contains(q)) return true;
            if (!string.IsNullOrEmpty(p.Slug) && p.Slug.ToLowerInvariant().Contains(q)) return true;

            return false;
        }

        private void LoadPage(string pageId)
        {
            _currentPageId = pageId;

            string title = "";
            string body = "";

            using (var conn = Open())
            using (var cmd = new SQLiteCommand("SELECT title, body_markdown FROM wiki_pages WHERE page_id=@id LIMIT 1;", conn))
            {
                cmd.Parameters.AddWithValue("@id", pageId);
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        title = Convert.ToString(r["title"]);
                        body = Convert.ToString(r["body_markdown"]);
                    }
                }
            }

            _lblTitle.Text = title ?? "";

            body = StripLeadingH1(body);

            RenderMarkdownLite(body);
            LoadImagesForPage(pageId);

            // Scroll to top when switching pages
            try
            {
                _rtbBody.SelectionStart = 0;
                _rtbBody.ScrollToCaret();
            }
            catch { }
        }

        private static string StripLeadingH1(string body)
        {
            if (string.IsNullOrEmpty(body)) return body;

            var lines = body.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            int firstNonEmpty = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    firstNonEmpty = i;
                    break;
                }
            }
            if (firstNonEmpty < 0) return body;

            if (lines[firstNonEmpty].StartsWith("# "))
            {
                lines[firstNonEmpty] = "";
                return string.Join("\n", lines);
            }

            return body;
        }

        // -----------------------------
        // Markdown-lite + [[links]]
        // -----------------------------
        private void RenderMarkdownLite(string markdown)
        {
            _rtbBody.Clear();
            _rtbBody.SelectionBullet = false;
            _linkRanges.Clear();

            if (markdown == null) markdown = "";
            var lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? "";

                if (line.StartsWith("### "))
                {
                    AppendHeading(line.Substring(4).Trim(), 12f);
                    continue;
                }
                if (line.StartsWith("## "))
                {
                    AppendHeading(line.Substring(3).Trim(), 14f);
                    continue;
                }
                if (line.StartsWith("# "))
                {
                    AppendHeading(line.Substring(2).Trim(), 16f);
                    continue;
                }

                if (line.StartsWith("- "))
                {
                    AppendBullet(line.Substring(2).Trim());
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    AppendParagraph("");
                    continue;
                }

                AppendParagraph(line);
            }

            StyleWikiLinksInBody();
        }

        private void AppendHeading(string text, float size)
        {
            _rtbBody.SelectionStart = _rtbBody.TextLength;
            _rtbBody.SelectionLength = 0;
            _rtbBody.SelectionFont = new Font("Arial", size, FontStyle.Bold);
            _rtbBody.SelectionBullet = false;
            _rtbBody.AppendText(text + Environment.NewLine);
            _rtbBody.SelectionFont = new Font("Arial", 10f, FontStyle.Regular);
        }

        private void AppendParagraph(string text)
        {
            _rtbBody.SelectionStart = _rtbBody.TextLength;
            _rtbBody.SelectionLength = 0;
            _rtbBody.SelectionBullet = false;
            _rtbBody.SelectionFont = new Font("Arial", 10f, FontStyle.Regular);
            _rtbBody.AppendText(text + Environment.NewLine);
        }

        private void AppendBullet(string text)
        {
            _rtbBody.SelectionStart = _rtbBody.TextLength;
            _rtbBody.SelectionLength = 0;
            _rtbBody.SelectionFont = new Font("Arial", 10f, FontStyle.Regular);
            _rtbBody.SelectionBullet = true;
            _rtbBody.AppendText(text + Environment.NewLine);
            _rtbBody.SelectionBullet = false;
        }

        private void StyleWikiLinksInBody()
        {
            string text = _rtbBody.Text ?? "";
            if (text.Length == 0) return;

            _linkRanges.Clear();

            var matches = Regex.Matches(text, @"\[\[([^\]\r\n]+)\]\]");
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                if (!m.Success) continue;

                string target = m.Groups[1].Value.Trim();
                if (target.Length == 0) continue;

                string resolvedPageId = ResolveTargetToPageId(target);
                if (string.IsNullOrEmpty(resolvedPageId)) continue;

                int start = m.Index;
                int len = m.Length;

                _linkRanges.Add(new WikiLinkRange(start, len, resolvedPageId, target));

                _rtbBody.Select(start, len);
                _rtbBody.SelectionColor = Color.Blue;
                _rtbBody.SelectionFont = new Font(_rtbBody.SelectionFont ?? _rtbBody.Font, FontStyle.Underline);
            }

            _rtbBody.Select(_rtbBody.TextLength, 0);
            _rtbBody.SelectionColor = _rtbBody.ForeColor;
            _rtbBody.SelectionFont = _rtbBody.Font;
        }

        private string ResolveTargetToPageId(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;

            string pageId;
            if (_lookupToPageId.TryGetValue(target, out pageId))
                return pageId;

            target = target.Trim();
            if (_lookupToPageId.TryGetValue(target, out pageId))
                return pageId;

            return null;
        }

        private void RtbBody_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            try
            {
                int idx = _rtbBody.GetCharIndexFromPosition(e.Location);

                for (int i = 0; i < _linkRanges.Count; i++)
                {
                    var lr = _linkRanges[i];
                    if (idx >= lr.Start && idx < (lr.Start + lr.Length))
                    {
                        NavigateToPage(lr.PageId);
                        return;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private void NavigateToPage(string pageId)
        {
            if (string.IsNullOrEmpty(pageId)) return;

            for (int i = 0; i < _lstPages.Items.Count; i++)
            {
                var it = _lstPages.Items[i] as WikiListItem;
                if (it != null && string.Equals(it.PageId, pageId, StringComparison.OrdinalIgnoreCase))
                {
                    _lstPages.SelectedIndex = i;
                    return;
                }
            }

            // If filtered out, clear search and try again
            if (_txtSearch != null && !string.IsNullOrEmpty(_txtSearch.Text))
            {
                _txtSearch.Text = "";
                for (int i = 0; i < _lstPages.Items.Count; i++)
                {
                    var it = _lstPages.Items[i] as WikiListItem;
                    if (it != null && string.Equals(it.PageId, pageId, StringComparison.OrdinalIgnoreCase))
                    {
                        _lstPages.SelectedIndex = i;
                        return;
                    }
                }
            }

            // Fallback: load directly (still works)
            LoadPage(pageId);
        }

        // -----------------------------
        // Multiple images (right strip)
        // -----------------------------
        private void LoadImagesForPage(string pageId)
        {
            _pnlImages.Controls.Clear();
            _imagesHost.Visible = false;

            var imgs = new List<WikiImageEx>();
            try
            {
                using (var conn = Open())
                using (var cmd = new SQLiteCommand(
                    "SELECT image_id, image_path, caption, sort_order FROM wiki_images WHERE page_id=@id ORDER BY sort_order, image_path;",
                    conn))
                {
                    cmd.Parameters.AddWithValue("@id", pageId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            imgs.Add(new WikiImageEx
                            {
                                ImageId = Convert.ToString(r["image_id"]),
                                ImagePath = Convert.ToString(r["image_path"]),
                                Caption = Convert.ToString(r["caption"]),
                                SortOrder = SafeInt(r["sort_order"])
                            });
                        }
                    }
                }
            }
            catch
            {
                // wiki_images might not exist yet; fail silently
                return;
            }

            // Build cards for any images that exist on disk
            for (int i = 0; i < imgs.Count; i++)
            {
                var img = imgs[i];
                string full = ResolveAssetPath(img.ImagePath);
                if (!File.Exists(full)) continue;

                _pnlImages.Controls.Add(BuildRightImageCard(img, full));
            }

            _imagesHost.Visible = (_pnlImages.Controls.Count > 0);
        }

        private Control BuildRightImageCard(WikiImageEx img, string fullPath)
        {
            var card = new Panel();
            card.Width = IMAGE_STRIP_WIDTH - 12;
            card.Height = IMAGE_HEIGHT + CAPTION_HEIGHT;
            card.Margin = new Padding(0, 0, 0, 10);

            var pb = new PictureBox();
            pb.Dock = DockStyle.Top;
            pb.Height = IMAGE_HEIGHT;
            pb.BorderStyle = BorderStyle.None;
            pb.SizeMode = PictureBoxSizeMode.Zoom;
            pb.Cursor = Cursors.Hand;

            var lbl = new Label();
            lbl.Dock = DockStyle.Fill;
            lbl.Height = CAPTION_HEIGHT;
            lbl.Padding = new Padding(2, 2, 2, 0);
            lbl.AutoEllipsis = true;
            lbl.Text = img.Caption ?? "";

            // Load without locking file
            try
            {
                byte[] bytes = File.ReadAllBytes(fullPath);
                using (var ms = new MemoryStream(bytes))
                using (var tmp = Image.FromStream(ms))
                {
                    pb.Image = new Bitmap(tmp);
                }
            }
            catch
            {
                // If image fails to load, skip it
                return new Panel { Width = 1, Height = 1, Visible = false };
            }

            pb.Click += (s, e) =>
            {
                try
                {
                    using (var f = new ImagePreviewForm(fullPath, img.Caption))
                        f.ShowDialog(this);
                }
                catch
                {
                    MessageBox.Show(this, "Unable to preview image:\n" + fullPath, "Wiki", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            card.Controls.Add(lbl);
            card.Controls.Add(pb);
            return card;
        }

        private string ResolveAssetPath(string relPath)
        {
            relPath = (relPath ?? "").Replace('/', Path.DirectorySeparatorChar);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, relPath);
        }

        private static int SafeInt(object o)
        {
            if (o == null || o == DBNull.Value) return 0;
            int v;
            if (int.TryParse(Convert.ToString(o), out v)) return v;
            return 0;
        }

        // -----------------------------
        // Helper types
        // -----------------------------
        private sealed class WikiListItem
        {
            public readonly string PageId;
            public readonly string Title;

            public WikiListItem(string id, string title)
            {
                PageId = id;
                Title = title;
            }

            public override string ToString()
            {
                return Title;
            }
        }

        private sealed class WikiPageIndexItem
        {
            public string PageId;
            public string Slug;
            public string Title;
            public string Tags;
        }

        private sealed class WikiImageEx
        {
            public string ImageId;
            public string ImagePath;
            public string Caption;
            public int SortOrder;
        }

        private sealed class WikiLinkRange
        {
            public readonly int Start;
            public readonly int Length;
            public readonly string PageId;
            public readonly string Target;

            public WikiLinkRange(int start, int len, string pageId, string target)
            {
                Start = start;
                Length = len;
                PageId = pageId;
                Target = target;
            }
        }
    }
}
