using System;
using System.IO;
using System.Windows.Forms;

namespace StarMap2010
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string dbPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "starinfo.db");

            Application.Run(new MainForm(dbPath));
        }
    }
}
