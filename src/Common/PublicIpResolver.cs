using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace RemotePC.Common
{
    /// <summary>
    /// Finds the public IPv4 address of the current network connection and classifies
    /// addresses that can never be reached directly from the internet.
    ///
    /// This is the safety net for networks where the router refuses NAT-PMP and UPnP:
    /// the app still needs to show the address the other PC has to dial (or explain
    /// honestly that the network is a carrier-grade NAT and direct access is impossible).
    /// </summary>
    public static class PublicIpResolver
    {
        private static readonly string[] Endpoints = new string[]
        {
            "https://api.ipify.org",
            "https://ifconfig.me/ip",
            "https://icanhazip.com",
            "https://checkip.amazonaws.com"
        };

        private static int _inFlight;
        private static string _lastKnown;

        static PublicIpResolver()
        {
            // Windows 7/8 default to TLS 1.0 for HttpWebRequest; the endpoints above require
            // TLS 1.2. Enabling it here is harmless on newer Windows versions.
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch { }
        }

        /// <summary>Most recently discovered public IPv4 address, or null.</summary>
        public static string LastKnown
        {
            get { return _lastKnown; }
        }

        /// <summary>
        /// Resolves the public IPv4 address on a worker thread and calls
        /// <paramref name="onComplete"/> with the address, or null when every provider failed.
        /// </summary>
        public static void ResolveAsync(Action<string> onComplete)
        {
            if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
            {
                // A lookup is already running; hand back the cached value instead of stacking
                // up HTTP requests on every network event.
                if (onComplete != null) onComplete(_lastKnown);
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                string result = null;
                try
                {
                    result = Resolve();
                    if (!string.IsNullOrEmpty(result)) _lastKnown = result;
                }
                finally
                {
                    Interlocked.Exchange(ref _inFlight, 0);
                }

                if (onComplete != null) onComplete(result);
            });
        }

        private static string Resolve()
        {
            for (int i = 0; i < Endpoints.Length; i++)
            {
                try
                {
                    string body = HttpGet(Endpoints[i]);
                    string ip = FirstToken(body);
                    IPAddress parsed;
                    if (ip != null && IPAddress.TryParse(ip, out parsed) && IsPublicIPv4(parsed))
                    {
                        return parsed.ToString();
                    }
                }
                catch
                {
                    // Try the next provider.
                }
            }

            return null;
        }

        private static string HttpGet(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 4000;
            request.ReadWriteTimeout = 4000;
            request.KeepAlive = false;
            request.UserAgent = "RemotePC";
            request.Proxy = null;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static string FirstToken(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;

            string trimmed = body.Trim();
            int split = trimmed.IndexOfAny(new char[] { ' ', '\t', '\r', '\n', '<' });
            if (split > 0) trimmed = trimmed.Substring(0, split);
            return trimmed.Trim();
        }

        /// <summary>True when the address is a normal internet-routable IPv4 address.</summary>
        public static bool IsPublicIPv4(IPAddress address)
        {
            return address != null
                && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                && !IsPrivateOrShared(address);
        }

        /// <summary>
        /// True for addresses that cannot be reached from the public internet:
        /// RFC1918, carrier-grade NAT (100.64/10), loopback, link-local and multicast.
        /// </summary>
        public static bool IsPrivateOrShared(IPAddress address)
        {
            if (address == null) return true;

            // IPv4-mapped IPv6 addresses (::ffff:a.b.c.d) inherit the reachability of the
            // embedded IPv4 address; classify them the same way.
            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && address.IsIPv4MappedToIPv6)
            {
                return IsPrivateOrShared(address.MapToIPv4());
            }

            byte[] bytes = address.GetAddressBytes();

            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                if (bytes.Length < 4) return true;
                if (bytes[0] == 0 || bytes[0] == 10 || bytes[0] == 127) return true;
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                if (bytes[0] == 169 && bytes[1] == 254) return true;
                // RFC 6598 carrier-grade NAT: the address a home router reports when the ISP
                // has already NATed it. Port mapping on the home router is then useless.
                if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) return true;
                if (bytes[0] >= 224) return true;
                return false;
            }

            if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                if (bytes.Length < 16) return true;
                if (address.Equals(IPAddress.IPv6Loopback)) return true;
                if (address.Equals(IPAddress.IPv6Any)) return true;
                // fe80::/10 link-local, fec0::/10 site-local.
                if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80) return true;
                if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0xc0) return true;
                // fc00::/7 unique-local.
                if ((bytes[0] & 0xfe) == 0xfc) return true;
                // ff00::/8 multicast.
                if (bytes[0] == 0xff) return true;
                return false;
            }

            return true;
        }

        public static bool IsPrivateOrShared(string address)
        {
            IPAddress parsed;
            if (!IPAddress.TryParse(address, out parsed)) return true;
            return IsPrivateOrShared(parsed);
        }

        /// <summary>True for globally routable IPv6 addresses (2000::/3).</summary>
        public static bool IsPublicIPv6(IPAddress address)
        {
            if (address == null || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
                return false;

            if (IsPrivateOrShared(address)) return false;
            byte[] bytes = address.GetAddressBytes();
            return (bytes[0] & 0xe0) == 0x20;
        }
    }
}
