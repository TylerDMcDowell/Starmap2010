using System;
using System.Drawing;
using System.Windows.Forms;
using StarMap2010.Models;

namespace StarMap2010.Notes
{
    public sealed class NotesEditorForm : Form
    {
        private readonly NotesTarget _target;
        private readonly Func<string> _load;
        private readonly Action<string> _save;

        private readonly RichTextBox _rtb;
        private readonly Button _btnSave;
        private readonly Button _btnCancel;

        private string _originalText;
        private bool _loading;

        public NotesEditorForm(NotesTarget target, Func<string> load, Action<string> save)
        {
            if (target == null) throw new ArgumentNullException("target");
            if (load == null) throw new ArgumentNullException("load");
            if (save == null) throw new ArgumentNullException("save");

            _target = target;
            _load = load;
            _save = save;

            this.Text = string.IsNullOrEmpty(_target.Title) ? "Notes" : _target.Title;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(520, 420);
            this.Size = new Size(760, 560);
            this.KeyPreview = true;

            _rtb = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = SystemColors.Window,
                Font = new Font("Consolas", 10f, FontStyle.Regular),
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            _btnSave = new Button
            {
                Text = "Save",
                Width = 90,
                Height = 30,
                Enabled = false
            };

            _btnCancel = new Button
            {
                Text = "Cancel",
                Width = 90,
                Height = 30
            };

            _btnSave.Click += (s, e) => DoSaveAndClose();
            _btnCancel.Click += (s, e) => TryClose();

            _rtb.TextChanged += (s, e) =>
            {
                if (_loading) return;
                UpdateSaveEnabled();
                UpdateTitleDirty();
            };

            this.KeyDown += NotesEditorForm_KeyDown;
            this.FormClosing += NotesEditorForm_FormClosing;

            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                Padding = new Padding(10)
            };

            bottom.Controls.Add(_btnSave);
            bottom.Controls.Add(_btnCancel);

            bottom.Resize += (s, e) =>
            {
                int right = bottom.Width - 10;
                _btnCancel.Location = new Point(right - _btnCancel.Width, 8);
                _btnSave.Location = new Point(_btnCancel.Left - 8 - _btnSave.Width, 8);
            };

            this.Controls.Add(_rtb);
            this.Controls.Add(bottom);

            LoadInitial();
        }

        private void LoadInitial()
        {
            _loading = true;
            try
            {
                _originalText = _load() ?? "";
                _rtb.Text = _originalText;
                _rtb.SelectionStart = 0;
                _rtb.ScrollToCaret();
                _btnSave.Enabled = false;
                UpdateTitleDirty();
            }
            finally
            {
                _loading = false;
            }
        }

        private void UpdateSaveEnabled()
        {
            _btnSave.Enabled = !string.Equals(_rtb.Text ?? "", _originalText ?? "", StringComparison.Ordinal);
        }

        private void UpdateTitleDirty()
        {
            string baseTitle = string.IsNullOrEmpty(_target.Title) ? "Notes" : _target.Title;
            bool dirty = !string.Equals(_rtb.Text ?? "", _originalText ?? "", StringComparison.Ordinal);
            this.Text = dirty ? (baseTitle + " *") : baseTitle;
        }

        private void DoSaveAndClose()
        {
            try
            {
                _save(_rtb.Text ?? "");
                _originalText = _rtb.Text ?? "";
                _btnSave.Enabled = false;
                UpdateTitleDirty();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed:\r\n" + ex.Message, "Notes",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TryClose()
        {
            if (!_btnSave.Enabled)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return;
            }

            var r = MessageBox.Show(this,
                "You have unsaved changes.\r\n\r\nSave before closing?",
                "Notes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (r == DialogResult.Cancel) return;
            if (r == DialogResult.No)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                return;
            }

            // Yes
            DoSaveAndClose();
        }

        private void NotesEditorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_btnSave == null) return;
            if (!_btnSave.Enabled) return;

            // prevent closing by X unless user decides
            e.Cancel = true;
            TryClose();
        }

        private void NotesEditorForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                if (_btnSave.Enabled)
                    DoSaveAndClose();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                TryClose();
                e.Handled = true;
                return;
            }
        }
    }
}
