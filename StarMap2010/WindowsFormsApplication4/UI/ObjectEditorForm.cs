// ============================================================
// File: UI/ObjectEditorForm.cs
// Project: StarMap2010
//
// Large modal viewer/editor shell.
// View mode shows read-only summary + loaded DB tables.
// Edit mode still stubbed (Save does not persist yet).
//
// Locked rules honored:
// - No schema changes
// - radial_order is sorting only; never shown to users
// - Orbit text is derived (never stored)
// - Sidebar is minimal elsewhere; this modal shows the details
// ============================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using StarMap2010.Models;

namespace StarMap2010.Ui
{
    public enum ObjectEditorMode
    {
        View = 0,
        Edit = 1
    }



    public sealed class ObjectEditorForm : Form
    {
        private readonly string _dbPath;
        private readonly SystemObjectInfo _obj;
        private readonly ObjectEditorMode _mode;
        private readonly List<SystemObjectInfo> _all; // may be null

        // UI
        private Label _hdr;
        private TabControl _tabs;

        private TextBox _txtSummary;
        private DataGridView _gridDetails;
        private DataGridView _gridEnv;
        private DataGridView _gridTerraform;
        private DataGridView _gridAttrs;

        private Button _btnPrimary;
        private Button _btnCancel;

        private DataTable _dtEnvEdit;
        private DataTable _dtTerEdit;



        public ObjectEditorForm(string dbPath, SystemObjectInfo obj, ObjectEditorMode mode, List<SystemObjectInfo> all)
        {
            _dbPath = dbPath;
            _obj = obj;
            _mode = mode;
            _all = all;

            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(980, 720);
            MinimumSize = new Size(900, 650);
            Text = (_mode == ObjectEditorMode.View) ? "View Object" : "Edit Object";

            BuildUi();
            ApplyMode();

            // Load DB-backed tables now
            LoadAndBindTables();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            _hdr = new Label
            {
                AutoSize = true,
                Font = new Font("Arial", 13f, FontStyle.Bold),
                ForeColor = SystemColors.ControlText
            };
            root.Controls.Add(_hdr, 0, 0);

            _tabs = new TabControl { Dock = DockStyle.Fill };
            root.Controls.Add(_tabs, 0, 1);

            // ---- Summary tab ----
            var tabSummary = new TabPage("Summary");
            _txtSummary = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                BackColor = Color.White,
                Font = new Font("Consolas", 10f, FontStyle.Regular)
            };
            tabSummary.Controls.Add(_txtSummary);
            _tabs.TabPages.Add(tabSummary);

            // ---- Details tab ----
            var tabDetails = new TabPage("Details");
            _gridDetails = MakeGridProps();
            tabDetails.Controls.Add(_gridDetails);
            _tabs.TabPages.Add(tabDetails);

            // ---- Environment tab ----
            var tabEnv = new TabPage("Environment");
            _gridEnv = MakeGridProps();
            tabEnv.Controls.Add(_gridEnv);
            _tabs.TabPages.Add(tabEnv);

            // ---- Terraform tab ----
            var tabTerraform = new TabPage("Terraform");
            _gridTerraform = MakeGridProps();
            tabTerraform.Controls.Add(_gridTerraform);
            _tabs.TabPages.Add(tabTerraform);

            // ---- Attributes tab ---- (friendly 2-column)
            var tabAttrs = new TabPage("Attributes");
            _gridAttrs = MakeGridAttrs();
            tabAttrs.Controls.Add(_gridAttrs);
            _tabs.TabPages.Add(tabAttrs);

            // Footer buttons
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(0, 8, 0, 0)
            };

            _btnCancel = new Button
            {
                Text = "Cancel",
                Width = 100,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };

            _btnPrimary = new Button
            {
                Text = "Save",
                Width = 100,
                Height = 30
            };

            _btnPrimary.Click += (s, e) =>
            {
                if (_mode == ObjectEditorMode.View)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }

                try
                {
                    using (var conn = new SQLiteConnection("Data Source=" + _dbPath + ";Version=3;"))
                    {
                        conn.Open();
                        using (var tx = conn.BeginTransaction())
                        {
                            SaveEnvironment(conn, tx);
                            SaveTerraform(conn, tx);
                            tx.Commit();
                        }
                    }

                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Save failed:\r\n" + ex.Message,
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

            };

            buttons.Controls.Add(_btnCancel);
            buttons.Controls.Add(_btnPrimary);

            CancelButton = _btnCancel;
            root.Controls.Add(buttons, 0, 2);

