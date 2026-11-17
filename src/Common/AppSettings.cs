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

        // Network automation options (can be overridden by network.ini next to the exe)
        public string BindAddress { get; set; }
        public bool EnableFirewallAuto { get; set; }
        public bool EnableNatPmp { get; set; }
        public bool EnableUpnp { get; set; }
        public bool EnableDiscovery { get; set; }

        // Optional RustDesk integration (fallback for hard networks)
        public string RustDeskPath { get; set; }
        public string RustDeskId { get; set; }

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

            BindAddress = "";
            EnableFirewallAuto = true;
            EnableNatPmp = true;
            EnableUpnp = true;
            EnableDiscovery = true;

            RustDeskPath = "";
            RustDeskId = "";

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

        /// <summary>
        /// Reads a simple key=value network configuration file (network.ini) and overrides
        /// the matching settings. Missing keys keep their current values, so a partial
        /// file is valid.
        /// </summary>
        public static void ApplyNetworkConfigFile(AppSettings settings, string path)
        {
            if (settings == null || string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    if (key.Equals("BindAddress", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("HostBindAddress", StringComparison.OrdinalIgnoreCase))
                        settings.BindAddress = val;
                    else if (key.Equals("ServerPort", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("HostPort", StringComparison.OrdinalIgnoreCase))
                    { int p; if (int.TryParse(val, out p)) settings.ServerPort = p; }
                    else if (key.Equals("Password", StringComparison.OrdinalIgnoreCase) ||
                             key.Equals("HostPassword", StringComparison.OrdinalIgnoreCase))
                        settings.Password = val;
                    else if (key.Equals("AutoStartServer", StringComparison.OrdinalIgnoreCase))
                        settings.AutoStartServer = ParseBool(val);
                    else if (key.Equals("EnableFirewallAuto", StringComparison.OrdinalIgnoreCase))
                        settings.EnableFirewallAuto = ParseBool(val);
                    else if (key.Equals("EnableNatPmp", StringComparison.OrdinalIgnoreCase))
                        settings.EnableNatPmp = ParseBool(val);
                    else if (key.Equals("EnableUpnp", StringComparison.OrdinalIgnoreCase))
                        settings.EnableUpnp = ParseBool(val);
                    else if (key.Equals("EnableDiscovery", StringComparison.OrdinalIgnoreCase))
                        settings.EnableDiscovery = ParseBool(val);
                    else if (key.Equals("RustDeskPath", StringComparison.OrdinalIgnoreCase))
                        settings.RustDeskPath = val;
                    else if (key.Equals("RustDeskId", StringComparison.OrdinalIgnoreCase))
                        settings.RustDeskId = val;
                }
            }
            catch { }
        }

        /// <summary>
        /// Creates a default network.ini next to the executable if one is not present, so
        /// users have a documented place to tune network behaviour.
        /// </summary>
        public static void CreateDefaultNetworkConfig(string path)
        {
            if (string.IsNullOrEmpty(path) || File.Exists(path)) return;
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                using (StreamWriter sw = new StreamWriter(path, false))
                {
                    sw.WriteLine("# RemotePC network configuration");
                    sw.WriteLine("# Delete or edit these values; lines starting with # are ignored.");
                    sw.WriteLine("BindAddress=");
                    sw.WriteLine("ServerPort=8888");
                    sw.WriteLine("AutoStartServer=0");
                    sw.WriteLine("EnableFirewallAuto=1");
                    sw.WriteLine("EnableNatPmp=1");
                    sw.WriteLine("EnableUpnp=1");
                    sw.WriteLine("EnableDiscovery=1");
                    sw.WriteLine("RustDeskPath=");
                    sw.WriteLine("RustDeskId=");
                }
            }
            catch { }
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
                    sw.WriteLine("BindAddress=" + (BindAddress ?? ""));
                    sw.WriteLine("EnableFirewallAuto=" + (EnableFirewallAuto ? "1" : "0"));
                    sw.WriteLine("EnableNatPmp=" + (EnableNatPmp ? "1" : "0"));
                    sw.WriteLine("EnableUpnp=" + (EnableUpnp ? "1" : "0"));
                    sw.WriteLine("EnableDiscovery=" + (EnableDiscovery ? "1" : "0"));
                    sw.WriteLine("RustDeskPath=" + (RustDeskPath ?? ""));
                    sw.WriteLine("RustDeskId=" + (RustDeskId ?? ""));
                }
            }
            catch { }
        }
    }
}
