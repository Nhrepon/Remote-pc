using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace RemotePC.Common
{
    public static class AppShortcutHelper
    {
        private static bool CreateShortcut(string shortcutPath, string arguments, out string error)
        {
            error = null;
            try
            {
                string exePath = Application.ExecutablePath;
                string workDir = Path.GetDirectoryName(exePath);

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
                scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Remote PC - Remote Desktop Application" });

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

        public static bool CreateStartMenuShortcut(out string error)
        {
            try
            {
                string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string shortcutPath = Path.Combine(startMenu, "Remote PC.lnk");
                return CreateShortcut(shortcutPath, null, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool IsStartMenuShortcutPresent()
        {
            try
            {
                string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string shortcutPath = Path.Combine(startMenu, "Remote PC.lnk");
                return File.Exists(shortcutPath);
            }
            catch { return false; }
        }

        public static bool RemoveStartMenuShortcut(out string error)
        {
            error = null;
            try
            {
                string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string shortcutPath = Path.Combine(startMenu, "Remote PC.lnk");
                if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool CreateDesktopShortcut(out string error)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutPath = Path.Combine(desktop, "Remote PC.lnk");
                return CreateShortcut(shortcutPath, null, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool SetStartWithWindows(bool enable, out string error)
        {
            error = null;
            try
            {
                // Use Windows Startup folder (works universally with 0 registry elevation needed)
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string startupShortcut = Path.Combine(startupFolder, "Remote PC.lnk");

                if (enable)
                {
                    return CreateShortcut(startupShortcut, "-minimized", out error);
                }
                else
                {
                    if (File.Exists(startupShortcut)) File.Delete(startupShortcut);
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool IsStartWithWindowsEnabled()
        {
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string startupShortcut = Path.Combine(startupFolder, "Remote PC.lnk");
                return File.Exists(startupShortcut);
            }
            catch { return false; }
        }
    }
}
