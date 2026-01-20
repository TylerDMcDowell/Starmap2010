// ============================================================
// File: ImagePreviewForm.cs
// Project: StarMap2010
//
// Purpose:
//   Simple full-size image preview for wiki images.
//
// Notes:
//   - Loads image bytes to avoid file locking.
//   - VS2013 / .NET 4.x compatible.
//
// ============================================================

using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace StarMap2010.Ui
{
    public sealed class ImagePreviewForm : Form
    {
        private PictureBox _pb;
        private Label _lbl;

        public ImagePreviewForm(string fullImagePath, string caption)
        {
            Text = "Wiki Image";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1000;
            Height = 750;

            BuildUi();

            _lbl.Text = caption ?? "";

            if (!string.IsNullOrEmpty(fullImagePath) && File.Exists(fullImagePath))
            {
                byte[] bytes = File.ReadAllBytes(fullImagePath);
                using (var ms = new MemoryStream(bytes))
                using (var tmp = Image.FromStream(ms))
                {
                    _pb.Image = new Bitmap(tmp);
                }
            }
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            _pb = new PictureBox();
            _pb.Dock = DockStyle.Fill;
            _pb.SizeMode = PictureBoxSizeMode.Zoom;
            _pb.BorderStyle = BorderStyle.FixedSingle;
            root.Controls.Add(_pb, 0, 0);

            _lbl = new Label();
            _lbl.Dock = DockStyle.Fill;
            _lbl.Padding = new Padding(8);
            _lbl.AutoSize = true;
            root.Controls.Add(_lbl, 0, 1);
        }
    }
}
