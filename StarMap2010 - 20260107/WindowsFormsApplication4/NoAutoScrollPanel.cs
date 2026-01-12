using System.Drawing;
using System.Windows.Forms;

namespace StarMap2010
{
    public class NoAutoScrollPanel : Panel
    {
        protected override Point ScrollToControl(Control activeControl)
        {
            // Prevent WinForms from auto-scrolling when focus changes.
            return this.DisplayRectangle.Location;
        }
    }
}
