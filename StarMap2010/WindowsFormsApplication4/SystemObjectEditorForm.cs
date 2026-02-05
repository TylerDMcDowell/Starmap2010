using System;
using System.Drawing;
using System.Windows.Forms;
using StarMap2010.Models;

namespace StarMap2010
{
    public sealed class SystemObjectEditorForm : Form
    {
        private TextBox txtName;
        private ComboBox cmbKind;
        private NumericUpDown nudOrder;
        private TextBox txtNotes;

        private Button btnOk;
        private Button btnCancel;

        private readonly SystemObjectInfo _working;

        public SystemObjectInfo Result
        {
            get { return _working; }
        }

        public SystemObjectEditorForm(string title, SystemObjectInfo existingOrTemplate)
        {
            if (existingOrTemplate == null) throw new ArgumentNullException("existingOrTemplate");

            _working = Clone(existingOrTemplate);

            this.Text = title ?? "Edit Object";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.ClientSize = new Size(420, 360);

            BuildUi();
            LoadToUi();
        }

        private void BuildUi()
        {
            var lblName = new Label { Text = "Display Name", AutoSize = true, Left = 12, Top = 12 };
            txtName = new TextBox { Left = 12, Top = 30, Width = 390 };

            var lblKind = new Label { Text = "Object Kind", AutoSize = true, Left = 12, Top = 62 };
            cmbKind = new ComboBox
            {
                Left = 12,
                Top = 80,
                Width = 250,
                DropDownStyle = ComboBoxStyle.DropDown
            };

            // Common kinds (you can add as you expand the schema)
            cmbKind.Items.Add("system_root");
            cmbKind.Items.Add("star");
            cmbKind.Items.Add("planet");
            cmbKind.Items.Add("moon");
            cmbKind.Items.Add("belt");
            cmbKind.Items.Add("asteroid_belt");
            cmbKind.Items.Add("dwarf_planet");
            cmbKind.Items.Add("installation");
            cmbKind.Items.Add("station");
            cmbKind.Items.Add("gate_facility");

            var lblOrder = new Label { Text = "Radial Order", AutoSize = true, Left = 280, Top = 62 };
            nudOrder = new NumericUpDown
            {
                Left = 280,
                Top = 80,
                Width = 122,
                Minimum = -9999,
                Maximum = 9999,
                Value = 0
            };

            var lblNotes = new Label { Text = "Notes", AutoSize = true, Left = 12, Top = 116 };
            txtNotes = new TextBox
            {
                Left = 12,
                Top = 134,
                Width = 390,
                Height = 160,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };

            btnOk = new Button
            {
                Text = "OK",
                Left = 246,
                Top = 312,
                Width = 75,
                DialogResult = DialogResult.OK
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Left = 327,
                Top = 312,
                Width = 75,
                DialogResult = DialogResult.Cancel
            };

            btnOk.Click += (s, e) =>
            {
                if (!SaveFromUi())
                {
                    this.DialogResult = DialogResult.None;
                    return;
                }
            };

            this.Controls.Add(lblName);
            this.Controls.Add(txtName);
            this.Controls.Add(lblKind);
            this.Controls.Add(cmbKind);
            this.Controls.Add(lblOrder);
            this.Controls.Add(nudOrder);
            this.Controls.Add(lblNotes);
            this.Controls.Add(txtNotes);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void LoadToUi()
        {
            txtName.Text = _working.DisplayName ?? "";
            cmbKind.Text = _working.ObjectKind ?? "";
            try { nudOrder.Value = _working.RadialOrder; }
            catch { nudOrder.Value = 0; }
            txtNotes.Text = _working.Notes ?? "";
        }

        private bool SaveFromUi()
        {
            string name = (txtName.Text ?? "").Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "Display Name is required.", "System Object",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return false;
            }

            string kind = (cmbKind.Text ?? "").Trim();
            if (kind.Length == 0)
            {
                MessageBox.Show(this, "Object Kind is required.", "System Object",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbKind.Focus();
                return false;
            }

            _working.DisplayName = name;
            _working.ObjectKind = kind;
            _working.RadialOrder = (int)nudOrder.Value;

            string notes = (txtNotes.Text ?? "").Trim();
            _working.Notes = notes.Length == 0 ? null : notes;

            return true;
        }

        private static SystemObjectInfo Clone(SystemObjectInfo o)
        {
            var x = new SystemObjectInfo();
            x.ObjectId = o.ObjectId;
            x.SystemId = o.SystemId;

            x.ObjectKind = o.ObjectKind;
            x.ParentObjectId = o.ParentObjectId;
            x.OrbitHostObjectId = o.OrbitHostObjectId;
            x.RadialOrder = o.RadialOrder;

            x.DisplayName = o.DisplayName;
            x.Notes = o.Notes;

            x.RelatedTable = o.RelatedTable;
            x.RelatedId = o.RelatedId;
            x.Flags = o.Flags;

            x.CreatedUtc = o.CreatedUtc;
            x.UpdatedUtc = o.UpdatedUtc;

            return x;
        }
    }
}
