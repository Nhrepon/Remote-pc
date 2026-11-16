using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace RemotePC.Common
{
    public static class AppShortcutHelper
    {
        public const string ShortcutName = "Remote PC";
        private const string ShortcutDescription = "Remote PC - Remote Desktop Application";

        private static bool CreateShortcut(string shortcutPath, string exePath, string arguments, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrEmpty(exePath))
                    exePath = Application.ExecutablePath;

                string workDir = Path.GetDirectoryName(exePath);
                string parent = Path.GetDirectoryName(shortcutPath);
                if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
                    Directory.CreateDirectory(parent);

                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    error = "Windows Script Host is not available.";
                    return false;
                }

                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });

                Type scType = shortcut.GetType();
                scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exePath });
                scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workDir });
                scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { ShortcutDescription });

                if (!string.IsNullOrEmpty(arguments))
                {
                    scType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
                }

                scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string StartMenuPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), ShortcutName + ".lnk"); }
        }

        private static string DesktopPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName + ".lnk"); }
        }

        private static string StartupPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName + ".lnk"); }
        }

        #region Start Menu

        public static bool CreateStartMenuShortcut(out string error)
        {
            return CreateShortcut(StartMenuPath, null, null, out error);
        }

        public static bool CreateStartMenuShortcut(string exePath, string displayName, out string location)
        {
            string error;
            location = StartMenuPath;
            bool ok = CreateShortcut(StartMenuPath, exePath, null, out error);
            if (!ok) location = error;
            return ok;
        }

        public static bool IsStartMenuShortcutPresent()
        {
            try { return File.Exists(StartMenuPath); }
            catch { return false; }
        }

        public static bool RemoveStartMenuShortcut(out string error)
        {
            error = null;
            try
            {
                if (File.Exists(StartMenuPath)) File.Delete(StartMenuPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        #endregion

        #region Desktop

        public static bool CreateDesktopShortcut(out string error)
        {
            return CreateShortcut(DesktopPath, null, null, out error);
        }

        public static bool CreateDesktopShortcut(string exePath, string displayName, out string location)
        {
            string error;
            location = DesktopPath;
            bool ok = CreateShortcut(DesktopPath, exePath, null, out error);
            if (!ok) location = error;
            return ok;
        }

        #endregion

        #region Start With Windows

        public static bool CreateStartupShortcut(string exePath, string displayName, out string location)
        {
            string error;
            location = StartupPath;
            bool ok = CreateShortcut(StartupPath, exePath, "-minimized", out error);
            if (!ok) location = error;
            return ok;
        }

        public static bool SetStartWithWindows(bool enable, out string error)
        {
            error = null;
            try
            {
                if (enable) return CreateShortcut(StartupPath, null, "-minimized", out error);

                if (File.Exists(StartupPath)) File.Delete(StartupPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool IsStartWithWindowsEnabled()
        {
            try { return File.Exists(StartupPath); }
            catch { return false; }
        }

        #endregion
    }
}
