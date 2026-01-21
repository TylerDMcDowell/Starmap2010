// ============================================================
// File: WikiViewerForm.cs
// Project: StarMap2010
//
// Purpose:
//   Read-only viewer + basic editor (v1) for StarMap Wiki content.
//   Displays wiki pages stored in SQLite (wiki_pages, wiki_images).
//
// Layout (corner images):
//   - Left: search + page list
//   - Right:
//       * Title across top (+ New/Edit/Save/Cancel/Delete)
//       * Edit-mode ToolStrip (Bold/Italic/Quote/Bullet/Code/Link/Header)
//       * Content region below fills to bottom:
//           - Right: vertical strip of images (0..N)
//           - Left: single RichTextBox (fills to bottom)
//
// Rendering (Markdown-lite+):
//   - Headings: #, ##, ###
//   - Bullets: "- "
//   - Bold: **text**
//   - Italic: *text*
//   - Blockquote: "> text" (indented + italic/gray)
//   - Code blocks: ``` ... ``` (monospace + shaded background)
//   - Wiki links: [[page]] and [[page|display text]]
//   - Better spacing: blank line creates paragraph break
//
// Editor (v1):
//   - New page (draft in-memory until Save; then GUID page_id + generated unique slug)
//   - Edit mode shows RAW markdown in the same RichTextBox
//   - Save writes to wiki_pages via WikiDao (slug preserved for existing pages)
//   - Cancel reverts to last loaded DB state (draft cancels without DB row)
//   - Delete removes page (and optionally its wiki_images rows)
//
// Notes:
//   - Text does NOT flow under images.
//   - Multiple images require NO schema changes: wiki_images supports many rows per page_id.
//   - Uses ImagePreviewForm for click-to-enlarge (must exist in project).
// ============================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

using StarMap2010.Dao;
using StarMap2010.Models;

namespace StarMap2010.Ui
{
    public sealed class WikiViewerForm : Form
    {
        private readonly string _dbPath;
        private readonly WikiDao _dao;

        private SplitContainer _split;

        // Left
        private TextBox _txtSearch;
        private ListBox _lstPages;

        // Right header (view/edit)
        private Label _lblTitle;
        private TextBox _txtTitle;

        private Button _btnNew;
        private Button _btnEdit;
        private Button _btnSave;
        private Button _btnCancel;
        private Button _btnDelete;

        // Edit toolbar (edit mode only)
        private ToolStrip _tsEdit;
        private ToolStripButton _tsBold;
        private ToolStripButton _tsItalic;
        private ToolStripButton _tsQuote;
        private ToolStripButton _tsBullet;
        private ToolStripButton _tsCode;
        private ToolStripButton _tsLink;
        private ToolStripButton _tbImage;
        private ToolStripDropDownButton _tsHeader;

        // Right body
        private RichTextBox _rtbBody;

        // Right image strip (upper-right corner area; scrolls if too many)
        private Panel _imagesHost;
        private FlowLayoutPanel _pnlImages;

        private string _currentPageId;
        private string _currentSlug;   // preserved across edits (unless new page)

        private bool _editMode;
        private bool _dirty;
        private bool _suppressDirty;

        // Page index for filtering + [[link]] nav
        private readonly List<WikiPageIndexItem> _allPages = new List<WikiPageIndexItem>();
        private readonly Dictionary<string, string> _lookupToPageId =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Clickable link ranges in the rendered body
        private readonly List<WikiLinkRange> _linkRanges = new List<WikiLinkRange>();

        // Split sizing
        private const int LEFT_DESIRED_WIDTH = 240;
        private const int LEFT_MIN_WIDTH = 200;
        private const int RIGHT_MIN_WIDTH = 500;

        // Image strip sizing (tweak to taste)
        private const int IMAGE_STRIP_WIDTH = 300;
        private const int IMAGE_HEIGHT = 220;
        private const int CAPTION_HEIGHT = 38;

        // Spacing tweaks (tight title-to-content gap)
        private static readonly Padding RIGHT_ROOT_PADDING = new Padding(8);
        private static readonly Padding TITLE_PADDING = new Padding(4, 2, 4, 0);

        // Rendering fonts/colors
        private readonly Font _fontBody = new Font("Arial", 10f, FontStyle.Regular);
        private readonly Font _fontBodyBold = new Font("Arial", 10f, FontStyle.Bold);
        private readonly Font _fontBodyItalic = new Font("Arial", 10f, FontStyle.Italic);
        private readonly Font _fontBodyBoldItalic = new Font("Arial", 10f, FontStyle.Bold | FontStyle.Italic);

        private readonly Font _fontH1 = new Font("Arial", 16f, FontStyle.Bold);
        private readonly Font _fontH2 = new Font("Arial", 14f, FontStyle.Bold);
        private readonly Font _fontH3 = new Font("Arial", 12f, FontStyle.Bold);

        private readonly Font _fontCode = new Font("Consolas", 9.5f, FontStyle.Regular);

        private readonly Color _linkColor = Color.Blue;
        private readonly Color _quoteColor = Color.FromArgb(80, 80, 80);
        private readonly Color _codeBack = Color.FromArgb(245, 245, 245);

        // Draft-new-page (not saved to DB until Save)
        private bool _draftNewPage;
        private string _draftPrevPageId;
        private string _draftTempPageId; // a GUID used only for list selection/identity

