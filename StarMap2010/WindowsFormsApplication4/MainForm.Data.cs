using System;
using System.Collections.Generic;
using StarMap2010.Models;
using StarMap2010.Data;

namespace StarMap2010
{
    //MainForm.Data

    public partial class MainForm
    {
        // NOTE:
        // This file assumes these exist in MainForm.Core.cs (or another partial):
        //   private readonly StarSystemsDao _systemsDao;
        //   private readonly JumpGatesDao _gatesDao;
        //   private readonly Dictionary<string, JumpGate> gateBySystemId;
        //   private readonly Dictionary<string, JumpGate> gateByGateId;
        //   private readonly List<StarSystemInfo> systems;
        //   private List<StarSystemInfo> visibleSystems;
        //   private StarSystemInfo selectedA, selectedB;
        //   private string selectedIdA, selectedIdB;

        private void LoadSystemsFromDb()
        {
            if (_systemsDao == null) throw new InvalidOperationException("_systemsDao is null. Construct it in MainForm constructor.");

            systems.Clear();
            systems.AddRange(_systemsDao.LoadSystemsForMap());
        }

        private void LoadGatesAndLinksFromDb()
        {
            if (_gatesDao == null) throw new InvalidOperationException("_gatesDao is null. Construct it in MainForm constructor.");

            gateBySystemId.Clear();
            gateByGateId.Clear();
            gateLinks.Clear();

            var gates = _gatesDao.LoadGatesMinimalForMap();
            for (int i = 0; i < gates.Count; i++)
            {
                var g = gates[i];
                if (g == null) continue;

                if (string.IsNullOrWhiteSpace(g.GateId) || string.IsNullOrWhiteSpace(g.SystemId))
                    continue;

                g.GateId = g.GateId.Trim();
                g.SystemId = g.SystemId.Trim();

                gateBySystemId[g.SystemId] = g;
                gateByGateId[g.GateId] = g;
            }

            var links = _gatesDao.LoadRenderableLinksForMap();
            for (int i = 0; i < links.Count; i++)
            {
                var l = links[i];
                if (l == null) continue;

                if (string.IsNullOrWhiteSpace(l.SystemAId) || string.IsNullOrWhiteSpace(l.SystemBId))
                    continue;

                l.SystemAId = l.SystemAId.Trim();
                l.SystemBId = l.SystemBId.Trim();

                // normalize pair for stable drawing
                if (string.CompareOrdinal(l.SystemAId, l.SystemBId) > 0)
                {
                    string t = l.SystemAId;
                    l.SystemAId = l.SystemBId;
                    l.SystemBId = t;
                }

                gateLinks.Add(l);
            }
        }

        private void ApplyGovernmentFilter()
        {
            // Menu state is the filter state now (by GovernmentName)
            HashSet<string> allowedNames = GetSelectedGovernmentNamesFromMenu();

            // If nothing is checked, I recommend SHOW ALL (less confusing than blank map)
            bool showAll = (allowedNames == null || allowedNames.Count == 0);

            var filtered = new List<StarSystemInfo>(systems.Count);

            for (int i = 0; i < systems.Count; i++)
            {
                var s = systems[i];
                if (s == null) continue;

                string name = s.GovernmentName;
                if (string.IsNullOrWhiteSpace(name))
                    continue; // or include these if you want "unknown" govs visible

                name = name.Trim();

                if (showAll || allowedNames.Contains(name))
                    filtered.Add(s);
            }

            visibleSystems = filtered;

            // Keep selection valid
            if (!string.IsNullOrEmpty(selectedIdA))
            {
                bool found = false;
                for (int i = 0; i < visibleSystems.Count; i++)
                {
                    if (visibleSystems[i].SystemId == selectedIdA) { found = true; break; }
                }
                if (!found) ResetCompareSelection();
            }

            if (canvas != null)
            {
                canvas.SetData(visibleSystems);
                canvas.Invalidate();
            }

        }



        private void ReloadAndRefresh()
        {
            // ✅ Capture current filter state BEFORE rebuilding menu
            HashSet<string> restoreChecked = GetSelectedGovernmentNamesFromMenu();

            LoadSystemsFromDb();
            LoadGatesAndLinksFromDb();

            // Build governments menu from governments table (DAO returns DataTable)
            var govs = new List<GovernmentInfo>();
            var dt = _govDao.GetGovernmentLookup();

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                var r = dt.Rows[i];
                govs.Add(new GovernmentInfo
                {
                    GovernmentId = Convert.ToString(r["government_id"]),
                    GovernmentName = Convert.ToString(r["government_name"])
                });
            }

            // ✅ Rebuild menu while restoring checked state
            BuildGovernmentFilterMenuItems(govs, restoreChecked);

            ApplyGovernmentFilter();

            if (canvas != null)
            {
                canvas.SetGateLinks(gateLinks);
                canvas.ShowGates = IsShowGatesEnabled();
                canvas.Invalidate();
            }
        }




        private void ResetCompareSelection()
        {
            selectedA = null;
            selectedB = null;
            selectedIdA = null;
            selectedIdB = null;

            SetSelectedSystem(null);
            if (canvas != null) canvas.SetSelected(null);
        }

        // ------------------------------------------------------------
        // Lookup helper
        // IMPORTANT: You had FindSystemById duplicated in multiple files.
        // Keep ONLY ONE implementation in the entire project.
        //
        // To avoid name collisions right now, this file uses a private helper
        // with a unique name.
        // ------------------------------------------------------------
        private StarSystemInfo FindSystemById_FromLoadedSystems(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            for (int i = 0; i < systems.Count; i++)
            {
                var s = systems[i];
                if (s != null && string.Equals(s.SystemId, id, StringComparison.Ordinal))
                    return s;
            }

            return null;
        }
    }
}
