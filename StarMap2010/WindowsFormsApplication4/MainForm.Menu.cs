using System;
using System.Collections.Generic;
using System.Windows.Forms;
using StarMap2010.Models;

namespace StarMap2010
{
    //MainForm.Menu
    public partial class MainForm
    {
        // Key by GOVERNMENT NAME (because systems already have GovernmentName reliably)
        private readonly Dictionary<string, ToolStripMenuItem> govMenuItemsByName =
            new Dictionary<string, ToolStripMenuItem>(StringComparer.OrdinalIgnoreCase);

        // Prevent re-entrant / spam ApplyGovernmentFilter while toggling many items
        private bool _suppressGovMenuEvents = false;

        private void BuildMenuViewFilters()
        {
            // BuildUi() must create "menu" first
            if (menu == null) return;

            mnuShowJumpGates = new ToolStripMenuItem("Show Jump Gates")
            {
                CheckOnClick = true,
                Checked = true
            };
            mnuShowJumpGates.CheckedChanged += (s, e) =>
            {
                if (canvas == null) return;
                canvas.ShowGates = mnuShowJumpGates.Checked;
                canvas.Invalidate();
            };

            mnuFilters = new ToolStripMenuItem("&Filters");

            mnuFilterGovernments = new ToolStripMenuItem("&Governments");
            mnuFilterGovernments.DropDownItems.Add(
                new ToolStripMenuItem("(loading...)") { Enabled = false });

            // ✅ OPTION A: keep dropdown open when clicking items
            mnuFilterGovernments.DropDown.Closing += (s, e) =>
            {
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                    e.Cancel = true;
            };

            mnuFilters.DropDownItems.Add(mnuFilterGovernments);
            mnuFilters.DropDownItems.Add(mnuShowJumpGates);

            // Avoid duplicates if BuildUi() is ever called again
            if (!menu.Items.Contains(mnuFilters)) menu.Items.Add(mnuFilters);
        }


        public void BuildGovernmentFilterMenuItems(List<GovernmentInfo> governments, HashSet<string> restoreChecked)
        {
            govMenuItemsByName.Clear();

            if (mnuFilterGovernments == null) return;
            mnuFilterGovernments.DropDownItems.Clear();

            if (governments == null || governments.Count == 0)
            {
                mnuFilterGovernments.DropDownItems.Add(new ToolStripMenuItem("(none)") { Enabled = false });
                return;
            }

            bool hasRestore = (restoreChecked != null && restoreChecked.Count > 0);

            mnuFilterGovernments.DropDownItems.Add(new ToolStripMenuItem("All", null, (s, e) => SetAllGovMenuChecks(true)));
            mnuFilterGovernments.DropDownItems.Add(new ToolStripMenuItem("None", null, (s, e) => SetAllGovMenuChecks(false)));
            mnuFilterGovernments.DropDownItems.Add(new ToolStripSeparator());

            _suppressGovMenuEvents = true;
            try
            {
                for (int i = 0; i < governments.Count; i++)
                {
                    var g = governments[i];
                    if (g == null) continue;
                    if (string.IsNullOrWhiteSpace(g.GovernmentName)) continue;

                    string name = g.GovernmentName.Trim();

                    bool isChecked = hasRestore ? restoreChecked.Contains(name) : true;

                    var mi = new ToolStripMenuItem
                    {
                        Text = name,
                        CheckOnClick = true,
                        Checked = isChecked,
                        Tag = g
                    };


                    mi.CheckedChanged += (s, e) =>
                    {
                        if (_suppressGovMenuEvents) return;
                        ApplyGovernmentFilter();
                    };

                    mnuFilterGovernments.DropDownItems.Add(mi);
                    govMenuItemsByName[name] = mi;
                }
            }
            finally
            {
                _suppressGovMenuEvents = false;
            }

            // Safety: if restore set existed but nothing matched, avoid “blank map”
            if (hasRestore)
            {
                int checkedCount = 0;
                foreach (var kvp in govMenuItemsByName)
                    if (kvp.Value != null && kvp.Value.Checked) checkedCount++;

                if (checkedCount == 0)
                    SetAllGovMenuChecks(true);
            }
        }

        public HashSet<string> GetSelectedGovernmentNamesFromMenu()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in govMenuItemsByName)
            {
                var mi = kvp.Value;
                if (mi != null && mi.Checked)
                    set.Add(kvp.Key);
            }

            return set;
        }

        private bool IsShowGatesEnabled()
        {
            return mnuShowJumpGates == null || mnuShowJumpGates.Checked;
        }

        private void SetAllGovMenuChecks(bool isChecked)
        {
            _suppressGovMenuEvents = true;
            try
            {
                foreach (var kvp in govMenuItemsByName)
                    if (kvp.Value != null) kvp.Value.Checked = isChecked;
            }
            finally
            {
                _suppressGovMenuEvents = false;
            }

            ApplyGovernmentFilter();
        }
    }
}
