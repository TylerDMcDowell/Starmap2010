// ============================================================
// File: MainForm.Viewer.cs
// Project: StarMap2010
//
// Central entry point for opening the read-only modal viewer
// Provides DB path + system context so orbit phrasing can be derived.
// ============================================================

using System.Collections.Generic;
using StarMap2010.Models;
using StarMap2010.Ui;

namespace StarMap2010
{
    //MainForm.Viewer
    public partial class MainForm
    {
        private void OpenViewerForObject(SystemObjectInfo obj)
        {
            if (obj == null) return;

            // Prefer cached list from SystemTree (fast + consistent)
            List<SystemObjectInfo> all = GetCurrentSystemObjectsCached();

            // Fallback if needed
            if ((all == null || all.Count == 0) && _objDao != null && !string.IsNullOrEmpty(obj.SystemId))
                all = _objDao.LoadObjectsForSystem(obj.SystemId);

            using (var dlg = new ObjectEditorForm(_dbPath, obj, ObjectEditorMode.View, all))
            {
                dlg.ShowDialog(this);
            }
        }
    }
}
