using System;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using RemotePC.Common;

namespace RemotePC.Server
{
    public class ServerSession
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly object _writeLock = new object();
        private readonly string _serverPassword;
        private readonly bool _allowControl;
        private readonly bool _allowFileTransfer;
        private readonly Action<string> _logger;

        private Thread _readThread;
        private Thread _streamThread;
        private volatile bool _isRunning;
        private bool _isAuthenticated;
        private string _clientName = "Remote Client";

        private int _streamQuality = 60;
        private int _targetFps = 25;
        private long _currentFrameId = 0;
        private long _lastAckedFrameId = 0;
        private ScreenCapturer _capturer;

        // File upload tracking
        private FileStream _currentUploadStream;
        private string _currentUploadPath;
        private int _currentUploadChunksReceived;
        private int _currentUploadTotalChunks;

        public event Action<ServerSession, string> OnChatMessage;
        public event Action<ServerSession> OnDisconnected;
        public event Action<ServerSession> OnAuthenticated;

        public string ClientEndPoint { get; private set; }
        public string ClientName { get { return _clientName; } }
        public bool IsAuthenticated { get { return _isAuthenticated; } }

        public ServerSession(TcpClient client, string serverPassword, bool allowControl, bool allowFileTransfer, Action<string> logger)
        {
            _client = client;
            _stream = client.GetStream();
            _serverPassword = serverPassword;
            _allowControl = allowControl;
            _allowFileTransfer = allowFileTransfer;
            _logger = logger;

            try
            {
                ClientEndPoint = client.Client.RemoteEndPoint.ToString();
            }
            catch
            {
                ClientEndPoint = "Unknown";
            }
        }

        public void Start()
        {
            _isRunning = true;
            _readThread = new Thread(ReadLoop);
            _readThread.IsBackground = true;
            _readThread.Name = "ServerSession_Read_" + ClientEndPoint;
            _readThread.Start();
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;

            try
            {
                if (_stream != null) _stream.Close();
                if (_client != null) _client.Close();
            }
            catch { }

            CloseUploadStream();

            if (OnDisconnected != null)
            {
                OnDisconnected(this);
            }
        }

        public void SendChat(string sender, string message)
        {
            if (!_isRunning || !_isAuthenticated) return;
            byte[] payload = Protocol.SerializeChatMessage(sender, message);
            Protocol.SendPacket(_stream, _writeLock, PacketType.ChatMessage, payload);
        }

