using System;
using System.Data;
using System.Windows.Forms;

namespace StarMap2010
{
    public sealed partial class CompareSwapForm
    {
        private void LoadFromDb()
        {
            _govTable = _govDao.GetGovernmentLookup();

            DataTable dt = _swapDao.LoadTwoSystemsForCompare(_systemIdA, _systemIdB);

            _rowA = FindRowById(dt, _systemIdA);
            _rowB = FindRowById(dt, _systemIdB);

            if (_rowA == null || _rowB == null)
            {
                MessageBox.Show(this, "Could not load both systems.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void BindToUi()
        {
            cmbGovA.DataSource = _govTable;
            cmbGovA.DisplayMember = "government_name";
            cmbGovA.ValueMember = "government_id";

            cmbGovB.DataSource = _govTable.Copy();
            cmbGovB.DisplayMember = "government_name";
            cmbGovB.ValueMember = "government_id";

            PullRow(_rowA, txtSystemNameA, txtRealSystemNameA, txtPrimaryStarNameA, txtPrimaryStarTypeA, txtSystemTypeA, cmbGovA, txtNotesA);
            PullRow(_rowB, txtSystemNameB, txtRealSystemNameB, txtPrimaryStarNameB, txtPrimaryStarTypeB, txtSystemTypeB, cmbGovB, txtNotesB);
        }

        private static void PullRow(DataRow r, TextBox sys, TextBox real, TextBox pName, TextBox pType, TextBox sType, ComboBox gov, TextBox notes)
        {
            sys.Text = Convert.ToString(r["system_name"]);
            real.Text = Convert.ToString(r["real_system_name"]);
            pName.Text = Convert.ToString(r["primary_star_name"]);
            pType.Text = Convert.ToString(r["primary_star_type"]);
            sType.Text = Convert.ToString(r["system_type"]);
            notes.Text = Convert.ToString(r["notes"]);
            gov.SelectedValue = Convert.ToString(r["government_id"]);
        }

        private void PushControlsToRows()
        {
            PushRow(_rowA, txtSystemNameA, txtRealSystemNameA, txtPrimaryStarNameA, txtPrimaryStarTypeA, txtSystemTypeA, cmbGovA, txtNotesA);
            PushRow(_rowB, txtSystemNameB, txtRealSystemNameB, txtPrimaryStarNameB, txtPrimaryStarTypeB, txtSystemTypeB, cmbGovB, txtNotesB);
        }

        private static void PushRow(DataRow r, TextBox sys, TextBox real, TextBox pName, TextBox pType, TextBox sType, ComboBox gov, TextBox notes)
        {
            r["system_name"] = sys.Text ?? "";
            r["real_system_name"] = real.Text ?? "";
            r["primary_star_name"] = pName.Text ?? "";
            r["primary_star_type"] = pType.Text ?? "";
            r["system_type"] = sType.Text ?? "";
            r["notes"] = notes.Text ?? "";
            object gv = gov.SelectedValue;
            if (gv != null)
                r["government_id"] = gv.ToString();
            else
                r["government_id"] = "";
        }

        private void SaveToDb()
        {
            try
            {
                PushControlsToRows();
                _swapDao.UpdateTwoSystems(_rowA, _rowB);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Save failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static DataRow FindRowById(DataTable dt, string id)
        {
            foreach (DataRow r in dt.Rows)
                if (string.Equals(Convert.ToString(r["system_id"]), id, StringComparison.Ordinal))
                    return r;
            return null;
        }
    }
}
