using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using RemotePC.Client;

namespace RemotePC.UI
{
    public enum ViewMode
    {
        Fit,
        Stretch,
        Original
    }

    public class ViewerForm : Form
    {
        private readonly RemoteClient _client;

        // UI Controls
        private Panel _toolbar;
        private RemoteCanvas _canvas;
        private Label _lblInfo;
        private ComboBox _cboDisplay;
        private ComboBox _cboQuality;
        private ComboBox _cboViewMode;
        private CheckBox _chkControl;
        private Button _btnActions;
        private ContextMenuStrip _actionsMenu;
        private Button _btnFiles;
        private Button _btnChat;
        private Button _btnSysInfo;
        private Button _btnClipboard;
        private Button _btnScreenshot;
        private Button _btnDisconnect;
        private Button _btnFullscreen;

        // Sub-windows
        private FileManagerForm _fileManagerForm;
        private ChatForm _chatForm;

        // State
        private ViewMode _viewMode = ViewMode.Fit;
        private bool _isFullscreen = false;
        private FormWindowState _prevWindowState;
        private FormBorderStyle _prevBorderStyle;
        private bool _allowInput = true;

        // Stats
        private int _fpsCounter;
        private int _currentFps;
        private Timer _statsTimer;

        public ViewerForm(RemoteClient client)
        {
            _client = client;
            InitializeComponent();
            HookEvents();
            StartStatsTimer();
        }