        private void ReadLoop()
        {
            try
            {
                while (_isRunning)
                {
                    PacketType type;
                    byte[] payload;

                    if (!Protocol.ReceivePacket(_stream, out type, out payload))
                    {
                        break; // Connection lost
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
                Stop();
            }
        }

        private void ProcessPacket(PacketType type, byte[] payload)
        {
            if (type == PacketType.AuthRequest)
            {
                HandleAuth(payload);
                return;
            }

            // Require authentication for all other packets
            if (!_isAuthenticated)
            {
                Log("Unauthenticated packet received from " + ClientEndPoint + ". Disconnecting.");
                Stop();
                return;
            }

            switch (type)
            {
                case PacketType.FrameAck:
                    if (payload != null && payload.Length >= 8)
                    {
                        _lastAckedFrameId = BitConverter.ToInt64(payload, 0);
                    }
                    break;

                case PacketType.QualityChange:
                    int q, fps;
                    Protocol.DeserializeQualityChange(payload, out q, out fps);
                    if (q >= 10 && q <= 100) _streamQuality = q;
                    if (fps >= 5 && fps <= 60) _targetFps = fps;
                    break;

                case PacketType.MouseMove:
                    if (_allowControl)
                    {
                        int x, y, sw, sh;
                        Protocol.DeserializeMouseMove(payload, out x, out y, out sw, out sh);
                        InputSimulator.SendMouseMove(x, y, sw, sh);
                    }
                    break;

                case PacketType.MouseDown:
                    if (_allowControl)
                    {
                        int btn, x, y, sw, sh;
                        Protocol.DeserializeMouseButton(payload, out btn, out x, out y, out sw, out sh);
                        InputSimulator.SendMouseButton(btn, true, x, y, sw, sh);
                    }
                    break;

                case PacketType.MouseUp:
                    if (_allowControl)
                    {
                        int btn, x, y, sw, sh;
                        Protocol.DeserializeMouseButton(payload, out btn, out x, out y, out sw, out sh);
                        InputSimulator.SendMouseButton(btn, false, x, y, sw, sh);
                    }
                    break;

                case PacketType.MouseWheel:
                    if (_allowControl)
                    {
                        int delta, x, y;
                        Protocol.DeserializeMouseWheel(payload, out delta, out x, out y);
                        InputSimulator.SendMouseWheel(delta, x, y);
                    }
                    break;

                case PacketType.KeyDown:
                    if (_allowControl)
                    {
                        int vk;
                        bool ext;
                        Protocol.DeserializeKey(payload, out vk, out ext);
                        InputSimulator.SendKeyboard(vk, true, ext);
                    }
                    break;

                case PacketType.KeyUp:
                    if (_allowControl)
                    {
                        int vk;
                        bool ext;
                        Protocol.DeserializeKey(payload, out vk, out ext);
                        InputSimulator.SendKeyboard(vk, false, ext);
                    }
                    break;

                case PacketType.SpecialKeyCommand:
                    if (_allowControl && payload != null && payload.Length >= 4)
                    {
                        int cmd = BitConverter.ToInt32(payload, 0);
                        InputSimulator.ExecuteSpecialCommand(cmd);
                    }
                    break;

                case PacketType.ClipboardSync:
                    // Host received clipboard text from client
                    string text = Protocol.DeserializeString(payload);
                    if (!string.IsNullOrEmpty(text))
                    {
                        Thread staThread = new Thread(new ThreadStart(delegate()
                        {
                            try { System.Windows.Forms.Clipboard.SetText(text); } catch { }
                        }));
                        staThread.SetApartmentState(ApartmentState.STA);
                        staThread.Start();
                    }
                    break;

                case PacketType.FileListRequest:
                    if (_allowFileTransfer)
                    {
                        HandleFileList(Protocol.DeserializeString(payload));
                    }
                    break;

                case PacketType.FileDownloadRequest:
                    if (_allowFileTransfer)
                    {
                        HandleFileDownload(Protocol.DeserializeString(payload));
                    }
                    break;

                case PacketType.FileUploadHeader:
                    if (_allowFileTransfer)
                    {
                        HandleFileUploadHeader(payload);
                    }
                    break;

                case PacketType.FileUploadChunk:
                    if (_allowFileTransfer)
                    {
                        HandleFileUploadChunk(payload);
                    }
                    break;

                case PacketType.FileAction:
                    if (_allowFileTransfer)
                    {
                        HandleFileAction(payload);
                    }
                    break;

                case PacketType.SystemInfoRequest:
                    HandleSystemInfo();
                    break;

                case PacketType.ChatMessage:
                    string sender, msg, time;
                    Protocol.DeserializeChatMessage(payload, out sender, out msg, out time);
                    if (OnChatMessage != null)
                    {
                        OnChatMessage(this, sender + ": " + msg);
                    }
                    break;

                case PacketType.DisplayChange:
                    if (payload != null && payload.Length >= 1)
                    {
                        byte dMode = payload[0];
                        if (_capturer != null)
                        {
                            _capturer.CaptureMode = (ScreenCaptureMode)dMode;
                        }
                    }
                    break;

                case PacketType.Ping:
                    Protocol.SendPacket(_stream, _writeLock, PacketType.Pong, null);
                    break;

                case PacketType.Disconnect:
                    Stop();
                    break;
            }
        }

        private void HandleAuth(byte[] payload)
        {
            string password, clientName;
            Protocol.DeserializeAuthRequest(payload, out password, out clientName);
            _clientName = string.IsNullOrEmpty(clientName) ? "Remote Client" : clientName;

            bool matches = string.IsNullOrEmpty(_serverPassword) || string.Equals(_serverPassword, password, StringComparison.Ordinal);

            if (matches)
            {
                _isAuthenticated = true;
                Log(string.Format("Client authenticated successfully: {0} ({1})", _clientName, ClientEndPoint));

                Rectangle bounds = ScreenCapturer.GetCurrentPhysicalScreenBounds(ScreenCaptureMode.PrimaryDisplay);
                int screenW = bounds.Width;
                int screenH = bounds.Height;

                byte[] resp = Protocol.SerializeAuthResponse(true, "Authentication successful", screenW, screenH);
                Protocol.SendPacket(_stream, _writeLock, PacketType.AuthResponse, resp);

                if (OnAuthenticated != null)
                {
                    OnAuthenticated(this);
                }

                // Start screen capture streaming thread
                _streamThread = new Thread(ScreenStreamLoop);
                _streamThread.IsBackground = true;
                _streamThread.Name = "ServerSession_Stream_" + ClientEndPoint;
                _streamThread.Start();
            }
            else
            {
                Log(string.Format("Client authentication failed: {0} ({1})", _clientName, ClientEndPoint));
                byte[] resp = Protocol.SerializeAuthResponse(false, "Invalid password/PIN", 0, 0);
                Protocol.SendPacket(_stream, _writeLock, PacketType.AuthResponse, resp);
                Stop();
            }
        }

        private void ScreenStreamLoop()
        {
            try
            {
                _capturer = new ScreenCapturer(true, ScreenCaptureMode.PrimaryDisplay);
                while (_isRunning && _isAuthenticated)
                {
                    // Flow control: if client is more than 2 frames behind, skip capture to avoid lag
                    if (_currentFrameId - _lastAckedFrameId > 2)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    int quality = _streamQuality;
                    byte[] jpeg = _capturer.CaptureJpeg(quality);

                    if (jpeg != null && jpeg.Length > 0 && _isRunning)
                    {
                        _currentFrameId++;
                        byte[] framePayload = Protocol.SerializeScreenFrame(_currentFrameId, _capturer.ScreenWidth, _capturer.ScreenHeight, jpeg);
                        Protocol.SendPacket(_stream, _writeLock, PacketType.ScreenFrame, framePayload);
                    }

                    int delay = 1000 / (_targetFps > 0 ? _targetFps : 25);
                    Thread.Sleep(delay);
                }
            }
            catch (Exception ex)
            {
                Log("ScreenStreamLoop error: " + ex.Message);
            }
            finally
            {
                if (_capturer != null)
                {
                    _capturer.Dispose();
                    _capturer = null;
                }
            }
        }

        #region File System Handlers

        private void HandleFileList(string targetPath)
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                if (string.IsNullOrEmpty(targetPath) || targetPath == "DRIVES")
                {
                    sb.AppendLine("PATH|DRIVES");
                    DriveInfo[] drives = DriveInfo.GetDrives();
                    StringBuilder drivesSb = new StringBuilder();
                    for (int i = 0; i < drives.Length; i++)
                    {
                        if (drives[i].IsReady)
                        {
                            drivesSb.Append(drives[i].Name + ";");
                        }
                    }
                    sb.AppendLine("DRIVES|" + drivesSb.ToString());
                }
                else
                {
                    if (!Directory.Exists(targetPath))
                    {
                        targetPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    }

                    sb.AppendLine("PATH|" + targetPath);

                    DriveInfo[] drives = DriveInfo.GetDrives();
                    StringBuilder drivesSb = new StringBuilder();
                    for (int i = 0; i < drives.Length; i++)
                    {
                        if (drives[i].IsReady)
                        {
                            drivesSb.Append(drives[i].Name + ";");
                        }
                    }
                    sb.AppendLine("DRIVES|" + drivesSb.ToString());

                    // Get subdirectories
                    try
                    {
                        string[] dirs = Directory.GetDirectories(targetPath);
                        for (int i = 0; i < dirs.Length; i++)
                        {
                            DirectoryInfo di = new DirectoryInfo(dirs[i]);
                            // Skip hidden / system system files if restricted
                            if ((di.Attributes & FileAttributes.Hidden) != 0) continue;
                            sb.AppendLine(string.Format("DIR|{0}|{1}", di.Name, di.LastWriteTimeUtc.Ticks));
                        }
                    }
                    catch { }

                    // Get files
                    try
                    {
                        string[] files = Directory.GetFiles(targetPath);
                        for (int i = 0; i < files.Length; i++)
                        {
                            FileInfo fi = new FileInfo(files[i]);
                            if ((fi.Attributes & FileAttributes.Hidden) != 0) continue;
                            sb.AppendLine(string.Format("FILE|{0}|{1}|{2}", fi.Name, fi.Length, fi.LastWriteTimeUtc.Ticks));
                        }
                    }
                    catch { }
                }

                byte[] respPayload = Protocol.SerializeString(sb.ToString());
                Protocol.SendPacket(_stream, _writeLock, PacketType.FileListResponse, respPayload);
            }
            catch (Exception ex)
            {
                Log("HandleFileList error: " + ex.Message);
            }
        }

