using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using StarMap2010.Data;

namespace StarMap2010
{
    public sealed partial class CompareSwapForm : Form
    {
        private readonly string _dbPath;
        private readonly string _systemIdA;
        private readonly string _systemIdB;

        private readonly GovernmentsDao _govDao;
        private readonly CompareSwapDao _swapDao;

        private DataRow _rowA;
        private DataRow _rowB;
        private DataTable _govTable;

        // Controls (A)
        private TextBox txtSystemNameA, txtRealSystemNameA, txtPrimaryStarNameA, txtPrimaryStarTypeA, txtSystemTypeA;
        private ComboBox cmbGovA;
        private TextBox txtNotesA;

        // Controls (B)
        private TextBox txtSystemNameB, txtRealSystemNameB, txtPrimaryStarNameB, txtPrimaryStarTypeB, txtSystemTypeB;
        private ComboBox cmbGovB;
        private TextBox txtNotesB;

        // Buttons
        private Button btnSwapNames, btnSwapGov, btnSwapPrimary, btnSwapType, btnSwapNotes, btnSwapAll, btnSave, btnCancel;

        public CompareSwapForm(string dbPath, string systemIdA, string systemIdB)
        {
            _dbPath = dbPath;
            _systemIdA = systemIdA;
            _systemIdB = systemIdB;

            _govDao = new GovernmentsDao(_dbPath);
            _swapDao = new CompareSwapDao(_dbPath);

            Text = "Compare / Swap Star Systems";
            StartPosition = FormStartPosition.CenterParent;
            Width = 1100;
            Height = 620;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;

            BuildUi();
            LoadFromDb();
            BindToUi();
        }

        // ---------------- UI BUILD ----------------

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.Padding = new Padding(10);

            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

            Controls.Add(root);

            var header = new Label();
            header.Dock = DockStyle.Fill;
            header.TextAlign = ContentAlignment.MiddleLeft;
            header.Font = new Font("Arial", 11f, FontStyle.Bold);
            header.Text = "Field-by-field compare. Edit either side. Swap fields, then Save.";
            root.Controls.Add(header, 0, 0);

            var fields = new TableLayoutPanel();
            fields.Dock = DockStyle.Fill;
            fields.ColumnCount = 3;
            fields.RowCount = 8;

            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            for (int i = 0; i < 7; i++)
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(fields, 0, 1);

            fields.Controls.Add(MakeColHeader("System A"), 1, 0);
            fields.Controls.Add(MakeColHeader("System B"), 2, 0);

            fields.Controls.Add(MakeRowLabel("System Name"), 0, 1);
            txtSystemNameA = MakeTextBox();
            txtSystemNameB = MakeTextBox();
            fields.Controls.Add(txtSystemNameA, 1, 1);
            fields.Controls.Add(txtSystemNameB, 2, 1);

            fields.Controls.Add(MakeRowLabel("Real System Name"), 0, 2);
            txtRealSystemNameA = MakeTextBox();
            txtRealSystemNameB = MakeTextBox();
            fields.Controls.Add(txtRealSystemNameA, 1, 2);
            fields.Controls.Add(txtRealSystemNameB, 2, 2);

            fields.Controls.Add(MakeRowLabel("Primary Star Name"), 0, 3);
            txtPrimaryStarNameA = MakeTextBox();
            txtPrimaryStarNameB = MakeTextBox();
            fields.Controls.Add(txtPrimaryStarNameA, 1, 3);
            fields.Controls.Add(txtPrimaryStarNameB, 2, 3);

            fields.Controls.Add(MakeRowLabel("Primary Star Type"), 0, 4);
            txtPrimaryStarTypeA = MakeTextBox();
            txtPrimaryStarTypeB = MakeTextBox();
            fields.Controls.Add(txtPrimaryStarTypeA, 1, 4);
            fields.Controls.Add(txtPrimaryStarTypeB, 2, 4);

            fields.Controls.Add(MakeRowLabel("System Type"), 0, 5);
            txtSystemTypeA = MakeTextBox();
            txtSystemTypeB = MakeTextBox();
            fields.Controls.Add(txtSystemTypeA, 1, 5);
            fields.Controls.Add(txtSystemTypeB, 2, 5);

            fields.Controls.Add(MakeRowLabel("Government"), 0, 6);
            cmbGovA = MakeCombo();
            cmbGovB = MakeCombo();
            fields.Controls.Add(cmbGovA, 1, 6);
            fields.Controls.Add(cmbGovB, 2, 6);

