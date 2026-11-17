using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;

namespace RemotePC.Common
{
    /// <summary>
    /// UPnP IGD port mapping with automatic renewal.
    ///
    /// The original implementation created a permanent mapping once and forgot about it.
    /// On a laptop that is exactly the wrong behaviour: every Wi-Fi switch or router reboot
    /// silently invalidates the mapping while the UI still shows the old public address.
    /// This version rediscovers the active gateway, re-adds the mapping every ten minutes,
    /// and removes it when the server stops.
    /// </summary>
    public static class UPnPPortForwarder
    {
        private class UpnpService
        {
            public string ControlUrl;
            public string ServiceType;
            public string LocationUrl;
        }

        private static readonly string[] DiscoveryTargets = new string[]
        {
            "urn:schemas-upnp-org:device:InternetGatewayDevice:1",
            "urn:schemas-upnp-org:device:InternetGatewayDevice:2",
            "upnp:rootdevice"
        };

        private const int RenewalIntervalMs = 10 * 60 * 1000;

        private static readonly object _stateLock = new object();
        private static int _generation;
        private static volatile bool _renewing;
        private static UpnpService _service;
        private static int _mappedPort;
        private static string _localIp;

        /// <summary>Public address the router reports, valid once a mapping has succeeded.</summary>
        public static string ExternalPublicIp { get; private set; }

        /// <summary>True while a mapping for <see cref="MappedPort"/> is believed to be live.</summary>
        public static bool IsPortMapped { get; private set; }

        /// <summary>The external port that was mapped (normally the server port).</summary>
        public static int MappedPort { get; private set; }

        /// <summary>Raised whenever the router's public address changes.</summary>
        public static event Action<string> PublicIpChanged;

        /// <summary>
        /// Creates a TCP mapping for <paramref name="port"/> and keeps it alive in the
        /// background until <see cref="Stop"/> or another network change supersedes it.
        /// </summary>
        public static void ForwardPortAsync(int port, Action<bool, string> onComplete)
        {
            int generation = Interlocked.Increment(ref _generation);
            _renewing = true;

            ThreadPool.QueueUserWorkItem(delegate
            {
                string message;
                bool ok = Establish(port, generation, out message);

                // A newer request (usually a network change) took over while we were working.
                if (generation != _generation) return;

                if (onComplete != null) onComplete(ok, message);
                if (!ok) return;

                int consecutiveFailures = 0;
                while (_renewing && generation == _generation)
                {
                    if (!SleepInterruptible(RenewalIntervalMs, generation)) break;
                    if (generation != _generation) break;

                    string renewMessage;
                    bool renewed = Establish(port, generation, out renewMessage);

                    if (renewed)
                    {
                        consecutiveFailures = 0;
                    }
                    else
                    {
                        consecutiveFailures++;
                        // A single failed renewal is usually a router hiccup; don't overwrite
                        // the known-good address until it has failed repeatedly.
                        if (consecutiveFailures >= 3 && generation == _generation)
                        {
                            lock (_stateLock) { IsPortMapped = false; }
                        }
                    }
                }
            });
        }

        /// <summary>Removes the mapping (best effort) and stops the renewal loop.</summary>
        public static void Stop()
        {
            Interlocked.Increment(ref _generation);
            _renewing = false;

            UpnpService service;
            int port;
            lock (_stateLock)
            {
                service = _service;
                port = _mappedPort;
                _service = null;
                _mappedPort = 0;
                _localIp = null;
                IsPortMapped = false;
                MappedPort = 0;
                ExternalPublicIp = null;
            }

            if (service != null)
            {
                string error;
                DeletePortMapping(service, port, out error);
            }
        }

