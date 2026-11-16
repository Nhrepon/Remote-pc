using System;
using System.IO;

namespace RemotePC.Common
{
    public class AppSettings
    {
        private static readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemotePC");
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.ini");

        public int ServerPort { get; set; }
        public string Password { get; set; }
        public bool AutoStartServer { get; set; }
        public bool StartWithWindows { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool AllowControl { get; set; }
        public bool AllowFileTransfer { get; set; }

        public string LastHost { get; set; }
        public string LastPassword { get; set; }
        public bool AutoConnect { get; set; }

        // First-run automation bookkeeping
        public bool FirstRunComplete { get; set; }
        public string FirewallRulePorts { get; set; }
        public bool UpnpAttempted { get; set; }

        public AppSettings()
        {
            ServerPort = 8888;
            Password = new Random().Next(100000, 999999).ToString();
            AutoStartServer = false;
            StartWithWindows = false;
            MinimizeToTray = false;
            AllowControl = true;
            AllowFileTransfer = true;

            LastHost = "";
            LastPassword = "";
            AutoConnect = false;

            FirstRunComplete = false;
            FirewallRulePorts = "";
            UpnpAttempted = false;
        }

        public static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string[] lines = File.ReadAllLines(SettingsFile);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;

                        string key = line.Substring(0, eq).Trim();
                        string val = line.Substring(eq + 1).Trim();

                        // Keys written by older builds are still accepted.
                        if (key == "ServerPort" || key == "HostPort") { int p; if (int.TryParse(val, out p)) s.ServerPort = p; }
                        else if (key == "Password" || key == "HostPassword") s.Password = val;
                        else if (key == "AutoStartServer") s.AutoStartServer = ParseBool(val);
                        else if (key == "StartWithWindows") s.StartWithWindows = ParseBool(val);
                        else if (key == "MinimizeToTray") s.MinimizeToTray = ParseBool(val);
                        else if (key == "AllowControl") s.AllowControl = ParseBool(val);
                        else if (key == "AllowFileTransfer" || key == "AllowFiles") s.AllowFileTransfer = ParseBool(val);
                        else if (key == "LastHost" || key == "ClientHost") s.LastHost = val;
                        else if (key == "LastPassword" || key == "ClientPassword") s.LastPassword = val;
                        else if (key == "AutoConnect") s.AutoConnect = ParseBool(val);
                        else if (key == "FirstRunComplete") s.FirstRunComplete = ParseBool(val);
                        else if (key == "FirewallRulePorts") s.FirewallRulePorts = val;
                        else if (key == "UpnpAttempted") s.UpnpAttempted = ParseBool(val);
                    }
                }
            }
            catch { }
            return s;
        }

        private static bool ParseBool(string val)
        {
            return val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                {
                    Directory.CreateDirectory(SettingsDir);
                }

                using (StreamWriter sw = new StreamWriter(SettingsFile, false))
                {
                    sw.WriteLine("# RemotePC Persistent Configuration");
                    sw.WriteLine("ServerPort=" + ServerPort);
                    sw.WriteLine("Password=" + (Password ?? ""));
                    sw.WriteLine("AutoStartServer=" + (AutoStartServer ? "1" : "0"));
                    sw.WriteLine("StartWithWindows=" + (StartWithWindows ? "1" : "0"));
                    sw.WriteLine("MinimizeToTray=" + (MinimizeToTray ? "1" : "0"));
                    sw.WriteLine("AllowControl=" + (AllowControl ? "1" : "0"));
                    sw.WriteLine("AllowFileTransfer=" + (AllowFileTransfer ? "1" : "0"));
                    sw.WriteLine("LastHost=" + (LastHost ?? ""));
                    sw.WriteLine("LastPassword=" + (LastPassword ?? ""));
                    sw.WriteLine("AutoConnect=" + (AutoConnect ? "1" : "0"));
                    sw.WriteLine("FirstRunComplete=" + (FirstRunComplete ? "1" : "0"));
                    sw.WriteLine("FirewallRulePorts=" + (FirewallRulePorts ?? ""));
                    sw.WriteLine("UpnpAttempted=" + (UpnpAttempted ? "1" : "0"));
                }
            }
            catch { }
        }
    }
}
