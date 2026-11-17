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
                            string ipStr = addr.Address.ToString();
                            bool isTailscaleByName = ni.Description.IndexOf("Tailscale", StringComparison.OrdinalIgnoreCase) >= 0
                                || ni.Name.IndexOf("Tailscale", StringComparison.OrdinalIgnoreCase) >= 0;
                            bool isTailscaleByIp = false;
                            if (ipStr.StartsWith("100."))
                            {
                                // Tailscale borrows the 100.64.0.0/10 carrier-grade NAT range for its
                                // own nodes; a 100.x address outside that range is not Tailscale.
                                string[] octets = ipStr.Split('.');
                                if (octets.Length == 4)
                                {
                                    int second;
                                    if (int.TryParse(octets[1], out second))
                                    {
                                        isTailscaleByIp = second >= 64 && second <= 127;
                                    }
                                }
                            }
                            bool isTailscale = isTailscaleByName || isTailscaleByIp;

                            info.Name = isTailscale ? ("★ " + ni.Name + " (Internet/Tailscale)") : ni.Name;
                            info.Description = ni.Description;
                            info.IpAddress = ipStr;

                            if (!ContainsAddress(list, ipStr))
                            {
                                if (isTailscale)
                                {
                                    list.Insert(0, info); // Place Tailscale right at the top
                                }
                                else
                                {
                                    list.Add(info);
                                }
                            }
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
                            if (ContainsAddress(list, addresses[i].ToString())) continue;
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

        /// <summary>
        /// Globally routable IPv6 addresses on this PC. Many mobile hotspots and modern home
        /// ISPs hand out a public IPv6 address that accepts inbound connections without any
        /// router port forwarding, which is the closest thing to zero-configuration remote
        /// access that exists without a relay server.
        /// </summary>
        public static List<NetworkAdapterInfo> GetLocalIPv6Addresses()
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
                    // Tunnel adapters expose transient Teredo/ISATAP addresses that are not
                    // usable for a direct inbound connection.
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                    IPInterfaceProperties ipProps = ni.GetIPProperties();
                    for (int j = 0; j < ipProps.UnicastAddresses.Count; j++)
                    {
                        IPAddress address = ipProps.UnicastAddresses[j].Address;
                        if (address.AddressFamily != AddressFamily.InterNetworkV6) continue;
                        if (IPAddress.IsLoopback(address)) continue;

                        byte[] bytes = address.GetAddressBytes();
                        if (bytes.Length < 16) continue;
                        // Skip link-local (fe80::/10) and site-local (fec0::/10).
                        if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80) continue;
                        if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0xc0) continue;

                        bool isTailscale = bytes[0] == 0xfd && bytes[1] == 0x7a
                            && bytes[2] == 0x11 && bytes[3] == 0x5c;
                        bool isGlobal = (bytes[0] & 0xe0) == 0x20;

                        // Keep globally routable addresses and the Tailscale ULA, which is
                        // reachable from anywhere on the same Tailscale account.
                        if (!isGlobal && !isTailscale) continue;

                        string ipStr = address.ToString();
                        if (ContainsAddress(list, ipStr)) continue;

                        NetworkAdapterInfo info = new NetworkAdapterInfo();
                        info.Name = isTailscale ? "★ Tailscale IPv6" : "★ IPv6 (direct)";
                        info.Description = ni.Description + " - try this if both PCs have IPv6";
                        info.IpAddress = ipStr;
                        list.Add(info);
                    }
                }
            }
            catch { }

            return list;
        }

        /// <summary>
        /// Every address worth offering on the Host tab: IPv6 first (works over the internet
        /// on many networks), then LAN/Tailscale IPv4.
        /// </summary>
        public static List<NetworkAdapterInfo> GetLocalAddressesForRemoteAccess()
        {
            List<NetworkAdapterInfo> list = new List<NetworkAdapterInfo>();
            list.AddRange(GetLocalIPv6Addresses());
            list.AddRange(GetLocalIPv4Addresses());
            return list;
        }

        private static bool ContainsAddress(List<NetworkAdapterInfo> list, string address)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].IpAddress, address, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
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