        private static bool Establish(int port, int generation, out string message)
        {
            message = null;

            string discoveryError;
            List<UpnpService> services = DiscoverServices(out discoveryError);
            if (services.Count == 0)
            {
                message = string.IsNullOrEmpty(discoveryError)
                    ? "No UPnP router discovered on network."
                    : discoveryError;
                return false;
            }

            string localIp = GetLocalIpTowardsGateway();
            UpnpService working = null;
            string lastError = null;

            for (int i = 0; i < services.Count; i++)
            {
                string error;
                if (AddPortMapping(services[i], port, localIp, out error))
                {
                    working = services[i];
                    break;
                }
                lastError = error;
            }

            if (working == null)
            {
                message = string.IsNullOrEmpty(lastError)
                    ? "Router did not accept UPnP port mapping."
                    : lastError;
                return false;
            }

            string externalIp = GetExternalIpAddress(working);
            bool changed;
            lock (_stateLock)
            {
                if (generation != _generation)
                {
                    message = "Superseded by a newer network request.";
                    return false;
                }

                changed = !string.Equals(ExternalPublicIp, externalIp, StringComparison.OrdinalIgnoreCase);
                _service = working;
                _mappedPort = port;
                _localIp = localIp;
                MappedPort = port;
                IsPortMapped = true;
                ExternalPublicIp = externalIp;
            }

            if (changed && !string.IsNullOrEmpty(externalIp) && PublicIpChanged != null)
            {
                try { PublicIpChanged(externalIp); }
                catch { }
            }

            message = string.IsNullOrEmpty(externalIp)
                ? "UPnP mapping is live, but the router did not report its public IP."
                : string.Format("UPnP mapping live. Public address: {0}:{1}", externalIp, port);

            return true;
        }

        private static List<UpnpService> DiscoverServices(out string error)
        {
            error = null;
            List<string> locations = new List<string>();

            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                    socket.Bind(new IPEndPoint(IPAddress.Any, 0));

                    IPEndPoint ssdp = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
                    for (int i = 0; i < DiscoveryTargets.Length; i++)
                    {
                        string request =
                            "M-SEARCH * HTTP/1.1\r\n" +
                            "HOST: 239.255.255.250:1900\r\n" +
                            "ST: " + DiscoveryTargets[i] + "\r\n" +
                            "MAN: \"ssdp:discover\"\r\n" +
                            "MX: 2\r\n\r\n";
                        byte[] data = Encoding.ASCII.GetBytes(request);
                        try { socket.SendTo(data, ssdp); }
                        catch { }
                    }

                    DateTime deadline = DateTime.UtcNow.AddMilliseconds(2500);
                    byte[] buffer = new byte[8192];

                    while (DateTime.UtcNow < deadline)
                    {
                        int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                        if (remaining <= 0) break;
                        socket.ReceiveTimeout = Math.Max(1, remaining);

                        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                        int received;
                        try
                        {
                            received = socket.ReceiveFrom(buffer, ref from);
                        }
                        catch (SocketException)
                        {
                            break;
                        }

                        if (received <= 0) continue;

                        string response = Encoding.ASCII.GetString(buffer, 0, received);
                        string location = ExtractHeader(response, "LOCATION");
                        if (!string.IsNullOrEmpty(location) && !locations.Contains(location))
                        {
                            locations.Add(location);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                error = "UPnP discovery failed: " + ex.Message;
                return new List<UpnpService>();
            }

            if (locations.Count == 0)
            {
                error = "No UPnP router discovered on network.";
                return new List<UpnpService>();
            }

            List<UpnpService> result = new List<UpnpService>();
            for (int i = 0; i < locations.Count; i++)
            {
                UpnpService service = ReadServiceDescription(locations[i]);
                if (service != null) result.Add(service);
            }

            if (result.Count == 0)
            {
                error = "UPnP router found, but it exposes no WAN connection service.";
            }

            return result;
        }

        private static string ExtractHeader(string response, string headerName)
        {
            if (string.IsNullOrEmpty(response)) return null;

            string[] lines = response.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith(headerName + ":", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(headerName.Length + 1).Trim();
                }
            }

            return null;
        }

