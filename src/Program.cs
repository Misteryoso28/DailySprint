using System;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("DailySprint")]
[assembly: AssemblyDescription("Offline daily habits and expense tracker")]
[assembly: AssemblyCompany("DailySprint")]
[assembly: AssemblyProduct("DailySprint")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace DailySprint
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                DataStore store = DataStore.ForCurrentUser();
                AppData data = store.Load();
                Application.Run(new MainForm(store, data));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "DailySprint could not start.\r\n\r\n" + ex.Message,
                    "DailySprint startup error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
