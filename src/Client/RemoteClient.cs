using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using RemotePC.Common;

namespace RemotePC.Client
{
    public class RemoteClient
    {
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private readonly object _writeLock = new object();
        private Thread _readThread;
        private volatile bool _isConnected;

        private Stopwatch _pingStopwatch = new Stopwatch();
        public int LastPingMs { get; private set; }

        public int RemoteScreenWidth { get; private set; }
        public int RemoteScreenHeight { get; private set; }
        public string HostAddress { get; private set; }
        public int HostPort { get; private set; }
        public bool IsConnected { get { return _isConnected; } }

        // Events
        public event Action<Bitmap, long> OnFrameReceived;
        public event Action<bool, string> OnAuthResult;
        public event Action OnDisconnected;
        public event Action<string> OnLog;
        public event Action<string, string, string> OnChatMessage; // sender, message, time
        public event Action<string> OnFileListReceived;
        public event Action<string, int, int, int> OnDownloadProgress; // filename, currentChunk, totalChunks, percent
        public event Action<string, bool> OnDownloadComplete; // filename, success
        public event Action<string, int, int, int> OnUploadProgress; // filename, currentChunk, totalChunks, percent
        public event Action<string, bool> OnUploadComplete; // filename, success
        public event Action<bool, string> OnFileActionResponse;
        public event Action<string> OnSystemInfoReceived;

        // Download state
        private FileStream _downloadStream;
        private string _downloadFilePath;
        private string _downloadFileName;
        private int _downloadChunksReceived;
        private int _downloadTotalChunks;

        public bool Connect(string host, int port, string password, int timeoutMs)
        {
            try
            {
                HostAddress = host;
                HostPort = port;
                _tcpClient = new TcpClient();
                _tcpClient.NoDelay = true;
                _tcpClient.ReceiveBufferSize = 256 * 1024;
                _tcpClient.SendBufferSize = 256 * 1024;

                IAsyncResult ar = _tcpClient.BeginConnect(host, port, null, null);
                bool success = ar.AsyncWaitHandle.WaitOne(timeoutMs > 0 ? timeoutMs : 5000);
                if (!success || !_tcpClient.Connected)
                {
                    _tcpClient.Close();
                    Log("Connection to " + host + ":" + port + " timed out.");
                    return false;
                }
                _tcpClient.EndConnect(ar);

                _stream = _tcpClient.GetStream();
                _isConnected = true;

                // Send Auth Request
                byte[] authPayload = Protocol.SerializeAuthRequest(password, Environment.MachineName);
                Protocol.SendPacket(_stream, _writeLock, PacketType.AuthRequest, authPayload);

                // Wait for Auth Response synchronously
                PacketType respType;
                byte[] respPayload;
                if (!Protocol.ReceivePacket(_stream, out respType, out respPayload) || respType != PacketType.AuthResponse)
                {
                    Log("Failed to receive authentication response from host.");
                    Disconnect();
                    return false;
                }

                bool authSuccess;
                string authMsg;
                int rWidth, rHeight;
                Protocol.DeserializeAuthResponse(respPayload, out authSuccess, out authMsg, out rWidth, out rHeight);

                if (!authSuccess)
                {
                    Log("Authentication failed: " + authMsg);
                    if (OnAuthResult != null) OnAuthResult(false, authMsg);
                    Disconnect();
                    return false;
                }

                RemoteScreenWidth = rWidth;
                RemoteScreenHeight = rHeight;
                Log(string.Format("Connected & authenticated. Remote Resolution: {0}x{1}", rWidth, rHeight));

                if (OnAuthResult != null) OnAuthResult(true, "Connected successfully");

                // Start background reader
                _readThread = new Thread(ReadLoop);
                _readThread.IsBackground = true;
                _readThread.Name = "RemoteClient_ReadThread";
                _readThread.Start();

                return true;
            }
            catch (Exception ex)
            {
                Log("Connect error: " + ex.Message);
                Disconnect();
                return false;
            }
        }

