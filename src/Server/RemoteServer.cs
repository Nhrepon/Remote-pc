using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RemotePC.Server
{
    public class RemoteServer
    {
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _isRunning;
        private readonly List<ServerSession> _sessions = new List<ServerSession>();
        private readonly object _sessionsLock = new object();

        public int Port { get; private set; }
        public string Password { get; set; }
        public bool AllowControl { get; set; }
        public bool AllowFileTransfer { get; set; }
        public bool IsRunning { get { return _isRunning; } }

        public event Action<string> OnLog;
        public event Action<bool> OnStatusChanged;
        public event Action<ServerSession> OnClientConnected;
        public event Action<ServerSession> OnClientDisconnected;
        public event Action<ServerSession, string> OnChatMessageReceived;

        public RemoteServer()
        {
            Port = 8888;
            Password = "";
            AllowControl = true;
            AllowFileTransfer = true;
        }

        public void Start(int port)
        {
            if (_isRunning) return;

            Port = port;
            try
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Start(10);
                _isRunning = true;

                _acceptThread = new Thread(AcceptLoop);
                _acceptThread.IsBackground = true;
                _acceptThread.Name = "RemoteServer_AcceptThread";
                _acceptThread.Start();

                Log("Server started on port " + Port + ". Waiting for connections...");
                if (OnStatusChanged != null) OnStatusChanged(true);
            }
            catch (Exception ex)
            {
                Log("Failed to start server: " + ex.Message);
                Stop();
                throw;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;

            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                }
            }
            catch { }

            lock (_sessionsLock)
            {
                for (int i = 0; i < _sessions.Count; i++)
                {
                    _sessions[i].Stop();
                }
                _sessions.Clear();
            }

            Log("Server stopped.");
            if (OnStatusChanged != null) OnStatusChanged(false);
        }

        public void BroadcastChat(string sender, string message)
        {
            lock (_sessionsLock)
            {
                for (int i = 0; i < _sessions.Count; i++)
                {
                    if (_sessions[i].IsAuthenticated)
                    {
                        _sessions[i].SendChat(sender, message);
                    }
                }
            }
        }

        private void AcceptLoop()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    client.NoDelay = true; // Disable Nagle's algorithm for instant streaming
                    client.ReceiveBufferSize = 256 * 1024;
                    client.SendBufferSize = 256 * 1024;

                    ServerSession session = new ServerSession(client, Password, AllowControl, AllowFileTransfer, Log);
                    session.OnAuthenticated += HandleSessionAuthenticated;
                    session.OnDisconnected += HandleSessionDisconnected;
                    session.OnChatMessage += HandleSessionChat;

                    lock (_sessionsLock)
                    {
                        _sessions.Add(session);
                    }

                    session.Start();
                }
                catch (SocketException)
                {
                    // Occurs when listener is stopped
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Log("AcceptLoop error: " + ex.Message);
                    }
                }
            }
        }

        private void HandleSessionAuthenticated(ServerSession session)
        {
            if (OnClientConnected != null)
            {
                OnClientConnected(session);
            }
        }

        private void HandleSessionDisconnected(ServerSession session)
        {
            lock (_sessionsLock)
            {
                _sessions.Remove(session);
            }

            Log("Client disconnected: " + session.ClientEndPoint);

            if (OnClientDisconnected != null)
            {
                OnClientDisconnected(session);
            }
        }

        private void HandleSessionChat(ServerSession session, string message)
        {
            if (OnChatMessageReceived != null)
            {
                OnChatMessageReceived(session, message);
            }
        }

        private void Log(string msg)
        {
            if (OnLog != null)
            {
                OnLog(string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), msg));
            }
        }
    }
}
