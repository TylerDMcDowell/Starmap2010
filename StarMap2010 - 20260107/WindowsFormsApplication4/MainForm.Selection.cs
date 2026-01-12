using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using StarMap2010.Models;

namespace StarMap2010
{
    public partial class MainForm
    {
        private void SetSelectedSystem(StarSystemInfo s)
        {
            _currentSelectedForSidebar = s;

            if (canvas != null)
                canvas.SetSelected(s);

            if (s == null)
            {
                if (lblSysName != null) lblSysName.Text = "No system selected";
                if (lblGov != null) lblGov.Text = "-";
                if (lblCoords != null) lblCoords.Text = "Coords (ly): -";

                UpdateGateInfo(null);

                ResetSystemTreePlaceholder();

                if (btnNotes != null) btnNotes.Enabled = false;

                if (btnEditGates != null)
                {
                    btnEditGates.Enabled = false;
                    EnsureToolTip().SetToolTip(btnEditGates, "Select a star system on the map to edit its gate facility and routes.");
                }

                return;
            }

            string systemName = !string.IsNullOrEmpty(s.SystemName) ? s.SystemName : s.RealSystemName;

            if (lblSysName != null) lblSysName.Text = systemName;

            if (lblGov != null)
            {
                string gov = (s.GovernmentName ?? "").Trim();
                string st = (s.SystemType ?? "").Trim();

                if (gov.Length == 0 && st.Length == 0) lblGov.Text = "-";
                else if (gov.Length == 0) lblGov.Text = st;
                else if (st.Length == 0) lblGov.Text = gov;
                else lblGov.Text = gov + " \u2022 " + st;
            }

            if (lblCoords != null)
            {
                lblCoords.Text = string.Format(CultureInfo.InvariantCulture,
                    "Coords (ly): X={0:0.00}  Y={1:0.00}  Z={2:0.00}",
                    s.XReal, s.YReal, s.ZReal);
            }

            UpdateGateInfo(s);

            LoadSystemTree(s.SystemId);

            if (btnNotes != null)
                btnNotes.Enabled = true;

            if (btnEditGates != null)
            {
                btnEditGates.Enabled = true;
                EnsureToolTip().SetToolTip(btnEditGates, "Edit the gate facility and routes for the selected system.");
            }
        }

        private void UpdateGateInfo(StarSystemInfo s)
        {
            if (rtbGateInfo == null) return;

            rtbGateInfo.Clear();

            if (s == null)
            {
                if (btnEditGates != null) btnEditGates.Enabled = false;
                if (lblGateSummary != null) lblGateSummary.Text = "Gate: -";
                RtbAppendLine(rtbGateInfo, "Gate Facility", bold: true);
                return;
            }

            JumpGate gate;
            bool hasGate = gateBySystemId.TryGetValue(s.SystemId, out gate);

            int linkCount = 0;
            for (int i = 0; i < gateLinks.Count; i++)
            {
                var l = gateLinks[i];
                if (l == null) continue;
                if (l.TouchesSystem(s.SystemId)) linkCount++;
            }

            if (lblGateSummary != null)
            {
                if (!hasGate || gate == null)
                {
                    lblGateSummary.Text = "Gate Facility: None";
                }
                else
                {
                    string operational = (gate.IsOperational != 0) ? "Operational" : "Offline";
                    string type = string.IsNullOrWhiteSpace(gate.GateType) ? "-" : gate.GateType.Trim();
                    lblGateSummary.Text = "Gate Facility: " + operational + " \u2022 " + type + " \u2022 " +
                                         linkCount.ToString(CultureInfo.InvariantCulture) + " routes";
                }
            }

            // ---- Gate header ----
            RtbAppendLine(rtbGateInfo, "Gate Facility", bold: true);
            RtbAppendLine(rtbGateInfo, "────────", Color.FromArgb(140, 140, 140));

            if (!hasGate || gate == null)
            {
                RtbAppendLine(rtbGateInfo, "None", Color.FromArgb(120, 120, 120), bold: true);
                RtbAppendLine(rtbGateInfo);
            }
            else
            {
                string gateName = Norm(gate.GateName);
                string gateType = Norm(gate.GateType);

                bool operational = gate.IsOperational != 0;

                // Never show UUID gate_id to users
                if (!string.IsNullOrEmpty(gateName))
                    RtbAppendLine(rtbGateInfo, gateName, bold: true);
                else
                    RtbAppendLine(rtbGateInfo, "(Unnamed Gate Facility)", Color.FromArgb(120, 120, 120), bold: true);

                RtbAppend(rtbGateInfo, "Status: ");
                RtbAppendLine(rtbGateInfo, operational ? "Operational" : "Offline",
                    operational ? Color.FromArgb(20, 120, 60) : Color.FromArgb(140, 40, 40),
                    bold: true);

                RtbAppendLine(rtbGateInfo, "Type: " + (string.IsNullOrEmpty(gateType) ? "-" : gateType));

                string notes = Norm(gate.Notes);
                if (!string.IsNullOrEmpty(notes))
                {
                    int ix = notes.IndexOf('\n');
                    string firstLine = (ix >= 0) ? notes.Substring(0, ix) : notes;
                    RtbAppendLine(rtbGateInfo, "Notes: " + firstLine, Color.FromArgb(90, 90, 90));
                }

                RtbAppendLine(rtbGateInfo);
            }

            // ---- Routes ----
            RtbAppendLine(rtbGateInfo, "Routes", bold: true);
            RtbAppendLine(rtbGateInfo, "────────", Color.FromArgb(140, 140, 140));

            if (linkCount == 0)
            {
                RtbAppendLine(rtbGateInfo, "None", Color.FromArgb(120, 120, 120), bold: true);
            }
            else
            {
                for (int i = 0; i < gateLinks.Count; i++)
                {
                    var l = gateLinks[i];
                    if (l == null) continue;
                    if (!l.TouchesSystem(s.SystemId)) continue;

                    string otherSystemId = l.OtherSystem(s.SystemId);
                    StarSystemInfo other = FindSystemById(otherSystemId);

                    string otherName =
                        (other != null && !string.IsNullOrEmpty(other.SystemName)) ? other.SystemName :
                        (other != null ? other.RealSystemName : otherSystemId);

                    string st = NormStatus(l.Status);
                    Color stCol = StatusColor(st);

                    RtbAppend(rtbGateInfo, "• ");
                    RtbAppend(rtbGateInfo, otherName + "  ");
                    RtbAppendLine(rtbGateInfo, "[" + TitleCaseStatus(st) + "]", stCol, bold: true);
                }
            }

            rtbGateInfo.SelectionStart = 0;
            rtbGateInfo.ScrollToCaret();
        }
    }
}