        private void InitializeComponent()
        {
            this.Text = "Remote Desktop - " + _client.HostAddress + ":" + _client.HostPort;
            this.Size = new Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.FromArgb(20, 20, 20);
            this.KeyPreview = true;

            #region Top Toolbar

            _toolbar = new Panel();
            _toolbar.Dock = DockStyle.Top;
            _toolbar.Height = 42;
            _toolbar.BackColor = Color.FromArgb(30, 32, 38);
            _toolbar.Padding = new Padding(6);
            this.Controls.Add(_toolbar);

            // Host / Latency / FPS label
            _lblInfo = new Label();
            _lblInfo.Text = string.Format("Host: {0} | Res: {1}x{2} | FPS: -- | Ping: --ms",
                _client.HostAddress, _client.RemoteScreenWidth, _client.RemoteScreenHeight);
            _lblInfo.ForeColor = Color.FromArgb(220, 220, 220);
            _lblInfo.Location = new Point(10, 12);
            _lblInfo.AutoSize = true;
            _lblInfo.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            _toolbar.Controls.Add(_lblInfo);

            int rightX = this.ClientSize.Width - 10;

            // Disconnect Button
            _btnDisconnect = CreateToolbarButton("Disconnect", Color.FromArgb(220, 53, 69));
            _btnDisconnect.Location = new Point(rightX - 90, 6);
            _btnDisconnect.Size = new Size(85, 28);
            _btnDisconnect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnDisconnect.Click += delegate { _client.Disconnect(); this.Close(); };
            _toolbar.Controls.Add(_btnDisconnect);
            rightX -= 96;

            // Fullscreen Button
            _btnFullscreen = CreateToolbarButton("⛶ Fullscreen", Color.FromArgb(50, 55, 65));
            _btnFullscreen.Location = new Point(rightX - 95, 6);
            _btnFullscreen.Size = new Size(90, 28);
            _btnFullscreen.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnFullscreen.Click += delegate { ToggleFullscreen(); };
            _toolbar.Controls.Add(_btnFullscreen);
            rightX -= 100;

            // Chat Button
            _btnChat = CreateToolbarButton("💬 Chat", Color.FromArgb(50, 55, 65));
            _btnChat.Location = new Point(rightX - 75, 6);
            _btnChat.Size = new Size(70, 28);
            _btnChat.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnChat.Click += BtnChat_Click;
            _toolbar.Controls.Add(_btnChat);
            rightX -= 80;

            // Files Button
            _btnFiles = CreateToolbarButton("📁 Files", Color.FromArgb(50, 55, 65));
            _btnFiles.Location = new Point(rightX - 70, 6);
            _btnFiles.Size = new Size(65, 28);
            _btnFiles.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnFiles.Click += BtnFiles_Click;
            _toolbar.Controls.Add(_btnFiles);
            rightX -= 76;

            // SysInfo Button
            _btnSysInfo = CreateToolbarButton("ℹ Info", Color.FromArgb(50, 55, 65));
            _btnSysInfo.Location = new Point(rightX - 65, 6);
            _btnSysInfo.Size = new Size(60, 28);
            _btnSysInfo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnSysInfo.Click += delegate { _client.RequestSystemInfo(); };
            _toolbar.Controls.Add(_btnSysInfo);
            rightX -= 70;

            // Clipboard Sync Button
            _btnClipboard = CreateToolbarButton("📋 Clip", Color.FromArgb(50, 55, 65));
            _btnClipboard.Location = new Point(rightX - 65, 6);
            _btnClipboard.Size = new Size(60, 28);
            _btnClipboard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnClipboard.Click += BtnClipboard_Click;
            _toolbar.Controls.Add(_btnClipboard);
            rightX -= 70;

            // Screenshot Button
            _btnScreenshot = CreateToolbarButton("📷 Snap", Color.FromArgb(50, 55, 65));
            _btnScreenshot.Location = new Point(rightX - 65, 6);
            _btnScreenshot.Size = new Size(60, 28);
            _btnScreenshot.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnScreenshot.Click += BtnScreenshot_Click;
            _toolbar.Controls.Add(_btnScreenshot);
            rightX -= 70;

            // Quick Actions Dropdown Button
            BuildActionsMenu();
            _btnActions = CreateToolbarButton("⚡ Actions ▾", Color.FromArgb(0, 122, 204));
            _btnActions.Location = new Point(rightX - 85, 6);
            _btnActions.Size = new Size(80, 28);
            _btnActions.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnActions.Click += delegate { _actionsMenu.Show(_btnActions, new Point(0, _btnActions.Height)); };
            _toolbar.Controls.Add(_btnActions);
            rightX -= 90;

            // Quality Dropdown
            _cboQuality = new ComboBox();
            _cboQuality.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboQuality.Items.AddRange(new object[] { "Speed (35%)", "Balanced (60%)", "High (80%)", "Ultra (95%)" });
            _cboQuality.SelectedIndex = 1; // Balanced
            _cboQuality.Location = new Point(rightX - 110, 7);
            _cboQuality.Size = new Size(105, 25);
            _cboQuality.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cboQuality.SelectedIndexChanged += CboQuality_SelectedIndexChanged;
            _toolbar.Controls.Add(_cboQuality);
            rightX -= 115;

            // View Mode Dropdown
            _cboViewMode = new ComboBox();
            _cboViewMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboViewMode.Items.AddRange(new object[] { "Fit Screen", "Original (1:1)", "Stretch" });
            _cboViewMode.SelectedIndex = 0;
            _cboViewMode.Location = new Point(rightX - 105, 7);
            _cboViewMode.Size = new Size(100, 25);
            _cboViewMode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cboViewMode.SelectedIndexChanged += CboViewMode_SelectedIndexChanged;
            _toolbar.Controls.Add(_cboViewMode);
            rightX -= 110;

            // Display Monitor Dropdown
            _cboDisplay = new ComboBox();
            _cboDisplay.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboDisplay.Items.AddRange(new object[] { "Primary Display", "All Displays" });
            _cboDisplay.SelectedIndex = 0;
            _cboDisplay.Location = new Point(rightX - 120, 7);
            _cboDisplay.Size = new Size(115, 25);
            _cboDisplay.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _cboDisplay.SelectedIndexChanged += delegate
            {
                _client.SendDisplayChange((byte)_cboDisplay.SelectedIndex);
            };
            _toolbar.Controls.Add(_cboDisplay);
            rightX -= 125;

            // Control CheckBox
            _chkControl = new CheckBox();
            _chkControl.Text = "Control";
            _chkControl.Checked = true;
            _chkControl.ForeColor = Color.White;
            _chkControl.Location = new Point(rightX - 75, 9);
            _chkControl.Size = new Size(70, 22);
            _chkControl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _chkControl.CheckedChanged += delegate { _allowInput = _chkControl.Checked; };
            _toolbar.Controls.Add(_chkControl);

            #endregion

            #region Canvas Setup

            _canvas = new RemoteCanvas();
            _canvas.Dock = DockStyle.Fill;
            _canvas.BackColor = Color.FromArgb(15, 15, 15);
            this.Controls.Add(_canvas);
            _canvas.BringToFront();

            _canvas.OnCanvasMouseDown += Canvas_MouseDown;
            _canvas.OnCanvasMouseUp += Canvas_MouseUp;
            _canvas.OnCanvasMouseMove += Canvas_MouseMove;
            _canvas.OnCanvasMouseWheel += Canvas_MouseWheel;
            _canvas.OnCanvasKeyDown += Canvas_KeyDown;
            _canvas.OnCanvasKeyUp += Canvas_KeyUp;

            #endregion
        }

