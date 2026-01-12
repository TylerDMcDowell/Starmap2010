// ============================================================
// File: MainForm.SidebarLayout.cs
// Project: StarMap2010
//
// Sidebar accordion layout manager.
// Ensures that when sections collapse, remaining sections expand
// to consume available vertical space.
//
// This assumes the sidebar is composed of:
// - Summary panel (always visible)
// - CollapsibleSection: Gate Facility
// - CollapsibleSection: System Contents
//
// Contents section hosts SplitContainer (TreeView + Details host)
// ============================================================

using System;
using System.Drawing;
using System.Windows.Forms;
using StarMap2010.Ui;

namespace StarMap2010
{
    public partial class MainForm
    {
        private void WireSidebarLayout()
        {
            if (infoPanel == null) return;

            // Reflow whenever the sidebar changes size (window resize, splitter, etc.)
            infoPanel.Resize += (s, e) => ReflowSidebar();

            if (_secGates != null) _secGates.CollapsedChanged += (s, e) => ReflowSidebar();
            if (_secContents != null) _secContents.CollapsedChanged += (s, e) => ReflowSidebar();
        }

        private void ReflowSidebar()
        {
            if (infoPanel == null) return;
            if (_sidebar == null) return;
            if (_secGates == null) return;
            if (_secContents == null) return;

            infoPanel.SuspendLayout();
            _sidebar.SuspendLayout();

            try
            {
                int pad = infoPanel.Padding.Top + infoPanel.Padding.Bottom;

                int available = infoPanel.ClientSize.Height - pad;
                if (available < 0) available = 0;

                int summaryHeight = (_summaryHost != null) ? _summaryHost.Height : 0;

                int gatesHeader = _secGates.HeaderHeight;
                int contentsHeader = _secContents.HeaderHeight;

                bool gatesOpen = !_secGates.Collapsed;
                bool contentsOpen = !_secContents.Collapsed;

                // Collapsed Fill must become Top header-only
                if (!contentsOpen)
                {
                    _secContents.Dock = DockStyle.Top;
                    _secContents.Height = contentsHeader;
                }
                else
                {
                    _secContents.Dock = DockStyle.Fill;
                }

                _secGates.Dock = DockStyle.Top;

                // Space for bodies (summary + both headers always present)
                int bodyAvailable = available - summaryHeight - gatesHeader - contentsHeader;
                if (bodyAvailable < 0) bodyAvailable = 0;

                // ---- Gate sizing: keep stable regardless of System collapsed/expanded ----
                const int gatesPreferred = 240; // feels good for button + routes list
                const int gatesMin = 170;
                const int gatesMax = 340;

                int gatesBody = 0;

                if (gatesOpen)
                {
                    // Try preferred, clamp to available and min/max
                    gatesBody = gatesPreferred;
                    if (gatesBody < gatesMin) gatesBody = gatesMin;
                    if (gatesBody > gatesMax) gatesBody = gatesMax;

                    // But never exceed what's actually available
                    if (gatesBody > bodyAvailable) gatesBody = bodyAvailable;
                }
                else
                {
                    gatesBody = 0;
                }

                _secGates.Height = gatesHeader + gatesBody;

                // Gate internals
                if (rtbGateInfo != null && gatesOpen)
                {
                    int top = (btnEditGates != null) ? (btnEditGates.Bottom + 8) : 8;
                    int contentH = _secGates.ContentPanel.ClientSize.Height;

                    int rtbH = Math.Max(80, contentH - top - 8);
                    rtbGateInfo.Height = rtbH;
                }

                // Contents internals
                if (splitContents != null && contentsOpen)
                {
                    splitContents.Dock = DockStyle.Fill;
                }
            }
            finally
            {
                _sidebar.ResumeLayout(true);
                infoPanel.ResumeLayout(true);
            }
        }

    }
}