        public void Disconnect()
        {
            if (!_isConnected && _tcpClient == null) return;
            _isConnected = false;

            try
            {
                if (_stream != null && _stream.CanWrite)
                {
                    Protocol.SendPacket(_stream, _writeLock, PacketType.Disconnect, null);
                }
            }
            catch { }

            try
            {
                if (_stream != null) _stream.Close();
                if (_tcpClient != null) _tcpClient.Close();
            }
            catch { }

            _stream = null;
            _tcpClient = null;

            CloseDownloadStream(false);

            Log("Disconnected from host.");
            if (OnDisconnected != null)
            {
                OnDisconnected();
            }
        }

        private void ReadLoop()
        {
            try
            {
                while (_isConnected)
                {
                    PacketType type;
                    byte[] payload;

                    if (!Protocol.ReceivePacket(_stream, out type, out payload))
                    {
                        break;
                    }

                    ProcessPacket(type, payload);
                }
            }
            catch (Exception ex)
            {
                Log("Client read loop exception: " + ex.Message);
            }
            finally
            {
                Disconnect();
            }
        }

        private void ProcessPacket(PacketType type, byte[] payload)
        {
            switch (type)
            {
                case PacketType.ScreenFrame:
                    HandleScreenFrame(payload);
                    break;

                case PacketType.ChatMessage:
                    string sender, msg, time;
                    Protocol.DeserializeChatMessage(payload, out sender, out msg, out time);
                    if (OnChatMessage != null)
                    {
                        OnChatMessage(sender, msg, time);
                    }
                    break;

                case PacketType.FileListResponse:
                    string fileList = Protocol.DeserializeString(payload);
                    if (OnFileListReceived != null)
                    {
                        OnFileListReceived(fileList);
                    }
                    break;

                case PacketType.FileDownloadHeader:
                    HandleFileDownloadHeader(payload);
                    break;

                case PacketType.FileChunk:
                    HandleFileDownloadChunk(payload);
                    break;

                case PacketType.FileActionResponse:
                    HandleFileActionResponse(payload);
                    break;

                case PacketType.SystemInfoResponse:
                    string sysInfo = Protocol.DeserializeString(payload);
                    if (OnSystemInfoReceived != null)
                    {
                        OnSystemInfoReceived(sysInfo);
                    }
                    break;

                case PacketType.Pong:
                    _pingStopwatch.Stop();
                    LastPingMs = (int)_pingStopwatch.ElapsedMilliseconds;
                    break;
            }
        }

