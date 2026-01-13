// ============================================================
// File: Ui/ObjectEditorForm.cs
// Project: StarMap2010
//
// Large modal viewer/editor shell.
// View mode shows read-only summary + loaded DB tables.
// Edit mode still stubbed (Save disabled) until persistence is wired.
//
// IMPORTANT UX RULES (locked):
// - No raw IDs shown to users
// - Never show radial_order
// - Orbit phrasing is derived (uses provided _all list context)
// - Prefer clear, friendly labels over raw column names
// ============================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
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

        private DataGridView _gridBasics;      // kind-specific: planet_details / moon_details
        private DataGridView _gridEnv;         // body_environment
        private DataGridView _gridTerraform;   // terraform_constraints
        private DataGridView _gridAttrs;       // object_attributes (dictionary-joined)

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

            // Split: top = readable narrative summary, bottom = kind-specific basics table
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                Panel1MinSize = 180,
                Panel2MinSize = 180
            };
            tabSummary.Controls.Add(split);

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
            split.Panel1.Controls.Add(_txtSummary);

            _gridBasics = MakePropertyGrid();
            split.Panel2.Controls.Add(_gridBasics);

            _tabs.TabPages.Add(tabSummary);

            // ---- Environment tab ----
            var tabEnv = new TabPage("Environment");
            _gridEnv = MakePropertyGrid();
            tabEnv.Controls.Add(_gridEnv);
            _tabs.TabPages.Add(tabEnv);

            // ---- Terraform tab ----
            var tabTerraform = new TabPage("Terraforming");
            _gridTerraform = MakePropertyGrid();
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

            _btnCancel = new Button { Text = "Cancel", Width = 110, Height = 32, DialogResult = DialogResult.Cancel };
            _btnPrimary = new Button { Text = "Save", Width = 110, Height = 32, Enabled = false };

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
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false
            };
            return g;
        }

        // Property grid = two columns, nicer for 1-row tables
        private static DataGridView MakePropertyGrid()
        {
            var g = MakeGrid();
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            g.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            return g;
        }

        private void RenderHeaderAndSummary()
        {
            string name = (_obj != null && !string.IsNullOrWhiteSpace(_obj.DisplayName)) ? _obj.DisplayName.Trim() : "(unnamed)";
            string kindRaw = (_obj != null && !string.IsNullOrWhiteSpace(_obj.ObjectKind)) ? _obj.ObjectKind.Trim() : "-";

            _hdr.Text = name + "  [" + FriendlyKind(kindRaw) + "]";

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
                "Kind:          " + FriendlyKind(kindRaw) + "\r\n" +
                "Orbit:         " + orbitPhrase + "\r\n" +
                "Notes:         " + FirstLine(_obj.Notes) + "\r\n" +
                "\r\n" +
                "Tip: This window is read-only for now.\r\n" +
                "Use the tabs to view Environment, Terraforming, and Attributes.";
        }

        private void LoadAndBindTables()
        {
            if (_obj == null) return;

            if (string.IsNullOrWhiteSpace(_dbPath))
            {
                BindEmpty(_gridBasics, "No DB path provided.");
                BindEmpty(_gridEnv, "No DB path provided.");
                BindEmpty(_gridTerraform, "No DB path provided.");
                BindEmpty(_gridAttrs, "No DB path provided.");
                return;
            }

            try
            {
                string kind = (_obj.ObjectKind ?? "").Trim().ToLowerInvariant();

                // 1) Basics (planet_details OR moon_details OR minimal)
                DataTable dtBasics;
                if (kind == "planet" || kind == "dwarf_planet")
                {
                    dtBasics = QueryTable("SELECT * FROM planet_details WHERE object_id = @id;", _obj.ObjectId);
                    BindOneRowAsProperties(_gridBasics, dtBasics, GetPlanetFieldMap(), hideTimestamps: true);
                }
                else if (kind == "moon")
                {
                    dtBasics = QueryTable("SELECT * FROM moon_details WHERE object_id = @id;", _obj.ObjectId);
                    BindOneRowAsProperties(_gridBasics, dtBasics, GetMoonFieldMap(), hideTimestamps: true);
                }
                else
                {
                    BindEmpty(_gridBasics, "No world details table for: " + FriendlyKind(kind));
                }

                // 2) Environment
                var dtEnv = QueryTable("SELECT * FROM body_environment WHERE object_id = @id;", _obj.ObjectId);
                BindOneRowAsProperties(_gridEnv, dtEnv, GetEnvironmentFieldMap(), hideTimestamps: true);

                // 3) Terraform constraints
                var dtTer = QueryTable("SELECT * FROM terraform_constraints WHERE object_id = @id;", _obj.ObjectId);
                BindOneRowAsProperties(_gridTerraform, dtTer, GetTerraformFieldMap(), hideTimestamps: true);

                // 4) Attributes (dictionary-joined, render as a friendly list)
                var dtAttrs = QueryTable(@"
SELECT
    COALESCE(ad.display_name, oa.attr_key)    AS name,
    ad.category                               AS category,
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
ORDER BY
    COALESCE(ad.category, '') COLLATE NOCASE,
    name COLLATE NOCASE;
", _obj.ObjectId);

                if (dtAttrs == null || dtAttrs.Rows.Count == 0)
                {
                    BindEmpty(_gridAttrs, "No attributes for this object.");
                }
                else
                {
                    // Build a cleaner presentation: Name | Value | Units | Category | Notes
                    var pretty = new DataTable();
                    pretty.Columns.Add("Name", typeof(string));
                    pretty.Columns.Add("Value", typeof(string));
                    pretty.Columns.Add("Units", typeof(string));
                    pretty.Columns.Add("Category", typeof(string));
                    pretty.Columns.Add("Notes", typeof(string));

                    for (int i = 0; i < dtAttrs.Rows.Count; i++)
                    {
                        var r = dtAttrs.Rows[i];

                        string nm = SafeStr(r["name"]);
                        string units = SafeStr(r["units"]);
                        string cat = SafeStr(r["category"]);
                        string notes = FirstLine(SafeStr(r["notes"]));

                        string valueKind = SafeStr(r["value_kind"]);
                        string value = FormatAttributeValue(valueKind,
                            r["value_text"], r["value_num"], r["value_int"], r["value_bool"]);

                        pretty.Rows.Add(nm, value, units, cat, notes);
                    }

                    _gridAttrs.DataSource = pretty;

                    // Slightly better column sizing for the attributes list
                    _gridAttrs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                    _gridAttrs.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
                    _gridAttrs.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
                }
            }
            catch (Exception ex)
            {
                string msg = "Load failed: " + ex.Message;
                BindEmpty(_gridBasics, msg);
                BindEmpty(_gridEnv, msg);
                BindEmpty(_gridTerraform, msg);
                BindEmpty(_gridAttrs, msg);
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

        // Turns a 1-row wide table into a friendly 2-col property list: Field | Value
        private static void BindOneRowAsProperties(
            DataGridView grid,
            DataTable dt,
            Dictionary<string, string> fieldMap,
            bool hideTimestamps)
        {
            if (grid == null) return;

            if (dt == null || dt.Rows.Count == 0)
            {
                grid.DataSource = MakeSingleRow("info", "No data.");
                return;
            }

            if (dt.Rows.Count > 1)
            {
                // Rare for these tables, but if it happens, show raw rows
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
                if (col.Length == 0) continue;

                // never show raw IDs
                if (EqualsIgnoreCase(col, "object_id")) continue;

                // optional: hide created/updated timestamps (noise)
                if (hideTimestamps && (EqualsIgnoreCase(col, "created_utc") || EqualsIgnoreCase(col, "updated_utc")))
                    continue;

                object v = r[i];

                string label = GetFriendlyLabel(fieldMap, col);
                string value = FormatCellValue(col, v);

                props.Rows.Add(label, value);
            }

            if (props.Rows.Count == 0)
            {
                grid.DataSource = MakeSingleRow("info", "No visible fields.");
                return;
            }

            grid.DataSource = props;
        }

        private static string GetFriendlyLabel(Dictionary<string, string> map, string col)
        {
            if (map != null)
            {
                string key = col ?? "";
                if (map.ContainsKey(key)) return map[key];
            }

            // fallback: crude prettify (snake_case -> Title Case)
            return PrettifyColumnName(col);
        }

        private static string PrettifyColumnName(string col)
        {
            if (string.IsNullOrWhiteSpace(col)) return "-";

            string s = col.Trim();

            // common suffixes
            if (s.EndsWith("_km", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 3) + " (km)";
            if (s.EndsWith("_au", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 3) + " (AU)";
            if (s.EndsWith("_atm", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 4) + " (atm)";
            if (s.EndsWith("_c", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 2) + " (°C)";
            if (s.EndsWith("_deg", StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - 4) + " (°)";

            s = s.Replace("_", " ");

            // Title case-ish
            if (s.Length == 0) return "-";
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static string FormatCellValue(string colName, object v)
        {
            if (v == null || v == DBNull.Value) return "-";

            // bool-ish ints
            if (v is long || v is int || v is short || v is byte)
            {
                long n = Convert.ToInt64(v, CultureInfo.InvariantCulture);

                if (LooksBoolish(colName))
                    return (n != 0) ? "Yes" : "No";

                // population can be large
                if (EqualsIgnoreCase(colName, "population"))
                    return n.ToString("N0", CultureInfo.InvariantCulture);

                return n.ToString(CultureInfo.InvariantCulture);
            }

            if (v is double || v is float || v is decimal)
            {
                double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);

                // show a sane number of decimals
                if (Math.Abs(d) >= 1000) return d.ToString("N0", CultureInfo.InvariantCulture);
                if (Math.Abs(d) >= 100) return d.ToString("0.##", CultureInfo.InvariantCulture);
                if (Math.Abs(d) >= 1) return d.ToString("0.###", CultureInfo.InvariantCulture);
                return d.ToString("0.####", CultureInfo.InvariantCulture);
            }

            // strings and everything else
            string s = Convert.ToString(v, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(s)) return "-";
            return s.Trim();
        }

        private static bool LooksBoolish(string colName)
        {
            if (string.IsNullOrWhiteSpace(colName)) return false;

            string c = colName.Trim().ToLowerInvariant();
            return c.StartsWith("is_") ||
                   c == "tidally_locked";
        }

        private static string SafeStr(object v)
        {
            if (v == null || v == DBNull.Value) return "";
            return Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        }

        private static string FormatAttributeValue(string valueKind, object vt, object vn, object vi, object vb)
        {
            string kind = (valueKind ?? "").Trim().ToLowerInvariant();

            // Prefer matching storage column, but still fall back in case of odd data
            if (kind == "text")
            {
                string s = SafeStr(vt);
                if (s.Length > 0) return s;
            }
            else if (kind == "num" || kind == "number" || kind == "real")
            {
                if (vn != null && vn != DBNull.Value) return FormatCellValue("value_num", vn);
            }
            else if (kind == "int" || kind == "integer")
            {
                if (vi != null && vi != DBNull.Value) return FormatCellValue("value_int", vi);
            }
            else if (kind == "bool" || kind == "boolean")
            {
                if (vb != null && vb != DBNull.Value) return FormatCellValue("value_bool", vb);
            }

            // Fallback: first non-empty
            string t = SafeStr(vt);
            if (t.Length > 0) return t;

            if (vn != null && vn != DBNull.Value) return FormatCellValue("value_num", vn);
            if (vi != null && vi != DBNull.Value) return FormatCellValue("value_int", vi);
            if (vb != null && vb != DBNull.Value) return FormatCellValue("value_bool", vb);

            return "-";
        }

        private static void BindEmpty(DataGridView grid, string msg)
        {
            if (grid == null) return;
            grid.DataSource = MakeSingleRow("info", msg ?? "");
        }

        private static DataTable MakeSingleRow(string colName, string value)
        {
            var dt = new DataTable();
            dt.Columns.Add(colName ?? "info", typeof(string));
            dt.Rows.Add(value ?? "");
            return dt;
        }

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

        private static string FirstLine(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "-";
            s = s.Trim();
            int ix = s.IndexOf('\n');
            if (ix >= 0) s = s.Substring(0, ix);
            if (s.Length > 140) s = s.Substring(0, 140) + "…";
            return s;
        }

        private static bool EqualsIgnoreCase(string a, string b)
        {
            return string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
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

        // ---------------- Friendly field maps (per table) ----------------

        private static Dictionary<string, string> GetPlanetFieldMap()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "planet_class", "Class" },
                { "radius_km", "Radius (km)" },
                { "mass_earth", "Mass (Earths)" },
                { "gravity_g", "Surface gravity (g)" },
                { "day_length_hours", "Day length (hours)" },
                { "axial_tilt_deg", "Axial tilt (°)" },
                { "semi_major_axis_au", "Orbit radius (AU)" },
                { "orbital_period_days", "Orbital period (days)" },
                { "eccentricity", "Eccentricity" },
                { "albedo", "Albedo" },
                { "density_g_cm3", "Density (g/cm³)" },
                { "population", "Population" },
                { "tech_level", "Tech level" },
                { "notes", "Notes" }
            };
        }

        private static Dictionary<string, string> GetMoonFieldMap()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "moon_class", "Class" },
                { "radius_km", "Radius (km)" },
                { "mass_earth", "Mass (Earths)" },
                { "gravity_g", "Surface gravity (g)" },
                { "day_length_hours", "Day length (hours)" },
                { "tidally_locked", "Tidally locked" },
                { "orbital_period_days", "Orbital period (days)" },
                { "semi_major_axis_km", "Orbit radius (km)" },
                { "eccentricity", "Eccentricity" },
                { "density_g_cm3", "Density (g/cm³)" },
                { "population", "Population" },
                { "tech_level", "Tech level" },
                { "notes", "Notes" }
            };
        }

        private static Dictionary<string, string> GetEnvironmentFieldMap()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "env_stage", "Environment stage" },
                { "atmosphere_type", "Atmosphere" },
                { "pressure_atm", "Pressure (atm)" },
                { "avg_temp_c", "Average temperature (°C)" },
                { "hydrosphere_pct", "Hydrosphere (%)" },
                { "biosphere", "Biosphere" },
                { "radiation_level", "Radiation" },
                { "magnetosphere", "Magnetosphere" },
                { "habitability", "Habitability" },
                { "notes", "Notes" }
            };
        }

        private static Dictionary<string, string> GetTerraformFieldMap()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "terraform_tier", "Terraforming tier" },
                { "atmosphere_retention", "Atmosphere retention" },
                { "radiation_constraint", "Radiation constraint" },
                { "volatile_budget", "Volatile budget" },
                { "water_availability", "Water availability" },
                { "requires_imports", "Requires imports" },
                { "limiting_factors", "Limiting factors" },
                { "maintenance_burden", "Maintenance burden" },
                { "notes", "Notes" }
            };
        }

        // ---------------- Orbit phrase helpers (keep concept / no radial_order shown) ----------------

        private static string DeriveOrbitPhrase_NoContext(SystemObjectInfo obj)
        {
            if (obj == null) return "-";

            string kind = (obj.ObjectKind ?? "").Trim().ToLowerInvariant();

            if (kind == "planet") return "Planetary orbit (position derived)";
            if (kind == "moon") return "Satellite orbit (position derived)";
            if (kind == "dwarf_planet") return "Dwarf-planet orbit (position derived)";
            if (kind == "belt" || kind == "asteroid_belt") return "Belt region (position derived)";
            if (kind == "kuiper_belt") return "Outer belt region";
            if (kind == "oort_cloud" || kind == "comet_cloud") return "Outer cloud region";
            if (kind == "installation" || kind == "station") return "Artificial orbit (position derived)";
            if (kind == "ring_system") return "Ring region";
            return "Orbit position derived";
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
                        string hostName = host != null && !string.IsNullOrWhiteSpace(host.DisplayName)
                            ? host.DisplayName.Trim()
                            : "its primary";
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

            return "Orbit position derived";
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
            if (n <= 0) return n.ToString(CultureInfo.InvariantCulture);
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
