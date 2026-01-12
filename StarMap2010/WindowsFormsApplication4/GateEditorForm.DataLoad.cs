using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using StarMap2010.Data;
using StarMap2010.Models;

namespace StarMap2010
{
    public sealed partial class GateEditorForm : Form
    {
        private readonly JumpGatesDao _gatesDao;

        private JumpGate _gate; // loaded gate (may be null)
        private readonly List<JumpGateRenderableLink> _links = new List<JumpGateRenderableLink>();

        private void InitDaos()
        {
            // Call once from constructor AFTER _dbPath is set
            // (ctor already sets _dbPath)
            // Safe even if called multiple times, but keep it once.
        }

        // Call this from ctor: _gatesDao = new JumpGatesDao(_dbPath);
        // (I’m showing it here as a reminder because this file can’t alter your ctor signature.)
        // Make sure your ctor includes:
        // _gatesDao = new JumpGatesDao(_dbPath);

        private void LoadGateAndLinksFromDb()
        {
            if (_gatesDao == null)
                throw new InvalidOperationException("_gatesDao is null. Construct it in the form constructor.");

            if (string.IsNullOrWhiteSpace(_systemId))
                throw new InvalidOperationException("_systemId is null/empty. GateEditorForm requires a valid systemId.");

            _gate = _gatesDao.LoadGateBySystemId(_systemId);
            _links.Clear();

            if (_gate != null)
            {
                // Gate -> UI
                txtGateId.Text = _gate.GateId ?? "";
                txtOwnerGovId.Text = _gate.OwnerGovernmentId ?? "";
                txtGateNotes.Text = _gate.Notes ?? "";

                txtGateName.Text = _gate.GateName ?? "";

                SelectCombo(cmbGateType, _gate.GateType, "standard");
                SelectCombo(cmbGateClass, _gate.GateClass, "standard");
                SelectCombo(cmbGateRole, _gate.GateRole, "standard");

                txtCommissioned.Text = _gate.CommissionedDate ?? "";
                txtDecommissioned.Text = _gate.DecommissionedDate ?? "";
                chkOperational.Checked = (_gate.IsOperational != 0);

                // Links
                string gateId = (txtGateId.Text ?? "").Trim();
                if (gateId.Length > 0)
                    _links.AddRange(_gatesDao.LoadLinksByGateId(gateId));
            }
            else
            {
                // No gate yet -> defaults
                txtGateId.Text = "JG-" + _systemId;

                // owner defaults to system's government
                string gov = _gatesDao.GetSystemGovernmentId(_systemId);
                txtOwnerGovId.Text = gov ?? "";

                txtGateNotes.Text = "";
                txtGateName.Text = "";

                SelectCombo(cmbGateType, "standard", "standard");
                SelectCombo(cmbGateClass, "standard", "standard");
                SelectCombo(cmbGateRole, "standard", "standard");

                txtCommissioned.Text = "";
                txtDecommissioned.Text = "";
                chkOperational.Checked = true;
            }
        }

        private void BindTargetsFromDb()
        {
            var items = _gatesDao.LoadGateLinkTargets(_systemId);

            cmbTargetSystem.DataSource = items;
            cmbTargetSystem.DisplayMember = "Name";
            cmbTargetSystem.ValueMember = "SystemId";
        }