        private void HandleScreenFrame(byte[] payload)
        {
            try
            {
                long frameId;
                int width, height;
                byte[] jpegBytes;
                Protocol.DeserializeScreenFrame(payload, out frameId, out width, out height, out jpegBytes);

                RemoteScreenWidth = width;
                RemoteScreenHeight = height;

                // Send Frame Ack back to server to keep stream flowing smoothly
                byte[] ackPayload = BitConverter.GetBytes(frameId);
                Protocol.SendPacket(_stream, _writeLock, PacketType.FrameAck, ackPayload);

                if (jpegBytes != null && jpegBytes.Length > 0 && OnFrameReceived != null)
                {
                    using (MemoryStream ms = new MemoryStream(jpegBytes))
                    {
                        Bitmap bmp = new Bitmap(ms);
                        OnFrameReceived(bmp, frameId);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("HandleScreenFrame error: " + ex.Message);
            }
        }

        #region File Transfer Handlers

        public void SetDownloadDestination(string localSavePath)
        {
            _downloadFilePath = localSavePath;
        }

        private void HandleFileDownloadHeader(byte[] payload)
        {
            try
            {
                CloseDownloadStream(false);
                using (MemoryStream ms = new MemoryStream(payload))
                using (BinaryReader br = new BinaryReader(ms, Encoding.UTF8))
                {
                    _downloadFileName = br.ReadString();
                    long fileSize = br.ReadInt64();
                    _downloadTotalChunks = br.ReadInt32();
                    _downloadChunksReceived = 0;

                    if (string.IsNullOrEmpty(_downloadFilePath))
                    {
                        string downloadsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                        _downloadFilePath = Path.Combine(downloadsDir, _downloadFileName);
                    }

                    _downloadStream = new FileStream(_downloadFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                }
            }
            catch (Exception ex)
            {
                Log("FileDownloadHeader error: " + ex.Message);
                CloseDownloadStream(false);
            }
        }

        private void HandleFileDownloadChunk(byte[] payload)
        {
            try
            {
                if (_downloadStream == null) return;

                using (MemoryStream ms = new MemoryStream(payload))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    int chunkIndex = br.ReadInt32();
                    int totalChunks = br.ReadInt32();
                    int bytesLength = br.ReadInt32();
                    byte[] data = br.ReadBytes(bytesLength);

                    _downloadStream.Write(data, 0, bytesLength);
                    _downloadChunksReceived++;

                    int percent = (int)((_downloadChunksReceived / (double)totalChunks) * 100);
                    if (OnDownloadProgress != null)
                    {
                        OnDownloadProgress(_downloadFileName, _downloadChunksReceived, totalChunks, percent);
                    }

                    if (_downloadChunksReceived >= totalChunks)
                    {
                        CloseDownloadStream(true);
                        Log("Downloaded file: " + _downloadFilePath);
                        if (OnDownloadComplete != null)
                        {
                            OnDownloadComplete(_downloadFileName, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("FileDownloadChunk error: " + ex.Message);
                CloseDownloadStream(false);
                if (OnDownloadComplete != null)
                {
                    OnDownloadComplete(_downloadFileName, false);
                }
            }
        }

        private void CloseDownloadStream(bool flush)
        {
            if (_downloadStream != null)
            {
                try
                {
                    if (flush) _downloadStream.Flush();
                    _downloadStream.Close();
                    _downloadStream.Dispose();
                }
                catch { }
                _downloadStream = null;
            }
        }

        private void HandleFileActionResponse(byte[] payload)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(payload))
                using (BinaryReader br = new BinaryReader(ms, Encoding.UTF8))
                {
                    bool success = br.ReadBoolean();
                    string msg = br.ReadString();
                    if (OnFileActionResponse != null)
                    {
                        OnFileActionResponse(success, msg);
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Outgoing Control Commands

        public void SendMouseMove(int x, int y, int sw, int sh)
        {
            if (!_isConnected) return;
            byte[] payload = Protocol.SerializeMouseMove(x, y, sw, sh);
            Protocol.SendPacket(_stream, _writeLock, PacketType.MouseMove, payload);
        }

        public void SendMouseButton(int button, bool isDown, int x, int y, int sw, int sh)
        {
            if (!_isConnected) return;
            byte[] payload = Protocol.SerializeMouseButton(button, x, y, sw, sh);
            Protocol.SendPacket(_stream, _writeLock, isDown ? PacketType.MouseDown : PacketType.MouseUp, payload);
        }

        public void SendMouseWheel(int delta, int x, int y)
        {
            if (!_isConnected) return;
            byte[] payload = Protocol.SerializeMouseWheel(delta, x, y);
            Protocol.SendPacket(_stream, _writeLock, PacketType.MouseWheel, payload);
        }

        public void SendKeyboard(int virtualKey, bool isKeyDown, bool isExtended)
        {
            if (!_isConnected) return;
            byte[] payload = Protocol.SerializeKey(virtualKey, isExtended);
            Protocol.SendPacket(_stream, _writeLock, isKeyDown ? PacketType.KeyDown : PacketType.KeyUp, payload);
        }

        public void SendSpecialCommand(int commandId)
        {
            if (!_isConnected) return;
            byte[] payload = BitConverter.GetBytes(commandId);
            Protocol.SendPacket(_stream, _writeLock, PacketType.SpecialKeyCommand, payload);
        }

        public void SendQualitySettings(int quality, int fps)
        {
            if (!_isConnected) return;
            byte[] payload = Protocol.SerializeQualityChange(quality, fps);
            Protocol.SendPacket(_stream, _writeLock, PacketType.QualityChange, payload);
        }

        public void SendClipboard(string text)
        {
            if (!_isConnected || string.IsNullOrEmpty(text)) return;
            byte[] payload = Protocol.SerializeString(text);
            Protocol.SendPacket(_stream, _writeLock, PacketType.ClipboardSync, payload);
        }

        public void RequestFileList(string remotePath)
        {
            if (!_isConnected) return;
            byte[] payload = Protocol.SerializeString(remotePath ?? "");
            Protocol.SendPacket(_stream, _writeLock, PacketType.FileListRequest, payload);
        }

        public void RequestFileDownload(string remoteFilePath, string localSavePath)
        {
            if (!_isConnected) return;
            _downloadFilePath = localSavePath;
            byte[] payload = Protocol.SerializeString(remoteFilePath);
            Protocol.SendPacket(_stream, _writeLock, PacketType.FileDownloadRequest, payload);
        }

        public void UploadFile(string localFilePath, string remoteDestPath)
        {
            if (!_isConnected || !File.Exists(localFilePath)) return;

            new Thread(new ThreadStart(delegate()
            {
                try
                {
                    FileInfo fi = new FileInfo(localFilePath);
                    long fileSize = fi.Length;
                    int chunkSize = 64 * 1024;
                    int totalChunks = (int)Math.Ceiling((double)fileSize / chunkSize);
                    if (totalChunks == 0) totalChunks = 1;

                    // Send Header
                    using (MemoryStream ms = new MemoryStream())
                    using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8))
                    {
                        bw.Write(remoteDestPath);
                        bw.Write(fileSize);
                        bw.Write(totalChunks);
                        Protocol.SendPacket(_stream, _writeLock, PacketType.FileUploadHeader, ms.ToArray());
                    }

                    // Send Chunks
                    using (FileStream fs = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] buffer = new byte[chunkSize];
                        int chunkIndex = 0;
                        int bytesRead;

                        while ((bytesRead = fs.Read(buffer, 0, chunkSize)) > 0 && _isConnected)
                        {
                            using (MemoryStream ms = new MemoryStream(12 + bytesRead))
                            using (BinaryWriter bw = new BinaryWriter(ms))
                            {
                                bw.Write(chunkIndex);
                                bw.Write(totalChunks);
                                bw.Write(bytesRead);
                                bw.Write(buffer, 0, bytesRead);
                                Protocol.SendPacket(_stream, _writeLock, PacketType.FileUploadChunk, ms.ToArray());
                            }

                            chunkIndex++;
                            int percent = (int)((chunkIndex / (double)totalChunks) * 100);
                            if (OnUploadProgress != null)
                            {
                                OnUploadProgress(fi.Name, chunkIndex, totalChunks, percent);
                            }
                            Thread.Sleep(5);
                        }
                    }

                    if (OnUploadComplete != null)
                    {
                        OnUploadComplete(fi.Name, true);
                    }
                }
                catch (Exception ex)
                {
                    Log("UploadFile error: " + ex.Message);
                    if (OnUploadComplete != null)
                    {
                        OnUploadComplete(Path.GetFileName(localFilePath), false);
                    }
                }
            })).Start();
        }

        public void SendFileAction(int actionType, string path)
        {
            if (!_isConnected) return;
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                bw.Write(actionType);
                bw.Write(path ?? string.Empty);
                Protocol.SendPacket(_stream, _writeLock, PacketType.FileAction, ms.ToArray());
            }
        }

        public void RequestSystemInfo()
        {
            if (!_isConnected) return;
            Protocol.SendPacket(_stream, _writeLock, PacketType.SystemInfoRequest, null);
        }

        public void SendChatMessage(string message)
        {
            if (!_isConnected || string.IsNullOrEmpty(message)) return;
            byte[] payload = Protocol.SerializeChatMessage(Environment.MachineName, message);
            Protocol.SendPacket(_stream, _writeLock, PacketType.ChatMessage, payload);
        }

        public void SendPing()
        {
            if (!_isConnected) return;
            _pingStopwatch.Reset();
            _pingStopwatch.Start();
            Protocol.SendPacket(_stream, _writeLock, PacketType.Ping, null);
        }

        #endregion

        private void Log(string msg)
        {
            if (OnLog != null)
            {
                OnLog(string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), msg));
            }
        }
    }
}