        public WikiViewerForm(string dbPath)
        {
            _dbPath = dbPath ?? "";
            _dao = new WikiDao(_dbPath);

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
            // Right: Header (title + buttons) + ToolStrip + Content region (fills)
            // ----------------------------
            var rightRoot = new TableLayoutPanel();
            rightRoot.Dock = DockStyle.Fill;
            rightRoot.ColumnCount = 1;
            rightRoot.RowCount = 2;
            rightRoot.Padding = RIGHT_ROOT_PADDING;
            rightRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // header
            rightRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // content fill

            // Header row: title (label or textbox) + buttons
            var header = new TableLayoutPanel();
            header.Dock = DockStyle.Top;
            header.ColumnCount = 2;
            header.RowCount = 1;
            header.Margin = new Padding(0);
            header.Padding = new Padding(0);
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // Title label (view mode)
            _lblTitle = new Label();
            _lblTitle.Dock = DockStyle.Fill;
            _lblTitle.Font = new Font("Arial", 16f, FontStyle.Bold);
            _lblTitle.Height = 34;
            _lblTitle.Padding = TITLE_PADDING;

            // Title textbox (edit mode)
            _txtTitle = new TextBox();
            _txtTitle.Dock = DockStyle.Fill;
            _txtTitle.Font = new Font("Arial", 14f, FontStyle.Regular);
            _txtTitle.Margin = new Padding(4, 6, 4, 0);
            _txtTitle.Visible = false;
            _txtTitle.TextChanged += (s, e) => MarkDirty();

            // Title host panel allows us to swap label/textbox without changing layout
            var titleHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            titleHost.Controls.Add(_lblTitle);
            titleHost.Controls.Add(_txtTitle);

            // Buttons
            var btnHost = new FlowLayoutPanel();
            btnHost.Dock = DockStyle.Fill;
            btnHost.AutoSize = true;
            btnHost.WrapContents = false;
            btnHost.FlowDirection = FlowDirection.LeftToRight;
            btnHost.Margin = new Padding(0, 4, 0, 0);
            btnHost.Padding = new Padding(0);

            _btnNew = new Button { Text = "New", Width = 72, Height = 26, Margin = new Padding(4, 0, 0, 0) };
            _btnEdit = new Button { Text = "Edit", Width = 72, Height = 26, Margin = new Padding(4, 0, 0, 0) };
            _btnSave = new Button { Text = "Save", Width = 72, Height = 26, Margin = new Padding(4, 0, 0, 0) };
            _btnCancel = new Button { Text = "Cancel", Width = 72, Height = 26, Margin = new Padding(4, 0, 0, 0) };
            _btnDelete = new Button { Text = "Delete", Width = 72, Height = 26, Margin = new Padding(4, 0, 0, 0) };

            _btnNew.Click += (s, e) => NewPage();
            _btnEdit.Click += (s, e) => EnterEditMode();
            _btnSave.Click += (s, e) => SaveCurrent();
            _btnCancel.Click += (s, e) => CancelEdit();
            _btnDelete.Click += (s, e) => DeleteCurrent();

            // Order (can be tweaked later; current matches your existing)
            btnHost.Controls.Add(_btnDelete);
            btnHost.Controls.Add(_btnNew);
            btnHost.Controls.Add(_btnEdit);
            btnHost.Controls.Add(_btnSave);
            btnHost.Controls.Add(_btnCancel);

            header.Controls.Add(titleHost, 0, 0);
            header.Controls.Add(btnHost, 1, 0);

            // Content region: edit ToolStrip + right image strip + body fill
            var content = new Panel();
            content.Dock = DockStyle.Fill;
            content.Margin = new Padding(0);

            // ----------------------------
            // Edit ToolStrip (hidden unless edit mode)
            // ----------------------------
            _tsEdit = new ToolStrip();
            _tsEdit.Dock = DockStyle.Top;
            _tsEdit.GripStyle = ToolStripGripStyle.Hidden;
            _tsEdit.RenderMode = ToolStripRenderMode.System;
            _tsEdit.Visible = false;

            _tsBold = new ToolStripButton("B");
            _tsBold.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _tsBold.Font = new Font(_tsEdit.Font, FontStyle.Bold);
            _tsBold.ToolTipText = "Bold (** **)";
            _tsBold.Click += (s, e) => WrapSelection("**", "**");

            _tsItalic = new ToolStripButton("I");
            _tsItalic.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _tsItalic.Font = new Font(_tsEdit.Font, FontStyle.Italic);
            _tsItalic.ToolTipText = "Italic (* *)";
            _tsItalic.Click += (s, e) => WrapSelection("*", "*");

            _tsQuote = new ToolStripButton("Quote");
            _tsQuote.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _tsQuote.ToolTipText = "Blockquote (> )";
            _tsQuote.Click += (s, e) => PrefixSelectedLines("> ");

            _tsBullet = new ToolStripButton("Bullet");
            _tsBullet.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _tsBullet.ToolTipText = "Bullet (- )";
            _tsBullet.Click += (s, e) => PrefixSelectedLines("- ");

            _tsCode = new ToolStripButton("Code");
            _tsCode.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _tsCode.ToolTipText = "Code block (```)";
            _tsCode.Click += (s, e) => WrapAsCodeBlock();

            _tsLink = new ToolStripButton("Link");
            _tsLink.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _tsLink.ToolTipText = "Wiki link ([[page]] / [[page|text]])";
            _tsLink.Click += (s, e) => InsertWikiLink();

            _tsHeader = new ToolStripDropDownButton("H");
            _tsHeader.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _tsHeader.ToolTipText = "Header";

            _tbImage = new ToolStripButton("Image…");
            _tbImage.DisplayStyle = ToolStripItemDisplayStyle.Text;
            _tbImage.ToolTipText = "Add an image to this page (stored in wiki_images)";
            _tbImage.Click += (s, e) => AddImageFromToolbar();


            var miH1 = new ToolStripMenuItem("H1 ( # )");
            miH1.Click += (s, e) => ApplyHeader(1);

            var miH2 = new ToolStripMenuItem("H2 ( ## )");
            miH2.Click += (s, e) => ApplyHeader(2);

            var miH3 = new ToolStripMenuItem("H3 ( ### )");
            miH3.Click += (s, e) => ApplyHeader(3);

            var miClear = new ToolStripMenuItem("Clear header");
            miClear.Click += (s, e) => ApplyHeader(0);

            _tsHeader.DropDownItems.Add(miH1);
            _tsHeader.DropDownItems.Add(miH2);
            _tsHeader.DropDownItems.Add(miH3);
            _tsHeader.DropDownItems.Add(new ToolStripSeparator());
            _tsHeader.DropDownItems.Add(miClear);

            _tsEdit.Items.Add(_tsBold);
            _tsEdit.Items.Add(_tsItalic);
            _tsEdit.Items.Add(new ToolStripSeparator());
            _tsEdit.Items.Add(_tsQuote);
            _tsEdit.Items.Add(_tsBullet);
            _tsEdit.Items.Add(new ToolStripSeparator());
            _tsEdit.Items.Add(_tsCode);
            _tsEdit.Items.Add(_tsLink);
            _tsEdit.Items.Add(new ToolStripSeparator());
            _tsEdit.Items.Add(_tsHeader);
            _tsEdit.Items.Add(_tbImage);

            _imagesHost = new Panel();
            _imagesHost.Dock = DockStyle.Right;
            _imagesHost.Width = IMAGE_STRIP_WIDTH;
            _imagesHost.Padding = new Padding(6, 0, 0, 0); // gap between text and images
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
            _rtbBody.BorderStyle = BorderStyle.FixedSingle;
            _rtbBody.DetectUrls = false;
            _rtbBody.BackColor = SystemColors.Window;
            _rtbBody.Font = _fontBody;
            _rtbBody.Margin = new Padding(0);
            _rtbBody.MouseUp += RtbBody_MouseUp;
            _rtbBody.MouseMove += RtbBody_MouseMove;
            _rtbBody.TextChanged += (s, e) => { if (_editMode) MarkDirty(); };

            // Dock order matters:
            // - _tsEdit (Top) added after Fill so it sits above
            // - _imagesHost (Right)
            content.Controls.Add(_rtbBody);   // Fill
            content.Controls.Add(_tsEdit);    // Top
            content.Controls.Add(_imagesHost);// Right

            rightRoot.Controls.Add(header, 0, 0);
            rightRoot.Controls.Add(content, 0, 1);

            _split.Panel2.Controls.Add(rightRoot);

            Controls.Add(_split);

            UpdateEditorUiState();
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

            if (!ConfirmLoseEditsIfNeeded())
            {
                // revert selection
                ReselectCurrentIfPossible();
                return;
            }

            LoadPage(it.PageId);
        }

