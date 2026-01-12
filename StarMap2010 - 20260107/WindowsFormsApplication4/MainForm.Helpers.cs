using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using StarMap2010.Models;

namespace StarMap2010
{
    public partial class MainForm
    {
        // SINGLE canonical lookup method.
        // Other files (Input/Selection/Data) can call this.
        private StarSystemInfo FindSystemById(string id)
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

        // Smooth zoom/pan (prevents flicker)
        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static void SetRedraw(Control control, bool enable)
        {
            if (control == null || control.IsDisposed) return;

            SendMessage(control.Handle, WM_SETREDRAW, enable ? (IntPtr)1 : IntPtr.Zero, IntPtr.Zero);

            if (enable)
            {
                control.Invalidate(true);
                control.Update();
            }
        }
    }
}
