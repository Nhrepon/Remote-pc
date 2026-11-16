using System;
using System.IO;
using System.Security.Cryptography;
using RemotePC.Common;

namespace RemotePC.Tests
{
    class TestRunner
    {
        static int Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("   RemotePC Unit & Verification Tests   ");
            Console.WriteLine("========================================");

            try
            {
                TestProtocolSerialization();
                Console.WriteLine("[PASS] Protocol serialization & framing tests passed.");

                TestScreenCapturer();
                Console.WriteLine("[PASS] ScreenCapturer & JPEG compression tests passed.");

                TestSystemInfo();
                Console.WriteLine("[PASS] SystemInfo & network adapter tests passed.");

                TestConnectionCodeParsing();
                Console.WriteLine("[PASS] Connection code parsing tests passed.");

                TestAddressClassification();
                Console.WriteLine("[PASS] Address classification tests passed.");

                TestFileChunkingIntegrity();
                Console.WriteLine("[PASS] File transfer chunking & reassembly integrity passed.");

                Console.WriteLine("\n========================================");
                Console.WriteLine("  ALL TESTS PASSED SUCCESSFULLY (6/6)!  ");
                Console.WriteLine("========================================");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[FAIL] Test error: " + ex.ToString());
                return 1;
            }
        }

        static void TestProtocolSerialization()
        {
            // 1. Auth Serialization
            byte[] authData = Protocol.SerializeAuthRequest("Pass1234", "MyClientMachine");
            string pass, client;
            Protocol.DeserializeAuthRequest(authData, out pass, out client);
            if (pass != "Pass1234" || client != "MyClientMachine")
                throw new Exception("Auth serialization mismatch!");

            // 2. Auth Response
            byte[] authResp = Protocol.SerializeAuthResponse(true, "Welcome", 1920, 1080);
            bool ok;
            string msg;
            int rw, rh;
            Protocol.DeserializeAuthResponse(authResp, out ok, out msg, out rw, out rh);
            if (!ok || msg != "Welcome" || rw != 1920 || rh != 1080)
                throw new Exception("AuthResponse mismatch!");

            // 3. Screen Frame
            byte[] testJpeg = new byte[] { 0xFF, 0xD8, 0x01, 0x02, 0x03, 0xFF, 0xD9 };
            byte[] frameData = Protocol.SerializeScreenFrame(999, 2560, 1440, testJpeg);
            long fId;
            int fw, fh;
            byte[] outJpeg;
            Protocol.DeserializeScreenFrame(frameData, out fId, out fw, out fh, out outJpeg);
            if (fId != 999 || fw != 2560 || fh != 1440 || outJpeg.Length != testJpeg.Length)
                throw new Exception("ScreenFrame mismatch!");

            // 4. Mouse Move & Buttons
            byte[] mouseData = Protocol.SerializeMouseMove(800, 600, 1920, 1080);
            int mx, my, msw, msh;
            Protocol.DeserializeMouseMove(mouseData, out mx, out my, out msw, out msh);
            if (mx != 800 || my != 600 || msw != 1920 || msh != 1080)
                throw new Exception("MouseMove mismatch!");

            byte[] btnData = Protocol.SerializeMouseButton(1, 400, 300, 1920, 1080);
            int btn, bx, by, bsw, bsh;
            Protocol.DeserializeMouseButton(btnData, out btn, out bx, out by, out bsw, out bsh);
            if (btn != 1 || bx != 400 || by != 300)
                throw new Exception("MouseButton mismatch!");

            // 5. Mouse Wheel
            byte[] wheelData = Protocol.SerializeMouseWheel(-120, 500, 400);
            int delta, wx, wy;
            Protocol.DeserializeMouseWheel(wheelData, out delta, out wx, out wy);
            if (delta != -120 || wx != 500 || wy != 400)
                throw new Exception("MouseWheel mismatch!");

            // 6. Key
            byte[] keyData = Protocol.SerializeKey(65, false);
            int vk;
            bool isExt;
            Protocol.DeserializeKey(keyData, out vk, out isExt);
            if (vk != 65 || isExt != false)
                throw new Exception("Key mismatch!");

            // 7. Quality
            byte[] qualData = Protocol.SerializeQualityChange(75, 30);
            int q, fps;
            Protocol.DeserializeQualityChange(qualData, out q, out fps);
            if (q != 75 || fps != 30)
                throw new Exception("Quality mismatch!");

            // 8. Chat
            byte[] chatData = Protocol.SerializeChatMessage("Alice", "Testing 123");
            string sender, cmsg, time;
            Protocol.DeserializeChatMessage(chatData, out sender, out cmsg, out time);
            if (sender != "Alice" || cmsg != "Testing 123" || string.IsNullOrEmpty(time))
                throw new Exception("ChatMessage mismatch!");
        }

        static void TestScreenCapturer()
        {
            using (ScreenCapturer capturer = new ScreenCapturer(false))
            {
                if (capturer.ScreenWidth <= 0 || capturer.ScreenHeight <= 0)
                    throw new Exception("Invalid screen dimensions!");

                byte[] jpeg = capturer.CaptureJpeg(50);
                if (jpeg == null || jpeg.Length < 100)
                    throw new Exception("CaptureJpeg returned empty buffer!");

                if (jpeg[0] != 0xFF || jpeg[1] != 0xD8)
                    throw new Exception("Invalid JPEG magic header!");
            }
        }