            fields.Controls.Add(MakeRowLabel("Notes"), 0, 7);
            txtNotesA = MakeNotesBox();
            txtNotesB = MakeNotesBox();
            fields.Controls.Add(txtNotesA, 1, 7);
            fields.Controls.Add(txtNotesB, 2, 7);

            BuildButtons(root);
        }

        private void BuildButtons(TableLayoutPanel root)
        {
            var grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = 4;
            grid.RowCount = 2;

            for (int i = 0; i < 4; i++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            root.Controls.Add(grid, 0, 2);

            btnSwapNames = MakeBtn("Swap Names", new EventHandler(delegate { SwapNames(); }));
            btnSwapGov = MakeBtn("Swap Government", new EventHandler(delegate { SwapGovernment(); }));
            btnSwapPrimary = MakeBtn("Swap Primary", new EventHandler(delegate { SwapPrimary(); }));
            btnSwapType = MakeBtn("Swap System Type", new EventHandler(delegate { SwapSystemType(); }));
            btnSwapNotes = MakeBtn("Swap Notes", new EventHandler(delegate { SwapNotes(); }));
            btnSwapAll = MakeBtn("Swap All", new EventHandler(delegate { SwapAll(); }));
            btnSave = MakeBtn("Save", new EventHandler(delegate { SaveToDb(); }));
            btnCancel = MakeBtn("Cancel", new EventHandler(delegate { DialogResult = DialogResult.Cancel; Close(); }));

            grid.Controls.Add(btnSwapNames, 0, 0);
            grid.Controls.Add(btnSwapGov, 1, 0);
            grid.Controls.Add(btnSwapPrimary, 2, 0);
            grid.Controls.Add(btnSwapType, 3, 0);

            grid.Controls.Add(btnSwapNotes, 0, 1);
            grid.Controls.Add(btnSwapAll, 1, 1);
            grid.Controls.Add(btnSave, 2, 1);
            grid.Controls.Add(btnCancel, 3, 1);
        }

        private Label MakeColHeader(string text)
        {
            var l = new Label();
            l.Text = text;
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Font = new Font("Arial", 10f, FontStyle.Bold);
            return l;
        }

        private Label MakeRowLabel(string text)
        {
            var l = new Label();
            l.Text = text;
            l.Dock = DockStyle.Fill;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        private TextBox MakeTextBox()
        {
            var t = new TextBox();
            t.Dock = DockStyle.Fill;
            return t;
        }

        private TextBox MakeNotesBox()
        {
            var t = new TextBox();
            t.Dock = DockStyle.Fill;
            t.Multiline = true;
            t.ScrollBars = ScrollBars.Vertical;
            return t;
        }

        private ComboBox MakeCombo()
        {
            var c = new ComboBox();
            c.Dock = DockStyle.Fill;
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            return c;
        }

        private Button MakeBtn(string text, EventHandler onClick)
        {
            var b = new Button();
            b.Text = text;
            b.Dock = DockStyle.Fill;
            b.Height = 32;
            b.Click += onClick;
            return b;
        }

        // ---------------- Swap logic ----------------

        private void SwapText(TextBox a, TextBox b)
        {
            string t = a.Text;
            a.Text = b.Text;
            b.Text = t;
        }

        private void SwapCombo(ComboBox a, ComboBox b)
        {
            object t = a.SelectedValue;
            a.SelectedValue = b.SelectedValue;
            b.SelectedValue = t;
        }

        private void SwapNames()
        {
            SwapText(txtSystemNameA, txtSystemNameB);
            SwapText(txtRealSystemNameA, txtRealSystemNameB);
        }

        private void SwapGovernment()
        {
            SwapCombo(cmbGovA, cmbGovB);
        }

        private void SwapPrimary()
        {
            SwapText(txtPrimaryStarNameA, txtPrimaryStarNameB);
            SwapText(txtPrimaryStarTypeA, txtPrimaryStarTypeB);
        }

        private void SwapSystemType()
        {
            SwapText(txtSystemTypeA, txtSystemTypeB);
        }

        private void SwapNotes()
        {
            SwapText(txtNotesA, txtNotesB);
        }

        private void SwapAll()
        {
            SwapNames();
            SwapPrimary();
            SwapSystemType();
            SwapGovernment();
            SwapNotes();
        }
    }
}
