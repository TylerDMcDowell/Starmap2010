// ============================================================
// File: Ui/CollapsibleSection.cs
// Project: StarMap2010
//
// A lightweight accordion-style section control:
// - Header button (click to collapse/expand)
// - ContentPanel that hosts arbitrary controls
// - Raises CollapsedChanged so the sidebar can reflow
// ============================================================

using System;
using System.Drawing;
using System.Windows.Forms;

namespace StarMap2010.Ui
{
    public sealed class CollapsibleSection : Panel
    {
        private readonly Panel _header;
        private readonly Label _title;
        private readonly Label _chev;
        private readonly Panel _content;

        private bool _collapsed;
        private int _headerHeight = 28;

        public event EventHandler CollapsedChanged;

        public CollapsibleSection()
        {
            this.DoubleBuffered = true;
            this.BackColor = SystemColors.Control;
            this.Padding = new Padding(0);
            this.Margin = new Padding(0);

            _header = new Panel
            {
                Dock = DockStyle.Top,
                Height = _headerHeight,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(8, 5, 8, 5),
                Cursor = Cursors.Hand
            };

            _title = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Arial", 9.5f, FontStyle.Bold),
                ForeColor = SystemColors.ControlText
            };

            _chev = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Right,
                Width = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI Symbol", 10f, FontStyle.Regular),
                ForeColor = Color.FromArgb(80, 80, 80)
            };

            _header.Controls.Add(_title);
            _header.Controls.Add(_chev);

            _content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = SystemColors.Control,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            this.Controls.Add(_content);
            this.Controls.Add(_header);

            _header.Click += (s, e) => Toggle();
            _title.Click += (s, e) => Toggle();
            _chev.Click += (s, e) => Toggle();

            UpdateChevron();
        }

        public Panel ContentPanel { get { return _content; } }

        public string Title
        {
            get { return _title.Text; }
            set { _title.Text = value ?? ""; }
        }

        public int HeaderHeight
        {
            get { return _headerHeight; }
            set
            {
                if (value < 22) value = 22;
                _headerHeight = value;
                _header.Height = _headerHeight;
                PerformLayout();
            }
        }

        public bool Collapsed
        {
            get { return _collapsed; }
            set
            {
                if (_collapsed == value) return;
                _collapsed = value;

                _content.Visible = !_collapsed;
                UpdateChevron();

                var h = CollapsedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public void Toggle()
        {
            Collapsed = !Collapsed;
        }

        private void UpdateChevron()
        {
            // Simple ASCII-like chevrons that look okay everywhere.
            _chev.Text = _collapsed ? "▶" : "▼";
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            // keep header crisp (optional subtle bottom line)
            _header.Invalidate();
        }
    }
}
