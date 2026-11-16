using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace RemotePC.Common
{
    public enum ScreenCaptureMode
    {
        PrimaryDisplay = 0,
        AllDisplays = 1
    }

    public class ScreenCapturer : IDisposable
    {
        private static ImageCodecInfo _jpegCodec;
        private Bitmap _screenBitmap;
        private Graphics _screenGraphics;
        private int _screenWidth;
        private int _screenHeight;
        private int _screenX;
        private int _screenY;
        private bool _drawCursor;
        private ScreenCaptureMode _captureMode;

        static ScreenCapturer()
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < codecs.Length; i++)
            {
                if (codecs[i].FormatID == ImageFormat.Jpeg.Guid)
                {
                    _jpegCodec = codecs[i];
                    break;
                }
            }
        }

        public ScreenCapturer(bool drawCursor) : this(drawCursor, ScreenCaptureMode.PrimaryDisplay)
        {
        }

        public ScreenCapturer(bool drawCursor, ScreenCaptureMode mode)
        {
            _drawCursor = drawCursor;
            _captureMode = mode;
            InitializeBitmap();
        }

        public int ScreenWidth { get { return _screenWidth; } }
        public int ScreenHeight { get { return _screenHeight; } }
        public int ScreenX { get { return _screenX; } }
        public int ScreenY { get { return _screenY; } }

        public bool DrawCursor
        {
            get { return _drawCursor; }
            set { _drawCursor = value; }
        }

        public ScreenCaptureMode CaptureMode
        {
            get { return _captureMode; }
            set
            {
                if (_captureMode != value)
                {
                    _captureMode = value;
                    InitializeBitmap();
                }
            }
        }

        public static Rectangle GetCurrentPhysicalScreenBounds(ScreenCaptureMode mode)
        {
            if (mode == ScreenCaptureMode.AllDisplays && Screen.AllScreens.Length > 1)
            {
                Rectangle vs = SystemInformation.VirtualScreen;
                return vs;
            }

            // Primary display - query true physical device capabilities
            IntPtr hdc = NativeMethods.GetDC(IntPtr.Zero);
            int physW = NativeMethods.GetDeviceCaps(hdc, NativeMethods.DESKTOPHORZRES);
            int physH = NativeMethods.GetDeviceCaps(hdc, NativeMethods.DESKTOPVERTRES);
            NativeMethods.ReleaseDC(IntPtr.Zero, hdc);

            Rectangle pb = Screen.PrimaryScreen.Bounds;
            int w = Math.Max(physW, pb.Width);
            int h = Math.Max(physH, pb.Height);

            return new Rectangle(0, 0, w, h);
        }

        private void InitializeBitmap()
        {
            Rectangle bounds = GetCurrentPhysicalScreenBounds(_captureMode);
            _screenX = bounds.X;
            _screenY = bounds.Y;
            _screenWidth = bounds.Width;
            _screenHeight = bounds.Height;

            if (_screenWidth <= 0) _screenWidth = 1920;
            if (_screenHeight <= 0) _screenHeight = 1080;

            if (_screenGraphics != null)
            {
                _screenGraphics.Dispose();
                _screenGraphics = null;
            }
            if (_screenBitmap != null)
            {
                _screenBitmap.Dispose();
                _screenBitmap = null;
            }

            _screenBitmap = new Bitmap(_screenWidth, _screenHeight, PixelFormat.Format32bppRgb);
            _screenGraphics = Graphics.FromImage(_screenBitmap);
        }

        public byte[] CaptureJpeg(int quality)
        {
            // Verify if resolution changed
            Rectangle currentBounds = GetCurrentPhysicalScreenBounds(_captureMode);
            if (currentBounds.Width != _screenWidth || currentBounds.Height != _screenHeight ||
                currentBounds.X != _screenX || currentBounds.Y != _screenY)
            {
                InitializeBitmap();
            }

            if (quality < 10) quality = 10;
            if (quality > 100) quality = 100;

            try
            {
                // BitBlt with CAPTUREBLT from desktop DC for 100% full physical desktop capture
                IntPtr hdcSrc = NativeMethods.GetDC(IntPtr.Zero);
                IntPtr hdcDest = _screenGraphics.GetHdc();
                try
                {
                    NativeMethods.BitBlt(hdcDest, 0, 0, _screenWidth, _screenHeight,
                        hdcSrc, _screenX, _screenY,
                        NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);
                }
                finally
                {
                    _screenGraphics.ReleaseHdc(hdcDest);
                    NativeMethods.ReleaseDC(IntPtr.Zero, hdcSrc);
                }

                // Draw cursor if enabled
                if (_drawCursor)
                {
                    DrawCurrentCursor(_screenGraphics);
                }

                using (MemoryStream ms = new MemoryStream(64 * 1024))
                {
                    EncoderParameters encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);

                    _screenBitmap.Save(ms, _jpegCodec, encoderParams);
                    return ms.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        private void DrawCurrentCursor(Graphics g)
        {
            try
            {
                NativeMethods.CURSORINFO cursorInfo = new NativeMethods.CURSORINFO();
                cursorInfo.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.CURSORINFO));

                if (NativeMethods.GetCursorInfo(out cursorInfo) && cursorInfo.flags == NativeMethods.CURSOR_SHOWING)
                {
                    IntPtr hdc = g.GetHdc();
                    try
                    {
                        int cx = cursorInfo.ptScreenPos.x - _screenX;
                        int cy = cursorInfo.ptScreenPos.y - _screenY;
                        NativeMethods.DrawIcon(hdc, cx, cy, cursorInfo.hCursor);
                    }
                    finally
                    {
                        g.ReleaseHdc(hdc);
                    }
                }
            }
            catch
            {
                // Ignore cursor drawing failure
            }
        }

        public void Dispose()
        {
            if (_screenGraphics != null)
            {
                _screenGraphics.Dispose();
                _screenGraphics = null;
            }
            if (_screenBitmap != null)
            {
                _screenBitmap.Dispose();
                _screenBitmap = null;
            }
        }
    }
}
