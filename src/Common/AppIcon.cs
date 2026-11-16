using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace RemotePC.Common
{
    /// <summary>
    /// The application's brand artwork. Both the icon and the logo are embedded inside
    /// RemotePC.exe, so a single copied .exe still shows the correct artwork on the second PC.
    /// </summary>
    public static class AppIcon
    {
        // Logical names given to the files on the compiler command line
        // (/resource:assets\app.ico,RemotePC.AppIcon.ico).
        private const string IconResource = "RemotePC.AppIcon.ico";
        private const string LogoResource = "RemotePC.AppLogo.png";

        private static Image _logo;

        /// <summary>16x16, for the notification-area icon.</summary>
        public static Icon Small { get { return GetIcon(16); } }

        /// <summary>32x32, for window title bars and the taskbar.</summary>
        public static Icon Large { get { return GetIcon(32); } }

        /// <summary>
        /// Returns the best matching size from the embedded multi-resolution icon, falling
        /// back to the icon already stamped on the executable, and finally to the Windows
        /// default so a missing resource can never crash a form.
        /// </summary>
        public static Icon GetIcon(int size)
        {
            try
            {
                using (Stream s = OpenResource(IconResource))
                {
                    if (s != null)
                    {
                        using (Icon full = new Icon(s))
                        {
                            return new Icon(full, new Size(size, size));
                        }
                    }
                }
            }
            catch { }

            try
            {
                string exe = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                {
                    Icon fromExe = Icon.ExtractAssociatedIcon(exe);
                    if (fromExe != null) return new Icon(fromExe, new Size(size, size));
                }
            }
            catch { }

            return SystemIcons.Application;
        }

        /// <summary>The square logo tile, drawn from the embedded PNG.</summary>
        public static Image Logo
        {
            get
            {
                if (_logo == null)
                {
                    Image loaded = null;
                    try
                    {
                        using (Stream s = OpenResource(LogoResource))
                        {
                            if (s != null)
                            {
                                using (Image tmp = Image.FromStream(s))
                                {
                                    loaded = new Bitmap(tmp);
                                }
                            }
                        }
                    }
                    catch { }

                    _logo = loaded != null ? loaded : new Bitmap(1, 1);
                }
                return _logo;
            }
        }

        /// <summary>Gives a form the branded title-bar / taskbar icon.</summary>
        public static void Apply(Form form)
        {
            if (form == null) return;
            try { form.Icon = Large; }
            catch { }
        }

        /// <summary>A click-free logo badge for a tab header.</summary>
        public static PictureBox CreateLogoBadge(int size)
        {
            PictureBox pb = new PictureBox();
            pb.Size = new Size(size, size);
            pb.SizeMode = PictureBoxSizeMode.Zoom;
            pb.BackColor = Color.Transparent;
            pb.TabStop = false;
            pb.Image = Logo;
            return pb;
        }

        private static Stream OpenResource(string logicalName)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Stream s = asm.GetManifestResourceStream(logicalName);
                if (s != null) return s;
            }
            catch { }
            return null;
        }
    }
}
