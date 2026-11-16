using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace RemotePC.Common
{
    public class DiscoveredHost
    {
        public string MachineName { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public DateTime LastSeen { get; set; }

        public override string ToString()
        {
            return string.Format("{0} ({1}:{2})", MachineName, IpAddress, Port);
        }
    }

    public static class NetworkDiscovery
    {
        public const int DISCOVERY_PORT = 8889;
        private static Thread _broadcastThread;
        private static volatile bool _isBroadcasting;
        private static int _broadcastServerPort;

        private static Thread _listenThread;
        private static volatile bool _isListening;
        private static Action<DiscoveredHost> _onHostDiscovered;

        public static void StartBroadcasting(int serverPort)
        {
            if (_isBroadcasting) return;
            _broadcastServerPort = serverPort;
            _isBroadcasting = true;

            _broadcastThread = new Thread(BroadcastLoop);
            _broadcastThread.IsBackground = true;
            _broadcastThread.Name = "RemotePC_UDP_Broadcaster";
            _broadcastThread.Start();
        }

        public static void StopBroadcasting()
        {
            _isBroadcasting = false;
        }

        private static void BroadcastLoop()
        {
            UdpClient udp = null;
            try
            {
                udp = new UdpClient();
                udp.EnableBroadcast = true;
                IPEndPoint ep = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);

                while (_isBroadcasting)
                {
                    try
                    {
                        string msg = string.Format("RPC_DISCOVERY|{0}|{1}", Environment.MachineName, _broadcastServerPort);
                        byte[] bytes = Encoding.UTF8.GetBytes(msg);
                        udp.Send(bytes, bytes.Length, ep);
                    }
                    catch { }

                    Thread.Sleep(2500);
                }
            }
            catch { }
            finally
            {
                if (udp != null)
                {
                    try { udp.Close(); } catch { }
                }
            }
        }

        public static void StartListening(Action<DiscoveredHost> onHostDiscovered)
        {
            if (_isListening) return;
            _onHostDiscovered = onHostDiscovered;
            _isListening = true;

            _listenThread = new Thread(ListenLoop);
            _listenThread.IsBackground = true;
            _listenThread.Name = "RemotePC_UDP_Listener";
            _listenThread.Start();
        }

        public static void StopListening()
        {
            _isListening = false;
        }

        private static void ListenLoop()
        {
            UdpClient udp = null;
            try
            {
                udp = new UdpClient();
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, DISCOVERY_PORT));

                while (_isListening)
                {
                    try
                    {
                        IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                        byte[] bytes = udp.Receive(ref from);
                        string msg = Encoding.UTF8.GetString(bytes);

                        string[] parts = msg.Split('|');
                        if (parts.Length >= 3 && parts[0] == "RPC_DISCOVERY")
                        {
                            string machineName = parts[1];
                            int port = 8888;
                            int.TryParse(parts[2], out port);

                            DiscoveredHost host = new DiscoveredHost();
                            host.MachineName = machineName;
                            host.IpAddress = from.Address.ToString();
                            host.Port = port;
                            host.LastSeen = DateTime.Now;

                            if (_onHostDiscovered != null)
                            {
                                _onHostDiscovered(host);
                            }
                        }
                    }
                    catch (SocketException)
                    {
                        if (!_isListening) break;
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                if (udp != null)
                {
                    try { udp.Close(); } catch { }
                }
            }
        }
    }
}