        private void ReselectCurrentIfPossible()
        {
            if (string.IsNullOrEmpty(_currentPageId)) return;

            for (int i = 0; i < _lstPages.Items.Count; i++)
            {
                var it = _lstPages.Items[i] as WikiListItem;
                if (it != null && string.Equals(it.PageId, _currentPageId, StringComparison.OrdinalIgnoreCase))
                {
                    _lstPages.SelectedIndexChanged -= LstPages_SelectedIndexChanged;
                    try { _lstPages.SelectedIndex = i; }
                    finally { _lstPages.SelectedIndexChanged += LstPages_SelectedIndexChanged; }
                    return;
                }
            }
        }

        // ============================================================
        // DAO-backed data loading
        // ============================================================

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

            List<WikiPageIndexVO> pages;
            try
            {
                pages = _dao.GetPageIndex();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to load wiki page list:\n" + ex.Message, "Wiki",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            for (int i = 0; i < pages.Count; i++)
            {
                var p = pages[i];
                var idx = new WikiPageIndexItem();
                idx.PageId = p.PageId;
                idx.Slug = p.Slug;
                idx.Title = p.Title;
                idx.Tags = p.Tags;
                idx.SortOrder = p.SortOrder;
                _allPages.Add(idx);

                AddLookupKey(idx.PageId, idx.PageId);
                AddLookupKey(idx.Slug, idx.PageId);
                AddLookupKey(idx.Title, idx.PageId);
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

            WikiPageVO p = null;
            try
            {
                p = _dao.GetPageById(pageId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to load page:\n" + ex.Message, "Wiki",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string title = (p == null) ? "" : (p.Title ?? "");
            string body = (p == null) ? "" : (p.BodyMarkdown ?? "");
            _currentSlug = (p == null) ? "" : (p.Slug ?? "");

            _lblTitle.Text = title;
            _txtTitle.Text = title;

            body = StripLeadingH1(body);

            _dirty = false;
            _editMode = false;
            UpdateEditorUiState();

            RenderMarkdownPlus(body);
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

        // ============================================================
        // Editor v1 (Steps 1-4)
        // ============================================================

        private void UpdateEditorUiState()
        {
            // View vs Edit
            _btnEdit.Visible = !_editMode;
            _btnNew.Visible = !_editMode;
            _btnDelete.Visible = !_editMode;

            _btnSave.Visible = _editMode;
            _btnCancel.Visible = _editMode;

            // Enable rules
            _btnEdit.Enabled = !_editMode && !string.IsNullOrEmpty(_currentPageId);
            _btnDelete.Enabled = !_editMode && !string.IsNullOrEmpty(_currentPageId);

            _btnSave.Enabled = _editMode;
            _btnCancel.Enabled = _editMode;

            // Title swap
            _lblTitle.Visible = !_editMode;
            _txtTitle.Visible = _editMode;

            // Text editability
            _rtbBody.ReadOnly = !_editMode;
            _txtTitle.ReadOnly = !_editMode;

            // Edit ToolStrip
            if (_tsEdit != null)
                _tsEdit.Visible = _editMode;
        }

        private void MarkDirty()
        {
            if (_suppressDirty) return;
            if (!_editMode) return;
            _dirty = true;
        }

        private bool ConfirmLoseEditsIfNeeded()
        {
            if (!_editMode) return true;
            if (!_dirty) return true;

            var res = MessageBox.Show(this,
                "You have unsaved changes.\n\nSave them?",
                "Wiki",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (res == DialogResult.Cancel)
                return false;

            if (res == DialogResult.Yes)
                return SaveCurrent();

            // No: discard
            _dirty = false;
            _editMode = false;
            UpdateEditorUiState();
            return true;
        }

        private void EnterEditMode()
        {
            if (string.IsNullOrEmpty(_currentPageId))
                return;

            if (_editMode)
                return;

            // Load raw body from DB again (safe baseline)
            WikiPageVO p = null;
            try
            {
                p = _dao.GetPageById(_currentPageId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Failed to enter edit mode:\n" + ex.Message, "Wiki",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string title = (p == null) ? "" : (p.Title ?? "");
            string body = (p == null) ? "" : (p.BodyMarkdown ?? "");
            _currentSlug = (p == null) ? _currentSlug : (p.Slug ?? _currentSlug);

            _suppressDirty = true;
            try
            {
                _editMode = true;
                _dirty = false;

                _txtTitle.Text = title;
                _lblTitle.Text = title;

                _rtbBody.Clear();
                _rtbBody.SelectionBullet = false;
                _rtbBody.SelectionIndent = 0;
                _rtbBody.SelectionHangingIndent = 0;
                _rtbBody.SelectionBackColor = _rtbBody.BackColor;
                _rtbBody.SelectionColor = _rtbBody.ForeColor;
                _rtbBody.Font = _fontBody;
                _rtbBody.Text = body ?? "";
            }
            finally
            {
                _suppressDirty = false;
            }

            UpdateEditorUiState();
            _txtTitle.Focus();
            _txtTitle.SelectAll();
        }

        private void CancelEdit()
        {
            if (!_editMode)
                return;

            if (_dirty)
            {
                var res = MessageBox.Show(this,
                    "Discard unsaved changes?",
                    "Wiki",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (res != DialogResult.Yes)
                    return;
            }

            // If this was a brand new draft, remove the temp page and go back
            if (_draftNewPage)
            {
                _draftNewPage = false;
                _dirty = false;
                _editMode = false;
                UpdateEditorUiState();

                RemoveTempDraftListItem();

                var backTo = _draftPrevPageId;
                _draftPrevPageId = null;
                _draftTempPageId = null;

                if (!string.IsNullOrEmpty(backTo))
                {
                    SelectPageInList(backTo);
                    LoadPage(backTo);
                }
                else
                {
                    // No previous page: just clear view
                    _currentPageId = null;
                    _currentSlug = null;
                    _lblTitle.Text = "";
                    _rtbBody.Clear();
                    _pnlImages.Controls.Clear();
                    _imagesHost.Visible = false;
                }

                return;
            }

            // Normal cancel: revert current page from DB
            _dirty = false;
            _editMode = false;
            UpdateEditorUiState();

            if (!string.IsNullOrEmpty(_currentPageId))
                LoadPage(_currentPageId);
        }

        private void RemoveTempDraftListItem()
        {
            if (string.IsNullOrEmpty(_draftTempPageId)) return;

            for (int i = 0; i < _lstPages.Items.Count; i++)
            {
                var it = _lstPages.Items[i] as WikiListItem;
                if (it != null && string.Equals(it.PageId, _draftTempPageId, StringComparison.OrdinalIgnoreCase))
                {
                    _lstPages.Items.RemoveAt(i);
                    break;
                }
            }
        }

        private bool SaveCurrent()
        {
            if (!_editMode)
                return true;

            // In draft mode, we don't have a real DB page yet.
            // For normal mode, _currentPageId is a real page_id.
            string pageId = _currentPageId;

            if (string.IsNullOrEmpty(pageId))
                return false;

            string title = (_txtTitle.Text ?? "").Trim();
            if (title.Length == 0)
            {
                MessageBox.Show(this, "Title is required.", "Wiki", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtTitle.Focus();
                return false;
            }

            string body = _rtbBody.Text ?? "";

            // Determine slug:
            // - existing page: preserve _currentSlug
            // - draft: generate unique from title against current index
            string slug = (_currentSlug ?? "").Trim();
            if (_draftNewPage || slug.Length == 0)
                slug = MakeUniqueSlug(MakeSlug(title));

            // If this is a draft, we must assign a real page_id now
            string realPageId = pageId;
            if (_draftNewPage)
                realPageId = Guid.NewGuid().ToString();

            var p = new WikiPageVO
            {
                PageId = realPageId,
                Slug = slug,
                Title = title,
                BodyMarkdown = body,
                Tags = "",
                SortOrder = _draftNewPage ? GetNextSortOrder() : 0
            };

            // Preserve existing sort order if not draft
            if (!_draftNewPage)
            {
                for (int i = 0; i < _allPages.Count; i++)
                {
                    var idx = _allPages[i];
                    if (idx != null && string.Equals(idx.PageId, realPageId, StringComparison.OrdinalIgnoreCase))
                    {
                        p.SortOrder = idx.SortOrder;
                        break;
                    }
                }
            }

            try
            {
                _dao.UpsertPage(p);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed:\n" + ex.Message, "Wiki", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            // Draft becomes real page now
            if (_draftNewPage)
            {
                _draftNewPage = false;
                RemoveTempDraftListItem();
                _draftTempPageId = null;
                _draftPrevPageId = null;
            }

            _currentPageId = realPageId;
            _currentSlug = slug;

            _dirty = false;
            _editMode = false;
            UpdateEditorUiState();

            // Refresh index list (title may have changed)
            string keepId = _currentPageId;
            LoadPageList();
            SelectPageInList(keepId);

            LoadPage(keepId);
            return true;
        }

        private void SelectPageInList(string pageId)
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
        }

        private void NewPage()
        {
            if (!ConfirmLoseEditsIfNeeded())
                return;

            // Remember where we were so Cancel can go back
            _draftPrevPageId = _currentPageId;

            _draftNewPage = true;
            _dirty = false;

            // Create a temp id so we can select a placeholder entry in the list
            _draftTempPageId = Guid.NewGuid().ToString();
            _currentPageId = _draftTempPageId;
            _currentSlug = ""; // will be generated on Save

            // Add a temporary item at the top of the list (NOT in _allPages / NOT in DB)
            _lstPages.SelectedIndexChanged -= LstPages_SelectedIndexChanged;
            try
            {
                _lstPages.Items.Insert(0, new WikiListItem(_draftTempPageId, "(New Page — unsaved)"));
                _lstPages.SelectedIndex = 0;
            }
            finally
            {
                _lstPages.SelectedIndexChanged += LstPages_SelectedIndexChanged;
            }

            // Enter edit mode with empty content
            _suppressDirty = true;
            try
            {
                _editMode = true;
                UpdateEditorUiState();

                _txtTitle.Text = "New Page";
                _lblTitle.Text = "New Page";

                _rtbBody.Clear();
                _rtbBody.Text = "";
            }
            finally
            {
                _suppressDirty = false;
            }

            _txtTitle.Focus();
            _txtTitle.SelectAll();
        }

        private int GetNextSortOrder()
        {
            int max = 0;
            for (int i = 0; i < _allPages.Count; i++)
            {
                var p = _allPages[i];
                if (p != null && p.SortOrder > max)
                    max = p.SortOrder;
            }
            return max + 1;
        }

        private string MakeUniqueSlug(string baseSlug)
        {
            if (string.IsNullOrWhiteSpace(baseSlug))
                baseSlug = "page";

            // Check against current in-memory index keys (slug is included as a key)
            string candidate = baseSlug;
            int n = 2;

            while (_lookupToPageId.ContainsKey(candidate))
            {
                candidate = baseSlug + "-" + n;
                n++;
            }

            return candidate;
        }

        private static string MakeSlug(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "page";

            title = title.Trim().ToLowerInvariant();

            var sb = new StringBuilder(title.Length);
            bool dash = false;

            for (int i = 0; i < title.Length; i++)
            {
                char c = title[i];

                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    dash = false;
                }
                else
                {
                    if (!dash)
                    {
                        sb.Append('-');
                        dash = true;
                    }
                }
            }

            var s = sb.ToString().Trim('-');
            if (s.Length == 0) s = "page";
            return s;
        }

        // ============================================================
        // Rendering: Markdown-lite+
        // ============================================================

        private void RenderMarkdownPlus(string markdown)
        {
            // Render mode uses formatting + link ranges
            _rtbBody.SuspendLayout();

            try
            {
                _rtbBody.Clear();
                _linkRanges.Clear();
                _rtbBody.SelectionBullet = false;
                _rtbBody.SelectionIndent = 0;
                _rtbBody.SelectionHangingIndent = 0;
                _rtbBody.SelectionBackColor = _rtbBody.BackColor;
                _rtbBody.SelectionColor = _rtbBody.ForeColor;

                if (markdown == null) markdown = "";

                string norm = markdown.Replace("\r\n", "\n").Replace("\r", "\n");
                string[] lines = norm.Split('\n');

                bool inCode = false;
                bool lastWasBlank = true;

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i] ?? "";

                    // Code fence
                    if (IsFence(line))
                    {
                        inCode = !inCode;
                        // Add a blank line around code blocks for readability
                        AppendLineBreak();
                        lastWasBlank = true;
                        continue;
                    }

                    if (inCode)
                    {
                        AppendCodeLine(line);
                        lastWasBlank = false;
                        continue;
                    }

                    // Better spacing: treat blank as paragraph break (one extra line)
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        if (!lastWasBlank)
                        {
                            AppendLineBreak();
                            AppendLineBreak();
                        }
                        lastWasBlank = true;
                        continue;
                    }

                    lastWasBlank = false;

                    // Headings
                    if (StartsWithHeading(line, "### "))
                    {
                        AppendHeading(line.Substring(4).Trim(), _fontH3);
                        AppendLineBreak();
                        continue;
                    }
                    if (StartsWithHeading(line, "## "))
                    {
                        AppendHeading(line.Substring(3).Trim(), _fontH2);
                        AppendLineBreak();
                        continue;
                    }
                    if (StartsWithHeading(line, "# "))
                    {
                        AppendHeading(line.Substring(2).Trim(), _fontH1);
                        AppendLineBreak();
                        continue;
                    }

                    // Blockquote
                    if (line.StartsWith("> "))
                    {
                        AppendBlockQuote(line.Substring(2));
                        AppendLineBreak();
                        continue;
                    }

                    // Bullets
                    if (line.StartsWith("- "))
                    {
                        AppendBulletLine(line.Substring(2));
                        continue;
                    }

                    // Normal paragraph line
                    AppendParagraphLine(line);
                }
            }
            finally
            {
                _rtbBody.ResumeLayout();
            }
        }

        private static bool IsFence(string line)
        {
            if (line == null) return false;
            line = line.Trim();
            return line.StartsWith("```");
        }

        private static bool StartsWithHeading(string line, string prefix)
        {
            if (line == null) return false;
            return line.StartsWith(prefix);
        }

        private void AppendHeading(string text, Font font)
        {
            SetSelectionDefaults();
            _rtbBody.SelectionFont = font;
            _rtbBody.SelectionBullet = false;
            _rtbBody.SelectionIndent = 0;
            _rtbBody.SelectionHangingIndent = 0;
            _rtbBody.SelectionColor = _rtbBody.ForeColor;

            AppendInline(text, _fontBodyBold, isQuote: false);
        }

        private void AppendParagraphLine(string text)
        {
            SetSelectionDefaults();
            _rtbBody.SelectionFont = _fontBody;
            _rtbBody.SelectionBullet = false;
            _rtbBody.SelectionIndent = 0;
            _rtbBody.SelectionHangingIndent = 0;
            _rtbBody.SelectionColor = _rtbBody.ForeColor;

            AppendInline(text, _fontBody, isQuote: false);
            AppendLineBreak();
        }

        private void AppendBulletLine(string text)
        {
            SetSelectionDefaults();
            _rtbBody.SelectionFont = _fontBody;
            _rtbBody.SelectionBullet = true;
            _rtbBody.SelectionIndent = 18;
            _rtbBody.SelectionHangingIndent = 10;
            _rtbBody.SelectionColor = _rtbBody.ForeColor;

            AppendInline(text, _fontBody, isQuote: false);
            AppendLineBreak();

            // reset bullet mode for next lines
            _rtbBody.SelectionBullet = false;
            _rtbBody.SelectionIndent = 0;
            _rtbBody.SelectionHangingIndent = 0;
        }

        private void AppendBlockQuote(string text)
        {
            SetSelectionDefaults();

            _rtbBody.SelectionBullet = false;
            _rtbBody.SelectionIndent = 24;
            _rtbBody.SelectionHangingIndent = 0;
            _rtbBody.SelectionColor = _quoteColor;

            AppendInline(text, _fontBodyItalic, isQuote: true);

            // reset indents/colors after quote line
            _rtbBody.SelectionIndent = 0;
            _rtbBody.SelectionHangingIndent = 0;
            _rtbBody.SelectionColor = _rtbBody.ForeColor;
        }

        private void AppendCodeLine(string text)
        {
            SetSelectionDefaults();

            _rtbBody.SelectionBullet = false;
            _rtbBody.SelectionIndent = 12;
            _rtbBody.SelectionHangingIndent = 0;
            _rtbBody.SelectionFont = _fontCode;
            _rtbBody.SelectionBackColor = _codeBack;
            _rtbBody.SelectionColor = Color.Black;

            // Code blocks do NOT parse links or emphasis
            _rtbBody.AppendText((text ?? "") + Environment.NewLine);

            // reset background for future normal text
            _rtbBody.SelectionBackColor = _rtbBody.BackColor;
            _rtbBody.SelectionIndent = 0;
        }

        private void AppendLineBreak()
        {
            _rtbBody.AppendText(Environment.NewLine);
        }

        private void SetSelectionDefaults()
        {
            _rtbBody.SelectionStart = _rtbBody.TextLength;
            _rtbBody.SelectionLength = 0;
            _rtbBody.SelectionFont = _fontBody;
            _rtbBody.SelectionBackColor = _rtbBody.BackColor;
            _rtbBody.SelectionColor = _rtbBody.ForeColor;
        }

        // ------------------------------------------------------------
        // Inline parsing: links + bold/italic (+ inline code)
        //   - Links: [[target]] or [[target|display]]
        //   - Bold: **text**
        //   - Italic: *text*
        //   - Inline code: `code`
        // ------------------------------------------------------------
        private void AppendInline(string raw, Font baseFont, bool isQuote)
        {
            if (raw == null) raw = "";

            int i = 0;
            while (i < raw.Length)
            {
                int nextLink = IndexOf(raw, "[[", i);
                int nextBold = IndexOf(raw, "**", i);
                int nextItalic = IndexOf(raw, "*", i);
                int nextCode = IndexOf(raw, "`", i);

                int next = MinPositive(nextLink, nextBold, nextItalic, nextCode);
                if (next < 0)
                {
                    AppendRun(raw.Substring(i), baseFont, isQuote);
                    break;
                }

                if (next > i)
                    AppendRun(raw.Substring(i, next - i), baseFont, isQuote);

                // Link
                if (next == nextLink)
                {
                    int end = raw.IndexOf("]]", next + 2, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        AppendRun(raw.Substring(next), baseFont, isQuote);
                        break;
                    }

                    string inner = raw.Substring(next + 2, end - (next + 2));
                    string target = inner;
                    string display = inner;

                    int bar = inner.IndexOf('|');
                    if (bar >= 0)
                    {
                        target = inner.Substring(0, bar).Trim();
                        display = inner.Substring(bar + 1).Trim();
                        if (display.Length == 0) display = target;
                    }

                    string resolved = ResolveTargetToPageId(target);

                    if (!string.IsNullOrEmpty(resolved))
                    {
                        AppendLink(display, resolved, baseFont, isQuote);
                    }
                    else
                    {
                        // Unknown link target: just write display text (no link style)
                        AppendRun(display, baseFont, isQuote);
                    }

                    i = end + 2;
                    continue;
                }

                // Bold
                if (next == nextBold)
                {
                    int end = raw.IndexOf("**", next + 2, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        AppendRun(raw.Substring(next), baseFont, isQuote);
                        break;
                    }

                    string inner = raw.Substring(next + 2, end - (next + 2));
                    AppendRun(inner, _fontBodyBold, isQuote);

                    i = end + 2;
                    continue;
                }

                // Inline code
                if (next == nextCode)
                {
                    int end = raw.IndexOf("`", next + 1, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        AppendRun(raw.Substring(next), baseFont, isQuote);
                        break;
                    }

                    string inner = raw.Substring(next + 1, end - (next + 1));
                    AppendInlineCode(inner);

                    i = end + 1;
                    continue;
                }

                // Italic (single *)
                if (next == nextItalic)
                {
                    // Avoid treating '**' as italic starter
                    if (next + 1 < raw.Length && raw[next + 1] == '*')
                    {
                        AppendRun("*", baseFont, isQuote);
                        i = next + 1;
                        continue;
                    }

                    int end = raw.IndexOf("*", next + 1, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        AppendRun(raw.Substring(next), baseFont, isQuote);
                        break;
                    }

                    string inner = raw.Substring(next + 1, end - (next + 1));
                    AppendRun(inner, _fontBodyItalic, isQuote);

                    i = end + 1;
                    continue;
                }

                AppendRun(raw.Substring(next, 1), baseFont, isQuote);
                i = next + 1;
            }
        }

        private void AppendInlineCode(string text)
        {
            SetSelectionDefaults();
            _rtbBody.SelectionFont = _fontCode;
            _rtbBody.SelectionBackColor = _codeBack;
            _rtbBody.SelectionColor = Color.Black;
            _rtbBody.AppendText(text ?? "");
            _rtbBody.SelectionBackColor = _rtbBody.BackColor;
            _rtbBody.SelectionFont = _fontBody;
            _rtbBody.SelectionColor = _rtbBody.ForeColor;
        }

        private void AppendRun(string text, Font font, bool isQuote)
        {
            if (string.IsNullOrEmpty(text)) return;

            SetSelectionDefaults();
            _rtbBody.SelectionFont = font ?? _fontBody;
            _rtbBody.SelectionColor = isQuote ? _quoteColor : _rtbBody.ForeColor;
            _rtbBody.AppendText(text);
        }

        private void AppendLink(string displayText, string pageId, Font baseFont, bool isQuote)
        {
            if (displayText == null) displayText = "";

            int start = _rtbBody.TextLength;

            SetSelectionDefaults();
            _rtbBody.SelectionFont = new Font((baseFont ?? _fontBody), FontStyle.Underline | FontStyle.Bold);
            _rtbBody.SelectionColor = _linkColor;

            _rtbBody.AppendText(displayText);

            int len = _rtbBody.TextLength - start;
            if (len > 0)
                _linkRanges.Add(new WikiLinkRange(start, len, pageId, displayText));

            SetSelectionDefaults();
            _rtbBody.SelectionFont = _fontBody;
            _rtbBody.SelectionColor = isQuote ? _quoteColor : _rtbBody.ForeColor;
        }

        private static int IndexOf(string s, string token, int start)
        {
            if (s == null || token == null) return -1;
            return s.IndexOf(token, start, StringComparison.Ordinal);
        }

        private static int MinPositive(params int[] values)
        {
            int best = int.MaxValue;
            bool found = false;
            for (int i = 0; i < values.Length; i++)
            {
                int v = values[i];
                if (v >= 0 && v < best)
                {
                    best = v;
                    found = true;
                }
            }
            return found ? best : -1;
        }

        private string ResolveTargetToPageId(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;

            target = target.Trim();
            if (target.Length == 0) return null;

            string pageId;
            if (_lookupToPageId.TryGetValue(target, out pageId))
                return pageId;

            return null;
        }

        // ============================================================
        // Link clicking + hover (rendered view)
        // ============================================================

        private void RtbBody_MouseUp(object sender, MouseEventArgs e)
        {
            if (_editMode) return; // no navigation while editing
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

        private void RtbBody_MouseMove(object sender, MouseEventArgs e)
        {
            if (_editMode)
            {
                _rtbBody.Cursor = Cursors.IBeam;
                return;
            }

            try
            {
                int idx = _rtbBody.GetCharIndexFromPosition(e.Location);
                bool over = false;

                for (int i = 0; i < _linkRanges.Count; i++)
                {
                    var lr = _linkRanges[i];
                    if (idx >= lr.Start && idx < (lr.Start + lr.Length))
                    {
                        over = true;
                        break;
                    }
                }

                _rtbBody.Cursor = over ? Cursors.Hand : Cursors.IBeam;
            }
            catch
            {
                _rtbBody.Cursor = Cursors.IBeam;
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

        // ============================================================
        // Multiple images (right strip) - DAO-backed
        // ============================================================

        private void LoadImagesForPage(string pageId)
        {
            _pnlImages.Controls.Clear();
            _imagesHost.Visible = false;

            List<WikiImageVO> imgs;
            try
            {
                imgs = _dao.GetImagesForPage(pageId);
            }
            catch
            {
                return;
            }

            // Build cards for any images that exist on disk
            for (int i = 0; i < imgs.Count; i++)
            {
                var img = imgs[i];
                string full = ResolveAssetPath(img.ImagePath);
                if (!File.Exists(full)) continue;

                Control c = BuildRightImageCard(img, full);
                if (c != null && c.Visible)
                    _pnlImages.Controls.Add(c);
            }

            _imagesHost.Visible = (_pnlImages.Controls.Count > 0);
        }

        private Control BuildRightImageCard(WikiImageVO img, string fullPath)
        {
            var card = new Panel();
            card.Width = IMAGE_STRIP_WIDTH - 12;
            card.Height = IMAGE_HEIGHT + CAPTION_HEIGHT;
            card.Margin = new Padding(0, 0, 0, 10);

            var pb = new PictureBox();
            pb.Dock = DockStyle.Top;
            pb.Height = IMAGE_HEIGHT;
            pb.BorderStyle = BorderStyle.FixedSingle;
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
                return null;
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

        // ============================================================
        // Edit ToolStrip helpers (edit mode only)
        // ============================================================

        private void WrapSelection(string left, string right)
        {
            if (!_editMode) return;

            var rtb = _rtbBody;
            int start = rtb.SelectionStart;
            int len = rtb.SelectionLength;

            string sel = (len > 0) ? rtb.SelectedText : "";

            rtb.SelectedText = left + sel + right;

            // put caret inside wrapper
            rtb.SelectionStart = start + left.Length;
            rtb.SelectionLength = sel.Length;
            rtb.Focus();

            MarkDirty();
        }

        private void WrapAsCodeBlock()
        {
            if (!_editMode) return;

            var rtb = _rtbBody;
            string sel = rtb.SelectionLength > 0 ? rtb.SelectedText : "";

            if (string.IsNullOrEmpty(sel))
            {
                int start = rtb.SelectionStart;
                rtb.SelectedText = "```\r\n\r\n```";
                rtb.SelectionStart = start + 4; // after ```\r\n
                rtb.SelectionLength = 0;
                rtb.Focus();
            }
            else
            {
                rtb.SelectedText = "```\r\n" + sel + "\r\n```";
                rtb.Focus();
            }

            MarkDirty();
        }

        private void PrefixSelectedLines(string prefix)
        {
            if (!_editMode) return;
            if (prefix == null) prefix = "";

            var rtb = _rtbBody;

            int selStart = rtb.SelectionStart;
            int selEnd = selStart + rtb.SelectionLength;

            int lineStart = rtb.GetLineFromCharIndex(selStart);
            int lineEnd = rtb.GetLineFromCharIndex(selEnd);

            string[] lines = rtb.Lines;
            if (lines == null || lines.Length == 0) return;

            if (lineStart < 0) lineStart = 0;
            if (lineEnd < 0) lineEnd = lineStart;
            if (lineEnd >= lines.Length) lineEnd = lines.Length - 1;

            for (int i = lineStart; i <= lineEnd; i++)
            {
                string l = lines[i] ?? "";
                if (!l.StartsWith(prefix))
                    lines[i] = prefix + l;
            }

            rtb.Lines = lines;

            rtb.SelectionStart = selStart;
            rtb.SelectionLength = Math.Max(0, selEnd - selStart);
            rtb.Focus();

            MarkDirty();
        }

        private void InsertWikiLink()
        {
            if (!_editMode) return;

            var rtb = _rtbBody;
            string sel = rtb.SelectionLength > 0 ? rtb.SelectedText : "";

            string target = PromptForText("Link Target", "Enter the page id / slug / title:", "");
            if (target == null) return; // cancel

            target = target.Trim();
            if (target.Length == 0) return;

            string insert;
            if (!string.IsNullOrEmpty(sel))
                insert = "[[" + target + "|" + sel + "]]";
            else
                insert = "[[" + target + "]]";

            rtb.SelectedText = insert;
            rtb.Focus();

            MarkDirty();
        }

        private void ApplyHeader(int level)
        {
            if (!_editMode) return;

            var rtb = _rtbBody;

            int selStart = rtb.SelectionStart;
            int selEnd = selStart + rtb.SelectionLength;

            int lineStart = rtb.GetLineFromCharIndex(selStart);
            int lineEnd = rtb.GetLineFromCharIndex(selEnd);

            string[] lines = rtb.Lines;
            if (lines == null || lines.Length == 0) return;

            if (lineStart < 0) lineStart = 0;
            if (lineEnd < 0) lineEnd = lineStart;
            if (lineEnd >= lines.Length) lineEnd = lines.Length - 1;

            string prefix = (level > 0) ? (new string('#', level) + " ") : "";

            for (int i = lineStart; i <= lineEnd; i++)
            {
                string clean = StripHeaderPrefix(lines[i]);
                lines[i] = prefix + clean;
            }

            rtb.Lines = lines;
            rtb.SelectionStart = selStart;
            rtb.SelectionLength = Math.Max(0, selEnd - selStart);
            rtb.Focus();

            MarkDirty();
        }

        private static string StripHeaderPrefix(string line)
        {
            if (line == null) return "";
            string t = line.TrimStart();
            if (t.StartsWith("### ")) return t.Substring(4);
            if (t.StartsWith("## ")) return t.Substring(3);
            if (t.StartsWith("# ")) return t.Substring(2);
            return t;
        }

        private string PromptForText(string title, string prompt, string defaultValue)
        {
            // Tiny VS2013-friendly input box
            using (var f = new Form())
            {
                f.Text = title ?? "Input";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.Width = 420;
                f.Height = 160;

                var lbl = new Label { Left = 10, Top = 10, Width = 390, Height = 30, Text = prompt ?? "" };
                var txt = new TextBox { Left = 10, Top = 45, Width = 390, Text = defaultValue ?? "" };

                var btnOk = new Button { Text = "OK", Left = 245, Width = 75, Top = 80, DialogResult = DialogResult.OK };
                var btnCancel = new Button { Text = "Cancel", Left = 325, Width = 75, Top = 80, DialogResult = DialogResult.Cancel };

                f.Controls.Add(lbl);
                f.Controls.Add(txt);
                f.Controls.Add(btnOk);
                f.Controls.Add(btnCancel);

                f.AcceptButton = btnOk;
                f.CancelButton = btnCancel;

                var res = f.ShowDialog(this);
                if (res != DialogResult.OK) return null;
                return txt.Text;
            }
        }

        // ============================================================
        // Helper types
        // ============================================================

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

        private void DeleteCurrent()
        {
            // If draft, just discard it (same as cancel)
            if (_draftNewPage)
            {
                CancelEdit();
                return;
            }

            if (string.IsNullOrEmpty(_currentPageId))
                return;

            if (_editMode && _dirty)
            {
                var res0 = MessageBox.Show(this,
                    "You have unsaved changes.\n\nDelete this page anyway?",
                    "Wiki",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (res0 != DialogResult.Yes)
                    return;
            }

            var res = MessageBox.Show(this,
                "Delete this wiki page?\n\nThis removes it from the database.",
                "Wiki",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res != DialogResult.Yes)
                return;

            // Delete associated wiki_images rows too (recommended)
            try
            {
                _dao.DeletePage(_currentPageId, deleteImagesToo: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Delete failed:\n" + ex.Message, "Wiki",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _currentPageId = null;
            _currentSlug = null;
            _dirty = false;
            _editMode = false;
            UpdateEditorUiState();

            LoadPageList();

            // Select next available page if any
            if (_lstPages.Items.Count > 0)
                _lstPages.SelectedIndex = 0;
            else
            {
                _lblTitle.Text = "";
                _rtbBody.Clear();
                _pnlImages.Controls.Clear();
                _imagesHost.Visible = false;
            }
        }

        private void AddImageFromToolbar()
        {
            if (_editMode == false)
            {
                MessageBox.Show(this, "Click Edit first to add images.", "Wiki",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrEmpty(_currentPageId) || _draftNewPage)
            {
                MessageBox.Show(this, "Save the page first (so it has a real page_id), then add images.", "Wiki",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Select image";
                ofd.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All Files|*.*";
                ofd.CheckFileExists = true;
                ofd.Multiselect = false;

                if (ofd.ShowDialog(this) != DialogResult.OK)
                    return;

                string srcPath = ofd.FileName;

                // Destination: bin\Debug\Assets\Wiki\Pages\<slug>\filename.ext
                string slug = (_currentSlug ?? "").Trim();
                if (string.IsNullOrEmpty(slug))
                    slug = MakeSlug((_txtTitle.Text ?? "").Trim());

                if (string.IsNullOrEmpty(slug))
                    slug = "page";

                string relDir = Path.Combine("Assets", "Wiki", "Pages", slug);
                string destDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relDir);
                Directory.CreateDirectory(destDir);

                string fileName = Path.GetFileName(srcPath);
                string destPath = Path.Combine(destDir, fileName);

                // Avoid overwriting: foo.png -> foo-2.png etc.
                destPath = MakeUniqueFilePath(destPath);

                try
                {
                    File.Copy(srcPath, destPath, false);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to copy image:\n" + ex.Message, "Wiki",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Store relative path with forward slashes (matches your convention)
                string relPath = Path.Combine(relDir, Path.GetFileName(destPath))
                    .Replace(Path.DirectorySeparatorChar, '/');

                string caption = PromptText(this, "Caption (optional):", "Add Image", "");
                int sortOrder = GetNextImageSortOrder(_currentPageId);

                try
                {
                    InsertWikiImageRow(
                        imageId: Guid.NewGuid().ToString(),
                        pageId: _currentPageId,
                        imagePath: relPath,
                        caption: caption,
                        sortOrder: sortOrder
                    );
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to insert wiki_images row:\n" + ex.Message, "Wiki",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Refresh strip now
                LoadImagesForPage(_currentPageId);
            }
        }

        private static string MakeUniqueFilePath(string path)
        {
            if (!File.Exists(path)) return path;

            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            int n = 2;
            while (true)
            {
                string candidate = Path.Combine(dir, name + "-" + n + ext);
                if (!File.Exists(candidate)) return candidate;
                n++;
            }
        }

        private int GetNextImageSortOrder(string pageId)
        {
            int max = -1;

            try
            {
                using (var conn = new System.Data.SQLite.SQLiteConnection("Data Source=" + _dbPath + ";Version=3;"))
                {
                    conn.Open();
                    using (var cmd = new System.Data.SQLite.SQLiteCommand(
                        "SELECT COALESCE(MAX(sort_order), -1) FROM wiki_images WHERE page_id=@id;", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", pageId);
                        object o = cmd.ExecuteScalar();
                        int v;
                        if (o != null && int.TryParse(Convert.ToString(o), out v))
                            max = v;
                    }
                }
            }
            catch
            {
                // if table missing / error, default
                max = -1;
            }

            return max + 1;
        }

        private void InsertWikiImageRow(string imageId, string pageId, string imagePath, string caption, int sortOrder)
        {
            using (var conn = new System.Data.SQLite.SQLiteConnection("Data Source=" + _dbPath + ";Version=3;"))
            {
                conn.Open();
                using (var cmd = new System.Data.SQLite.SQLiteCommand(
                    "INSERT INTO wiki_images (image_id, page_id, image_path, caption, sort_order) " +
                    "VALUES (@iid, @pid, @path, @cap, @ord);", conn))
                {
                    cmd.Parameters.AddWithValue("@iid", imageId);
                    cmd.Parameters.AddWithValue("@pid", pageId);
                    cmd.Parameters.AddWithValue("@path", imagePath ?? "");
                    cmd.Parameters.AddWithValue("@cap", caption ?? "");
                    cmd.Parameters.AddWithValue("@ord", sortOrder);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static string PromptText(IWin32Window owner, string text, string caption, string defaultValue)
        {
            using (var f = new Form())
            {
                f.Text = caption ?? "Input";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.ClientSize = new Size(420, 120);

                var lbl = new Label();
                lbl.AutoSize = false;
                lbl.Text = text ?? "";
                lbl.SetBounds(10, 10, 400, 18);

                var tb = new TextBox();
                tb.Text = defaultValue ?? "";
                tb.SetBounds(10, 34, 400, 22);

                var ok = new Button();
                ok.Text = "OK";
                ok.DialogResult = DialogResult.OK;
                ok.SetBounds(254, 74, 75, 26);

                var cancel = new Button();
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.SetBounds(335, 74, 75, 26);

                f.Controls.Add(lbl);
                f.Controls.Add(tb);
                f.Controls.Add(ok);
                f.Controls.Add(cancel);

                f.AcceptButton = ok;
                f.CancelButton = cancel;

                return f.ShowDialog(owner) == DialogResult.OK
                    ? (tb.Text ?? "")
                    : null;
            }
        }



    }
}
