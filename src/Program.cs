using System;
using System.Windows.Forms;
using RemotePC.Common;
using RemotePC.UI;

namespace RemotePC
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
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
