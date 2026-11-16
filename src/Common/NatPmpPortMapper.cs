using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace RemotePC.Common
{
    /// <summary>
    /// NAT-PMP client (RFC 6886) for automatic router port forwarding.
    ///
    /// Many consumer routers answer NAT-PMP even when their UPnP IGD service is switched off
    /// entirely, which is exactly the case on a network where the SSDP M-SEARCH returns
    /// nothing and yet a mapping can still be created. Trying this protocol before falling
    /// back to UPnP is what makes hands-free internet access work on those routers.
    /// </summary>
    public static class NatPmpPortMapper
    {
        private const int NatPmpPort = 5351;
        private const byte ProcVersion = 0;
        private const byte OpcodeExternalAddress = 0;
        private const byte OpcodeMapTcp = 2;

        // RFC 6886 recommended retry schedule: 250 ms, 500 ms, 1000 ms.
        private static readonly int[] RetryTimeoutsMs = new int[] { 250, 500, 1000 };

        /// <summary>Public address the router reports, valid once a mapping has succeeded.</summary>
        public static string ExternalPublicIp { get; private set; }

        /// <summary>True while a mapping for <see cref="MappedPort"/> is believed to be live.</summary>
        public static bool IsMapped { get; private set; }

        /// <summary>Lease length the router granted, in seconds.</summary>
        public static int LifetimeSeconds { get; private set; }

        public static int MappedPort { get; private set; }

        /// <summary>The external port the router finally granted (may differ from the request).</summary>
        public static int ExternalPort { get; private set; }

        /// <summary>Raised whenever the router's public address changes.</summary>
        public static event Action<string> PublicIpChanged;

        private static volatile bool _renewing;
        private static int _generation;
        private static readonly object _stateLock = new object();

        /// <summary>
        /// Asks the gateway for a TCP mapping of <paramref name="port"/> to this machine, then
        /// keeps it alive in the background until <see cref="Unmap"/> is called. The callback
        /// runs on a worker thread.
        /// </summary>
        public static void MapAsync(int port, Action<bool, string> onComplete)
        {
            int generation = Interlocked.Increment(ref _generation);
            _renewing = true;

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                bool ok = TryMap(port, generation, out error);

                // A newer request (normally a network change) superseded this one.
                if (generation != _generation) return;

                if (onComplete != null) onComplete(ok, error);

                if (!ok) return;

                // Renew at half the lease so the mapping never lapses between renewals.
                int intervalMs = Math.Max(30, LifetimeSeconds / 2) * 1000;

                while (_renewing && generation == _generation)
                {
                    if (!SleepInterruptible(intervalMs, generation)) break;
                    if (!_renewing || generation != _generation) break;

                    string renewError;
                    if (!TryMap(port, generation, out renewError))
                    {
                        // A single failed renewal is not fatal; the loop retries next round.
                    }
                }
            });
        }

        /// <summary>Cancels renewal and removes the mapping from the router.</summary>
        public static void Unmap()
        {
            // Invalidate any renewal loop that is still running.
            Interlocked.Increment(ref _generation);
            _renewing = false;

            int port;
            lock (_stateLock)
            {
                if (!IsMapped) return;
                port = MappedPort;
                IsMapped = false;
                MappedPort = 0;
                ExternalPort = 0;
            }

            try
            {
                IPAddress gateway = GetGateway();
                if (gateway == null) return;

                // A mapping request with lifetime 0 is the documented delete operation.
                byte[] request = BuildMapRequest(port, port, 0);
                Transact(gateway, request);
            }
            catch { }
        }

        private static bool TryMap(int port, int generation, out string error)
        {
            error = null;

            IPAddress gateway = GetGateway();
            if (gateway == null)
            {
                error = "No default gateway found.";
                return false;
            }

            // 1. Confirm the router speaks NAT-PMP before asking it to forward anything.
            byte[] externalResponse = Transact(gateway, new byte[] { ProcVersion, OpcodeExternalAddress });

            if (externalResponse == null)
            {
                error = "Router did not answer NAT-PMP.";
                return false;
            }
            if (externalResponse.Length < 12)
            {
                error = "NAT-PMP reply was too short.";
                return false;
            }
            if (externalResponse[1] != OpcodeExternalAddress + 128)
            {
                error = "Router does not support NAT-PMP.";
                return false;
            }

            int externalResult = ReadUInt16(externalResponse, 2);
            if (externalResult != 0)
            {
                error = "NAT-PMP external address request refused (" + DescribeResult(externalResult) + ").";
                return false;
            }

            string publicIp = string.Format("{0}.{1}.{2}.{3}",
                externalResponse[8], externalResponse[9], externalResponse[10], externalResponse[11]);

            // 2. Ask for the TCP mapping, with a one hour lease.
            const int requestedLifetime = 3600;
            byte[] mapResponse = Transact(gateway, BuildMapRequest(port, port, requestedLifetime));

            if (mapResponse == null)
            {
                error = "Router did not answer the NAT-PMP mapping request.";
                return false;
            }
            if (mapResponse.Length < 16)
            {
                error = "NAT-PMP mapping reply was too short.";
                return false;
            }
            if (mapResponse[1] != OpcodeMapTcp + 128)
            {
                error = "Router rejected the mapping request.";
                return false;
            }

            int mapResult = ReadUInt16(mapResponse, 2);
            if (mapResult != 0)
            {
                error = "Router refused the port mapping (" + DescribeResult(mapResult) + ").";
                return false;
            }

            int grantedLifetime = ReadInt32(mapResponse, 12);
            int grantedExternalPort = ReadUInt16(mapResponse, 10);

            // A router is allowed to hand back a different external port; that changes the
            // address the other PC must dial, so surface it rather than assuming.
            if (grantedExternalPort != port)
            {
                error = string.Format(
                    "Router mapped external port {0} instead of {1}; the other PC must use {2}.",
                    grantedExternalPort, port, grantedExternalPort);
            }

            bool changed;
            lock (_stateLock)
            {
                if (generation != _generation)
                {
                    error = "Superseded by a newer network request.";
                    return false;
                }

                changed = !string.Equals(ExternalPublicIp, publicIp, StringComparison.OrdinalIgnoreCase);
                ExternalPublicIp = publicIp;
                IsMapped = true;
                MappedPort = grantedExternalPort > 0 ? grantedExternalPort : port;
                ExternalPort = MappedPort;
                LifetimeSeconds = grantedLifetime > 0 ? grantedLifetime : requestedLifetime;
            }

            if (changed && PublicIpChanged != null)
            {
                try { PublicIpChanged(publicIp); }
                catch { }
            }

            if (error == null)
            {
                error = string.Format(
                    "Port {0} forward is live. Public address: {1}:{0} (lease {2}s).",
                    MappedPort, publicIp, LifetimeSeconds);
            }

            return true;
        }

        private static byte[] BuildMapRequest(int externalPort, int internalPort, int lifetimeSeconds)
        {
            byte[] request = new byte[12];
            request[0] = ProcVersion;
            request[1] = OpcodeMapTcp;
            WriteUInt16(request, 4, internalPort);
            WriteUInt16(request, 6, externalPort);
            WriteInt32(request, 8, lifetimeSeconds);
            return request;
        }

        /// <summary>
        /// Sends one NAT-PMP request and returns the reply, or null if the router stays silent
        /// through the whole retry schedule.
        /// </summary>
        private static byte[] Transact(IPAddress gateway, byte[] request)
        {
            for (int attempt = 0; attempt < RetryTimeoutsMs.Length; attempt++)
            {
                try
                {
                    using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    {
                        s.ReceiveTimeout = RetryTimeoutsMs[attempt];
                        s.Bind(new IPEndPoint(IPAddress.Any, 0));
                        s.SendTo(request, new IPEndPoint(gateway, NatPmpPort));

                        byte[] buffer = new byte[32];
                        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                        int received = s.ReceiveFrom(buffer, ref from);

                        if (received > 0)
                        {
                            byte[] reply = new byte[received];
                            Array.Copy(buffer, reply, received);
                            return reply;
                        }
                    }
                }
                catch
                {
                    // Timeout or transient socket error - fall through to the next attempt.
                }
            }

            return null;
        }

        /// <summary>The IPv4 default gateway of the active adapter, or null.</summary>
        public static IPAddress GetGateway()
        {
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < interfaces.Length; i++)
                {
                    NetworkInterface ni = interfaces[i];
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    GatewayIPAddressInformationCollection gateways = ni.GetIPProperties().GatewayAddresses;
                    for (int j = 0; j < gateways.Count; j++)
                    {
                        IPAddress address = gateways[j].Address;
                        if (address == null) continue;
                        if (address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (address.Equals(IPAddress.Any)) continue;
                        return address;
                    }
                }
            }
            catch { }

            return null;
        }

        private static string DescribeResult(int code)
        {
            switch (code)
            {
                case 0: return "success";
                case 1: return "unsupported protocol version";
                case 2: return "not authorised - forwarding may be disabled on the router";
                case 3: return "network failure";
                case 4: return "out of resources";
                case 5: return "unsupported opcode";
                default: return "result code " + code;
            }
        }

        private static int ReadUInt16(byte[] buffer, int offset)
        {
            return (buffer[offset] << 8) | buffer[offset + 1];
        }

        private static int ReadInt32(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24) | (buffer[offset + 1] << 16)
                 | (buffer[offset + 2] << 8) | buffer[offset + 3];
        }

        private static void WriteUInt16(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 1] = (byte)(value & 0xFF);
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
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
