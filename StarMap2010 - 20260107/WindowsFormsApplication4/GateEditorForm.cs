using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using StarMap2010.Models;
using StarMap2010.Data;  

namespace StarMap2010
{
    public sealed partial class GateEditorForm : Form
    {
        private readonly string _dbPath;
        private readonly string _systemId;
        private readonly List<StarSystemInfo> _allSystems;

        // Gate fields
        private TextBox txtGateId;
        private ComboBox cmbGateType;
        private TextBox txtOwnerGovId;
        private TextBox txtGateNotes;

        private TextBox txtGateName;
        private ComboBox cmbGateClass;
        private ComboBox cmbGateRole;
        private TextBox txtCommissioned;
        private TextBox txtDecommissioned;
        private CheckBox chkOperational;

        // Link list + editor
        private ListView lvLinks;
        private ComboBox cmbTargetSystem;
        private ComboBox cmbLinkStatus;
        private TextBox txtLinkNotes;

        private TextBox txtActiveFrom;
        private TextBox txtActiveUntil;
        private CheckBox chkBidirectional;
        private TextBox txtTransitHours;
        private TextBox txtTollCredits;

        private Button btnAddLink;
        private Button btnRemoveLink;

        private Button btnSave;
        private Button btnCancel;

        public GateEditorForm(string dbPath, string systemId, List<StarSystemInfo> allSystems)
        {
            _dbPath = dbPath;
            _systemId = systemId;
            if (string.IsNullOrWhiteSpace(_systemId))
                throw new ArgumentException("systemId is required", "systemId");
            _allSystems = allSystems ?? new List<StarSystemInfo>();

            _gatesDao = new JumpGatesDao(_dbPath);   // <-- ADD THIS LINE

            Text = "Edit Jump Gate";
            StartPosition = FormStartPosition.CenterParent;
            Width = 980;
            Height = 650;
            MinimizeBox = false;
            MaximizeBox = false;

            BuildUi();

            // Data loads (in GateEditorForm.DataLoad.cs)
            LoadGateAndLinksFromDb();
            BindTargetsFromDb();
            RefreshLinksList();
        }


        private void StyleButton(Button b)
        {
            if (b == null) return;
            b.UseVisualStyleBackColor = false;
            b.FlatStyle = FlatStyle.Standard;
            b.BackColor = Color.FromArgb(240, 240, 240);
            b.ForeColor = Color.Black;
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            Controls.Add(root);

            // --- Gate group ---
            var gateGroup = new GroupBox
            {
                Text = "Gate (one per system)",
                Dock = DockStyle.Fill
            };
            root.Controls.Add(gateGroup, 0, 0);

            var gateGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 6,
                Padding = new Padding(10)
            };

            gateGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            gateGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            gateGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            gateGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            for (int i = 0; i < 6; i++)
                gateGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            gateGroup.Controls.Add(gateGrid);

