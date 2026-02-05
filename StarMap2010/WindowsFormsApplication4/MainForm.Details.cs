// ============================================================
// File: MainForm.Details.cs
// Project: StarMap2010
//
// Hosts the Details area under the System Contents tree.
// Shows a minimal WorldCardPanel for planets/moons.
// For everything else, shows a small hint.
// Orbit line is derived (no raw radial_order shown).
// ============================================================

using System;
using System.Drawing;
using System.Windows.Forms;
using StarMap2010.Models;
using StarMap2010.Ui;

namespace StarMap2010
{
    //MainForm.Details
    public partial class MainForm
    {
        private WorldCardPanel _worldCard;

        private void ShowWorldCardForObject(SystemObjectInfo obj)
        {
            if (_detailsContentHost == null) return;

            _detailsContentHost.SuspendLayout();
            try
            {
                _detailsContentHost.Controls.Clear();

                if (obj == null)
                {
                    _detailsContentHost.Controls.Add(MakeDetailsHint(
                        "Select an item.\r\nDouble-click or right-click → View… for details."));
                    return;
                }

                if (!IsWorld(obj))
                {
                    _detailsContentHost.Controls.Add(MakeDetailsHint(
                        "Double-click or right-click → View… for details."));
                    return;
                }

                if (_worldCard == null)
                    _worldCard = new WorldCardPanel { Dock = DockStyle.Fill };

                _worldCard.SetWorld(obj);

                // Derived orbit phrasing (never show radial_order)
                string orbit = GetOrbitDescription(obj);
                _worldCard.SetOrbitText(orbit);

                _detailsContentHost.Controls.Add(_worldCard);
            }
            finally
            {
                _detailsContentHost.ResumeLayout(true);
            }
        }

        private static bool IsWorld(SystemObjectInfo o)
        {
            if (o == null) return false;

            string kind = (o.ObjectKind ?? "").Trim();
            return
                string.Equals(kind, "planet", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "moon", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "dwarf_planet", StringComparison.OrdinalIgnoreCase);
        }

        private Control MakeDetailsHint(string text)
        {
            var lbl = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(100, 100, 100),
                Text = text ?? ""
            };
            return lbl;
        }
    }
}
