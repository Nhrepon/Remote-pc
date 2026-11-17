using System;
using System.Diagnostics;
using System.IO;

namespace RemotePC.Common
{
    /// <summary>
    /// Best-effort integration with a locally installed RustDesk client. RemotePC cannot embed
    /// RustDesk (it is a separate Rust application with its own relay/ID system), but it can
    /// detect it, launch it, and offer it as a fallback when a direct connection fails.
    /// </summary>
    public static class RustDeskHelper
    {
        public static string FindExecutable(string configuredPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(configuredPath) && File.Exists(configuredPath))
                    return configuredPath;
            }
            catch { }

            string[] candidates = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RustDesk", "RustDesk.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "RustDesk", "RustDesk.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RustDesk", "RustDesk.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RustDesk", "RustDesk.exe")
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                try { if (File.Exists(candidates[i])) return candidates[i]; }
                catch { }
            }

            // Last resort: rely on PATH.
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("where", "rustdesk");
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(3000);
                    string[] lines = output.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string candidate = lines[i].Trim();
                        if (candidate.Length > 0 && File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch { }

            return null;
        }

        public static bool IsInstalled(string configuredPath)
        {
            return FindExecutable(configuredPath) != null;
        }

        /// <summary>
        /// Launches RustDesk. If a peer id is supplied it is passed through; otherwise the
        /// RustDesk main window opens so the user can type the id manually.
        /// </summary>
        public static bool Launch(string configuredPath, string peerId, out string error)
        {
            error = null;
            string exe = FindExecutable(configuredPath);
            if (exe == null)
            {
                error = "RustDesk is not installed.";
                return false;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe);
                if (!string.IsNullOrEmpty(peerId))
                {
                    psi.Arguments = "--connect " + peerId.Trim();
                }
                psi.UseShellExecute = true;
                Process.Start(psi);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}