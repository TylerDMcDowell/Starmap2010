// ============================================================
// File: UI/ObjectEditorForm.cs
// Project: StarMap2010
//
// Large modal viewer/editor shell.
// View mode shows read-only summary + loaded DB tables.
// Edit mode still stubbed (Save disabled) until persistence is wired.
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

            // ---- Attributes tab ----
            var tabAttrs = new TabPage("Attributes");
            _gridAttrs = MakeGrid();
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

            _btnCancel = new Button { Text = "Cancel", Width = 100, Height = 30, DialogResult = DialogResult.Cancel };
            _btnPrimary = new Button { Text = "Save", Width = 100, Height = 30, Enabled = false };

            _btnPrimary.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };

            buttons.Controls.Add(_btnCancel);
            buttons.Controls.Add(_btnPrimary);

            CancelButton = _btnCancel;
            root.Controls.Add(buttons, 0, 2);

            RenderHeaderAndSummary();
        }

        private static DataGridView MakeGrid()
        {
            var g = new DataGridView
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
            return g;
        }

        private static DataGridView MakeGridProps()
        {
            var g = MakeGrid();
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.RowHeadersVisible = false;
            return g;
        }

        private void RenderHeaderAndSummary()
        {
            string name = (_obj != null && !string.IsNullOrWhiteSpace(_obj.DisplayName)) ? _obj.DisplayName.Trim() : "(unnamed)";
            string kind = (_obj != null && !string.IsNullOrWhiteSpace(_obj.ObjectKind)) ? _obj.ObjectKind.Trim() : "-";

            _hdr.Text = name + "  [" + kind + "]";

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
                "Name:          " + name + "\r\n" +
                "Kind:          " + FriendlyKind(kind) + "\r\n" +
                "Orbit:         " + orbitPhrase + "\r\n" +
                "Notes:         " + FirstLine(_obj.Notes) + "\r\n" +
                "\r\n" +
                "Tip: Double-click or right-click → View… from the tree.\r\n" +
                "Tabs: Details / Environment / Terraform / Attributes are DB-backed.";
        }

        private void LoadAndBindTables()
        {
            if (_obj == null) return;

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

                // 1) Details (planet_details OR moon_details OR minimal)
                DataTable dtDetails;
                if (kind == "planet" || kind == "dwarf_planet")
                    dtDetails = QueryTable("SELECT * FROM planet_details WHERE object_id = @id;", _obj.ObjectId);
                else if (kind == "moon")
                    dtDetails = QueryTable("SELECT * FROM moon_details WHERE object_id = @id;", _obj.ObjectId);
                else
                    dtDetails = MakeSingleRow("info", "No kind-specific details table for: " + kind);

                BindOneRowAsFriendlyProperties(_gridDetails, dtDetails);

                // 2) Environment
                var dtEnv = QueryTable("SELECT * FROM body_environment WHERE object_id = @id;", _obj.ObjectId);
                BindOneRowAsFriendlyProperties(_gridEnv, dtEnv);

                // 3) Terraform constraints
                var dtTer = QueryTable("SELECT * FROM terraform_constraints WHERE object_id = @id;", _obj.ObjectId);
                BindOneRowAsFriendlyProperties(_gridTerraform, dtTer);

                // 4) Attributes (join to dictionary, then collapse to Name/Value/Units/Notes)
                var dtAttrsRaw = QueryTable(@"
SELECT
    oa.attr_key                               AS attr_key,
    COALESCE(ad.display_name, oa.attr_key)    AS name,
    ad.units                                  AS units,
    ad.value_kind                             AS value_kind,
    oa.value_text                             AS value_text,
    oa.value_num                              AS value_num,
    oa.value_int                              AS value_int,
    oa.value_bool                             AS value_bool,
    oa.notes                                  AS notes
FROM object_attributes oa
LEFT JOIN attribute_dictionary ad
    ON ad.attr_key = oa.attr_key
WHERE oa.object_id = @id
ORDER BY name COLLATE NOCASE;
", _obj.ObjectId);

                BindAttributesFriendly(dtAttrsRaw);
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

        private static void BindOneRowAsFriendlyProperties(DataGridView grid, DataTable dt)
        {
            if (grid == null) return;

            if (dt == null || dt.Rows.Count == 0)
            {
                BindPropsEmpty(grid, "No data.");
                return;
            }

            // If more than 1 row, show raw table (rare for our current use)
            if (dt.Rows.Count > 1)
            {
                grid.DataSource = dt;
                return;
            }

            var props = new DataTable();
            props.Columns.Add("Field", typeof(string));
            props.Columns.Add("Value", typeof(string));

            DataRow r = dt.Rows[0];

            for (int i = 0; i < dt.Columns.Count; i++)
            {
                var c = dt.Columns[i];
                if (c == null) continue;

                string col = c.ColumnName ?? "";

                // Hide noise / technical columns
                if (EqualsIgnore(col, "object_id")) continue;
                if (EndsWithIgnore(col, "_utc")) continue;

                object v = r[i];
                string s = (v == null || v == DBNull.Value) ? "-" : Convert.ToString(v, CultureInfo.InvariantCulture);

                props.Rows.Add(HumanizeField(col), s);
            }

            grid.DataSource = props;

            // Make it look like a clean property list
            if (grid.Columns.Count >= 2)
            {
                grid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }
        }

        private void BindAttributesFriendly(DataTable dtAttrsRaw)
        {
            if (dtAttrsRaw == null || dtAttrsRaw.Rows.Count == 0)
            {
                BindAttrsEmpty("No attributes for this object.");
                return;
            }

            var dt = new DataTable();
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Value", typeof(string));
            dt.Columns.Add("Units", typeof(string));
            dt.Columns.Add("Notes", typeof(string));

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

                dt.Rows.Add(
                    string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name,
                    string.IsNullOrWhiteSpace(value) ? "-" : value,
                    string.IsNullOrWhiteSpace(units) ? "" : units,
                    string.IsNullOrWhiteSpace(notes) ? "" : notes
                );
            }

            _gridAttrs.DataSource = dt;
        }

        private static string CollapseAttrValue(object vText, object vNum, object vInt, object vBool, string valueKind)
        {
            if (vText != null && vText != DBNull.Value)
            {
                string s = Convert.ToString(vText);
                if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
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
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Value", typeof(string));
            dt.Columns.Add("Units", typeof(string));
            dt.Columns.Add("Notes", typeof(string));
            dt.Rows.Add("Info", msg ?? "", "", "");
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
                    _btnPrimary.Enabled = false; // enable once we add fields + dirty tracking
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
            if (string.IsNullOrWhiteSpace(s)) return "-";
            s = s.Trim();
            int ix = s.IndexOf('\n');
            if (ix >= 0) s = s.Substring(0, ix);
            if (s.Length > 120) s = s.Substring(0, 120) + "…";
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
            if (string.IsNullOrWhiteSpace(snake)) return "-";

            // small unit polish
            // radius_km -> Radius (km)
            // semi_major_axis_au -> Semi major axis (AU)
            string s = snake.Trim();

            string unit = null;
            if (EndsWithIgnore(s, "_km")) { unit = "km"; s = s.Substring(0, s.Length - 3); }
            else if (EndsWithIgnore(s, "_au")) { unit = "AU"; s = s.Substring(0, s.Length - 3); }
            else if (EndsWithIgnore(s, "_atm")) { unit = "atm"; s = s.Substring(0, s.Length - 4); }
            else if (EndsWithIgnore(s, "_c")) { unit = "°C"; s = s.Substring(0, s.Length - 2); }
            else if (EndsWithIgnore(s, "_pct")) { unit = "%"; s = s.Substring(0, s.Length - 4); }
            else if (EndsWithIgnore(s, "_deg")) { unit = "°"; s = s.Substring(0, s.Length - 4); }

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
            if (string.IsNullOrWhiteSpace(s)) return s;
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
            if (all == null || all.Count == 0)
                return DeriveOrbitPhrase_NoContext(obj);

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
                    if (!string.Equals((o.ObjectKind ?? "").Trim(), "planet", StringComparison.OrdinalIgnoreCase))
                        continue;

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
                    if (!string.Equals((o.ObjectKind ?? "").Trim(), "moon", StringComparison.OrdinalIgnoreCase))
                        continue;

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

                if (inner != null && outer != null)
                    return "Between " + inner.DisplayName.Trim() + " and " + outer.DisplayName.Trim();

                if (inner != null)
                    return "Outside " + inner.DisplayName.Trim();

                if (outer != null)
                    return "Inside " + outer.DisplayName.Trim();
            }

            return "Orbit position derived from order";
        }

        private static bool IsOrbitingKind(string kind)
        {
            kind = (kind ?? "").Trim().ToLowerInvariant();
            return kind == "planet" || kind == "moon" || kind == "dwarf_planet" ||
                   kind == "belt" || kind == "asteroid_belt" || kind == "kuiper_belt" ||
                   kind == "oort_cloud" || kind == "comet_cloud" ||
                   kind == "ring_system" ||
                   kind == "installation" || kind == "station";
        }

        private static string HostNameForPhrase(SystemObjectInfo host)
        {
            if (host != null && !string.IsNullOrWhiteSpace(host.DisplayName))
            {
                string n = host.DisplayName.Trim();
                if (string.Equals(n, "Sol", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(n, "Sun", StringComparison.OrdinalIgnoreCase))
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
    }
}
