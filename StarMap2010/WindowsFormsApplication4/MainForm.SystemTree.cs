// ============================================================
// File: MainForm.SystemTree.cs
// Project: StarMap2010
//
// System Contents tree:
// - Loads system_objects for a system and builds the TreeView
// - Caches loaded objects for orbit phrasing + details panel
// - Context menu: View (modal), Add/Edit/Delete
// - Double-click: View (modal)
// ============================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using StarMap2010.Models;
using StarMap2010.Ui;

namespace StarMap2010
{
    // MainForm.SystemTree
    public partial class MainForm
    {
        private ContextMenuStrip _treeMenu;

        // Cache of currently loaded system objects for the selected system
        private List<SystemObjectInfo> _currentSystemObjects;
        private Dictionary<string, SystemObjectInfo> _currentSystemObjectsById;

        private bool _systemTreeHandlersWired = false;

        private void EnsureSystemTreeMenu()
        {
            if (_treeMenu != null) return;
            if (tvSystemObjects == null) return;

            _treeMenu = new ContextMenuStrip();

            var miView = new ToolStripMenuItem("View…");
            var miAddChild = new ToolStripMenuItem("Add Child…");
            var miAddSibling = new ToolStripMenuItem("Add Sibling…");
            var miEdit = new ToolStripMenuItem("Edit…");
            var miDelete = new ToolStripMenuItem("Delete…");
            var miEditDetails = new ToolStripMenuItem("Edit Details…");
            miEditDetails.Click += (s, e) => Tree_EditDetails();

            miView.Click += (s, e) => Tree_View();
            miAddChild.Click += (s, e) => Tree_AddChild();
            miAddSibling.Click += (s, e) => Tree_AddSibling();
            miEdit.Click += (s, e) => Tree_Edit();
            miDelete.Click += (s, e) => Tree_Delete();

            _treeMenu.Items.Add(miView);
            _treeMenu.Items.Add(miEditDetails);
            _treeMenu.Items.Add(new ToolStripSeparator());
            _treeMenu.Items.Add(miAddChild);
            _treeMenu.Items.Add(miAddSibling);
            _treeMenu.Items.Add(new ToolStripSeparator());
            _treeMenu.Items.Add(miEdit);
            _treeMenu.Items.Add(miDelete);


            _treeMenu.Opening += (s, e) =>
            {
                bool hasSystem = (selectedA != null && !string.IsNullOrEmpty(selectedA.SystemId));
                SystemObjectInfo cur = GetSelectedTreeObject();

                // Placeholder nodes have Tag=null
                bool hasNode = (cur != null);

                miView.Enabled = hasSystem && hasNode;
                miAddChild.Enabled = hasSystem && hasNode;

                // sibling/delete/edit disabled if no node OR if node has no parent (root)
                bool isRoot = (cur != null && cur.ParentObjectId == null);

                miAddSibling.Enabled = hasSystem && hasNode && !isRoot;
                miEdit.Enabled = hasSystem && hasNode;
                miDelete.Enabled = hasSystem && hasNode && !isRoot;
                miEditDetails.Enabled = hasSystem && hasNode;
            };

            tvSystemObjects.ContextMenuStrip = _treeMenu;

            // Handy shortcuts inside the tree
            tvSystemObjects.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    Tree_View();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.Insert)
                {
                    Tree_AddChild();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.Delete)
                {
                    Tree_Delete();
                    e.Handled = true;
                    return;
                }
                if (e.KeyCode == Keys.F2)
                {
                    Tree_Edit();
                    e.Handled = true;
                    return;
                }
            };
        }

        private void EnsureSystemTreeHandlers()
        {
            if (_systemTreeHandlersWired) return;
            if (tvSystemObjects == null) return;

            tvSystemObjects.AfterSelect += (s, e) =>
            {
                var cur = GetSelectedTreeObject();
                ShowWorldCardForObject(cur);
            };

            tvSystemObjects.NodeMouseDoubleClick += (s, e) =>
            {
                // Only open modal for real nodes
                var obj = (e.Node != null) ? (e.Node.Tag as SystemObjectInfo) : null;
                if (obj == null) return;
                OpenViewerForObject(obj);
            };

            _systemTreeHandlersWired = true;
        }

        private SystemObjectInfo GetSelectedTreeObject()
        {
            if (tvSystemObjects == null) return null;
            TreeNode n = tvSystemObjects.SelectedNode;
            if (n == null) return null;
            return n.Tag as SystemObjectInfo;
        }

        private void ResetSystemTreePlaceholder()
        {
            if (tvSystemObjects == null) return;

            tvSystemObjects.BeginUpdate();
            try
            {
                tvSystemObjects.Nodes.Clear();
                tvSystemObjects.Nodes.Add(new TreeNode("(select a system)") { Tag = null });
            }
            finally
            {
                tvSystemObjects.EndUpdate();
            }

            // clear cache
            _currentSystemObjects = null;
            _currentSystemObjectsById = null;

            // Details panel should show a hint when nothing is selected
            ShowWorldCardForObject(null);
        }

        private void LoadSystemTree(string systemId)
        {
            if (_objDao == null || tvSystemObjects == null) return;

            EnsureSystemTreeMenu();
            EnsureSystemTreeHandlers();

            if (string.IsNullOrEmpty(systemId))
            {
                ResetSystemTreePlaceholder();
                return;
            }

            string primaryStarType = null;
            StarSystemInfo sys = FindSystemById(systemId);
            if (sys != null) primaryStarType = sys.PrimaryStarType;

            tvSystemObjects.BeginUpdate();
            try
            {
                tvSystemObjects.Nodes.Clear();

                List<SystemObjectInfo> all = _objDao.LoadObjectsForSystem(systemId);

                // Cache for orbit description + details UI
                _currentSystemObjects = all ?? new List<SystemObjectInfo>();
                _currentSystemObjectsById = new Dictionary<string, SystemObjectInfo>(StringComparer.Ordinal);

                for (int i = 0; i < _currentSystemObjects.Count; i++)
                {
                    var o = _currentSystemObjects[i];
                    if (o == null) continue;
                    if (string.IsNullOrWhiteSpace(o.ObjectId)) continue;
                    _currentSystemObjectsById[o.ObjectId] = o;
                }

                if (all == null || all.Count == 0)
                {
                    tvSystemObjects.Nodes.Add(new TreeNode("(no objects)") { Tag = null });
                    ShowWorldCardForObject(null);
                    return;
                }

                var childrenByParent = new Dictionary<string, List<SystemObjectInfo>>(StringComparer.Ordinal);
                SystemObjectInfo root = null;

                for (int i = 0; i < all.Count; i++)
                {
                    var o = all[i];
                    if (o == null) continue;

                    if (root == null && string.Equals(o.ObjectKind, "system_root", StringComparison.OrdinalIgnoreCase))
                        root = o;

                    string p = o.ParentObjectId ?? "";
                    List<SystemObjectInfo> bucket;
                    if (!childrenByParent.TryGetValue(p, out bucket))
                    {
                        bucket = new List<SystemObjectInfo>();
                        childrenByParent[p] = bucket;
                    }
                    bucket.Add(o);
                }

                if (root != null)
                {
                    TreeNode rn = MakeNode(root, primaryStarType);
                    tvSystemObjects.Nodes.Add(rn);

                    AddChildrenRecursive(rn, root.ObjectId, childrenByParent, primaryStarType);
                    rn.Expand();

                    tvSystemObjects.SelectedNode = rn;
                    ShowWorldCardForObject(root);
                }
                else
                {
                    List<SystemObjectInfo> tops;
                    if (childrenByParent.TryGetValue("", out tops))
                    {
                        SortSiblings(tops);

                        for (int i = 0; i < tops.Count; i++)
                        {
                            TreeNode tn = MakeNode(tops[i], primaryStarType);
                            tvSystemObjects.Nodes.Add(tn);
                            AddChildrenRecursive(tn, tops[i].ObjectId, childrenByParent, primaryStarType);
                        }

                        // select first top node if present
                        if (tvSystemObjects.Nodes.Count > 0)
                        {
                            tvSystemObjects.SelectedNode = tvSystemObjects.Nodes[0];
                            ShowWorldCardForObject(tvSystemObjects.Nodes[0].Tag as SystemObjectInfo);
                        }
                        else
                        {
                            ShowWorldCardForObject(null);
                        }
                    }
                    else
                    {
                        tvSystemObjects.Nodes.Add(new TreeNode("(no root)") { Tag = null });
                        ShowWorldCardForObject(null);
                    }
                }
            }
            catch (Exception ex)
            {
                tvSystemObjects.Nodes.Clear();
                tvSystemObjects.Nodes.Add(new TreeNode("Tree load failed: " + ex.Message) { Tag = null });

                _currentSystemObjects = null;
                _currentSystemObjectsById = null;

                ShowWorldCardForObject(null);
            }
            finally
            {
                tvSystemObjects.EndUpdate();
            }
        }

        private void AddChildrenRecursive(
            TreeNode parentNode,
            string parentObjectId,
            Dictionary<string, List<SystemObjectInfo>> childrenByParent,
            string primaryStarType)
        {
            if (parentNode == null) return;
            if (childrenByParent == null) return;

            List<SystemObjectInfo> kids;
            if (!childrenByParent.TryGetValue(parentObjectId ?? "", out kids)) return;

            SortSiblings(kids);

            for (int i = 0; i < kids.Count; i++)
            {
                var c = kids[i];
                if (c == null) continue;

                // safety against cycles
                if (!string.IsNullOrEmpty(parentObjectId) && string.Equals(c.ObjectId, parentObjectId, StringComparison.Ordinal))
                    continue;

                TreeNode n = MakeNode(c, primaryStarType);
                parentNode.Nodes.Add(n);

                AddChildrenRecursive(n, c.ObjectId, childrenByParent, primaryStarType);
            }
        }

        private static void SortSiblings(List<SystemObjectInfo> list)
        {
            if (list == null) return;

            list.Sort(delegate(SystemObjectInfo a, SystemObjectInfo b)
            {
                if (a == null && b == null) return 0;
                if (a == null) return -1;
                if (b == null) return 1;

                int ra = a.RadialOrder;
                int rb = b.RadialOrder;
                if (ra != rb) return ra.CompareTo(rb);

                string ka = a.ObjectKind ?? "";
                string kb = b.ObjectKind ?? "";
                int ck = string.Compare(ka, kb, StringComparison.OrdinalIgnoreCase);
                if (ck != 0) return ck;

                string na = a.DisplayName ?? "";
                string nb = b.DisplayName ?? "";
                return string.Compare(na, nb, StringComparison.OrdinalIgnoreCase);
            });
        }

        private TreeNode MakeNode(SystemObjectInfo o, string primaryStarType)
        {
            string name = (o != null && !string.IsNullOrEmpty(o.DisplayName)) ? o.DisplayName : "(unnamed)";
            string kind = (o != null) ? (o.ObjectKind ?? "") : "";

            // Gate facility: use jump_gates.gate_name if linked; NEVER show UUID
            if (IsGateFacilityNode(o, kind))
            {
                string gateName = ResolveGateFacilityName(o);
                if (!string.IsNullOrEmpty(gateName)) name = gateName;
                else name = "(Unnamed Gate Facility)";
            }

            // Star: show primary star type
            if (string.Equals(kind, "star", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(primaryStarType))
                    name = name + " (" + primaryStarType.Trim() + ")";
            }

            string text = name;
            if (!string.IsNullOrEmpty(kind))
                text = text + " [" + kind + "]";

            TreeNode n = new TreeNode(text) { Tag = o };
            n.ForeColor = ColorForKind(kind);

            return n;
        }

        private bool IsGateFacilityNode(SystemObjectInfo o, string kind)
        {
            if (o == null) return false;

            if (string.Equals(kind, "gate_facility", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(o.RelatedTable) &&
                string.Equals(o.RelatedTable.Trim(), "jump_gates", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private string ResolveGateFacilityName(SystemObjectInfo o)
        {
            if (o == null) return null;

            if (!string.IsNullOrWhiteSpace(o.RelatedTable) &&
                string.Equals(o.RelatedTable.Trim(), "jump_gates", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(o.RelatedId))
            {
                string gid = o.RelatedId.Trim();

                JumpGate g;
                if (gateByGateId != null && gateByGateId.TryGetValue(gid, out g) && g != null)
                {
                    if (!string.IsNullOrWhiteSpace(g.GateName))
                        return g.GateName.Trim();
                }

                return null;
            }

            return null;
        }

        private static Color ColorForKind(string kind)
        {
            kind = (kind ?? "").Trim().ToLowerInvariant();

            switch (kind)
            {
                case "system_root": return Color.FromArgb(60, 60, 60);
                case "star": return Color.FromArgb(170, 120, 0);
                case "planet": return Color.FromArgb(0, 90, 170);
                case "moon": return Color.FromArgb(100, 100, 100);
                case "belt":
                case "asteroid_belt": return Color.FromArgb(130, 90, 40);
                case "kuiper_belt":
                case "oort_cloud":
                case "comet_cloud": return Color.FromArgb(60, 110, 120);
                case "dwarf_planet": return Color.FromArgb(90, 90, 130);
                case "installation":
                case "station":
                case "gate_facility": return Color.FromArgb(110, 60, 140);
                default: return SystemColors.ControlText;
            }
        }

        // ------------------------------------------------------------
        // Cache helpers for orbit phrasing + Details panel
        // ------------------------------------------------------------
        private List<SystemObjectInfo> GetCurrentSystemObjectsCached()
        {
            return _currentSystemObjects;
        }

        private SystemObjectInfo GetObjectByIdCached(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId)) return null;
            if (_currentSystemObjectsById == null) return null;

            SystemObjectInfo o;
            if (_currentSystemObjectsById.TryGetValue(objectId.Trim(), out o))
                return o;

            return null;
        }

        // ------------------------------------------------------------
        // View / Add / Edit / Delete
        // ------------------------------------------------------------
        private void Tree_View()
        {
            var cur = GetSelectedTreeObject();
            if (cur == null) return;
            OpenViewerForObject(cur);
        }

        private void Tree_EditDetails()
        {
            var cur = GetSelectedTreeObject();
            if (cur == null) return;
            OpenEditorForObject(cur);
        }


        private void Tree_AddChild()
        {
            if (_objDao == null) return;
            if (selectedA == null || string.IsNullOrEmpty(selectedA.SystemId)) return;

            var parent = GetSelectedTreeObject();
            if (parent == null) return;

            var o = new SystemObjectInfo();
            o.ObjectId = Guid.NewGuid().ToString();
            o.SystemId = selectedA.SystemId;
            o.ParentObjectId = parent.ObjectId;
            o.ObjectKind = "planet";
            o.DisplayName = "New Object";
            o.RadialOrder = 0;

            using (var dlg = new SystemObjectEditorForm("Add Child", o))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _objDao.Upsert(dlg.Result);
            }

            ReloadTreeAndReselect(o.ObjectId);
        }

        private void Tree_AddSibling()
        {
            if (_objDao == null) return;
            if (selectedA == null || string.IsNullOrEmpty(selectedA.SystemId)) return;

            var cur = GetSelectedTreeObject();
            if (cur == null) return;
            if (cur.ParentObjectId == null) return;

            var o = new SystemObjectInfo();
            o.ObjectId = Guid.NewGuid().ToString();
            o.SystemId = selectedA.SystemId;
            o.ParentObjectId = cur.ParentObjectId;
            o.ObjectKind = string.IsNullOrWhiteSpace(cur.ObjectKind) ? "planet" : cur.ObjectKind;
            o.DisplayName = "New Object";
            o.RadialOrder = cur.RadialOrder;

            using (var dlg = new SystemObjectEditorForm("Add Sibling", o))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _objDao.Upsert(dlg.Result);
            }

            ReloadTreeAndReselect(o.ObjectId);
        }

        private void Tree_Edit()
        {
            if (_objDao == null) return;
            if (selectedA == null || string.IsNullOrEmpty(selectedA.SystemId)) return;

            var cur = GetSelectedTreeObject();
            if (cur == null) return;

            // Basic editor: keep hierarchy stable; just edit name/kind/order/notes
            var t = new SystemObjectInfo();
            t.ObjectId = cur.ObjectId;
            t.SystemId = cur.SystemId;
            t.ParentObjectId = cur.ParentObjectId;
            t.OrbitHostObjectId = cur.OrbitHostObjectId;
            t.ObjectKind = cur.ObjectKind;
            t.RadialOrder = cur.RadialOrder;
            t.DisplayName = cur.DisplayName;
            t.Notes = cur.Notes;
            t.RelatedTable = cur.RelatedTable;
            t.RelatedId = cur.RelatedId;
            t.Flags = cur.Flags;

            using (var dlg = new SystemObjectEditorForm("Edit Object", t))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _objDao.Upsert(dlg.Result);
            }

            ReloadTreeAndReselect(cur.ObjectId);
        }

        private void Tree_Delete()
        {
            if (_objDao == null) return;
            if (selectedA == null || string.IsNullOrEmpty(selectedA.SystemId)) return;

            var cur = GetSelectedTreeObject();
            if (cur == null) return;
            if (cur.ParentObjectId == null) return; // don’t delete root

            string name = cur.DisplayName ?? "(unnamed)";
            var confirm = MessageBox.Show(
                this,
                "Delete \"" + name + "\" and its children?",
                "Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            string parentId = cur.ParentObjectId;

            _objDao.DeleteByObjectId(cur.ObjectId);

            // After delete, try to reselect parent
            ReloadTreeAndReselect(parentId);
        }

        private void ReloadTreeAndReselect(string objectId)
        {
            if (selectedA == null || string.IsNullOrEmpty(selectedA.SystemId)) return;

            LoadSystemTree(selectedA.SystemId);

            if (string.IsNullOrEmpty(objectId)) return;

            TreeNode n = FindNodeByObjectId(tvSystemObjects.Nodes, objectId);
            if (n != null)
            {
                tvSystemObjects.SelectedNode = n;
                n.EnsureVisible();
            }
        }

        private TreeNode FindNodeByObjectId(TreeNodeCollection nodes, string objectId)
        {
            if (nodes == null) return null;

            for (int i = 0; i < nodes.Count; i++)
            {
                TreeNode n = nodes[i];

                var o = n.Tag as SystemObjectInfo;
                if (o != null && string.Equals(o.ObjectId, objectId, StringComparison.Ordinal))
                    return n;

                TreeNode sub = FindNodeByObjectId(n.Nodes, objectId);
                if (sub != null) return sub;
            }

            return null;
        }
    }
}