        private static UpnpService ReadServiceDescription(string locationUrl)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(locationUrl);
                request.Timeout = 4000;
                request.ReadWriteTimeout = 4000;
                request.KeepAlive = false;
                request.Proxy = null; // the gateway is on the LAN; a web proxy would break this

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    XmlDocument document = new XmlDocument();
                    document.LoadXml(reader.ReadToEnd());

                    XmlNodeList services = document.GetElementsByTagName("service");
                    for (int i = 0; i < services.Count; i++)
                    {
                        XmlNode service = services[i];
                        string serviceType = service["serviceType"] != null
                            ? service["serviceType"].InnerText
                            : "";
                        if (serviceType.IndexOf("WANIPConnection", StringComparison.OrdinalIgnoreCase) < 0 &&
                            serviceType.IndexOf("WANPPPConnection", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        string controlUrl = service["controlURL"] != null
                            ? service["controlURL"].InnerText
                            : "";
                        if (string.IsNullOrEmpty(controlUrl)) continue;

                        if (!controlUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            controlUrl = new Uri(new Uri(locationUrl), controlUrl).ToString();
                        }

                        UpnpService result = new UpnpService();
                        result.ControlUrl = controlUrl;
                        result.ServiceType = serviceType;
                        result.LocationUrl = locationUrl;
                        return result;
                    }
                }
            }
            catch { }

            return null;
        }

        private static bool AddPortMapping(UpnpService service, int port, string localIp, out string error)
        {
            error = null;

            string body = BuildActionBody(service.ServiceType, "AddPortMapping", new string[,]
            {
                { "NewRemoteHost", "" },
                { "NewExternalPort", port.ToString() },
                { "NewProtocol", "TCP" },
                { "NewInternalPort", port.ToString() },
                { "NewInternalClient", localIp },
                { "NewEnabled", "1" },
                { "NewPortMappingDescription", "RemotePC" },
                { "NewLeaseDuration", "0" }
            });

            string response;
            if (SoapRequest(service, "AddPortMapping", body, out response, out error)) return true;

            // Error 718 means the router already has an entry for this port. Remove the stale
            // entry once and retry; this is common after a previous run was killed.
            if (!string.IsNullOrEmpty(error) && error.IndexOf("718", StringComparison.Ordinal) >= 0)
            {
                string deleteError;
                DeletePortMapping(service, port, out deleteError);
                if (SoapRequest(service, "AddPortMapping", body, out response, out error)) return true;
            }

            return false;
        }

        private static bool DeletePortMapping(UpnpService service, int port, out string error)
        {
            error = null;
            if (service == null || port <= 0) return false;

            string body = BuildActionBody(service.ServiceType, "DeletePortMapping", new string[,]
            {
                { "NewRemoteHost", "" },
                { "NewExternalPort", port.ToString() },
                { "NewProtocol", "TCP" }
            });

            string response;
            return SoapRequest(service, "DeletePortMapping", body, out response, out error);
        }