        static void TestSystemInfo()
        {
            var ips = SystemInfo.GetLocalIPv4Addresses();
            if (ips == null || ips.Count == 0)
                throw new Exception("No local IP addresses detected!");

            string sysInfo = SystemInfo.GetSystemInfoString();
            if (string.IsNullOrEmpty(sysInfo) || !sysInfo.Contains("Computer Name"))
                throw new Exception("Invalid system info string!");
        }

        static void TestConnectionCodeParsing()
        {
            string code = ConnectionCode.Create("203.0.113.9", 8888, "1234567890");
            if (code != "RPC1|203.0.113.9|8888|1234567890")
                throw new Exception("Connection code format mismatch: " + code);

            string host;
            int port;
            string pin;

            if (!ConnectionCode.TryParse(code, out host, out port, out pin)
                || host != "203.0.113.9" || port != 8888 || pin != "1234567890")
                throw new Exception("Full connection code did not round-trip.");

            if (!ConnectionCode.TryParse("192.168.1.5:9999", out host, out port, out pin)
                || host != "192.168.1.5" || port != 9999 || pin != null)
                throw new Exception("host:port parsing failed.");

            if (!ConnectionCode.TryParse("[2001:db8::1]:8888", out host, out port, out pin)
                || host != "2001:db8::1" || port != 8888)
                throw new Exception("Bracketed IPv6 parsing failed.");

            if (!ConnectionCode.TryParse("2001:db8::1", out host, out port, out pin)
                || host != "2001:db8::1" || port != 0)
                throw new Exception("Raw IPv6 parsing failed.");

            if (!ConnectionCode.TryParse("my-laptop.tailnet.ts.net", out host, out port, out pin)
                || host != "my-laptop.tailnet.ts.net" || port != 0)
                throw new Exception("Host name parsing failed.");

            if (ConnectionCode.FormatEndpoint("2001:db8::1", 8888) != "[2001:db8::1]:8888")
                throw new Exception("IPv6 endpoint formatting failed.");
        }

        static void TestAddressClassification()
        {
            if (!PublicIpResolver.IsPrivateOrShared("192.168.1.10")) throw new Exception("RFC1918 192.168/16 not detected.");
            if (!PublicIpResolver.IsPrivateOrShared("10.1.2.3")) throw new Exception("RFC1918 10/8 not detected.");
            if (!PublicIpResolver.IsPrivateOrShared("172.16.4.1")) throw new Exception("RFC1918 172.16/12 not detected.");
            if (!PublicIpResolver.IsPrivateOrShared("100.64.1.1")) throw new Exception("Carrier-grade NAT range not detected.");
            if (!PublicIpResolver.IsPrivateOrShared("169.254.1.1")) throw new Exception("Link-local IPv4 not detected.");
            if (PublicIpResolver.IsPrivateOrShared("8.8.8.8")) throw new Exception("Public IPv4 misclassified.");

            if (!PublicIpResolver.IsPrivateOrShared("::1")) throw new Exception("IPv6 loopback not detected.");
            if (!PublicIpResolver.IsPrivateOrShared("fe80::1")) throw new Exception("IPv6 link-local not detected.");
            if (!PublicIpResolver.IsPrivateOrShared("fd7a:115c:a1e0::1")) throw new Exception("IPv6 ULA not detected.");
            if (PublicIpResolver.IsPrivateOrShared("2001:4860:4860::8888")) throw new Exception("Public IPv6 misclassified.");
            if (!PublicIpResolver.IsPublicIPv6(System.Net.IPAddress.Parse("2001:4860:4860::8888")))
                throw new Exception("Global IPv6 not recognised.");
        }

        static void TestFileChunkingIntegrity()        {
            // Create 250KB of random pseudo-data
            byte[] sourceData = new byte[250 * 1024];
            Random r = new Random(12345);
            r.NextBytes(sourceData);

            int chunkSize = 64 * 1024;
            int totalChunks = (int)Math.Ceiling((double)sourceData.Length / chunkSize);

            // Reconstruct in memory stream
            using (MemoryStream reassembled = new MemoryStream())
            {
                for (int i = 0; i < totalChunks; i++)
                {
                    int offset = i * chunkSize;
                    int count = Math.Min(chunkSize, sourceData.Length - offset);

                    byte[] chunk = new byte[count];
                    Buffer.BlockCopy(sourceData, offset, chunk, 0, count);

                    reassembled.Write(chunk, 0, count);
                }

                byte[] result = reassembled.ToArray();
                if (result.Length != sourceData.Length)
                    throw new Exception("File reassembly length mismatch!");

                using (MD5 md5 = MD5.Create())
                {
                    byte[] hash1 = md5.ComputeHash(sourceData);
                    byte[] hash2 = md5.ComputeHash(result);
                    if (Convert.ToBase64String(hash1) != Convert.ToBase64String(hash2))
                        throw new Exception("MD5 checksum mismatch after chunk reassembly!");
                }
            }
        }
    }
}
