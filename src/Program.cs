using System;
using System.Threading;
using System.Windows.Forms;

namespace SlideSync
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\SlideSync-single-instance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("SlideSync jest już uruchomiony na tym komputerze (sprawdź pasek zadań lub zasobnik).",
                        "SlideSync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                bool autoStart = false;
                foreach (string a in args)
                {
                    if (a.Equals("/start", StringComparison.OrdinalIgnoreCase) || a.Equals("--start", StringComparison.OrdinalIgnoreCase))
                        autoStart = true;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(Settings.Load(), autoStart));
            }
        }
    }
}