            RenderHeaderAndSummary();
        }

        private static DataGridView MakeGrid()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        // 2-column "Field / Value" presentation
        private static DataGridView MakeGridProps()
        {
            var g = MakeGrid();
            g.RowHeadersVisible = false;
            return g;
        }

        // 2-column "Attribute / Value" presentation
        private static DataGridView MakeGridAttrs()
        {
            var g = MakeGrid();
            g.RowHeadersVisible = false;
            return g;
        }

        private void RenderHeaderAndSummary()
        {
            string name = (_obj != null && !string.IsNullOrWhiteSpace(_obj.DisplayName)) ? _obj.DisplayName.Trim() : "(unnamed)";
            string kind = (_obj != null && !string.IsNullOrWhiteSpace(_obj.ObjectKind)) ? _obj.ObjectKind.Trim() : "-";

            _hdr.Text = name + " [" + kind + "]";

            if (_obj == null)
            {
                _txtSummary.Text =
                    "No object was provided.\r\n\r\n" +
                    "This usually means the selected TreeNode.Tag was null.\r\n" +
                    "Make sure placeholder nodes use Tag=null and real nodes use Tag=SystemObjectInfo.";
                return;
            }

            string orbitPhrase = DeriveOrbitPhrase(_all, _obj);

            _txtSummary.Text =
                "Name: " + name + "\r\n" +
                "Kind: " + FriendlyKind(kind) + "\r\n" +
                "Orbit: " + orbitPhrase + "\r\n" +
                "Notes: " + FirstLine(_obj.Notes) + "\r\n" +
                "\r\n" +
                "Tip: Double-click or right-click → View… from the tree.\r\n" +
                "Tabs: Details / Environment / Terraform / Attributes are DB-backed.";
        }

        private void LoadAndBindTables()
        {
            if (_obj == null)
                return;

            if (string.IsNullOrWhiteSpace(_dbPath))
            {
                BindPropsEmpty(_gridDetails, "No DB path provided.");
                BindPropsEmpty(_gridEnv, "No DB path provided.");
                BindPropsEmpty(_gridTerraform, "No DB path provided.");
                BindAttrsEmpty("No DB path provided.");
                return;
            }

            try
            {
                string kind = (_obj.ObjectKind ?? "").Trim().ToLowerInvariant();

                EnsureRowExists("body_environment", _obj.ObjectId);
                EnsureRowExists("terraform_constraints", _obj.ObjectId);


                // 1) Details (planet_details OR moon_details OR minimal)
                DataTable dtDetails;
                if (kind == "planet" || kind == "dwarf_planet")
                    dtDetails = QueryTable("SELECT * FROM planet_details WHERE object_id = @id;", _obj.ObjectId);
                else if (kind == "moon")
                    dtDetails = QueryTable("SELECT * FROM moon_details WHERE object_id = @id;", _obj.ObjectId);
                else
                    dtDetails = MakeSingleRow("info", "No kind-specific details table for: " + kind);

                BindOneRowAsFriendlyProperties(_gridDetails, dtDetails, DetailsOrder);


                // 2) Environment
                _dtEnvEdit = QueryTable("SELECT * FROM body_environment WHERE object_id = @id;", _obj.ObjectId);

                if (_mode == ObjectEditorMode.Edit)
                {
                    _gridEnv.ReadOnly = false;
                    _gridEnv.DataSource = _dtEnvEdit;
                    HideRawColumns(_gridEnv);
                }
                else
                {
                    _gridEnv.ReadOnly = true;
                    BindOneRowAsFriendlyProperties(_gridEnv, _dtEnvEdit, EnvironmentOrder);
                }

                // 3) Terraform constraints
                _dtTerEdit = QueryTable("SELECT * FROM terraform_constraints WHERE object_id = @id;", _obj.ObjectId);

                if (_mode == ObjectEditorMode.Edit)
                {
                    _gridTerraform.ReadOnly = false;
                    _gridTerraform.DataSource = _dtTerEdit;
                    HideRawColumns(_gridTerraform);
                }
                else
                {
                    _gridTerraform.ReadOnly = true;
                    BindOneRowAsFriendlyProperties(_gridTerraform, _dtTerEdit, TerraformOrder);
                }



                // 4) Attributes (join to dictionary for display)
                var dtAttrsRaw = QueryTable(
                    @"SELECT
                        oa.attr_key AS attr_key,
                        COALESCE(ad.display_name, oa.attr_key) AS name,
                        ad.units AS units,
                        ad.value_kind AS value_kind,
                        oa.value_text AS value_text,
                        oa.value_num AS value_num,
                        oa.value_int AS value_int,
                        oa.value_bool AS value_bool,
                        oa.notes AS notes
                      FROM object_attributes oa
                      LEFT JOIN attribute_dictionary ad ON ad.attr_key = oa.attr_key
                      WHERE oa.object_id = @id
                      ORDER BY name COLLATE NOCASE;",
                    _obj.ObjectId);

                BindAttributesCollapsed(dtAttrsRaw);
            }
            catch (Exception ex)
            {
                BindPropsEmpty(_gridDetails, "Load failed: " + ex.Message);
                BindPropsEmpty(_gridEnv, "Load failed: " + ex.Message);
                BindPropsEmpty(_gridTerraform, "Load failed: " + ex.Message);
                BindAttrsEmpty("Load failed: " + ex.Message);
            }
        }

        private DataTable QueryTable(string sql, string objectId)
        {
            var dt = new DataTable();

            using (var conn = new SQLiteConnection("Data Source=" + _dbPath + ";Version=3;"))
            using (var cmd = new SQLiteCommand(sql, conn))
            using (var da = new SQLiteDataAdapter(cmd))
            {
                cmd.Parameters.AddWithValue("@id", objectId ?? "");
                conn.Open();
                da.Fill(dt);
            }

            return dt;
        }

        // ---------- Friendly binders ----------

        private static void BindOneRowAsFriendlyProperties(DataGridView grid, DataTable dt, string[] preferredOrder)
        {
            if (grid == null) return;

            if (dt == null || dt.Rows.Count == 0)
            {
                BindPropsEmpty(grid, "No data.");
                return;
            }

            if (dt.Rows.Count > 1)
            {
                // Rare for our current detail tables; show raw
                grid.DataSource = dt;
                return;
            }

            var props = new DataTable();
            props.Columns.Add("Field", typeof(string));
            props.Columns.Add("Value", typeof(string));

            DataRow r = dt.Rows[0];

            // Collect visible columns (except notes; handled specially)
            var cols = new List<DataColumn>();
            string notesValue = null;

            for (int i = 0; i < dt.Columns.Count; i++)
            {
                var c = dt.Columns[i];
                if (c == null) continue;

                string col = c.ColumnName ?? "";
                if (ShouldHideColumn(col)) continue;

                object v = r[i];
                string s = FriendlyValue(col, v);
                if (s == null) continue;

                if (EqualsIgnore(col, "notes"))
                {
                    notesValue = s;
                    continue;
                }

                cols.Add(c);
            }

            // Sort by preferred order, then alpha
            cols.Sort(delegate(DataColumn a, DataColumn b)
            {
                int ra = GetOrderRank(preferredOrder, a.ColumnName);
                int rb = GetOrderRank(preferredOrder, b.ColumnName);
                if (ra != rb) return ra.CompareTo(rb);
                return string.Compare(a.ColumnName, b.ColumnName, StringComparison.OrdinalIgnoreCase);
            });

            // Add rows in order
            for (int i = 0; i < cols.Count; i++)
            {
                string col = cols[i].ColumnName ?? "";
                string val = FriendlyValue(col, r[col]);
                if (val == null) continue;

                props.Rows.Add(FriendlyFieldLabel(col), val);
            }

            // Notes rule: short = near top, long = bottom
            if (!string.IsNullOrWhiteSpace(notesValue))
            {
                var notesRow = props.NewRow();
                notesRow[0] = "Notes";
                notesRow[1] = notesValue.Trim();

                if (notesValue.Length <= NotesShortLimit)
                {
                    // Put right after Class if present, otherwise at top
                    int insertAt = 0;
                    for (int i = 0; i < props.Rows.Count; i++)
                    {
                        var f = Convert.ToString(props.Rows[i]["Field"]);
                        if (string.Equals(f, "Class", StringComparison.OrdinalIgnoreCase))
                        {
                            insertAt = i + 1;
                            break;
                        }
                    }
                    props.Rows.InsertAt(notesRow, insertAt);
                }
                else
                {
                    props.Rows.Add(notesRow);
                }
            }

            grid.DataSource = props;

            if (grid.Columns.Count >= 2)
            {
                grid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
        }


        private void BindAttributesCollapsed(DataTable dtAttrsRaw)
        {
            if (dtAttrsRaw == null || dtAttrsRaw.Rows.Count == 0)
            {
                BindAttrsEmpty("No attributes for this object.");
                return;
            }

            var dt = new DataTable();
            dt.Columns.Add("Attribute", typeof(string));
            dt.Columns.Add("Value", typeof(string));

            for (int i = 0; i < dtAttrsRaw.Rows.Count; i++)
            {
                DataRow r = dtAttrsRaw.Rows[i];

                string name = Convert.ToString(r["name"]);
                string units = Convert.ToString(r["units"]);
                string notes = Convert.ToString(r["notes"]);

                string value = CollapseAttrValue(
                    r["value_text"],
                    r["value_num"],
                    r["value_int"],
                    r["value_bool"],
                    Convert.ToString(r["value_kind"])
                );

                if (string.IsNullOrWhiteSpace(name))
                    name = "(unnamed)";

                // If no usable value, skip (reduces noise)
                if (string.IsNullOrWhiteSpace(value) || value == "-")
                    continue;

                string collapsed = CollapseValueUnitsNotes(value, units, notes);
                dt.Rows.Add(name.Trim(), collapsed);
            }

            if (dt.Rows.Count == 0)
                dt.Rows.Add("Info", "No displayable attributes.");

            _gridAttrs.DataSource = dt;

            if (_gridAttrs.Columns.Count >= 2)
            {
                _gridAttrs.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                _gridAttrs.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
        }

        private static string CollapseValueUnitsNotes(string value, string units, string notes)
        {
            var sb = new StringBuilder();
            sb.Append(value.Trim());

            if (!string.IsNullOrWhiteSpace(units))
                sb.Append(" ").Append(units.Trim());

            if (!string.IsNullOrWhiteSpace(notes))
                sb.Append(" — ").Append(notes.Trim());

            return sb.ToString();
        }

        private static string CollapseAttrValue(object vText, object vNum, object vInt, object vBool, string valueKind)
        {
            if (vText != null && vText != DBNull.Value)
            {
                string s = Convert.ToString(vText);
                if (!string.IsNullOrWhiteSpace(s))
                    return s.Trim();
            }

            if (vNum != null && vNum != DBNull.Value)
            {
                double d;
                if (double.TryParse(Convert.ToString(vNum, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                    return d.ToString("0.###", CultureInfo.InvariantCulture);

                return Convert.ToString(vNum, CultureInfo.InvariantCulture);
            }

            if (vInt != null && vInt != DBNull.Value)
                return Convert.ToString(vInt, CultureInfo.InvariantCulture);

            if (vBool != null && vBool != DBNull.Value)
            {
                int b;
                if (int.TryParse(Convert.ToString(vBool, CultureInfo.InvariantCulture), out b))
                    return (b != 0) ? "Yes" : "No";

                return Convert.ToString(vBool, CultureInfo.InvariantCulture);
            }

            // fallback hint: value_kind exists but no value set
            if (!string.IsNullOrWhiteSpace(valueKind))
                return "-";

            return "-";
        }


        private static void BindPropsEmpty(DataGridView grid, string msg)
        {
            if (grid == null) return;

            var dt = new DataTable();
            dt.Columns.Add("Field", typeof(string));
            dt.Columns.Add("Value", typeof(string));
            dt.Rows.Add("Info", msg ?? "");

            grid.DataSource = dt;

            if (grid.Columns.Count >= 2)
            {
                grid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
        }

        private void BindAttrsEmpty(string msg)
        {
            if (_gridAttrs == null) return;

            var dt = new DataTable();
            dt.Columns.Add("Attribute", typeof(string));
            dt.Columns.Add("Value", typeof(string));
            dt.Rows.Add("Info", msg ?? "");

            _gridAttrs.DataSource = dt;
        }

        private static DataTable MakeSingleRow(string colName, string value)
        {
            var dt = new DataTable();
            dt.Columns.Add(colName ?? "info", typeof(string));
            dt.Rows.Add(value ?? "");
            return dt;
        }

        // ---------- Mode ----------

        private void ApplyMode()
        {
            if (_mode == ObjectEditorMode.View)
            {
                if (_btnPrimary != null)
                {
                    _btnPrimary.Text = "Close";
                    _btnPrimary.Enabled = true;
                    _btnPrimary.DialogResult = DialogResult.OK;
                }

                if (_btnCancel != null)
                    _btnCancel.Visible = false;

                AcceptButton = _btnPrimary;
                CancelButton = _btnPrimary;
            }
            else
            {
                if (_btnPrimary != null)
                {
                    _btnPrimary.Text = "Save";
                    _btnPrimary.Enabled = true; // enabled, but stubbed
                    _btnPrimary.DialogResult = DialogResult.None;
                }

                if (_btnCancel != null)
                {
                    _btnCancel.Text = "Cancel";
                    _btnCancel.Visible = true;
                    _btnCancel.DialogResult = DialogResult.Cancel;
                }

                AcceptButton = _btnPrimary;
                CancelButton = _btnCancel;
            }
        }

        // ---------- Text helpers ----------

        private static string FirstLine(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "-";

            s = s.Trim();
            int ix = s.IndexOf('\n');
            if (ix >= 0)
                s = s.Substring(0, ix);

            if (s.Length > 120)
                s = s.Substring(0, 120) + "…";

            return s;
        }

        private static string FriendlyKind(string kind)
        {
            kind = (kind ?? "").Trim().ToLowerInvariant();
            switch (kind)
            {
                case "planet": return "Planet";
                case "moon": return "Moon";
                case "dwarf_planet": return "Dwarf planet";
                case "belt":
                case "asteroid_belt": return "Belt";
                case "kuiper_belt": return "Kuiper belt";
                case "oort_cloud":
                case "comet_cloud": return "Oort cloud";
                case "ring_system": return "Ring system";
                case "installation": return "Installation";
                case "station": return "Station";
                case "gate_facility": return "Gate facility";
                case "star": return "Star";
                case "system_root": return "System root";
                default:
                    if (kind.Length == 0) return "-";
                    return char.ToUpperInvariant(kind[0]) + kind.Substring(1);
            }
        }

        private static string HumanizeField(string snake)
        {
            if (string.IsNullOrWhiteSpace(snake))
                return "-";

            // small unit polish:
            // radius_km -> Radius (km)
            // semi_major_axis_au -> Semi major axis (AU)
            string s = snake.Trim();
            string unit = null;

            if (EndsWithIgnore(s, "_km"))
            {
                unit = "km";
                s = s.Substring(0, s.Length - 3);
            }
            else if (EndsWithIgnore(s, "_au"))
            {
                unit = "AU";
                s = s.Substring(0, s.Length - 3);
            }
            else if (EndsWithIgnore(s, "_atm"))
            {
                unit = "atm";
                s = s.Substring(0, s.Length - 4);
            }
            else if (EndsWithIgnore(s, "_c"))
            {
                unit = "°C";
                s = s.Substring(0, s.Length - 2);
            }
            else if (EndsWithIgnore(s, "_pct"))
            {
                unit = "%";
                s = s.Substring(0, s.Length - 4);
            }
            else if (EndsWithIgnore(s, "_deg"))
            {
                unit = "°";
                s = s.Substring(0, s.Length - 4);
            }

            s = s.Replace("_", " ");
            s = TitleCaseWords(s);

            if (!string.IsNullOrEmpty(unit))
                s += " (" + unit + ")";

            // a couple of nicer labels
            if (EqualsIgnore(snake, "env_stage")) return "Environment stage";
            if (EqualsIgnore(snake, "pressure_atm")) return "Pressure (atm)";
            if (EqualsIgnore(snake, "avg_temp_c")) return "Average temperature (°C)";

            return s;
        }

        private static string TitleCaseWords(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return s;

            var ti = CultureInfo.InvariantCulture.TextInfo;
            return ti.ToTitleCase(s.ToLowerInvariant());
        }

        private static bool EndsWithIgnore(string s, string suffix)
        {
            return s != null && suffix != null && s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EqualsIgnore(string a, string b)
        {
            return string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
        }

        // ---------------- Orbit phrase helpers (existing logic you had) ----------------

        private static string DeriveOrbitPhrase_NoContext(SystemObjectInfo obj)
        {
            if (obj == null) return "-";

            string kind = (obj.ObjectKind ?? "").Trim().ToLowerInvariant();
            if (kind == "planet") return "Planetary orbit (position derived from order)";
            if (kind == "moon") return "Satellite orbit (position derived from order)";
            if (kind == "dwarf_planet") return "Dwarf-planet orbit (position derived from order)";
            if (kind == "belt" || kind == "asteroid_belt") return "Belt region (position derived from order)";
            if (kind == "kuiper_belt") return "Outer belt region";
            if (kind == "oort_cloud" || kind == "comet_cloud") return "Outer cloud region";
            if (kind == "installation" || kind == "station") return "Artificial orbit (position derived from order)";
            if (kind == "ring_system") return "Ring region";

            return "Orbit position derived from order";
        }

        private static string DeriveOrbitPhrase(List<SystemObjectInfo> all, SystemObjectInfo obj)
        {
            if (obj == null) return "-";
            if (all == null || all.Count == 0) return DeriveOrbitPhrase_NoContext(obj);

            string kind = (obj.ObjectKind ?? "").Trim().ToLowerInvariant();
            string hostId = FirstNonEmpty(obj.OrbitHostObjectId, obj.ParentObjectId);
            SystemObjectInfo host = FindById(all, hostId);

            var orbiters = new List<SystemObjectInfo>();

            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o == null) continue;

                string oHost = FirstNonEmpty(o.OrbitHostObjectId, o.ParentObjectId);
                if (!string.Equals(oHost ?? "", hostId ?? "", StringComparison.Ordinal)) continue;

                if (IsOrbitingKind(o.ObjectKind))
                    orbiters.Add(o);
            }

            orbiters.Sort(delegate(SystemObjectInfo a, SystemObjectInfo b)
            {
                int ra = a != null ? a.RadialOrder : 0;
                int rb = b != null ? b.RadialOrder : 0;
                if (ra != rb) return ra.CompareTo(rb);

                string na = (a != null ? a.DisplayName : "") ?? "";
                string nb = (b != null ? b.DisplayName : "") ?? "";
                return string.Compare(na, nb, StringComparison.OrdinalIgnoreCase);
            });

            if (kind == "planet")
            {
                int planetIndex = 0;

                for (int i = 0; i < orbiters.Count; i++)
                {
                    var o = orbiters[i];
                    if (o == null) continue;
                    if (!string.Equals((o.ObjectKind ?? "").Trim(), "planet", StringComparison.OrdinalIgnoreCase)) continue;

                    planetIndex++;

                    if (string.Equals(o.ObjectId, obj.ObjectId, StringComparison.Ordinal))
                    {
                        string hostName = HostNameForPhrase(host);
                        return Ordinal(planetIndex) + " planet from " + hostName;
                    }
                }

                return "Planetary orbit";
            }

            if (kind == "moon")
            {
                int moonIndex = 0;

                for (int i = 0; i < orbiters.Count; i++)
                {
                    var o = orbiters[i];
                    if (o == null) continue;
                    if (!string.Equals((o.ObjectKind ?? "").Trim(), "moon", StringComparison.OrdinalIgnoreCase)) continue;

                    moonIndex++;

                    if (string.Equals(o.ObjectId, obj.ObjectId, StringComparison.Ordinal))
                    {
                        string hostName = host != null && !string.IsNullOrWhiteSpace(host.DisplayName) ? host.DisplayName.Trim() : "its primary";
                        return Ordinal(moonIndex) + " moon of " + hostName;
                    }
                }

                return "Satellite orbit";
            }

            int idx = IndexOfById(orbiters, obj.ObjectId);
            if (idx >= 0)
            {
                var inner = FindNearestInnerNamed(orbiters, idx);
                var outer = FindNearestOuterNamed(orbiters, idx);

                if (inner != null && outer != null) return "Between " + inner.DisplayName.Trim() + " and " + outer.DisplayName.Trim();
                if (inner != null) return "Outside " + inner.DisplayName.Trim();
                if (outer != null) return "Inside " + outer.DisplayName.Trim();
            }

            return "Orbit position derived from order";
        }

        private static bool IsOrbitingKind(string kind)
        {
            kind = (kind ?? "").Trim().ToLowerInvariant();
            return kind == "planet" || kind == "moon" || kind == "dwarf_planet" ||
                   kind == "belt" || kind == "asteroid_belt" || kind == "kuiper_belt" ||
                   kind == "oort_cloud" || kind == "comet_cloud" || kind == "ring_system" ||
                   kind == "installation" || kind == "station";
        }

        private static string HostNameForPhrase(SystemObjectInfo host)
        {
            if (host != null && !string.IsNullOrWhiteSpace(host.DisplayName))
            {
                string n = host.DisplayName.Trim();
                if (string.Equals(n, "Sol", StringComparison.OrdinalIgnoreCase) || string.Equals(n, "Sun", StringComparison.OrdinalIgnoreCase))
                    return "the Sun";
                return n;
            }
            return "primary";
        }

        private static string Ordinal(int n)
        {
            if (n <= 0) return n.ToString();
            int mod100 = n % 100;
            if (mod100 >= 11 && mod100 <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }

        private static int IndexOfById(List<SystemObjectInfo> list, string objectId)
        {
            if (list == null || string.IsNullOrEmpty(objectId)) return -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && string.Equals(list[i].ObjectId, objectId, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        private static SystemObjectInfo FindNearestInnerNamed(List<SystemObjectInfo> list, int fromIdx)
        {
            for (int i = fromIdx - 1; i >= 0; i--)
            {
                var o = list[i];
                if (o != null && !string.IsNullOrWhiteSpace(o.DisplayName))
                    return o;
            }
            return null;
        }

        private static SystemObjectInfo FindNearestOuterNamed(List<SystemObjectInfo> list, int fromIdx)
        {
            for (int i = fromIdx + 1; i < list.Count; i++)
            {
                var o = list[i];
                if (o != null && !string.IsNullOrWhiteSpace(o.DisplayName))
                    return o;
            }
            return null;
        }

        private static SystemObjectInfo FindById(List<SystemObjectInfo> all, string id)
        {
            if (all == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < all.Count; i++)
            {
                var o = all[i];
                if (o != null && string.Equals(o.ObjectId, id, StringComparison.Ordinal))
                    return o;
            }
            return null;
        }

        private static string FirstNonEmpty(string a, string b)
        {
            if (!string.IsNullOrWhiteSpace(a)) return a.Trim();
            if (!string.IsNullOrWhiteSpace(b)) return b.Trim();
            return null;
        }

        // ------------------------------------------------------------
        // Field ordering (DM-first) + Notes placement rule
        // ------------------------------------------------------------

        private const int NotesShortLimit = 120;

        private static readonly string[] DetailsOrder = new[]
{
    // What is it?
    "planet_class", "moon_class",
    "population",
    "tech_level",

    // Lived experience
    "day_length_hours",
    "tidally_locked",
    "axial_tilt_deg",

    // Calendar / orbit
    "orbital_period_days",
    "semi_major_axis_au",
    "semi_major_axis_km",
    "eccentricity",

    // Hard stats (least narrative)
    "gravity_g",
    "radius_km",
    "mass_earth",
    "density_g_cm3",
    "albedo"

    // notes handled specially
};

        private static readonly string[] EnvironmentOrder = new[]
{
    // Can we live here?
    "habitability",
    "atmosphere_type",
    "pressure_atm",
    "avg_temp_c",
    "hydrosphere_pct",
    "biosphere",

    // Threats / protection
    "radiation_level",
    "magnetosphere",

    // Meta
    "env_stage"

    // notes handled specially
};

        private static readonly string[] TerraformOrder = new[]
{
    // Feasibility first
    "terraform_tier",
    "limiting_factors",
    "requires_imports",

    // Constraints
    "water_availability",
    "volatile_budget",
    "atmosphere_retention",
    "radiation_constraint",
    "maintenance_burden"

    // notes handled specially
};

        private static int GetOrderRank(string[] order, string col)
        {
            if (order == null || string.IsNullOrWhiteSpace(col)) return 9999;

            for (int i = 0; i < order.Length; i++)
                if (EqualsIgnore(col, order[i])) return i;

            return 9999;
        }

        private static string FriendlyFieldLabel(string col)
        {
            if (EqualsIgnore(col, "planet_class") || EqualsIgnore(col, "moon_class")) return "Class";
            if (EqualsIgnore(col, "tidally_locked")) return "Tidally locked";
            if (EqualsIgnore(col, "semi_major_axis_au")) return "Semi-major axis (AU)";
            if (EqualsIgnore(col, "semi_major_axis_km")) return "Semi-major axis (km)";
            if (EqualsIgnore(col, "axial_tilt_deg")) return "Axial tilt (°)";
            if (EqualsIgnore(col, "day_length_hours")) return "Day length (hours)";
            if (EqualsIgnore(col, "orbital_period_days")) return "Orbital period (days)";
            if (EqualsIgnore(col, "avg_temp_c")) return "Avg temp (°C)";
            if (EqualsIgnore(col, "pressure_atm")) return "Pressure (atm)";
            if (EqualsIgnore(col, "hydrosphere_pct")) return "Hydrosphere (%)";
            return HumanizeField(col);
        }

        private static string FriendlyValue(string col, object v)
        {
            if (v == null || v == DBNull.Value) return null;

            string s = Convert.ToString(v, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();

            // Booleans stored as 0/1
            if (EqualsIgnore(col, "tidally_locked"))
            {
                int b;
                if (int.TryParse(s, out b))
                    return (b != 0) ? "Yes" : "No";
            }

            return s;
        }

        private static bool ShouldHideColumn(string col)
        {
            if (string.IsNullOrWhiteSpace(col))
                return true;

            // Hard hides
            if (EqualsIgnore(col, "object_id"))
                return true;

            // Timestamps / noise
            if (EqualsIgnore(col, "created_utc") || EqualsIgnore(col, "updated_utc"))
                return true;
            if (EndsWithIgnore(col, "_utc"))
                return true;

            // Sorting-only / internal
            if (EqualsIgnore(col, "radial_order"))
                return true;

            // Common internal IDs
            if (EqualsIgnore(col, "created_by") || EqualsIgnore(col, "updated_by"))
                return true;

            return false;
        }
        private void EnsureRowExists(string tableName, string objectId)
        {
            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(objectId))
                return;

            using (var conn = new SQLiteConnection("Data Source=" + _dbPath + ";Version=3;"))
            using (var cmd = new SQLiteCommand(conn))
            {
                conn.Open();

                cmd.CommandText = "SELECT COUNT(*) FROM " + tableName + " WHERE object_id = @id;";
                cmd.Parameters.AddWithValue("@id", objectId);
                long count = (long)cmd.ExecuteScalar();

                if (count > 0) return;

                cmd.Parameters.Clear();
                cmd.CommandText = "INSERT INTO " + tableName + " (object_id) VALUES (@id);";
                cmd.Parameters.AddWithValue("@id", objectId);
                cmd.ExecuteNonQuery();
            }
        }

        private static void HideRawColumns(DataGridView grid)
        {
            if (grid == null) return;

            for (int i = 0; i < grid.Columns.Count; i++)
            {
                var c = grid.Columns[i];
                if (c == null) continue;

                string name = c.Name ?? c.DataPropertyName ?? "";
                if (ShouldHideColumn(name))
                    c.Visible = false;
            }
        }

        private static object DbOrNull(object v)
        {
            if (v == null || v == DBNull.Value) return DBNull.Value;

            var s = v as string;
            if (s != null && string.IsNullOrWhiteSpace(s)) return DBNull.Value;

            return v;
        }

        private void SaveEnvironment(SQLiteConnection conn, SQLiteTransaction tx)
        {
            if (_dtEnvEdit == null || _dtEnvEdit.Rows.Count == 0) return;

            DataRow r = _dtEnvEdit.Rows[0];

            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    @"UPDATE body_environment SET
                env_stage = @env_stage,
                atmosphere_type = @atmosphere_type,
                pressure_atm = @pressure_atm,
                avg_temp_c = @avg_temp_c,
                hydrosphere_pct = @hydrosphere_pct,
                biosphere = @biosphere,
                radiation_level = @radiation_level,
                magnetosphere = @magnetosphere,
                habitability = @habitability,
                notes = @notes
              WHERE object_id = @object_id;";

                cmd.Parameters.AddWithValue("@object_id", _obj.ObjectId);

                cmd.Parameters.AddWithValue("@env_stage", DbOrNull(r["env_stage"]));
                cmd.Parameters.AddWithValue("@atmosphere_type", DbOrNull(r["atmosphere_type"]));
                cmd.Parameters.AddWithValue("@pressure_atm", DbOrNull(r["pressure_atm"]));
                cmd.Parameters.AddWithValue("@avg_temp_c", DbOrNull(r["avg_temp_c"]));
                cmd.Parameters.AddWithValue("@hydrosphere_pct", DbOrNull(r["hydrosphere_pct"]));
                cmd.Parameters.AddWithValue("@biosphere", DbOrNull(r["biosphere"]));
                cmd.Parameters.AddWithValue("@radiation_level", DbOrNull(r["radiation_level"]));
                cmd.Parameters.AddWithValue("@magnetosphere", DbOrNull(r["magnetosphere"]));
                cmd.Parameters.AddWithValue("@habitability", DbOrNull(r["habitability"]));
                cmd.Parameters.AddWithValue("@notes", DbOrNull(r["notes"]));

                cmd.ExecuteNonQuery();
            }
        }

        private void SaveTerraform(SQLiteConnection conn, SQLiteTransaction tx)
        {
            if (_dtTerEdit == null || _dtTerEdit.Rows.Count == 0) return;

            DataRow r = _dtTerEdit.Rows[0];

            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.Transaction = tx;
                cmd.CommandText =
                    @"UPDATE terraform_constraints SET
                terraform_tier = @terraform_tier,
                atmosphere_retention = @atmosphere_retention,
                radiation_constraint = @radiation_constraint,
                volatile_budget = @volatile_budget,
                water_availability = @water_availability,
                requires_imports = @requires_imports,
                limiting_factors = @limiting_factors,
                maintenance_burden = @maintenance_burden,
                notes = @notes
              WHERE object_id = @object_id;";

                cmd.Parameters.AddWithValue("@object_id", _obj.ObjectId);

                cmd.Parameters.AddWithValue("@terraform_tier", DbOrNull(r["terraform_tier"]));
                cmd.Parameters.AddWithValue("@atmosphere_retention", DbOrNull(r["atmosphere_retention"]));
                cmd.Parameters.AddWithValue("@radiation_constraint", DbOrNull(r["radiation_constraint"]));
                cmd.Parameters.AddWithValue("@volatile_budget", DbOrNull(r["volatile_budget"]));
                cmd.Parameters.AddWithValue("@water_availability", DbOrNull(r["water_availability"]));
                cmd.Parameters.AddWithValue("@requires_imports", DbOrNull(r["requires_imports"]));
                cmd.Parameters.AddWithValue("@limiting_factors", DbOrNull(r["limiting_factors"]));
                cmd.Parameters.AddWithValue("@maintenance_burden", DbOrNull(r["maintenance_burden"]));
                cmd.Parameters.AddWithValue("@notes", DbOrNull(r["notes"]));

                cmd.ExecuteNonQuery();
            }
        }


    }
}