        private void HandleFileDownload(string remoteFilePath)
        {
            new Thread(new ThreadStart(delegate()
            {
                try
                {
                    if (!File.Exists(remoteFilePath)) return;

                    FileInfo fi = new FileInfo(remoteFilePath);
                    long fileSize = fi.Length;
                    int chunkSize = 64 * 1024;
                    int totalChunks = (int)Math.Ceiling((double)fileSize / chunkSize);
                    if (totalChunks == 0) totalChunks = 1;

                    // Send header
                    using (MemoryStream ms = new MemoryStream())
                    using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8))
                    {
                        bw.Write(fi.Name);
                        bw.Write(fileSize);
                        bw.Write(totalChunks);
                        Protocol.SendPacket(_stream, _writeLock, PacketType.FileDownloadHeader, ms.ToArray());
                    }

                    // Send chunks
                    using (FileStream fs = new FileStream(remoteFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] buffer = new byte[chunkSize];
                        int chunkIndex = 0;
                        int bytesRead;

                        while ((bytesRead = fs.Read(buffer, 0, chunkSize)) > 0 && _isRunning)
                        {
                            using (MemoryStream ms = new MemoryStream(12 + bytesRead))
                            using (BinaryWriter bw = new BinaryWriter(ms))
                            {
                                bw.Write(chunkIndex);
                                bw.Write(totalChunks);
                                bw.Write(bytesRead);
                                bw.Write(buffer, 0, bytesRead);
                                Protocol.SendPacket(_stream, _writeLock, PacketType.FileChunk, ms.ToArray());
                            }
                            chunkIndex++;
                            Thread.Sleep(5); // Yield slight breather
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("HandleFileDownload error: " + ex.Message);
                }
            })).Start();
        }

