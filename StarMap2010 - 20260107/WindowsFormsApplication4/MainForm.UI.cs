using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace StarMap2010
{
    public partial class MainForm
    {
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
                Width = 320,
                BackColor = content.BackColor,
                Padding = new Padding(10),
                TabStop = false
            };
            content.Controls.Add(infoPanel);

            var infoScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = content.BackColor
            };
            infoPanel.Controls.Add(infoScroll);

            viewport = new NoAutoScrollPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Black
            };
            content.Controls.Add(viewport);

            // =========================================================
            // TOP: System summary (compact)
            // =========================================================
            lblSysName = new Label
            {
                AutoSize = true,
                Font = new Font("Arial", 12f, FontStyle.Bold),
                ForeColor = SystemColors.ControlText
            };

            // repurposed: "Gov • system_type"
            lblGov = new Label
            {
                AutoSize = true,
                Font = new Font("Arial", 9.25f, FontStyle.Regular),
                ForeColor = SystemColors.ControlText
            };

            // keep coords, but compact
            lblCoords = new Label
            {
                AutoSize = true,
                Font = new Font("Arial", 9.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            // Leave these fields in the class for now (Selection.cs may still reference them),
            // but don't add them to the UI to avoid clutter.
            // lblPrimary, lblType, lblPrimaryStarType, lblNotes remain null here.

            infoScroll.Controls.Add(lblSysName);
            infoScroll.Controls.Add(lblGov);
            infoScroll.Controls.Add(lblCoords);

            // =========================================================
            // Gate Facility section
            // =========================================================
            btnEditGates = new Button
            {
                Text = "Edit Gate Facility…",
                Width = 260,
                Height = 30,
                Enabled = false
            };
            StyleButton(btnEditGates);

            EnsureToolTip().SetToolTip(btnEditGates,
                "Select a star system on the map to edit its gate facility and routes.");

            rtbGateInfo = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemColors.Window,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Width = 290,
                Height = 140   // tightened
            };

            infoScroll.Controls.Add(btnEditGates);
            infoScroll.Controls.Add(rtbGateInfo);

            // =========================================================
            // System Contents Tree
            // =========================================================
            lblSystemContents = new Label
            {
                AutoSize = true,
                Text = "System Contents",
                Font = new Font("Arial", 9.5f, FontStyle.Bold),
                ForeColor = SystemColors.ControlText
            };

            tvSystemObjects = new TreeView
            {
                Width = 290,
                Height = 320,
                HideSelection = false
            };

            // ensure context menu exists + attached
            EnsureSystemTreeMenu();

            // right-click selects node
            tvSystemObjects.NodeMouseClick += (s, e) =>
            {
                if (e == null) return;
                if (e.Button == MouseButtons.Right)
                    tvSystemObjects.SelectedNode = e.Node;
            };

            // double-click edits
            tvSystemObjects.NodeMouseDoubleClick += (s, e) =>
            {
                if (e == null) return;
                tvSystemObjects.SelectedNode = e.Node;
                Tree_Edit();
            };

            infoScroll.Controls.Add(lblSystemContents);
            infoScroll.Controls.Add(tvSystemObjects);

            // =========================================================
            // Manual layout (tight + consistent)
            // =========================================================
            int x = 10;
            int y = 10;
            int w = 290;

            lblSysName.SetBounds(x, y, w, 24);
            y = lblSysName.Bottom + 4;

            lblGov.SetBounds(x, y, w, 18);
            y = lblGov.Bottom + 2;

            lblCoords.SetBounds(x, y, w, 18);
            y = lblCoords.Bottom + 10;

            btnEditGates.Location = new Point(x, y);
            y = btnEditGates.Bottom + 8;

            rtbGateInfo.Location = new Point(x, y);
            y = rtbGateInfo.Bottom + 12;

            lblSystemContents.Location = new Point(x, y);
            y = lblSystemContents.Bottom + 6;

            tvSystemObjects.Location = new Point(x, y);

            // start state
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

            btnEditGates.Click += (s, e) =>
            {
                if (selectedA == null) return;

                using (var dlg = new GateEditorForm(_dbPath, selectedA.SystemId, systems))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        LoadGatesAndLinksFromDb();
                        canvas.SetGateLinks(gateLinks);

                        UpdateGateInfo(selectedA);

                        // keep tree in sync (gate facility names come from gate cache)
                        LoadSystemTree(selectedA.SystemId);

                        canvas.Invalidate();
                    }
                }
            };

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
    }
}
