using System;
using System.Drawing;
using System.Windows.Forms;

namespace StarMap2010
{
    //MainForm.GateUi
    public partial class MainForm
    {
        private static string Norm(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? "" : s.Trim();
        }

        private static string NormStatus(string st)
        {
            if (string.IsNullOrWhiteSpace(st)) return "open";
            return st.Trim().ToLowerInvariant();
        }

        private static Color StatusColor(string status)
        {
            switch (NormStatus(status))
            {
                case "open": return Color.FromArgb(20, 120, 60);
                case "restricted": return Color.FromArgb(160, 120, 0);
                case "interdicted": return Color.FromArgb(180, 70, 0);
                case "closed": return Color.FromArgb(140, 40, 40);
                default: return Color.FromArgb(90, 90, 90);
            }
        }

        private static string TitleCaseStatus(string status)
        {
            status = NormStatus(status);
            if (status.Length == 0) return "";
            return char.ToUpperInvariant(status[0]) + status.Substring(1);
        }

        private static void RtbAppend(RichTextBox rtb, string text, Color? color = null, bool bold = false)
        {
            if (rtb == null) return;

            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;

            rtb.SelectionColor = color ?? rtb.ForeColor;
            rtb.SelectionFont = new Font(rtb.Font, bold ? FontStyle.Bold : FontStyle.Regular);

            rtb.AppendText(text);

            rtb.SelectionColor = rtb.ForeColor;
            rtb.SelectionFont = rtb.Font;
        }

        private static void RtbAppendLine(RichTextBox rtb, string text = "", Color? color = null, bool bold = false)
        {
            RtbAppend(rtb, text + Environment.NewLine, color, bold);
        }
    }
}