        private void HandleFileUploadHeader(byte[] payload)
        {
            try
            {
                CloseUploadStream();
                using (MemoryStream ms = new MemoryStream(payload))
                using (BinaryReader br = new BinaryReader(ms, Encoding.UTF8))
                {
                    _currentUploadPath = br.ReadString();
                    long fileSize = br.ReadInt64();
                    _currentUploadTotalChunks = br.ReadInt32();
                    _currentUploadChunksReceived = 0;

                    string dir = Path.GetDirectoryName(_currentUploadPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    _currentUploadStream = new FileStream(_currentUploadPath, FileMode.Create, FileAccess.Write, FileShare.None);
                }
            }
            catch (Exception ex)
            {
                Log("HandleFileUploadHeader error: " + ex.Message);
                CloseUploadStream();
            }
        }

        private void HandleFileUploadChunk(byte[] payload)
        {
            try
            {
                if (_currentUploadStream == null) return;

                using (MemoryStream ms = new MemoryStream(payload))
                using (BinaryReader br = new BinaryReader(ms))
                {
                    int chunkIndex = br.ReadInt32();
                    int totalChunks = br.ReadInt32();
                    int bytesLength = br.ReadInt32();
                    byte[] data = br.ReadBytes(bytesLength);

                    _currentUploadStream.Write(data, 0, bytesLength);
                    _currentUploadChunksReceived++;

                    if (_currentUploadChunksReceived >= totalChunks)
                    {
                        CloseUploadStream();
                        Log("File upload finished: " + _currentUploadPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Log("HandleFileUploadChunk error: " + ex.Message);
                CloseUploadStream();
            }
        }

        private void CloseUploadStream()
        {
            if (_currentUploadStream != null)
            {
                try
                {
                    _currentUploadStream.Flush();
                    _currentUploadStream.Close();
                    _currentUploadStream.Dispose();
                }
                catch { }
                _currentUploadStream = null;
            }
        }

        private void HandleFileAction(byte[] payload)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(payload))
                using (BinaryReader br = new BinaryReader(ms, Encoding.UTF8))
                {
                    int actionType = br.ReadInt32(); // 1=DelFile, 2=DelFolder, 3=CreateFolder
                    string path = br.ReadString();

                    bool success = false;
                    string msg = "";

                    if (actionType == 1) // Delete File
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                            success = true;
                            msg = "File deleted";
                        }
                    }
                    else if (actionType == 2) // Delete Folder
                    {
                        if (Directory.Exists(path))
                        {
                            Directory.Delete(path, true);
                            success = true;
                            msg = "Folder deleted";
                        }
                    }
                    else if (actionType == 3) // Create Folder
                    {
                        if (!Directory.Exists(path))
                        {
                            Directory.CreateDirectory(path);
                            success = true;
                            msg = "Folder created";
                        }
                    }

                    using (MemoryStream respMs = new MemoryStream())
                    using (BinaryWriter bw = new BinaryWriter(respMs, Encoding.UTF8))
                    {
                        bw.Write(success);
                        bw.Write(msg);
                        Protocol.SendPacket(_stream, _writeLock, PacketType.FileActionResponse, respMs.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                Log("HandleFileAction error: " + ex.Message);
            }
        }

        #endregion

        private void HandleSystemInfo()
        {
            try
            {
                string info = SystemInfo.GetSystemInfoString();
                byte[] payload = Protocol.SerializeString(info);
                Protocol.SendPacket(_stream, _writeLock, PacketType.SystemInfoResponse, payload);
            }
            catch { }
        }

        private void Log(string msg)
        {
            if (_logger != null)
            {
                _logger(string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), msg));
            }
        }
    }
}
