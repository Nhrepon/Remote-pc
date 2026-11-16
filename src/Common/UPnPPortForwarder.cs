using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;

namespace RemotePC.Common
{
    public static class UPnPPortForwarder
    {
        public static string ExternalPublicIp { get; private set; }
        public static bool IsPortMapped { get; private set; }

        public static void ForwardPortAsync(int port, Action<bool, string> onComplete)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // 1. Search for UPnP IGD router via SSDP M-SEARCH
                    string controlUrl = DiscoverUPnPControlUrl();
                    if (string.IsNullOrEmpty(controlUrl))
                    {
                        if (onComplete != null) onComplete(false, "No UPnP router discovered on network.");
                        return;
                    }

                    // 2. Get local IP
                    string localIp = GetLocalIpTowardsGateway();

                    // 3. Add port mapping via SOAP
                    bool mapped = AddPortMapping(controlUrl, port, localIp);
                    string extIp = GetExternalIpAddress(controlUrl);

                    ExternalPublicIp = extIp;
                    IsPortMapped = mapped;

                    if (onComplete != null)
                    {
                        if (mapped)
                        {
                            onComplete(true, string.Format("UPnP succeeded! Public IP: {0}:{1}", extIp ?? "Unknown", port));
                        }
                        else
                        {
                            onComplete(false, "Router did not accept UPnP port mapping.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (onComplete != null) onComplete(false, "UPnP error: " + ex.Message);
                }
            });
        }

        private static string DiscoverUPnPControlUrl()
        {
            try
            {
                string req = "M-SEARCH * HTTP/1.1\r\n" +
                             "HOST: 239.255.255.250:1900\r\n" +
                             "ST: urn:schemas-upnp-org:device:InternetGatewayDevice:1\r\n" +
                             "MAN: \"ssdp:discover\"\r\n" +
                             "MX: 2\r\n\r\n";
                byte[] data = Encoding.ASCII.GetBytes(req);

                using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 2000);
                    s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                    s.Bind(new IPEndPoint(IPAddress.Any, 0));
                    s.SendTo(data, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));

                    byte[] buf = new byte[4096];
                    EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    int r = s.ReceiveFrom(buf, ref ep);
                    string resp = Encoding.ASCII.GetString(buf, 0, r);

                    // Extract Location header
                    string[] lines = resp.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    string location = null;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase))
                        {
                            location = lines[i].Substring(9).Trim();
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(location))
                    {
                        return GetControlUrlFromDescription(location);
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetControlUrlFromDescription(string locationUrl)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(locationUrl);
                req.Timeout = 3000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                {
                    string xml = sr.ReadToEnd();
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(xml);

                    XmlNodeList services = doc.GetElementsByTagName("service");
                    for (int i = 0; i < services.Count; i++)
                    {
                        XmlNode s = services[i];
                        string st = s["serviceType"] != null ? s["serviceType"].InnerText : "";
                        if (st.Contains("WANIPConnection") || st.Contains("WANPPPConnection"))
                        {
                            string controlUrl = s["controlURL"] != null ? s["controlURL"].InnerText : "";
                            if (!controlUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                Uri uri = new Uri(locationUrl);
                                controlUrl = new Uri(uri, controlUrl).ToString();
                            }
                            return controlUrl;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool AddPortMapping(string controlUrl, int port, string localIp)
        {
            try
            {
                string soapBody =
                    "<?xml version=\"1.0\"?>" +
                    "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                    "<s:Body>" +
                    "<u:AddPortMapping xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\">" +
                    "<NewRemoteHost></NewRemoteHost>" +
                    "<NewExternalPort>" + port + "</NewExternalPort>" +
                    "<NewProtocol>TCP</NewProtocol>" +
                    "<NewInternalPort>" + port + "</NewInternalPort>" +
                    "<NewInternalClient>" + localIp + "</NewInternalClient>" +
                    "<NewEnabled>1</NewEnabled>" +
                    "<NewPortMappingDescription>RemotePC</NewPortMappingDescription>" +
                    "<NewLeaseDuration>0</NewLeaseDuration>" +
                    "</u:AddPortMapping>" +
                    "</s:Body>" +
                    "</s:Envelope>";

                byte[] bytes = Encoding.UTF8.GetBytes(soapBody);
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(controlUrl);
                req.Method = "POST";
                req.ContentType = "text/xml; charset=\"utf-8\"";
                req.Headers.Add("SOAPAction", "\"urn:schemas-upnp-org:service:WANIPConnection:1#AddPortMapping\"");
                req.ContentLength = bytes.Length;
                req.Timeout = 4000;

                using (Stream st = req.GetRequestStream())
                {
                    st.Write(bytes, 0, bytes.Length);
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    return resp.StatusCode == HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string GetExternalIpAddress(string controlUrl)
        {
            try
            {
                string soapBody =
                    "<?xml version=\"1.0\"?>" +
                    "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                    "<s:Body>" +
                    "<u:GetExternalIPAddress xmlns:u=\"urn:schemas-upnp-org:service:WANIPConnection:1\" />" +
                    "</s:Body>" +
                    "</s:Envelope>";

                byte[] bytes = Encoding.UTF8.GetBytes(soapBody);
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(controlUrl);
                req.Method = "POST";
                req.ContentType = "text/xml; charset=\"utf-8\"";
                req.Headers.Add("SOAPAction", "\"urn:schemas-upnp-org:service:WANIPConnection:1#GetExternalIPAddress\"");
                req.ContentLength = bytes.Length;
                req.Timeout = 4000;

                using (Stream st = req.GetRequestStream())
                {
                    st.Write(bytes, 0, bytes.Length);
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                {
                    string xml = sr.ReadToEnd();
                    int start = xml.IndexOf("<NewExternalIPAddress>");
                    int end = xml.IndexOf("</NewExternalIPAddress>");
                    if (start > 0 && end > start)
                    {
                        start += 22;
                        return xml.Substring(start, end - start);
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetLocalIpTowardsGateway()
        {
            try
            {
                using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    s.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = s.LocalEndPoint as IPEndPoint;
                    if (endPoint != null) return endPoint.Address.ToString();
                }
            }
            catch { }
            return "127.0.0.1";
        }
    }
}
