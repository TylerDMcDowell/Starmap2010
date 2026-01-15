// ============================================================
// File: UI/ObjectEditorForm.cs
// Project: StarMap2010
//
// Large modal viewer/editor shell.
// View mode shows read-only summary + loaded DB tables.
// Edit mode edits basics + details/environment/terraform (property list style).
//
// Locked rules honored:
// - radial_order is sorting only; never shown to users
// - Orbit text is derived (never stored)
// - Modal dialog is main place for detailed info
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

        // Basics
        private TextBox _txtName;
        private ComboBox _cmbKind;
        private ComboBox _cmbHost;
        private TextBox _txtNotes;

        private ListBox _lstOrbitOrder;
        private Button _btnOrbitUp;
        private Button _btnOrbitDown;
        private Label _lblOrbitHelp;

        // Tabs
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
            var sumWrap = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8)
            };
            sumWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            sumWrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tabSummary.Controls.Add(sumWrap);

            sumWrap.Controls.Add(BuildBasicsPanel(), 0, 0);

            _txtSummary = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Font = new Font("Consolas", 10f, FontStyle.Regular)
            };
            sumWrap.Controls.Add(_txtSummary, 0, 1);

            _tabs.TabPages.Add(tabSummary);

            // ---- Details tab ---- (now editable in Edit mode; same 2-col look)
            var tabDetails = new TabPage("Details");
            _gridDetails = MakeGridProps(true);
            tabDetails.Controls.Add(_gridDetails);
            _tabs.TabPages.Add(tabDetails);

            // ---- Environment tab ----
            var tabEnv = new TabPage("Environment");
            _gridEnv = MakeGridProps(true);
            tabEnv.Controls.Add(_gridEnv);
            _tabs.TabPages.Add(tabEnv);

            // ---- Terraform tab ----
            var tabTerraform = new TabPage("Terraform");
            _gridTerraform = MakeGridProps(true);
            tabTerraform.Controls.Add(_gridTerraform);
            _tabs.TabPages.Add(tabTerraform);

            // ---- Attributes tab ----
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
                Text = (_mode == ObjectEditorMode.View) ? "Close" : "Save",
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
                    SaveAll();
                    DialogResult = DialogResult.OK;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Save failed:\r\n\r\n" + ex.Message,
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
            LoadBasicsFields();
        }

        private Control BuildBasicsPanel()
        {
            var pnl = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(0),
                Margin = new Padding(0, 0, 0, 8)
            };

            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            pnl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            pnl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnl.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));

            // Display name
            pnl.Controls.Add(MakeLabel("Display name"), 0, 0);
            _txtName = new TextBox { Dock = DockStyle.Fill };
            pnl.Controls.Add(_txtName, 1, 0);

            // Kind
            pnl.Controls.Add(MakeLabel("Object kind"), 0, 1);
            _cmbKind = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 240,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbKind.Items.AddRange(new object[]
            {
                "planet","moon","dwarf_planet","belt","asteroid_belt","kuiper_belt","oort_cloud",
                "ring_system","installation","station","gate_facility","star","system_root"
            });
            pnl.Controls.Add(_cmbKind, 1, 1);

            // Orbit host dropdown
            pnl.Controls.Add(MakeLabel("Orbits around"), 0, 2);
            _cmbHost = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 420,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            pnl.Controls.Add(_cmbHost, 1, 2);

            // Orbit order (sorting only; radial_order is never shown)
            pnl.Controls.Add(MakeLabel("Orbit order"), 0, 3);

            var orbitWrap = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0)
            };
            orbitWrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            orbitWrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            orbitWrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            orbitWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _lstOrbitOrder = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false
            };
            orbitWrap.Controls.Add(_lstOrbitOrder, 0, 0);
            orbitWrap.SetRowSpan(_lstOrbitOrder, 2);

            var orbitBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0)
            };

            _btnOrbitUp = new Button { Text = "Up", Width = 90, Height = 28 };
            _btnOrbitDown = new Button { Text = "Down", Width = 90, Height = 28 };
            orbitBtns.Controls.Add(_btnOrbitUp);
            orbitBtns.Controls.Add(_btnOrbitDown);

            orbitWrap.Controls.Add(orbitBtns, 1, 0);

            _lblOrbitHelp = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.GrayText,
                Text = "Drag to reorder (Edit mode)."
            };
            orbitWrap.Controls.Add(_lblOrbitHelp, 1, 1);

            pnl.Controls.Add(orbitWrap, 1, 3);

            // Notes under (full-width)
            var notesWrap = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(0),
                Margin = new Padding(0, 8, 0, 0)
            };
            notesWrap.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            notesWrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            notesWrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            notesWrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));

            notesWrap.Controls.Add(MakeLabel("Notes"), 0, 0);
            _txtNotes = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };
            notesWrap.Controls.Add(_txtNotes, 1, 0);
            notesWrap.SetRowSpan(_txtNotes, 2);

            // Wrap basics+notes
            var outer = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };

            outer.Controls.Add(pnl);
            outer.Controls.Add(notesWrap);

            // Orbit list interactions
            if (_lstOrbitOrder != null)
            {
                _lstOrbitOrder.MouseDown += OrbitList_MouseDown;
                _lstOrbitOrder.DragOver += OrbitList_DragOver;
                _lstOrbitOrder.DragDrop += OrbitList_DragDrop;
                _lstOrbitOrder.AllowDrop = true;
            }

            if (_btnOrbitUp != null) _btnOrbitUp.Click += (s, e) => MoveOrbitSelection(-1);
            if (_btnOrbitDown != null) _btnOrbitDown.Click += (s, e) => MoveOrbitSelection(1);

            if (_cmbHost != null)
                _cmbHost.SelectedIndexChanged += (s, e) => RefreshOrbitOrderList();

            return outer;
        }

        private static Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 6, 6, 6)
            };
        }

        private static DataGridView MakeGridBase()
        {
            return new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders
            };
        }

        // 2-column "Field / Value" presentation (optionally editable in Edit mode)
        private DataGridView MakeGridProps(bool canEditInEditMode)
        {
            var g = MakeGridBase();
            g.ReadOnly = !canEditInEditMode;
            g.SelectionMode = DataGridViewSelectionMode.CellSelect;
            g.EditMode = DataGridViewEditMode.EditOnEnter;

            g.CellClick += (s, e) =>
            {
                if (_mode != ObjectEditorMode.Edit) return;
                if (!canEditInEditMode) return;
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                var grid = (DataGridView)s;
                if (grid.Columns[e.ColumnIndex].Name == "Value")
                {
                    grid.CurrentCell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    grid.BeginEdit(true);
                }
            };

            return g;
        }

        // 2-column "Attribute / Value" presentation (read-only)
        private static DataGridView MakeGridAttrs()
        {
            var g = MakeGridBase();
            g.ReadOnly = true;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            return g;
        }

        private void ApplyMode()
        {
            bool isEdit = (_mode == ObjectEditorMode.Edit);

            bool basicsEditable = isEdit;
            if (_txtName != null) _txtName.ReadOnly = !basicsEditable;
            if (_cmbKind != null) _cmbKind.Enabled = basicsEditable;
            if (_cmbHost != null) _cmbHost.Enabled = basicsEditable;
            if (_txtNotes != null) _txtNotes.ReadOnly = !basicsEditable;

            if (_lstOrbitOrder != null) _lstOrbitOrder.Enabled = basicsEditable;
            if (_btnOrbitUp != null) _btnOrbitUp.Enabled = basicsEditable;
            if (_btnOrbitDown != null) _btnOrbitDown.Enabled = basicsEditable;

            if (_btnPrimary != null)
            {
                _btnPrimary.Text = isEdit ? "Save" : "Close";
                _btnPrimary.DialogResult = isEdit ? DialogResult.None : DialogResult.OK;
            }

            if (_btnCancel != null)
            {
                _btnCancel.Visible = isEdit;
                _btnCancel.Text = "Cancel";
                _btnCancel.DialogResult = DialogResult.Cancel;
            }

            AcceptButton = _btnPrimary;
            CancelButton = isEdit ? _btnCancel : _btnPrimary;
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
                "Edit tips:\r\n" +
                "- Basics: edit name/kind/orbit host + reorder orbit list\r\n" +
                "- Details/Environment/Terraform: single-click Value to edit\r\n" +
                "- Attributes: read-only display\r\n";
        }

        private void LoadBasicsFields()
        {
            if (_obj == null) return;

            _txtName.Text = _obj.DisplayName ?? "";

            string kind = (_obj.ObjectKind ?? "").Trim();
            int idx = _cmbKind.FindStringExact(kind);
            if (idx >= 0) _cmbKind.SelectedIndex = idx;
            else
            {
                if (kind.Length > 0) _cmbKind.Items.Add(kind);
                _cmbKind.SelectedItem = kind;
            }

            PopulateHostDropdown();
            SelectHostInDropdown(FirstNonEmpty(_obj.OrbitHostObjectId, _obj.ParentObjectId));

            // Orbit order list
            RefreshOrbitOrderList();

            _txtNotes.Text = _obj.Notes ?? "";
        }

        private void PopulateHostDropdown()
        {
            _cmbHost.Items.Clear();

            _cmbHost.Items.Add(new ComboItem { Id = "", Text = "(none)" });

            if (_all == null || _all.Count == 0 || _obj == null)
            {
                _cmbHost.SelectedIndex = 0;
                return;
            }

            for (int i = 0; i < _all.Count; i++)
            {
                var o = _all[i];
                if (o == null) continue;
                if (string.Equals(o.ObjectId, _obj.ObjectId, StringComparison.Ordinal)) continue;

                string text = (o.DisplayName ?? "").Trim();
                if (text.Length == 0) text = "(unnamed)";
                string okind = (o.ObjectKind ?? "").Trim();
                if (okind.Length > 0) text += " [" + okind + "]";

                _cmbHost.Items.Add(new ComboItem { Id = o.ObjectId, Text = text });
            }

            _cmbHost.SelectedIndex = 0;
        }

        private void SelectHostInDropdown(string hostId)
        {
            if (_cmbHost.Items.Count == 0) return;

            hostId = hostId ?? "";

            for (int i = 0; i < _cmbHost.Items.Count; i++)
            {
                var ci = _cmbHost.Items[i] as ComboItem;
                if (ci == null) continue;

                if (string.Equals(ci.Id ?? "", hostId, StringComparison.Ordinal))
                {
                    _cmbHost.SelectedIndex = i;
                    return;
                }
            }

            _cmbHost.SelectedIndex = 0;
        }

        private sealed class ComboItem
        {
            public string Id;
            public string Text;
            public override string ToString() { return Text ?? ""; }
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

                DataTable dtDetails;
                if (kind == "planet" || kind == "dwarf_planet")
                    dtDetails = QueryTable("SELECT * FROM planet_details WHERE object_id = @id;", _obj.ObjectId);
                else if (kind == "moon")
                    dtDetails = QueryTable("SELECT * FROM moon_details WHERE object_id = @id;", _obj.ObjectId);
                else
                    dtDetails = MakeSingleRow("info", "No kind-specific details table for: " + kind);

                BindOneRowAsFriendlyProperties(_gridDetails, dtDetails, DetailsOrder);

                var dtEnv = QueryTable("SELECT * FROM body_environment WHERE object_id = @id;", _obj.ObjectId);
                BindOneRowAsFriendlyProperties(_gridEnv, dtEnv, EnvironmentOrder);

                var dtTer = QueryTable("SELECT * FROM terraform_constraints WHERE object_id = @id;", _obj.ObjectId);
                BindOneRowAsFriendlyProperties(_gridTerraform, dtTer, TerraformOrder);

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
                grid.DataSource = dt;
                return;
            }

            var props = new DataTable();
            props.Columns.Add("__col", typeof(string)); // hidden: real column name
            props.Columns.Add("Field", typeof(string));
            props.Columns.Add("Value", typeof(string));

            DataRow r = dt.Rows[0];

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

            cols.Sort(delegate(DataColumn a, DataColumn b)
            {
                int ra = GetOrderRank(preferredOrder, a.ColumnName);
                int rb = GetOrderRank(preferredOrder, b.ColumnName);
                if (ra != rb) return ra.CompareTo(rb);
                return string.Compare(a.ColumnName, b.ColumnName, StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < cols.Count; i++)
            {
                string col = cols[i].ColumnName ?? "";
                string val = FriendlyValue(col, r[col]);
                if (val == null) continue;

                string label = FriendlyFieldLabel(col);

                props.Rows.Add(col, label, val);
            }

            if (!string.IsNullOrWhiteSpace(notesValue))
            {
                var notesRow = props.NewRow();
                notesRow["__col"] = "notes";
                notesRow["Field"] = "Notes";
                notesRow["Value"] = notesValue.Trim();

                if (notesValue.Length <= NotesShortLimit)
                {
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

            // Hide internal column mapping
            if (grid.Columns.Contains("__col"))
                grid.Columns["__col"].Visible = false;

            if (grid.Columns.Contains("Field"))
                grid.Columns["Field"].ReadOnly = true;

            if (grid.Columns.Count >= 2)
            {
                grid.Columns["Field"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                grid.Columns["Value"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
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

            if (!string.IsNullOrWhiteSpace(valueKind))
                return "-";

            return "-";
        }

        private static void BindPropsEmpty(DataGridView grid, string msg)
        {
            if (grid == null) return;

            var dt = new DataTable();
            dt.Columns.Add("__col", typeof(string));
            dt.Columns.Add("Field", typeof(string));
            dt.Columns.Add("Value", typeof(string));
            dt.Rows.Add("info", "Info", msg ?? "");

            grid.DataSource = dt;

            if (grid.Columns.Contains("__col"))
                grid.Columns["__col"].Visible = false;

            if (grid.Columns.Count >= 2)
            {
                grid.Columns["Field"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                grid.Columns["Value"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
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

        // ---------- Save ----------

        private void SaveAll()
        {
            if (_obj == null) throw new InvalidOperationException("No object.");
            if (string.IsNullOrWhiteSpace(_dbPath)) throw new InvalidOperationException("No DB path.");

            using (var conn = new SQLiteConnection("Data Source=" + _dbPath + ";Version=3;"))
            {
                conn.Open();

                using (var tx = conn.BeginTransaction())
                {
                    SaveSystemObject(conn, tx);
                    PersistOrbitOrder(conn, tx);
                    SaveDetailsIfApplicable(conn, tx);
                    SavePropsTable(conn, tx, "body_environment", "object_id", _obj.ObjectId, _gridEnv);
                    SavePropsTable(conn, tx, "terraform_constraints", "object_id", _obj.ObjectId, _gridTerraform);

                    tx.Commit();
                }
            }
        }

        private void SaveSystemObject(SQLiteConnection conn, SQLiteTransaction tx)
        {
            string newName = (_txtName != null) ? (_txtName.Text ?? "").Trim() : "";
            if (newName.Length == 0) newName = _obj.DisplayName ?? "";

            string newKind = (_cmbKind != null && _cmbKind.SelectedItem != null)
                ? Convert.ToString(_cmbKind.SelectedItem)
                : (_obj.ObjectKind ?? "");

            string newNotes = (_txtNotes != null) ? (_txtNotes.Text ?? "") : (_obj.Notes ?? "");

            string hostId = "";
            if (_cmbHost != null && _cmbHost.SelectedItem != null)
            {
                var ci = _cmbHost.SelectedItem as ComboItem;
                hostId = (ci != null) ? (ci.Id ?? "") : "";
            }

            int orbitPos = DetermineOrbitPosFromList();

            using (var cmd = new SQLiteCommand(
                @"UPDATE system_objects
                  SET display_name = @name,
                      object_kind = @kind,
                      orbit_host_object_id = @host,
                      radial_order = @order,
                      notes = @notes
                  WHERE object_id = @id;", conn, tx))
            {
                cmd.Parameters.AddWithValue("@name", newName);
                cmd.Parameters.AddWithValue("@kind", newKind);
                cmd.Parameters.AddWithValue("@host", string.IsNullOrWhiteSpace(hostId) ? (object)DBNull.Value : hostId);
                cmd.Parameters.AddWithValue("@order", orbitPos);
                cmd.Parameters.AddWithValue("@notes", string.IsNullOrWhiteSpace(newNotes) ? (object)DBNull.Value : newNotes);
                cmd.Parameters.AddWithValue("@id", _obj.ObjectId);
                cmd.ExecuteNonQuery();
            }

            _obj.DisplayName = newName;
            _obj.ObjectKind = newKind;
            _obj.OrbitHostObjectId = string.IsNullOrWhiteSpace(hostId) ? null : hostId;
            _obj.RadialOrder = orbitPos;
            _obj.Notes = newNotes;
        }

        private static void SavePropsTable(SQLiteConnection conn, SQLiteTransaction tx, string tableName, string pkCol, string pkVal, DataGridView grid)
        {
            if (grid == null) return;
            if (grid.DataSource == null) return;

            var dt = grid.DataSource as DataTable;
            if (dt == null) return;
            if (!dt.Columns.Contains("__col") || !dt.Columns.Contains("Value")) return;

            using (var ensure = new SQLiteCommand(
                "INSERT OR IGNORE INTO " + tableName + " (" + pkCol + ") VALUES (@id);", conn, tx))
            {
                ensure.Parameters.AddWithValue("@id", pkVal);
                ensure.ExecuteNonQuery();
            }

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                var r = dt.Rows[i];
                string col = Convert.ToString(r["__col"]);
                if (string.IsNullOrWhiteSpace(col)) continue;

                if (EqualsIgnore(col, pkCol)) continue;
                if (ShouldHideColumn(col)) continue;

                object raw = r["Value"];
                string s = (raw != null) ? Convert.ToString(raw) : null;

                object dbVal = DBNull.Value;

                if (!string.IsNullOrWhiteSpace(s))
                {
                    s = s.Trim();

                    if (EqualsIgnore(col, "tidally_locked"))
                    {
                        dbVal = (s.Equals("yes", StringComparison.OrdinalIgnoreCase) || s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1")
                            ? 1
                            : 0;
                    }
                    else
                    {
                        int iVal;
                        double dVal;

                        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out iVal))
                            dbVal = iVal;
                        else if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out dVal))
                            dbVal = dVal;
                        else
                            dbVal = s;
                    }
                }

                using (var cmd = new SQLiteCommand(
                    "UPDATE " + tableName + " SET " + col + " = @val WHERE " + pkCol + " = @id;", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@val", dbVal);
                    cmd.Parameters.AddWithValue("@id", pkVal);
                    cmd.ExecuteNonQuery();
                }
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

        // ---------------- Orbit phrase helpers ----------------

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
            "planet_class", "moon_class",
            "population",
            "tech_level",
            "day_length_hours",
            "tidally_locked",
            "axial_tilt_deg",
            "orbital_period_days",
            "semi_major_axis_au",
            "semi_major_axis_km",
            "eccentricity",
            "gravity_g",
            "radius_km",
            "mass_earth",
            "density_g_cm3",
            "albedo"
        };

        private static readonly string[] EnvironmentOrder = new[]
        {
            "habitability",
            "atmosphere_type",
            "pressure_atm",
            "avg_temp_c",
            "hydrosphere_pct",
            "biosphere",
            "radiation_level",
            "magnetosphere",
            "env_stage"
        };

        private static readonly string[] TerraformOrder = new[]
        {
            "terraform_tier",
            "limiting_factors",
            "requires_imports",
            "water_availability",
            "volatile_budget",
            "atmosphere_retention",
            "radiation_constraint",
            "maintenance_burden"
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

            if (EqualsIgnore(col, "object_id"))
                return true;

            if (EqualsIgnore(col, "created_utc") || EqualsIgnore(col, "updated_utc"))
                return true;
            if (EndsWithIgnore(col, "_utc"))
                return true;

            if (EqualsIgnore(col, "radial_order"))
                return true;

            if (EqualsIgnore(col, "created_by") || EqualsIgnore(col, "updated_by"))
                return true;

            return false;
        }

        // ------------------------------------------------------------
        // Orbit order (drag + up/down). We never display radial_order.
        // ------------------------------------------------------------

        private sealed class OrbitItem
        {
            public string Id;
            public string Text;
            public override string ToString() { return Text ?? ""; }
        }

        private void RefreshOrbitOrderList()
        {
            if (_lstOrbitOrder == null) return;
            _lstOrbitOrder.Items.Clear();

            if (_obj == null || _all == null || _all.Count == 0)
            {
                _lstOrbitOrder.Items.Add(new OrbitItem { Id = _obj != null ? _obj.ObjectId : "", Text = "(no system context)" });
                _lstOrbitOrder.SelectedIndex = 0;
                return;
            }

            string hostId = "";
            if (_cmbHost != null && _cmbHost.SelectedItem != null)
            {
                var ci = _cmbHost.SelectedItem as ComboItem;
                hostId = (ci != null) ? (ci.Id ?? "") : "";
            }

            if (string.IsNullOrWhiteSpace(hostId))
                hostId = FirstNonEmpty(_obj.OrbitHostObjectId, _obj.ParentObjectId) ?? "";

            var orbiters = new List<SystemObjectInfo>();

            for (int i = 0; i < _all.Count; i++)
            {
                var o = _all[i];
                if (o == null) continue;

                string oHost = FirstNonEmpty(o.OrbitHostObjectId, o.ParentObjectId) ?? "";
                if (!string.Equals(oHost, hostId, StringComparison.Ordinal)) continue;

                if (IsOrbitingKind(o.ObjectKind))
                    orbiters.Add(o);
            }

            orbiters.Sort(delegate(SystemObjectInfo a, SystemObjectInfo b)
            {
                int ra = (a != null) ? a.RadialOrder : 0;
                int rb = (b != null) ? b.RadialOrder : 0;
                if (ra != rb) return ra.CompareTo(rb);
                string na = (a != null ? a.DisplayName : "") ?? "";
                string nb = (b != null ? b.DisplayName : "") ?? "";
                return string.Compare(na, nb, StringComparison.OrdinalIgnoreCase);
            });

            int selectIndex = -1;

            for (int i = 0; i < orbiters.Count; i++)
            {
                var o = orbiters[i];
                if (o == null) continue;

                string t = (o.DisplayName ?? "").Trim();
                if (t.Length == 0) t = "(unnamed)";
                string k = (o.ObjectKind ?? "").Trim();
                if (k.Length > 0) t += " [" + k + "]";

                var item = new OrbitItem { Id = o.ObjectId, Text = t };
                _lstOrbitOrder.Items.Add(item);

                if (_obj != null && string.Equals(o.ObjectId, _obj.ObjectId, StringComparison.Ordinal))
                    selectIndex = _lstOrbitOrder.Items.Count - 1;
            }

            if (_obj != null && selectIndex < 0)
            {
                string t = (_obj.DisplayName ?? "").Trim();
                if (t.Length == 0) t = "(unnamed)";
                string k = (_obj.ObjectKind ?? "").Trim();
                if (k.Length > 0) t += " [" + k + "]";
                _lstOrbitOrder.Items.Add(new OrbitItem { Id = _obj.ObjectId, Text = t });
                selectIndex = _lstOrbitOrder.Items.Count - 1;
            }

            if (_lstOrbitOrder.Items.Count > 0)
                _lstOrbitOrder.SelectedIndex = (selectIndex >= 0) ? selectIndex : 0;
        }

        private void MoveOrbitSelection(int delta)
        {
            if (_lstOrbitOrder == null) return;
            if (_mode != ObjectEditorMode.Edit) return;

            int i = _lstOrbitOrder.SelectedIndex;
            if (i < 0) return;

            int j = i + delta;
            if (j < 0 || j >= _lstOrbitOrder.Items.Count) return;

            object item = _lstOrbitOrder.Items[i];
            _lstOrbitOrder.Items.RemoveAt(i);
            _lstOrbitOrder.Items.Insert(j, item);
            _lstOrbitOrder.SelectedIndex = j;
        }

        private int _orbitDragIndex = -1;

        private void OrbitList_MouseDown(object sender, MouseEventArgs e)
        {
            if (_mode != ObjectEditorMode.Edit) return;
            if (_lstOrbitOrder == null) return;

            _orbitDragIndex = _lstOrbitOrder.IndexFromPoint(e.Location);
            if (_orbitDragIndex < 0) return;

            _lstOrbitOrder.DoDragDrop(_lstOrbitOrder.Items[_orbitDragIndex], DragDropEffects.Move);
        }

        private void OrbitList_DragOver(object sender, DragEventArgs e)
        {
            if (_mode != ObjectEditorMode.Edit) { e.Effect = DragDropEffects.None; return; }
            e.Effect = DragDropEffects.Move;
        }

        private void OrbitList_DragDrop(object sender, DragEventArgs e)
        {
            if (_mode != ObjectEditorMode.Edit) return;
            if (_lstOrbitOrder == null) return;

            var pt = _lstOrbitOrder.PointToClient(new Point(e.X, e.Y));
            int dropIndex = _lstOrbitOrder.IndexFromPoint(pt);
            if (dropIndex < 0) dropIndex = _lstOrbitOrder.Items.Count - 1;

            object data = e.Data.GetData(typeof(OrbitItem));
            if (data == null) return;

            int srcIndex = _lstOrbitOrder.Items.IndexOf(data);
            if (srcIndex < 0) return;

            if (srcIndex == dropIndex) return;

            _lstOrbitOrder.Items.RemoveAt(srcIndex);
            _lstOrbitOrder.Items.Insert(dropIndex, data);
            _lstOrbitOrder.SelectedIndex = dropIndex;
        }

        private int DetermineOrbitPosFromList()
        {
            if (_lstOrbitOrder == null || _lstOrbitOrder.Items.Count == 0 || _obj == null)
                return _obj != null ? _obj.RadialOrder : 0;

            for (int i = 0; i < _lstOrbitOrder.Items.Count; i++)
            {
                var it = _lstOrbitOrder.Items[i] as OrbitItem;
                if (it != null && string.Equals(it.Id, _obj.ObjectId, StringComparison.Ordinal))
                    return i * 10;
            }

            return _obj.RadialOrder;
        }

        private void PersistOrbitOrder(SQLiteConnection conn, SQLiteTransaction tx)
        {
            if (_lstOrbitOrder == null || _lstOrbitOrder.Items.Count == 0) return;
            if (_obj == null) return;

            for (int i = 0; i < _lstOrbitOrder.Items.Count; i++)
            {
                var it = _lstOrbitOrder.Items[i] as OrbitItem;
                if (it == null || string.IsNullOrWhiteSpace(it.Id)) continue;

                int ro = i * 10;

                using (var cmd = new SQLiteCommand(
                    @"UPDATE system_objects
                      SET radial_order = @ro
                      WHERE object_id = @id;", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@ro", ro);
                    cmd.Parameters.AddWithValue("@id", it.Id);
                    cmd.ExecuteNonQuery();
                }

                if (string.Equals(it.Id, _obj.ObjectId, StringComparison.Ordinal))
                    _obj.RadialOrder = ro;
            }
        }

        private void SaveDetailsIfApplicable(SQLiteConnection conn, SQLiteTransaction tx)
        {
            if (_obj == null) return;

            string kind = (_obj.ObjectKind ?? "").Trim().ToLowerInvariant();

            if (kind == "planet" || kind == "dwarf_planet")
                SavePropsTable(conn, tx, "planet_details", "object_id", _obj.ObjectId, _gridDetails);
            else if (kind == "moon")
                SavePropsTable(conn, tx, "moon_details", "object_id", _obj.ObjectId, _gridDetails);
        }
    }
}
