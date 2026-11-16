using System;
using System.Collections.Generic;
using System.Windows.Forms;
using RemotePC.Common;
using RemotePC.UI;

namespace RemotePC
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Elevated setup helper: create firewall rules, then exit without showing any UI.
            if (args != null && args.Length > 0 &&
                string.Equals(args[0], FirewallHelper.SetupSwitch, StringComparison.OrdinalIgnoreCase))
            {
                List<int> ports = new List<int>();
                for (int i = 1; i < args.Length; i++)
                {
                    int port;
                    if (int.TryParse(args[i], out port) && port >= 1024 && port <= 65535)
                        ports.Add(port);
                }
                if (ports.Count == 0) ports.Add(8888);

                string error;
                int exitCode = FirewallHelper.ApplyRulesElevated(ports, out error);
                Environment.Exit(exitCode);
                return;
            }

            // Enable Per-Monitor V2 DPI awareness first to prevent Windows DWM scaling/cropping
            NativeMethods.EnableDpiAwareness();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                string msg = ex != null ? ex.Message : "Unknown fatal error occurred.";
                MessageBox.Show("An unexpected error occurred: " + msg, "Remote PC Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                MessageBox.Show("An unexpected error occurred: " + e.Exception.Message, "Remote PC Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.Run(new MainForm());
        }
    }
}
