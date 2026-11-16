using System;
using System.IO;

namespace RemotePC.Common
{
    public class AppSettings
    {
        private static readonly string SettingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemotePC");
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.ini");

        public int HostPort { get; set; }
        public string HostPassword { get; set; }
        public bool AutoStartServer { get; set; }
        public bool StartWithWindows { get; set; }
        public bool MinimizeToTray { get; set; }
        public bool AllowControl { get; set; }
        public bool AllowFiles { get; set; }

        public string ClientHost { get; set; }
        public int ClientPort { get; set; }
        public string ClientPassword { get; set; }
        public bool AutoConnect { get; set; }

        public AppSettings()
        {
            HostPort = 8888;
            HostPassword = new Random().Next(100000, 999999).ToString();
            AutoStartServer = false;
            StartWithWindows = false;
            MinimizeToTray = false;
            AllowControl = true;
            AllowFiles = true;

            ClientHost = "";
            ClientPort = 8888;
            ClientPassword = "";
            AutoConnect = false;
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

                        if (key == "HostPort") { int p; if (int.TryParse(val, out p)) s.HostPort = p; }
                        else if (key == "HostPassword") s.HostPassword = val;
                        else if (key == "AutoStartServer") s.AutoStartServer = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                        else if (key == "StartWithWindows") s.StartWithWindows = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                        else if (key == "MinimizeToTray") s.MinimizeToTray = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                        else if (key == "AllowControl") s.AllowControl = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                        else if (key == "AllowFiles") s.AllowFiles = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                        else if (key == "ClientHost") s.ClientHost = val;
                        else if (key == "ClientPort") { int p; if (int.TryParse(val, out p)) s.ClientPort = p; }
                        else if (key == "ClientPassword") s.ClientPassword = val;
                        else if (key == "AutoConnect") s.AutoConnect = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch { }
            return s;
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
                    sw.WriteLine("HostPort=" + HostPort);
                    sw.WriteLine("HostPassword=" + (HostPassword ?? ""));
                    sw.WriteLine("AutoStartServer=" + (AutoStartServer ? "1" : "0"));
                    sw.WriteLine("StartWithWindows=" + (StartWithWindows ? "1" : "0"));
                    sw.WriteLine("MinimizeToTray=" + (MinimizeToTray ? "1" : "0"));
                    sw.WriteLine("AllowControl=" + (AllowControl ? "1" : "0"));
                    sw.WriteLine("AllowFiles=" + (AllowFiles ? "1" : "0"));
                    sw.WriteLine("ClientHost=" + (ClientHost ?? ""));
                    sw.WriteLine("ClientPort=" + ClientPort);
                    sw.WriteLine("ClientPassword=" + (ClientPassword ?? ""));
                    sw.WriteLine("AutoConnect=" + (AutoConnect ? "1" : "0"));
                }
            }
            catch { }
        }
    }
}
