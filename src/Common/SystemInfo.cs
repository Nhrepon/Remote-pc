using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace RemotePC.Common
{
    public class NetworkAdapterInfo
    {
        public string Name { get; set; }
        public string IpAddress { get; set; }
        public string Description { get; set; }

        public override string ToString()
        {
            return string.Format("{0} ({1}) - {2}", IpAddress, Name, Description);
        }
    }

    public static class SystemInfo
    {
        public static List<NetworkAdapterInfo> GetLocalIPv4Addresses()
        {
            List<NetworkAdapterInfo> list = new List<NetworkAdapterInfo>();

            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < interfaces.Length; i++)
                {
                    NetworkInterface ni = interfaces[i];
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    IPInterfaceProperties ipProps = ni.GetIPProperties();
                    for (int j = 0; j < ipProps.UnicastAddresses.Count; j++)
                    {
                        UnicastIPAddressInformation addr = ipProps.UnicastAddresses[j];
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            NetworkAdapterInfo info = new NetworkAdapterInfo();
                            info.Name = ni.Name;
                            info.Description = ni.Description;
                            info.IpAddress = addr.Address.ToString();
                            list.Add(info);
                        }
                    }
                }
            }
            catch
            {
                // Fallback
                try
                {
                    string hostName = Dns.GetHostName();
                    IPAddress[] addresses = Dns.GetHostAddresses(hostName);
                    for (int i = 0; i < addresses.Length; i++)
                    {
                        if (addresses[i].AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addresses[i]))
                        {
                            NetworkAdapterInfo info = new NetworkAdapterInfo();
                            info.Name = "Local Adapter";
                            info.Description = hostName;
                            info.IpAddress = addresses[i].ToString();
                            list.Add(info);
                        }
                    }
                }
                catch { }
            }

            if (list.Count == 0)
            {
                NetworkAdapterInfo info = new NetworkAdapterInfo();
                info.Name = "Loopback";
                info.Description = "Localhost";
                info.IpAddress = "127.0.0.1";
                list.Add(info);
            }

            return list;
        }

        public static string GetSystemUptime()
        {
            try
            {
                TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount & int.MaxValue);
                return string.Format("{0} days, {1} hrs, {2} mins", uptime.Days, uptime.Hours, uptime.Minutes);
            }
            catch
            {
                return "Unknown";
            }
        }

        public static string FormatFileSize(long bytes)
        {
            if (bytes < 0) return "0 B";
            if (bytes < 1024) return string.Format("{0} B", bytes);
            if (bytes < 1024 * 1024) return string.Format("{0:F1} KB", bytes / 1024.0);
            if (bytes < 1024 * 1024 * 1024) return string.Format("{0:F2} MB", bytes / (1024.0 * 1024.0));
            return string.Format("{0:F2} GB", bytes / (1024.0 * 1024.0 * 1024.0));
        }

        public static string GetSystemInfoString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Computer Name: " + Environment.MachineName);
            sb.AppendLine("User Name: " + Environment.UserName);
            sb.AppendLine("OS Version: " + Environment.OSVersion.ToString());
            sb.AppendLine("64-bit OS: " + (Environment.Is64BitOperatingSystem ? "Yes" : "No"));
            sb.AppendLine("Processor Count: " + Environment.ProcessorCount.ToString());
            sb.AppendLine("Screen Resolution: " + Screen.PrimaryScreen.Bounds.Width + " x " + Screen.PrimaryScreen.Bounds.Height);
            sb.AppendLine("System Uptime: " + GetSystemUptime());
            sb.AppendLine(".NET CLR Version: " + Environment.Version.ToString());
            return sb.ToString();
        }
    }
}