        private static string GetExternalIpAddress(UpnpService service)
        {
            string body = BuildActionBody(service.ServiceType, "GetExternalIPAddress", new string[0, 0]);
            string response;
            string error;
            if (!SoapRequest(service, "GetExternalIPAddress", body, out response, out error)) return null;
            if (string.IsNullOrEmpty(response)) return null;

            int start = response.IndexOf("<NewExternalIPAddress", StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;

            int open = response.IndexOf('>', start);
            if (open < 0) return null;

            int close = response.IndexOf("</NewExternalIPAddress>", open, StringComparison.OrdinalIgnoreCase);
            if (close <= open) return null;

            string address = response.Substring(open + 1, close - open - 1).Trim();
            return address.Length == 0 ? null : address;
        }

        private static bool SoapRequest(UpnpService service, string action, string body,
            out string response, out string error)
        {
            response = null;
            error = null;

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(body);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(service.ControlUrl);
                request.Method = "POST";
                request.ContentType = "text/xml; charset=\"utf-8\"";
                request.Headers.Add("SOAPAction", "\"" + service.ServiceType + "#" + action + "\"");
                request.ContentLength = bytes.Length;
                request.Timeout = 5000;
                request.ReadWriteTimeout = 5000;
                request.KeepAlive = false;
                request.Proxy = null;

                using (Stream stream = request.GetRequestStream())
                    stream.Write(bytes, 0, bytes.Length);

                using (HttpWebResponse httpResponse = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(httpResponse.GetResponseStream(), Encoding.UTF8))
                {
                    response = reader.ReadToEnd();
                }

                return true;
            }
            catch (WebException ex)
            {
                HttpWebResponse httpResponse = ex.Response as HttpWebResponse;
                if (httpResponse != null)
                {
                    try
                    {
                        using (StreamReader reader = new StreamReader(httpResponse.GetResponseStream(), Encoding.UTF8))
                        {
                            string errorBody = reader.ReadToEnd();
                            int code = ExtractUpnpErrorCode(errorBody);
                            error = code > 0
                                ? "UPnP error " + code + " (" + DescribeUpnpError(code) + ")"
                                : ex.Message;
                        }
                    }
                    catch
                    {
                        error = ex.Message;
                    }
                    finally
                    {
                        httpResponse.Close();
                    }
                }
                else
                {
                    error = ex.Message;
                }

                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string BuildActionBody(string serviceType, string action, string[,] arguments)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\"?>");
            sb.Append("<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" ");
            sb.Append("s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">");
            sb.Append("<s:Body>");
            sb.Append("<u:").Append(action).Append(" xmlns:u=\"").Append(serviceType).Append("\">");

            int rows = arguments.GetLength(0);
            for (int i = 0; i < rows; i++)
            {
                string name = arguments[i, 0];
                string value = arguments[i, 1];
                sb.Append('<').Append(name).Append('>');
                sb.Append(EscapeXml(value));
                sb.Append("</").Append(name).Append('>');
            }

            sb.Append("</u:").Append(action).Append(">");
            sb.Append("</s:Body>");
            sb.Append("</s:Envelope>");
            return sb.ToString();
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private static int ExtractUpnpErrorCode(string body)
        {
            if (string.IsNullOrEmpty(body)) return 0;

            int start = body.IndexOf("<errorCode>", StringComparison.OrdinalIgnoreCase);
            if (start < 0) return 0;

            start += 11;
            int end = body.IndexOf("</errorCode>", start, StringComparison.OrdinalIgnoreCase);
            if (end <= start) return 0;

            int code;
            if (int.TryParse(body.Substring(start, end - start).Trim(), out code)) return code;
            return 0;
        }

        private static string DescribeUpnpError(int code)
        {
            switch (code)
            {
                case 401: return "invalid action";
                case 402: return "invalid arguments";
                case 501: return "action failed";
                case 606: return "action not authorised";
                case 714: return "no such entry in array";
                case 718: return "conflict in mapping entry";
                case 724: return "same port values required";
                case 725: return "only permanent leases supported";
                case 727: return "external port cannot be reserved";
                default: return "router rejected the request";
            }
        }

        private static string GetLocalIpTowardsGateway()
        {
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    // UDP connect only selects the route; no packet is sent.
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                    if (endPoint != null) return endPoint.Address.ToString();
                }
            }
            catch { }

            try
            {
                System.Net.NetworkInformation.NetworkInterface[] interfaces =
                    System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < interfaces.Length; i++)
                {
                    System.Net.NetworkInformation.NetworkInterface ni = interfaces[i];
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    foreach (System.Net.NetworkInformation.UnicastIPAddressInformation addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                            !addr.Address.Equals(System.Net.IPAddress.Loopback))
                        {
                            return addr.Address.ToString();
                        }
                    }
                }
            }
            catch { }

            return "127.0.0.1";
        }

        private static bool SleepInterruptible(int totalMs, int generation)
        {
            int slept = 0;
            while (slept < totalMs)
            {
                if (!_renewing || generation != _generation) return false;
                int slice = Math.Min(500, totalMs - slept);
                Thread.Sleep(slice);
                slept += slice;
            }
            return _renewing && generation == _generation;
        }
    }
}