            // Row 0
            gateGrid.Controls.Add(new Label { Text = "Gate ID", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
            txtGateId = new TextBox { Dock = DockStyle.Fill };
            gateGrid.Controls.Add(txtGateId, 1, 0);

            gateGrid.Controls.Add(new Label { Text = "Gate Name", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 2, 0);
            txtGateName = new TextBox { Dock = DockStyle.Fill };
            gateGrid.Controls.Add(txtGateName, 3, 0);

            // Row 1
            gateGrid.Controls.Add(new Label { Text = "Gate Type", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
            cmbGateType = new ComboBox { Dock = DockStyle.Left, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbGateType.Items.AddRange(new object[] { "legacy", "standard", "advanced", "military" });
            cmbGateType.SelectedIndex = 1;
            gateGrid.Controls.Add(cmbGateType, 1, 1);

            gateGrid.Controls.Add(new Label { Text = "Owner Gov ID", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 2, 1);
            txtOwnerGovId = new TextBox { Dock = DockStyle.Fill };
            gateGrid.Controls.Add(txtOwnerGovId, 3, 1);

            // Row 2
            gateGrid.Controls.Add(new Label { Text = "Gate Class", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 2);
            cmbGateClass = new ComboBox { Dock = DockStyle.Left, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbGateClass.Items.AddRange(new object[] { "standard", "hub", "spur", "military", "relay", "custom" });
            cmbGateClass.SelectedIndex = 0;
            gateGrid.Controls.Add(cmbGateClass, 1, 2);

            gateGrid.Controls.Add(new Label { Text = "Gate Role", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 2, 2);
            cmbGateRole = new ComboBox { Dock = DockStyle.Left, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbGateRole.Items.AddRange(new object[] { "standard", "trade", "military", "diplomatic", "frontier", "restricted", "custom" });
            cmbGateRole.SelectedIndex = 0;
            gateGrid.Controls.Add(cmbGateRole, 3, 2);

            // Row 3
            gateGrid.Controls.Add(new Label { Text = "Commissioned", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 3);
            txtCommissioned = new TextBox { Dock = DockStyle.Fill };
            gateGrid.Controls.Add(txtCommissioned, 1, 3);

            gateGrid.Controls.Add(new Label { Text = "Decommissioned", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 2, 3);
            txtDecommissioned = new TextBox { Dock = DockStyle.Fill };
            gateGrid.Controls.Add(txtDecommissioned, 3, 3);

            // Row 4
            gateGrid.Controls.Add(new Label { Text = "Operational", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 4);
            chkOperational = new CheckBox { Dock = DockStyle.Left, Checked = true, Text = "Yes" };
            gateGrid.Controls.Add(chkOperational, 1, 4);

            // Row 5
            gateGrid.Controls.Add(new Label { Text = "Gate Notes", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 5);
            txtGateNotes = new TextBox { Dock = DockStyle.Fill };
            gateGrid.Controls.Add(txtGateNotes, 1, 5);
            gateGrid.SetColumnSpan(txtGateNotes, 3);

            // --- Links group ---
            var linksGroup = new GroupBox
            {
                Text = "Gate Links (gate ↔ gate)",
                Dock = DockStyle.Fill
            };
            root.Controls.Add(linksGroup, 0, 1);

            var linksRoot = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(10)
            };
            linksRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            linksRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            linksGroup.Controls.Add(linksRoot);

            lvLinks = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true
            };
            lvLinks.Columns.Add("Connected System", 280);
            lvLinks.Columns.Add("Status", 90);
            lvLinks.Columns.Add("Bi", 40);
            lvLinks.Columns.Add("Hours", 60);
            lvLinks.Columns.Add("Toll", 60);
            lvLinks.Columns.Add("Active From", 90);
            lvLinks.Columns.Add("Active Until", 90);
            lvLinks.Columns.Add("Notes", 260);
            linksRoot.Controls.Add(lvLinks, 0, 0);

            lvLinks.SelectedIndexChanged += (s, e) => LoadSelectedLinkToEditor();

            var addPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 3
            };
            addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));

            addPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            addPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            addPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            linksRoot.Controls.Add(addPanel, 0, 1);

            // Row 0
            addPanel.Controls.Add(new Label { Text = "Target System", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            cmbTargetSystem = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                IntegralHeight = true
            };

            cmbTargetSystem.TextUpdate += (s, e) =>
            {
                var cb = (ComboBox)s;
                string t = cb.Text;
                if (string.IsNullOrWhiteSpace(t)) return;

                int idx = cb.FindString(t);
                if (idx >= 0)
                {
                    cb.SelectedIndex = idx;
                    cb.SelectionStart = t.Length;
                    cb.SelectionLength = cb.Text.Length - cb.SelectionStart;
                }
            };

            addPanel.Controls.Add(cmbTargetSystem, 1, 0);

            addPanel.Controls.Add(new Label { Text = "Status", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 0);
            cmbLinkStatus = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLinkStatus.Items.AddRange(new object[] { "open", "restricted", "interdicted", "closed" });
            cmbLinkStatus.SelectedIndex = 0;
            addPanel.Controls.Add(cmbLinkStatus, 3, 0);

            btnAddLink = new Button { Text = "Add" , Dock = DockStyle.Fill };
            addPanel.Controls.Add(btnAddLink, 4, 0);

            btnRemoveLink = new Button { Text = "Remove Selected", Dock = DockStyle.Fill };
            addPanel.Controls.Add(btnRemoveLink, 5, 0);

            // Row 1
            addPanel.Controls.Add(new Label { Text = "Active From", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            txtActiveFrom = new TextBox { Dock = DockStyle.Fill };
            addPanel.Controls.Add(txtActiveFrom, 1, 1);

            addPanel.Controls.Add(new Label { Text = "Active Until", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 1);
            txtActiveUntil = new TextBox { Dock = DockStyle.Fill };
            addPanel.Controls.Add(txtActiveUntil, 3, 1);

            chkBidirectional = new CheckBox { Text = "Bidirectional", Dock = DockStyle.Left, Checked = true };
            addPanel.Controls.Add(chkBidirectional, 4, 1);
            addPanel.SetColumnSpan(chkBidirectional, 2);

            // Row 2
            addPanel.Controls.Add(new Label { Text = "Transit Hours", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            txtTransitHours = new TextBox { Dock = DockStyle.Fill };
            addPanel.Controls.Add(txtTransitHours, 1, 2);

            addPanel.Controls.Add(new Label { Text = "Toll (credits)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 2);
            txtTollCredits = new TextBox { Dock = DockStyle.Fill };
            addPanel.Controls.Add(txtTollCredits, 3, 2);

            addPanel.Controls.Add(new Label { Text = "Link Notes", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 4, 2);
            txtLinkNotes = new TextBox { Dock = DockStyle.Fill };
            addPanel.Controls.Add(txtLinkNotes, 5, 2);

            StyleButton(btnAddLink);
            StyleButton(btnRemoveLink);

            btnAddLink.Click += (s, e) => AddOrUpdateLinkInMemory();
            btnRemoveLink.Click += (s, e) => RemoveSelectedLinkInMemory();

            // --- Bottom buttons ---
            var bottom = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft
            };
            root.Controls.Add(bottom, 0, 2);

            btnSave = new Button { Text = "Save", Width = 120, Height = 32, Margin = new Padding(6) };
            btnCancel = new Button { Text = "Cancel", Width = 120, Height = 32, Margin = new Padding(6) };

            StyleButton(btnSave);
            StyleButton(btnCancel);

            btnSave.Click += (s, e) => SaveToDb();
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            bottom.Controls.Add(btnSave);
            bottom.Controls.Add(btnCancel);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // GateEditorForm
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "GateEditorForm";
            this.Load += new System.EventHandler(this.GateEditorForm_Load);
            this.ResumeLayout(false);

        }

        private void GateEditorForm_Load(object sender, EventArgs e)
        {

        }

        private void RefreshLinksList()
        {
            if (lvLinks == null) return;

            lvLinks.Items.Clear();

            for (int i = 0; i < _links.Count; i++)
            {
                JumpGateRenderableLink l = _links[i];
                if (l == null) continue;

                string otherSystemId = l.OtherSystem(_systemId);
                string otherName = ResolveSystemName(otherSystemId);

                var it = new ListViewItem(otherName);

                string status = string.IsNullOrWhiteSpace(l.Status) ? "open" : l.Status;
                it.SubItems.Add(status);

                it.SubItems.Add((l.IsBidirectional != 0) ? "Y" : "N");

                it.SubItems.Add(l.TransitHours.HasValue
                    ? l.TransitHours.Value.ToString("0.##", CultureInfo.InvariantCulture)
                    : "");

                it.SubItems.Add(l.TollCredits.HasValue
                    ? l.TollCredits.Value.ToString(CultureInfo.InvariantCulture)
                    : "");

                it.SubItems.Add(l.ActiveFrom ?? "");
                it.SubItems.Add(l.ActiveUntil ?? "");
                it.SubItems.Add(l.Notes ?? "");

                it.Tag = l;
                lvLinks.Items.Add(it);
            }
        }

        private void LoadSelectedLinkToEditor()
        {
            if (lvLinks == null) return;
            if (lvLinks.SelectedItems.Count == 0) return;

            var link = lvLinks.SelectedItems[0].Tag as JumpGateRenderableLink;
            if (link == null) return;

            // status + notes
            SelectCombo(cmbLinkStatus, link.Status, "open");
            if (txtLinkNotes != null) txtLinkNotes.Text = link.Notes ?? "";

            // extended fields
            if (txtActiveFrom != null) txtActiveFrom.Text = link.ActiveFrom ?? "";
            if (txtActiveUntil != null) txtActiveUntil.Text = link.ActiveUntil ?? "";

            if (chkBidirectional != null) chkBidirectional.Checked = (link.IsBidirectional != 0);

            if (txtTransitHours != null)
                txtTransitHours.Text = link.TransitHours.HasValue
                    ? link.TransitHours.Value.ToString("0.##", CultureInfo.InvariantCulture)
                    : "";

            if (txtTollCredits != null)
                txtTollCredits.Text = link.TollCredits.HasValue
                    ? link.TollCredits.Value.ToString(CultureInfo.InvariantCulture)
                    : "";
        }

        // You already had this earlier — needed by RefreshLinksList()
        private string ResolveSystemName(string systemId)
        {
            if (string.IsNullOrEmpty(systemId)) return "";

            for (int i = 0; i < _allSystems.Count; i++)
            {
                StarSystemInfo s = _allSystems[i];
                if (s != null && string.Equals(s.SystemId, systemId, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(s.SystemName)) return s.SystemName;
                    if (!string.IsNullOrEmpty(s.RealSystemName)) return s.RealSystemName;
                    return systemId;
                }
            }

            return systemId;
        }



    }
}