        private Button CreateToolbarButton(string text, Color bg)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.Font = new Font("Segoe UI", 8.5f);
            return btn;
        }

        private void BuildActionsMenu()
        {
            _actionsMenu = new ContextMenuStrip();
            _actionsMenu.Items.Add("Ctrl + Alt + Del", null, delegate { _client.SendSpecialCommand(1); });
            _actionsMenu.Items.Add("Alt + Tab", null, delegate { _client.SendSpecialCommand(2); });
            _actionsMenu.Items.Add("Windows Key", null, delegate { _client.SendSpecialCommand(3); });
            _actionsMenu.Items.Add("Show Desktop (Win+D)", null, delegate { _client.SendSpecialCommand(4); });
            _actionsMenu.Items.Add("Open Task Manager", null, delegate { _client.SendSpecialCommand(5); });
            _actionsMenu.Items.Add(new ToolStripSeparator());
            _actionsMenu.Items.Add("Lock Remote PC", null, delegate { _client.SendSpecialCommand(6); });
        }

        private void HookEvents()
        {
            _client.OnFrameReceived += Client_OnFrameReceived;
            _client.OnDisconnected += Client_OnDisconnected;
            _client.OnChatMessage += Client_OnChatMessage;
            _client.OnSystemInfoReceived += Client_OnSystemInfoReceived;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _client.OnFrameReceived -= Client_OnFrameReceived;
            _client.OnDisconnected -= Client_OnDisconnected;
            _client.OnChatMessage -= Client_OnChatMessage;
            _client.OnSystemInfoReceived -= Client_OnSystemInfoReceived;

            if (_statsTimer != null)
            {
                _statsTimer.Stop();
                _statsTimer.Dispose();
            }

            if (_client.IsConnected)
            {
                _client.Disconnect();
            }

            base.OnFormClosing(e);
        }

        private void StartStatsTimer()
        {
            _statsTimer = new Timer();
            _statsTimer.Interval = 1000;
            _statsTimer.Tick += delegate
            {
                _currentFps = _fpsCounter;
                _fpsCounter = 0;

                _client.SendPing();

                if (_client.IsConnected)
                {
                    _lblInfo.Text = string.Format("Host: {0} | Res: {1}x{2} | FPS: {3} | Ping: {4}ms",
                        _client.HostAddress, _client.RemoteScreenWidth, _client.RemoteScreenHeight,
                        _currentFps, _client.LastPingMs);
                }
            };
            _statsTimer.Start();
        }

        private void Client_OnFrameReceived(Bitmap frame, long frameId)
        {
            _fpsCounter++;
            _canvas.UpdateFrame(frame, _viewMode);
        }

