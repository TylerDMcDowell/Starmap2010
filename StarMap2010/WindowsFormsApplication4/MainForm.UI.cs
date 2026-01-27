// ============================================================
// File: MainForm.UI.cs
// Project: StarMap2010
//
// Builds the WinForms UI in code.
// This version restores the accordion/collapsible sidebar:
// - Summary (always visible)
// - Collapsible "Gate Facility" section
// - Collapsible "System Contents" section
//
// The sidebar reflows via MainForm.SidebarLayout.cs
// ============================================================

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using StarMap2010.Ui;
using StarMap2010.Models;

namespace StarMap2010
{
    public partial class MainForm
    {
        // Sidebar building blocks used by SidebarLayout.cs
        private Panel _summaryHost;

        
        private CheckBox _chkMeasure;
        private Label _lblMeasure;
private void StyleButton(Button b)
        {
            if (b == null) return;

            b.UseVisualStyleBackColor = true;
            b.FlatStyle = FlatStyle.Standard;
            b.BackColor = SystemColors.Control;
            b.ForeColor = SystemColors.ControlText;
        }


        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 2));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            this.Controls.Add(root);

            menu = new MenuStrip
            {
                Dock = DockStyle.Fill,
                Stretch = true
            };

            mnuTools = new ToolStripMenuItem("&Tools");

            mnuGovernments = new ToolStripMenuItem("Edit &Governments");
            mnuGovernments.ShortcutKeys = Keys.Control | Keys.G;
            mnuGovernments.ShowShortcutKeys = false;
            mnuGovernments.Click += MnuGovernments_Click;

            mnuTools.DropDownItems.Add(mnuGovernments);

            BuildMenuViewFilters();

            mnuWiki = new ToolStripMenuItem("&Wiki...");
            mnuWiki.ShortcutKeys = Keys.Control | Keys.W;
            mnuWiki.ShowShortcutKeys = false;
            mnuWiki.Click += MnuWiki_Click;

            mnuTools.DropDownItems.Add(new ToolStripSeparator());
            mnuTools.DropDownItems.Add(mnuWiki);




            if (!menu.Items.Contains(mnuTools))
                menu.Items.Insert(0, mnuTools);

            this.MainMenuStrip = menu;
            root.Controls.Add(menu, 0, 0);

            var shadow = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            shadow.Paint += (s, e) =>
            {
                using (var p1 = new Pen(Color.FromArgb(210, 210, 210)))
                using (var p2 = new Pen(Color.FromArgb(245, 245, 245)))
                {
                    e.Graphics.DrawLine(p1, 0, 0, shadow.Width, 0);
                    e.Graphics.DrawLine(p2, 0, 1, shadow.Width, 1);
                }
            };
            root.Controls.Add(shadow, 0, 1);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(246, 246, 246),
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.Controls.Add(content, 0, 2);

            infoPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 340,
                BackColor = content.BackColor,
                Padding = new Padding(10),
                TabStop = false
            };
            content.Controls.Add(infoPanel);

            // IMPORTANT: we'll host everything in a single dock-fill sidebar panel.
            _sidebar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = content.BackColor
            };
            infoPanel.Controls.Add(_sidebar);

            viewport = new NoAutoScrollPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Black
            };
            content.Controls.Add(viewport);

            // =========================================================
            // Summary (always visible)
            // =========================================================
            _summaryHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = 140,
                BackColor = content.BackColor
            };

            lblSysName = new Label
            {
                AutoSize = false,
                Font = new Font("Arial", 12f, FontStyle.Bold),
                ForeColor = SystemColors.ControlText,
                AutoEllipsis = true
            };

            lblGov = new Label
            {
                AutoSize = false,
                Font = new Font("Arial", 9.25f, FontStyle.Regular),
                ForeColor = SystemColors.ControlText,
                AutoEllipsis = true
            };

            lblCoords = new Label
            {
                AutoSize = false,
                Font = new Font("Arial", 9.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoEllipsis = true
            };

            _summaryHost.Controls.Add(lblSysName);
            _summaryHost.Controls.Add(lblGov);
            _summaryHost.Controls.Add(lblCoords);
            _chkMeasure = new CheckBox();
            _chkMeasure.Text = "Measure";
            _chkMeasure.AutoSize = true;
            _chkMeasure.CheckedChanged += (s, e) => SetMeasureMode(_chkMeasure.Checked);
            _summaryHost.Controls.Add(_chkMeasure);

            _lblMeasure = new Label();
            _lblMeasure.Text = "";
            _lblMeasure.AutoSize = false;
            _lblMeasure.AutoEllipsis = true;
            _lblMeasure.TextAlign = ContentAlignment.TopLeft;
            _lblMeasure.BorderStyle = BorderStyle.FixedSingle;
            _lblMeasure.BackColor = SystemColors.Window;
            _lblMeasure.ForeColor = SystemColors.ControlText;
            _lblMeasure.Padding = new Padding(4, 3, 4, 3);
            _summaryHost.Controls.Add(_lblMeasure);
            // Manual layout inside summary (use _summaryHost width, not infoPanel)
            int sw = _summaryHost.ClientSize.Width;
            lblSysName.SetBounds(0, 0, sw, 24);
            lblGov.SetBounds(0, 26, sw, 18);
            lblCoords.SetBounds(0, 46, sw, 18);
            if (_chkMeasure != null) _chkMeasure.SetBounds(0, 66, 90, 18);
            if (_lblMeasure != null) _lblMeasure.SetBounds(0, 66, sw, 44);
            _summaryHost.Resize += (s, e) =>
            {
                int w = _summaryHost.ClientSize.Width;
                lblSysName.Width = w;
                lblGov.Width = w;
                lblCoords.Width = w;
            if (_lblMeasure != null) _lblMeasure.Width = w;
            };

            // =========================================================
            // Gate Facility section (collapsible)
            // =========================================================
            _secGates = new CollapsibleSection
            {
                Dock = DockStyle.Top,
                Title = "Gate Facility",
                HeaderHeight = 28,
                Collapsed = false
            };

            rtbGateInfo = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemColors.Window,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            // Layout inside gates content panel
            _secGates.ContentPanel.Padding = new Padding(0, 8, 0, 8);
            _secGates.ContentPanel.Controls.Add(rtbGateInfo);

            _secGates.ContentPanel.Resize += (s, e) =>
            {
                int w = _secGates.ContentPanel.ClientSize.Width;

                rtbGateInfo.SetBounds(0, 0, w,
                    Math.Max(80, _secGates.ContentPanel.ClientSize.Height));
            };

            // =========================================================
            // System Contents section (collapsible)
            // =========================================================
            _secContents = new CollapsibleSection
            {
                Dock = DockStyle.Fill, // important: this is the "takes the rest" section
                Title = "System Contents",
                HeaderHeight = 28,
                Collapsed = false
            };

            splitContents = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                Panel1MinSize = 120,
                Panel2MinSize = 100,
                FixedPanel = FixedPanel.None
            };

            tvSystemObjects = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false
            };

            pnlDetailsHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(252, 252, 252),
                BorderStyle = BorderStyle.FixedSingle
            };

            splitContents.Panel1.Controls.Add(tvSystemObjects);
            splitContents.Panel2.Controls.Add(pnlDetailsHost);

            _secContents.ContentPanel.Controls.Add(splitContents);

            // =========================================================
            // CRITICAL: Sidebar docking order
            // Add FILL first, then TOP sections from bottom->top so Summary stays at top.
            // =========================================================
            _sidebar.Controls.Add(_secContents);   // Fill
            _sidebar.Controls.Add(_secGates);      // Top
            _sidebar.Controls.Add(_summaryHost);   // Top (visually first)

            // ensure context menu exists + attached
            EnsureSystemTreeMenu();

            // right-click selects node
            tvSystemObjects.NodeMouseClick += (s, e) =>
            {
                if (e == null) return;
                if (e.Button == MouseButtons.Right)
                    tvSystemObjects.SelectedNode = e.Node;
            };

            // NOTE: details panel is still placeholder for now; we'll replace it with WorldCardPanel next.
            tvSystemObjects.AfterSelect += (s, e) =>
            {
                ShowWorldCardForObject(
                    e.Node != null ? (e.Node.Tag as SystemObjectInfo) : null
                );
            };

            // =========================================================
            // Start state
            // =========================================================
            SetSelectedSystem(null);
            ResetSystemTreePlaceholder();

            // =========================================================
            // CANVAS
            // =========================================================
            canvas = new MapCanvas
            {
                Size = new Size(CANVAS_SIZE, CANVAS_SIZE),
                BackColor = Color.Black,
                ZoomFactor = zoomFactor,
                TabStop = true
            };
            canvas.SetWorld(CANVAS_SIZE / 2f, CANVAS_SIZE / 2f, SCALE_A);
            viewport.Controls.Add(canvas);

            LoadAndApplyBackgroundTile();

            viewport.Scroll += (s, e) => canvas.Invalidate();

            canvas.SystemSelected += (s) =>
            {
                SetSelectedSystem(s);
                canvas.SetSelected(s);
            };

            canvas.ShowGates = IsShowGatesEnabled();

            canvas.MouseWheel += Any_MouseWheelZoom;
            canvas.MouseDown += (s, e) => canvas.Focus();

            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;
            this.KeyUp += MainForm_KeyUp;

            canvas.KeyDown += MainForm_KeyDown;
            canvas.KeyUp += MainForm_KeyUp;
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseUp += Canvas_MouseUp;

            // =========================================================
            // Sidebar reflow wiring
            // =========================================================
            WireSidebarLayout();
            ReflowSidebar();
        }


        private void LoadAndApplyBackgroundTile()
        {
            if (canvas == null) return;

            string pathBmp = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "background.bmp");

            if (!File.Exists(pathBmp))
                return;

            byte[] bytes = File.ReadAllBytes(pathBmp);
            using (var ms = new MemoryStream(bytes))
            using (var tmp = (Bitmap)Image.FromStream(ms))
            {
                canvas.SetSpaceTile(new Bitmap(tmp));
            }

            canvas.Invalidate();
        }

        private void MnuGovernments_Click(object sender, EventArgs e)
        {
            using (var dlg = new GovernmentEditorForm(_dbPath))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    ReloadAndRefresh();
            }
        }

        private void MnuWiki_Click(object sender, EventArgs e)
        {
            try
            {
                using (var f = new StarMap2010.Ui.WikiViewerForm(_dbPath))
                {
                    f.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Wiki", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "MainForm";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.ResumeLayout(false);

        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }
    }
}
