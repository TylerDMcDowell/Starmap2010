// ============================================================
// File: Ui/WorldCardPanel.cs
// Project: StarMap2010
//
// Minimal read-only world card for sidebar.
// Shows: Name + derived Orbit line + hint.
// ============================================================

using System.Drawing;
using System.Windows.Forms;
using StarMap2010.Models;

namespace StarMap2010.Ui
{
    public sealed class WorldCardPanel : UserControl
    {
        private Label _lblTitle;
        private Label _lblOrbit;
        private Label _lblHint;

        public WorldCardPanel()
        {
            this.BackColor = Color.FromArgb(252, 252, 252);
            this.Padding = new Padding(10);
            BuildUi();
        }

        private void BuildUi()
        {
            _lblTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Arial", 12f, FontStyle.Bold),
                ForeColor = SystemColors.ControlText,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "(world)"
            };

            _lblOrbit = new Label
            {
                Dock = DockStyle.Top,
                Height = 18,
                Font = new Font("Arial", 9.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(80, 80, 80),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Orbit: -"
            };

            _lblHint = new Label
            {
                Dock = DockStyle.Top,
                Height = 18,
                Font = new Font("Arial", 8.75f, FontStyle.Regular),
                ForeColor = Color.FromArgb(110, 110, 110),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Double-click to view details"
            };

            this.Controls.Add(_lblHint);
            this.Controls.Add(_lblOrbit);
            this.Controls.Add(_lblTitle);
        }

        public void SetWorld(SystemObjectInfo world)
        {
            string name = (world != null && !string.IsNullOrWhiteSpace(world.DisplayName))
                ? world.DisplayName.Trim()
                : "(unnamed)";

            _lblTitle.Text = name;
        }

        public void SetOrbitText(string orbitText)
        {
            _lblOrbit.Text = "Orbit: " + (string.IsNullOrWhiteSpace(orbitText) ? "-" : orbitText.Trim());
        }
    }
}