        private void Client_OnDisconnected()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(Client_OnDisconnected));
                return;
            }

            MessageBox.Show("Disconnected from remote host.", "Remote Session Ended", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }

        private void Client_OnChatMessage(string sender, string message, string time)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string, string, string>(Client_OnChatMessage), sender, message, time);
                return;
            }

            if (_chatForm == null || _chatForm.IsDisposed)
            {
                _chatForm = new ChatForm(_client.HostAddress, _client.SendChatMessage);
            }

            _chatForm.AppendMessage(sender, message, time, Color.FromArgb(200, 50, 50));

            if (!_chatForm.Visible)
            {
                _chatForm.Show(this);
            }
        }

        private void Client_OnSystemInfoReceived(string info)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(Client_OnSystemInfoReceived), info);
                return;
            }

            using (SystemInfoForm sif = new SystemInfoForm(info))
            {
                sif.ShowDialog(this);
            }
        }

        #region Input Handling

        private void Canvas_MouseDown(MouseButtons button, int rx, int ry)
        {
            if (!_allowInput) return;
            int btn = button == MouseButtons.Left ? 1 : (button == MouseButtons.Right ? 2 : 3);
            _client.SendMouseButton(btn, true, rx, ry, _client.RemoteScreenWidth, _client.RemoteScreenHeight);
        }

        private void Canvas_MouseUp(MouseButtons button, int rx, int ry)
        {
            if (!_allowInput) return;
            int btn = button == MouseButtons.Left ? 1 : (button == MouseButtons.Right ? 2 : 3);
            _client.SendMouseButton(btn, false, rx, ry, _client.RemoteScreenWidth, _client.RemoteScreenHeight);
        }

        private void Canvas_MouseMove(int rx, int ry)
        {
            if (!_allowInput) return;
            _client.SendMouseMove(rx, ry, _client.RemoteScreenWidth, _client.RemoteScreenHeight);
        }

        private void Canvas_MouseWheel(int delta, int rx, int ry)
        {
            if (!_allowInput) return;
            _client.SendMouseWheel(delta, rx, ry);
        }

        private void Canvas_KeyDown(Keys keyData)
        {
            if (!_allowInput) return;
            bool isExt = (keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Left || keyData == Keys.Right ||
                          keyData == Keys.Insert || keyData == Keys.Delete || keyData == Keys.Home || keyData == Keys.End ||
                          keyData == Keys.PageUp || keyData == Keys.PageDown);
            _client.SendKeyboard((int)(keyData & Keys.KeyCode), true, isExt);
        }

        private void Canvas_KeyUp(Keys keyData)
        {
            if (!_allowInput) return;
            bool isExt = (keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.Left || keyData == Keys.Right ||
                          keyData == Keys.Insert || keyData == Keys.Delete || keyData == Keys.Home || keyData == Keys.End ||
                          keyData == Keys.PageUp || keyData == Keys.PageDown);
            _client.SendKeyboard((int)(keyData & Keys.KeyCode), false, isExt);
        }

        #endregion

        #region Toolbar Action Handlers

        private void CboQuality_SelectedIndexChanged(object sender, EventArgs e)
        {
            int quality = 60;
            int fps = 25;
            switch (_cboQuality.SelectedIndex)
            {
                case 0: quality = 35; fps = 15; break; // Speed
                case 1: quality = 60; fps = 25; break; // Balanced
                case 2: quality = 80; fps = 30; break; // High
                case 3: quality = 95; fps = 30; break; // Ultra
            }
            _client.SendQualitySettings(quality, fps);
        }

        private void CboViewMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (_cboViewMode.SelectedIndex)
            {
                case 0: _viewMode = ViewMode.Fit; break;
                case 1: _viewMode = ViewMode.Original; break;
                case 2: _viewMode = ViewMode.Stretch; break;
            }
            _canvas.Invalidate();
        }

        private void BtnFiles_Click(object sender, EventArgs e)
        {
            if (_fileManagerForm == null || _fileManagerForm.IsDisposed)
            {
                _fileManagerForm = new FileManagerForm(_client);
            }
            _fileManagerForm.Show(this);
            _fileManagerForm.BringToFront();
        }

        private void BtnChat_Click(object sender, EventArgs e)
        {
            if (_chatForm == null || _chatForm.IsDisposed)
            {
                _chatForm = new ChatForm(_client.HostAddress, _client.SendChatMessage);
            }
            _chatForm.Show(this);
            _chatForm.BringToFront();
        }

        private void BtnClipboard_Click(object sender, EventArgs e)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText();
                    _client.SendClipboard(text);
                    MessageBox.Show("Local clipboard text sent to remote host.", "Clipboard Synced", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Local clipboard does not contain text.", "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Clipboard error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnScreenshot_Click(object sender, EventArgs e)
        {
            Bitmap current = _canvas.CurrentBitmap;
            if (current == null) return;

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg";
                sfd.FileName = "RemoteScreenshot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        current.Save(sfd.FileName);
                        MessageBox.Show("Screenshot saved successfully!", "Screenshot Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error saving screenshot: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void ToggleFullscreen()
        {
            if (!_isFullscreen)
            {
                _prevWindowState = this.WindowState;
                _prevBorderStyle = this.FormBorderStyle;

                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
                _btnFullscreen.Text = "⛶ Exit Full";
                _isFullscreen = true;
            }
            else
            {
                this.FormBorderStyle = _prevBorderStyle;
                this.WindowState = _prevWindowState;
                _btnFullscreen.Text = "⛶ Fullscreen";
                _isFullscreen = false;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F11)
            {
                ToggleFullscreen();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion
    }

    /// <summary>
    /// High-performance double-buffered render surface for the remote desktop stream.
    /// </summary>
    public class RemoteCanvas : Control
    {
        private Bitmap _frameBitmap;
        private readonly object _bitmapLock = new object();
        private ViewMode _viewMode = ViewMode.Fit;
        private Rectangle _drawnImageRect;

        public event Action<MouseButtons, int, int> OnCanvasMouseDown;
        public event Action<MouseButtons, int, int> OnCanvasMouseUp;
        public event Action<int, int> OnCanvasMouseMove;
        public event Action<int, int, int> OnCanvasMouseWheel;
        public event Action<Keys> OnCanvasKeyDown;
        public event Action<Keys> OnCanvasKeyUp;

        public Bitmap CurrentBitmap
        {
            get
            {
                lock (_bitmapLock)
                {
                    return _frameBitmap != null ? new Bitmap(_frameBitmap) : null;
                }
            }
        }

        public RemoteCanvas()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer |
                          ControlStyles.Selectable, true);
            this.TabStop = true;
        }

        public void UpdateFrame(Bitmap newBmp, ViewMode mode)
        {
            Bitmap oldBmp = null;
            lock (_bitmapLock)
            {
                oldBmp = _frameBitmap;
                _frameBitmap = newBmp;
                _viewMode = mode;
            }

            if (oldBmp != null)
            {
                oldBmp.Dispose();
            }

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;

            Bitmap bmpToDraw = null;
            lock (_bitmapLock)
            {
                if (_frameBitmap != null)
                {
                    bmpToDraw = _frameBitmap;
                }
            }

            if (bmpToDraw == null)
            {
                // Draw connecting / waiting screen
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(180, 180, 180)))
                using (Font font = new Font("Segoe UI", 12f))
                {
                    string msg = "Waiting for remote desktop screen stream...";
                    SizeF sz = g.MeasureString(msg, font);
                    g.DrawString(msg, font, brush, (this.Width - sz.Width) / 2, (this.Height - sz.Height) / 2);
                }
                return;
            }

            int w = bmpToDraw.Width;
            int h = bmpToDraw.Height;

            if (_viewMode == ViewMode.Fit)
            {
                double scaleX = (double)this.Width / w;
                double scaleY = (double)this.Height / h;
                double scale = Math.Min(scaleX, scaleY);

                int dw = (int)(w * scale);
                int dh = (int)(h * scale);
                int dx = (this.Width - dw) / 2;
                int dy = (this.Height - dh) / 2;

                _drawnImageRect = new Rectangle(dx, dy, dw, dh);
                g.DrawImage(bmpToDraw, _drawnImageRect);
            }
            else if (_viewMode == ViewMode.Stretch)
            {
                _drawnImageRect = new Rectangle(0, 0, this.Width, this.Height);
                g.DrawImage(bmpToDraw, _drawnImageRect);
            }
            else // Original 1:1
            {
                int dx = (this.Width - w) / 2;
                int dy = (this.Height - h) / 2;
                _drawnImageRect = new Rectangle(dx > 0 ? dx : 0, dy > 0 ? dy : 0, w, h);
                g.DrawImage(bmpToDraw, _drawnImageRect);
            }
        }

        private bool TranslateCoordinates(Point clientPt, out int rx, out int ry)
        {
            rx = 0;
            ry = 0;

            Bitmap bmp = null;
            lock (_bitmapLock) { bmp = _frameBitmap; }
            if (bmp == null || _drawnImageRect.Width <= 0 || _drawnImageRect.Height <= 0) return false;

            if (!_drawnImageRect.Contains(clientPt))
            {
                return false;
            }

            int relX = clientPt.X - _drawnImageRect.X;
            int relY = clientPt.Y - _drawnImageRect.Y;

            rx = (int)Math.Round((relX * (double)bmp.Width) / _drawnImageRect.Width);
            ry = (int)Math.Round((relY * (double)bmp.Height) / _drawnImageRect.Height);

            if (rx < 0) rx = 0;
            if (rx >= bmp.Width) rx = bmp.Width - 1;
            if (ry < 0) ry = 0;
            if (ry >= bmp.Height) ry = bmp.Height - 1;

            return true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            this.Focus();
            int rx, ry;
            if (TranslateCoordinates(e.Location, out rx, out ry) && OnCanvasMouseDown != null)
            {
                OnCanvasMouseDown(e.Button, rx, ry);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            int rx, ry;
            if (TranslateCoordinates(e.Location, out rx, out ry) && OnCanvasMouseUp != null)
            {
                OnCanvasMouseUp(e.Button, rx, ry);
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int rx, ry;
            if (TranslateCoordinates(e.Location, out rx, out ry) && OnCanvasMouseMove != null)
            {
                OnCanvasMouseMove(rx, ry);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int rx, ry;
            TranslateCoordinates(e.Location, out rx, out ry);
            if (OnCanvasMouseWheel != null)
            {
                OnCanvasMouseWheel(e.Delta, rx, ry);
            }
            base.OnMouseWheel(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (OnCanvasKeyDown != null)
            {
                OnCanvasKeyDown(e.KeyData);
            }
            e.Handled = true;
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (OnCanvasKeyUp != null)
            {
                OnCanvasKeyUp(e.KeyData);
            }
            e.Handled = true;
            base.OnKeyUp(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            // Allow all keys to be captured by the canvas for remote control
            return true;
        }
    }
}