        private void AddOrUpdateLinkInMemory()
        {
            string targetSystemId = null;
            if (cmbTargetSystem.SelectedValue != null)
                targetSystemId = Convert.ToString(cmbTargetSystem.SelectedValue);

            if (string.IsNullOrWhiteSpace(targetSystemId))
                return;

            string gateId = (txtGateId.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(gateId))
            {
                MessageBox.Show(this, "Gate ID is required before adding links.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string ownerGov = (txtOwnerGovId.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(ownerGov))
            {
                MessageBox.Show(this, "Owner Government ID is required before adding links.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Best-practice-ish: ensure current gate exists if user is adding links
            // (This does write to DB before clicking Save. If you want zero DB writes until Save,
            // we can change strategy, but this matches your previous behavior.)
            _gatesDao.UpsertGate(BuildGateFromControls());

            // Ensure target has a gate row, so we can link gate_id <-> gate_id
            string targetGateId = _gatesDao.EnsureGateForSystemExists(targetSystemId.Trim());

            // Gather editor fields
            string status = Convert.ToString(cmbLinkStatus.SelectedItem) ?? "open";
            string notes = txtLinkNotes.Text ?? "";

            string activeFrom = (txtActiveFrom.Text ?? "").Trim();
            string activeUntil = (txtActiveUntil.Text ?? "").Trim();
            int isBi = chkBidirectional.Checked ? 1 : 0;

            double? transitHours = ParseNullableDouble(txtTransitHours.Text);
            int? tollCredits = ParseNullableInt(txtTollCredits.Text);

            // Normalize pair for storage stability
            string a = gateId.Trim();
            string b = (targetGateId ?? "").Trim();

            NormalizePair(ref a, ref b);

            // Find existing link by gate ids
            JumpGateRenderableLink existing = null;
            for (int i = 0; i < _links.Count; i++)
            {
                string ea = (_links[i].GateAId ?? "").Trim();
                string eb = (_links[i].GateBId ?? "").Trim();
                NormalizePair(ref ea, ref eb);

                if (string.Equals(ea, a, StringComparison.Ordinal) &&
                    string.Equals(eb, b, StringComparison.Ordinal))
                {
                    existing = _links[i];
                    break;
                }
            }

            if (existing == null)
            {
                existing = new JumpGateRenderableLink();
                _links.Add(existing);
            }

            // Fill
            existing.GateAId = a;
            existing.GateBId = b;
            existing.SystemAId = _systemId;
            existing.SystemBId = targetSystemId.Trim();

            existing.Status = status;
            existing.Notes = notes;

            existing.ActiveFrom = activeFrom;
            existing.ActiveUntil = activeUntil;
            existing.IsBidirectional = isBi;
            existing.TransitHours = transitHours;
            existing.TollCredits = tollCredits;

            RefreshLinksList();
        }

        private void RemoveSelectedLinkInMemory()
        {
            if (lvLinks.SelectedItems.Count == 0) return;

            for (int i = lvLinks.SelectedItems.Count - 1; i >= 0; i--)
            {
                var it = lvLinks.SelectedItems[i];
                var link = it.Tag as JumpGateRenderableLink;
                if (link != null)
                    _links.Remove(link);
            }

            RefreshLinksList();
        }

        private void SaveToDb()
        {
            var gate = BuildGateFromControls();

            if (string.IsNullOrWhiteSpace(gate.GateId))
            {
                MessageBox.Show(this, "Gate ID is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(gate.OwnerGovernmentId))
            {
                MessageBox.Show(this, "Owner Government ID is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Gate
            _gatesDao.UpsertGate(gate);

            // Links
            _gatesDao.ReplaceLinksForGate(gate.GateId.Trim(), _links);

            DialogResult = DialogResult.OK;
            Close();
        }

        private JumpGate BuildGateFromControls()
        {
            var g = new JumpGate();

            g.SystemId = _systemId;

            g.GateId = (txtGateId.Text ?? "").Trim();
            g.OwnerGovernmentId = (txtOwnerGovId.Text ?? "").Trim();
            g.GateType = Convert.ToString(cmbGateType.SelectedItem) ?? "standard";
            g.Notes = (txtGateNotes.Text ?? "").Trim();

            g.GateName = (txtGateName.Text ?? "").Trim();
            g.GateClass = Convert.ToString(cmbGateClass.SelectedItem) ?? "standard";
            g.GateRole = Convert.ToString(cmbGateRole.SelectedItem) ?? "standard";
            g.CommissionedDate = (txtCommissioned.Text ?? "").Trim();
            g.DecommissionedDate = (txtDecommissioned.Text ?? "").Trim();
            g.IsOperational = chkOperational.Checked ? 1 : 0;

            // Normalize empty strings to null for date columns (optional)
            if (g.CommissionedDate.Length == 0) g.CommissionedDate = null;
            if (g.DecommissionedDate.Length == 0) g.DecommissionedDate = null;

            if (g.GateName.Length == 0) g.GateName = null;
            if (g.Notes.Length == 0) g.Notes = null;

            return g;
        }

        // ----------------- Local helpers -----------------

        private static void NormalizePair(ref string a, ref string b)
        {
            if (string.CompareOrdinal(a, b) > 0)
            {
                string t = a; a = b; b = t;
            }
        }

        private static double? ParseNullableDouble(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            double v;
            if (double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;
            return null;
        }

        private static int? ParseNullableInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            int v;
            if (int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                return v;
            return null;
        }

        // You already have this method in your UI file; leaving here for completeness
        private static void SelectCombo(ComboBox cmb, string value, string fallback)
        {
            if (cmb == null) return;

            string v = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (string.Equals(Convert.ToString(cmb.Items[i]), v, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedIndex = i;
                    return;
                }
            }

            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (string.Equals(Convert.ToString(cmb.Items[i]), fallback, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedIndex = i;
                    return;
                }
            }

            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }
    }
}
