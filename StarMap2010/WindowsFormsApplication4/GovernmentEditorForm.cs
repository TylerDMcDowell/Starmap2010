using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace StarMap2010
{
    public class GovernmentEditorForm : Form
    {
        private readonly string _dbPath;

        private DataGridView grid;
        private Button btnSave;
        private Button btnCancel;
        private Label lblHint;

        private SQLiteConnection conn;
        private SQLiteDataAdapter da;
        private DataTable dt;

        // Extra UI column (not stored in DB)
        private const string COL_PICK = "_pick_color";

        public GovernmentEditorForm(string dbPath)
        {
            _dbPath = dbPath;
            BuildUi();
        }

        private void BuildUi()
        {
            this.Text = "Edit Governments";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(820, 520);

            // ✅ Match MainForm / standard WinForms theme
            this.BackColor = SystemColors.Control;
            this.Font = SystemFonts.MessageBoxFont;

            lblHint = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = SystemColors.ControlText,
                BackColor = SystemColors.ControlLight,
                Padding = new Padding(10, 0, 10, 0),
                Text = "Edit rows directly. Add new row at bottom. Delete by selecting row and pressing Delete. Use Pick… to choose faction_color."
            };

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                EnableHeadersVisualStyles = false,

                // ✅ Standard light grid
                BackgroundColor = SystemColors.Window,
                GridColor = SystemColors.ControlDark
            };

            grid.DefaultCellStyle.BackColor = SystemColors.Window;
            grid.DefaultCellStyle.ForeColor = SystemColors.ControlText;
            grid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            grid.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;

            grid.ColumnHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;

            grid.RowHeadersDefaultCellStyle.BackColor = SystemColors.Control;
            grid.RowHeadersDefaultCellStyle.ForeColor = SystemColors.ControlText;

            // Confirm delete (optional but recommended)
            grid.UserDeletingRow += (s, e) =>
            {
                var res = MessageBox.Show(
                    this,
                    "Delete this government?\n\nNote: this may fail if star_systems reference it.",
                    "Confirm delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (res != DialogResult.Yes)
                    e.Cancel = true;
            };

            // Color picker button + swatch rendering
            grid.CellClick += Grid_CellClick;
            grid.CellFormatting += Grid_CellFormatting;

            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                Padding = new Padding(10),
                BackColor = SystemColors.ControlLight
            };

            btnSave = new Button { Text = "Save", Width = 110, Height = 30, Left = 10, Top = 10 };
            btnCancel = new Button { Text = "Cancel", Width = 110, Height = 30, Left = 130, Top = 10 };

            // ✅ Use standard button styling
            btnSave.UseVisualStyleBackColor = true;
            btnCancel.UseVisualStyleBackColor = true;

            btnSave.Click += BtnSave_Click;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            bottom.Controls.Add(btnSave);
            bottom.Controls.Add(btnCancel);

            this.Controls.Add(grid);
            this.Controls.Add(bottom);
            this.Controls.Add(lblHint);

            this.Load += GovernmentEditorForm_Load;
            this.FormClosing += GovernmentEditorForm_FormClosing;
        }

        private void GovernmentEditorForm_Load(object sender, EventArgs e)
        {
            string cs = "Data Source=" + _dbPath + ";Version=3;Busy Timeout=5000;";
            conn = new SQLiteConnection(cs);
            conn.Open();

            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = "PRAGMA journal_mode=WAL;";
                pragma.ExecuteNonQuery();
                pragma.CommandText = "PRAGMA busy_timeout=5000;";
                pragma.ExecuteNonQuery();
            }

            da = new SQLiteDataAdapter(
                "SELECT government_id, government_name, faction_color FROM governments ORDER BY government_name;",
                conn);

            // Let the adapter generate INSERT/UPDATE/DELETE automatically
            var cb = new SQLiteCommandBuilder(da);

            dt = new DataTable();
            da.Fill(dt);

            grid.DataSource = dt;

            // Add "Pick..." button column (not data-bound)
            if (grid.Columns[COL_PICK] == null)
            {
                var pick = new DataGridViewButtonColumn
                {
                    Name = COL_PICK,
                    HeaderText = "Color",
                    Text = "Pick…",
                    UseColumnTextForButtonValue = true,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                    Width = 70
                };
                grid.Columns.Insert(0, pick);
            }

            // Tune widths
            if (grid.Columns["government_id"] != null) grid.Columns["government_id"].FillWeight = 25;
            if (grid.Columns["government_name"] != null) grid.Columns["government_name"].FillWeight = 55;
            if (grid.Columns["faction_color"] != null) grid.Columns["faction_color"].FillWeight = 20;

            SetReadOnly("government_id", false);
            SetReadOnly("government_name", false);
        }

        private void SetReadOnly(string colName, bool readOnly)
        {
            var c = grid.Columns[colName];
            if (c != null) c.ReadOnly = readOnly;
        }

        // --- Color picker ---
        private void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = grid.Columns[e.ColumnIndex];
            if (col == null) return;

            if (col.Name != COL_PICK) return;

            grid.EndEdit();

            string hex = Convert.ToString(grid.Rows[e.RowIndex].Cells["faction_color"].Value);
            Color parsed;
            Color initial = TryParseHexColor(hex, out parsed) ? parsed : Color.SteelBlue;

            using (var dlg = new ColorDialog())
            {
                dlg.AllowFullOpen = true;
                dlg.FullOpen = true;
                dlg.Color = initial;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string newHex = ColorToHex(dlg.Color); // "#RRGGBB"
                    grid.Rows[e.RowIndex].Cells["faction_color"].Value = newHex;
                    grid.InvalidateRow(e.RowIndex);
                }
            }
        }

        // Paint faction_color cell as a swatch
        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = grid.Columns[e.ColumnIndex];
            if (col == null) return;

            if (!string.Equals(col.Name, "faction_color", StringComparison.OrdinalIgnoreCase))
                return;

            string hex = Convert.ToString(e.Value);

            Color c;
            if (TryParseHexColor(hex, out c))
            {
                e.CellStyle.BackColor = c;

                int lum = (int)(0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B);
                e.CellStyle.ForeColor = (lum < 140) ? Color.White : Color.Black;

                // keep selection readable
                e.CellStyle.SelectionBackColor = c;
                e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                grid.EndEdit();
                this.Validate();

                // Collect government_id renames (old -> new)
                var renames = new List<Tuple<string, string>>();

                foreach (DataRow row in dt.Rows)
                {
                    if (row.RowState == DataRowState.Deleted) continue;

                    string id = Convert.ToString(row["government_id"]);
                    string name = Convert.ToString(row["government_name"]);
                    string col = Convert.ToString(row["faction_color"]);

                    if (string.IsNullOrWhiteSpace(id)) throw new Exception("government_id cannot be blank.");
                    if (string.IsNullOrWhiteSpace(name)) throw new Exception("government_name cannot be blank.");

                    row["government_id"] = id.Trim();
                    row["government_name"] = name.Trim();

                    if (!string.IsNullOrWhiteSpace(col))
                    {
                        string norm = NormalizeHex(col);
                        if (norm != null) row["faction_color"] = norm;
                        else throw new Exception("faction_color must be a hex color like #RRGGBB.");
                    }

                    if (row.RowState == DataRowState.Modified)
                    {
                        string oldId = Convert.ToString(row["government_id", DataRowVersion.Original]);
                        string newId = Convert.ToString(row["government_id", DataRowVersion.Current]);

                        oldId = (oldId ?? "").Trim();
                        newId = (newId ?? "").Trim();

                        if (oldId.Length > 0 && newId.Length > 0 &&
                            !string.Equals(oldId, newId, StringComparison.OrdinalIgnoreCase))
                        {
                            renames.Add(Tuple.Create(oldId, newId));
                        }
                    }
                }

                // IMPORTANT:
                // Renaming a PK + updating child FKs can trip FK constraints unless you have ON UPDATE CASCADE.
                // We’ll temporarily disable FK checks for the save, then re-enable and verify.
                using (var pragma = conn.CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys=OFF;";
                    pragma.ExecuteNonQuery();
                }

                using (var tx = conn.BeginTransaction())
                {
                    // 1) Apply governments table changes (adapter)
                    da.UpdateCommand = new SQLiteCommandBuilder(da).GetUpdateCommand();
                    da.InsertCommand = new SQLiteCommandBuilder(da).GetInsertCommand();
                    da.DeleteCommand = new SQLiteCommandBuilder(da).GetDeleteCommand();

                    da.Update(dt);

                    // 2) Cascade renames into star_systems
                    for (int i = 0; i < renames.Count; i++)
                    {
                        string oldId = renames[i].Item1;
                        string newId = renames[i].Item2;

                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText =
                                "UPDATE star_systems SET government_id = @newId WHERE government_id = @oldId;";
                            cmd.Parameters.AddWithValue("@newId", newId);
                            cmd.Parameters.AddWithValue("@oldId", oldId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();
                }

                using (var pragma = conn.CreateCommand())
                {
                    pragma.CommandText = "PRAGMA foreign_keys=ON;";
                    pragma.ExecuteNonQuery();

                    // Optional sanity check:
                    pragma.CommandText = "PRAGMA foreign_key_check;";
                    using (var r = pragma.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            MessageBox.Show(this,
                                "Saved, but foreign_key_check reported issues.\n" +
                                "You may have broken references in the database.",
                                "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }

                dt.AcceptChanges();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                try
                {
                    using (var pragma = conn.CreateCommand())
                    {
                        pragma.CommandText = "PRAGMA foreign_keys=ON;";
                        pragma.ExecuteNonQuery();
                    }
                }
                catch { }

                MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void GovernmentEditorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (da != null) da.Dispose();
            if (dt != null) dt.Dispose();
            if (conn != null) { conn.Dispose(); conn = null; }
        }

        // --- Hex helpers ---
        private static string ColorToHex(Color c)
        {
            return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        private static string NormalizeHex(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();

            if (Regex.IsMatch(s, @"^[0-9A-Fa-f]{6}$"))
                return "#" + s.ToUpperInvariant();

            if (Regex.IsMatch(s, @"^#[0-9A-Fa-f]{6}$"))
                return s.ToUpperInvariant();

            return null;
        }

        private static bool TryParseHexColor(string s, out Color c)
        {
            c = Color.Empty;
            string norm = NormalizeHex(s);
            if (norm == null) return false;

            int r = int.Parse(norm.Substring(1, 2), NumberStyles.HexNumber);
            int g = int.Parse(norm.Substring(3, 2), NumberStyles.HexNumber);
            int b = int.Parse(norm.Substring(5, 2), NumberStyles.HexNumber);
            c = Color.FromArgb(r, g, b);
            return true;
        }
    }
}
