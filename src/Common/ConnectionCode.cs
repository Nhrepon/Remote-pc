using System;
using System.Globalization;
using System.Text;

namespace RemotePC.Common
{
    /// <summary>
    /// One-paste connection details: address, port and PIN in a single string.
    ///
    /// The host's "Copy Code" button builds <c>RPC1|host|port|pin</c>. The client accepts
    /// that, plain host names, <c>host:port</c> and bracketed IPv6 endpoints, so the user
    /// never has to type three separate fields (and never mistypes an IPv6 address).
    /// </summary>
    public static class ConnectionCode
    {
        private const string Prefix = "RPC1";

        public static string Create(string host, int port, string pin)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Prefix).Append('|');
            sb.Append(host == null ? "" : host.Trim()).Append('|');
            sb.Append(port.ToString(CultureInfo.InvariantCulture)).Append('|');
            sb.Append(pin == null ? "" : pin.Trim());
            return sb.ToString();
        }

        /// <summary>
        /// Parses any supported address form. <paramref name="port"/> is 0 and
        /// <paramref name="pin"/> is null when the input did not carry them.
        /// </summary>
        public static bool TryParse(string text, out string host, out int port, out string pin)
        {
            host = null;
            port = 0;
            pin = null;

            if (string.IsNullOrEmpty(text)) return false;

            string value = text.Trim().Trim('"', '\'');
            if (value.Length == 0) return false;

            // Full connection code: RPC1|host|port|pin
            if (value.StartsWith(Prefix + "|", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = value.Split('|');
                if (parts.Length < 2) return false;

                host = parts[1].Trim();
                if (parts.Length >= 3)
                {
                    int parsedPort;
                    if (int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedPort))
                        port = parsedPort;
                }
                if (parts.Length >= 4) pin = parts[3].Trim();

                return host.Length > 0;
            }

            // Bracketed IPv6: [2001:db8::1]:8888
            if (value[0] == '[')
            {
                int close = value.IndexOf(']');
                if (close < 0) return false;

                host = value.Substring(1, close - 1).Trim();
                if (close + 1 < value.Length && value[close + 1] == ':')
                {
                    int parsedPort;
                    if (int.TryParse(value.Substring(close + 2).Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out parsedPort))
                        port = parsedPort;
                }
                return host.Length > 0;
            }

            // host:port (exactly one colon; raw IPv6 has several and is handled below)
            int firstColon = value.IndexOf(':');
            int lastColon = value.LastIndexOf(':');
            if (firstColon > 0 && firstColon == lastColon)
            {
                string candidateHost = value.Substring(0, firstColon).Trim();
                int parsedPort;
                if (int.TryParse(value.Substring(firstColon + 1).Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out parsedPort))
                {
                    host = candidateHost;
                    port = parsedPort;
                    return host.Length > 0;
                }
            }

            // Plain IPv4 address, host name, or unbracketed IPv6 address.
            host = value;
            return true;
        }

        /// <summary>Formats an address and port, bracketing IPv6 as required by most APIs.</summary>
        public static string FormatEndpoint(string host, int port)
        {
            string value = host == null ? "" : host.Trim();
            if (value.IndexOf(':') >= 0 && !value.StartsWith("[", StringComparison.Ordinal))
                value = "[" + value + "]";
            return value + ":" + port.ToString(CultureInfo.InvariantCulture);
        }
    }
}
