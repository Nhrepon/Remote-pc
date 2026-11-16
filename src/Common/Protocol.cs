using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace RemotePC.Common
{
    public enum PacketType : byte
    {
        AuthRequest = 1,
        AuthResponse = 2,
        ScreenFrame = 3,
        FrameAck = 4,
        QualityChange = 5,
        MouseMove = 6,
        MouseDown = 7,
        MouseUp = 8,
        MouseWheel = 9,
        KeyDown = 10,
        KeyUp = 11,
        ClipboardSync = 12,
        SpecialKeyCommand = 13,
        FileListRequest = 14,
        FileListResponse = 15,
        FileDownloadRequest = 16,
        FileDownloadHeader = 17,
        FileChunk = 18,
        FileUploadHeader = 19,
        FileUploadChunk = 20,
        FileAction = 21,
        FileActionResponse = 22,
        SystemInfoRequest = 23,
        SystemInfoResponse = 24,
        ChatMessage = 25,
        Ping = 26,
        Pong = 27,
        Disconnect = 28,
        DisplayChange = 29
    }

    public static class Protocol
    {
        public const uint MAGIC = 0x31435052; // 'RPC1' in little endian

        /// <summary>
        /// Sends a framed packet over network stream in a thread-safe manner.
        /// Packet format: [Magic: 4 bytes] [PacketType: 1 byte] [PayloadLength: 4 bytes] [Payload: N bytes]
        /// </summary>
        public static void SendPacket(NetworkStream stream, object writeLock, PacketType type, byte[] payload)
        {
            if (stream == null || !stream.CanWrite) return;
            if (payload == null) payload = new byte[0];

            int totalLength = 4 + 1 + 4 + payload.Length;
            byte[] buffer = new byte[totalLength];

            // Magic: RPC1
            buffer[0] = (byte)'R';
            buffer[1] = (byte)'P';
            buffer[2] = (byte)'C';
            buffer[3] = (byte)'1';

            // Packet Type
            buffer[4] = (byte)type;

            // Payload Length (Little Endian)
            int len = payload.Length;
            buffer[5] = (byte)(len & 0xFF);
            buffer[6] = (byte)((len >> 8) & 0xFF);
            buffer[7] = (byte)((len >> 16) & 0xFF);
            buffer[8] = (byte)((len >> 24) & 0xFF);

            // Payload
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, buffer, 9, payload.Length);
            }

            lock (writeLock)
            {
                stream.Write(buffer, 0, buffer.Length);
                stream.Flush();
            }
        }

        /// <summary>
        /// Reads an exact number of bytes from the stream, blocking until all are received or connection drops.
        /// </summary>
        public static bool ReadExact(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read <= 0) return false;
                totalRead += read;
            }
            return true;
        }

        /// <summary>
        /// Reads the next packet header and payload from the network stream.
        /// </summary>
        public static bool ReceivePacket(NetworkStream stream, out PacketType type, out byte[] payload)
        {
            type = 0;
            payload = null;

            byte[] header = new byte[9];
            if (!ReadExact(stream, header, 0, 9))
            {
                return false;
            }

            // Verify Magic 'RPC1'
            if (header[0] != (byte)'R' || header[1] != (byte)'P' || header[2] != (byte)'C' || header[3] != (byte)'1')
            {
                return false;
            }

            type = (PacketType)header[4];
            int payloadLength = header[5] | (header[6] << 8) | (header[7] << 16) | (header[8] << 24);

            if (payloadLength < 0 || payloadLength > 50 * 1024 * 1024) // 50MB max sanity check
            {
                return false;
            }

            payload = new byte[payloadLength];
            if (payloadLength > 0)
            {
                if (!ReadExact(stream, payload, 0, payloadLength))
                {
                    return false;
                }
            }

            return true;
        }

        #region Serialization Helpers

        public static byte[] SerializeString(string text)
        {
            if (text == null) text = string.Empty;
            return Encoding.UTF8.GetBytes(text);
        }

        public static string DeserializeString(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return string.Empty;
            return Encoding.UTF8.GetString(payload);
        }

        public static byte[] SerializeAuthRequest(string password, string clientName)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                bw.Write(password ?? string.Empty);
                bw.Write(clientName ?? string.Empty);
                return ms.ToArray();
            }
        }

        public static void DeserializeAuthRequest(byte[] payload, out string password, out string clientName)
        {
            using (MemoryStream ms = new MemoryStream(payload))
            using (BinaryReader br = new BinaryReader(ms, Encoding.UTF8))
            {
                password = br.ReadString();
                clientName = br.ReadString();
            }
        }

        public static byte[] SerializeAuthResponse(bool success, string message, int width, int height)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                bw.Write(success);
                bw.Write(message ?? string.Empty);
                bw.Write(width);
                bw.Write(height);
                return ms.ToArray();
            }
        }

        public static void DeserializeAuthResponse(byte[] payload, out bool success, out string message, out int width, out int height)
        {
            using (MemoryStream ms = new MemoryStream(payload))
            using (BinaryReader br = new BinaryReader(ms, Encoding.UTF8))
            {
                success = br.ReadBoolean();
                message = br.ReadString();
                width = br.ReadInt32();
                height = br.ReadInt32();
            }
        }

        public static byte[] SerializeScreenFrame(long frameId, int width, int height, byte[] jpegBytes)
        {
            using (MemoryStream ms = new MemoryStream(16 + (jpegBytes != null ? jpegBytes.Length : 0)))
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(frameId);
                bw.Write(width);
                bw.Write(height);
                int length = jpegBytes != null ? jpegBytes.Length : 0;
                bw.Write(length);
                if (length > 0)
                {
                    bw.Write(jpegBytes);
                }
                return ms.ToArray();
            }
        }

        public static void DeserializeScreenFrame(byte[] payload, out long frameId, out int width, out int height, out byte[] jpegBytes)
        {
            using (MemoryStream ms = new MemoryStream(payload))
            using (BinaryReader br = new BinaryReader(ms))
            {
                frameId = br.ReadInt64();
                width = br.ReadInt32();
                height = br.ReadInt32();
                int length = br.ReadInt32();
                jpegBytes = br.ReadBytes(length);
            }
        }

        public static byte[] SerializeQualityChange(int quality, int fps)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(quality);
                bw.Write(fps);
                return ms.ToArray();
            }
        }

        public static void DeserializeQualityChange(byte[] payload, out int quality, out int fps)
        {
            using (MemoryStream ms = new MemoryStream(payload))
            using (BinaryReader br = new BinaryReader(ms))
            {
                quality = br.ReadInt32();
                fps = br.ReadInt32();
            }
        }

        public static byte[] SerializeMouseMove(int x, int y, int screenW, int screenH)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(x);
                bw.Write(y);
                bw.Write(screenW);
                bw.Write(screenH);
                return ms.ToArray();
            }
        }

        public static void DeserializeMouseMove(byte[] payload, out int x, out int y, out int screenW, out int screenH)
        {
            using (MemoryStream ms = new MemoryStream(payload))
            using (BinaryReader br = new BinaryReader(ms))
            {
                x = br.ReadInt32();
                y = br.ReadInt32();
                screenW = br.ReadInt32();
                screenH = br.ReadInt32();
            }
        }

        public static byte[] SerializeMouseButton(int button, int x, int y, int screenW, int screenH)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(button);
                bw.Write(x);
                bw.Write(y);
                bw.Write(screenW);
                bw.Write(screenH);
                return ms.ToArray();
            }
        }

        public static void DeserializeMouseButton(byte[] payload, out int button, out int x, out int y, out int screenW, out int screenH)
        {
            using (MemoryStream ms = new MemoryStream(payload))
            using (BinaryReader br = new BinaryReader(ms))
            {
                button = br.ReadInt32();
                x = br.ReadInt32();
                y = br.ReadInt32();
                screenW = br.ReadInt32();
                screenH = br.ReadInt32();
            }
        }

        public static byte[] SerializeMouseWheel(int delta, int x, int y)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(delta);
                bw.Write(x);
                bw.Write(y);
                return ms.ToArray();
            }
        }

        public static void DeserializeMouseWheel(byte[] payload, out int delta, out int x, out int y)
        {
            using (MemoryStream ms = new MemoryStream(payload))
            using (BinaryReader br = new BinaryReader(ms))
            {
                delta = br.ReadInt32();
                x = br.ReadInt32();
                y = br.ReadInt32();
            }
        }

        public static byte[] SerializeKey(int virtualKey, bool isExtended)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(virtualKey);
                bw.Write(isExtended);
                return ms.ToArray();
            }
        }

        public static void DeserializeKey(byte[] payload, out int virtualKey, out bool isExtended)
        {
            using (MemoryStream ms = new MemoryStream(payload))
            using (BinaryReader br = new BinaryReader(ms))
            {
                virtualKey = br.ReadInt32();
                isExtended = br.ReadBoolean();
            }
        }

        public static byte[] SerializeChatMessage(string sender, string message)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms, Encoding.UTF8))
            {
                bw.Write(sender ?? string.Empty);
                bw.Write(message ?? string.Empty);
                bw.Write(DateTime.Now.ToString("HH:mm:ss"));
                return ms.ToArray();
            }
        }

        public static void DeserializeChatMessage(byte[] payload, out string sender, out string message, out string time)
        {
            using (MemoryStream ms = new MemoryStream(payload))
            using (BinaryReader br = new BinaryReader(ms, Encoding.UTF8))
            {
                sender = br.ReadString();
                message = br.ReadString();
                time = br.ReadString();
            }
        }

        #endregion
    }
}
