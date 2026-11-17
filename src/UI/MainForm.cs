using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RemotePC.Client;
using RemotePC.Common;
using RemotePC.Server;

namespace RemotePC.UI
{
    public class MainForm : Form
    {
        private TabControl _tabControl;
        private TabPage _tabHost;
        private TabPage _tabClient;

        #region Host Controls

        private Label _lblHostStatusIndicator;
        private Label _lblHostStatusText;
        private Panel _pnlHostStatus;
        private GroupBox _gbHostNetwork;
        private ComboBox _cboHostIps;
        private Button _btnCopyIp;
        private Button _btnCopyCode;
        private NumericUpDown _numHostPort;
        private TextBox _txtHostPassword;
        private Button _btnToggleHostPass;
        private Button _btnGenPin;
        private TableLayoutPanel _tlpHostNetwork;
        private TableLayoutPanel _pnlHostActions;
        private CheckBox _chkAllowControl;
        private CheckBox _chkAllowFiles;
        private Button _btnToggleServer;
        private TableLayoutPanel _pnlLogHeader;
        private Label _lblHostLogTitle;
        private ListBox _lbHostLog;
        private Button _btnHostChat;

        // Automation controls - Host tab
        private GroupBox _gbHostAuto;
        private CheckBox _chkAutoStartServer;
        private CheckBox _chkTrayOnClose;
        private Label _lblAutoStatus;

        // Brand logo badges in the tab headers
        private PictureBox _logoHost;
        private PictureBox _logoClient;

        private RemoteServer _server;

        #endregion

        #region Client Controls

        private GroupBox _gbClientConnect;
        private TableLayoutPanel _tlpClientConnect;
        private ComboBox _cboClientIp;
        private NumericUpDown _numClientPort;
        private TextBox _txtClientPassword;
        private Button _btnToggleClientPass;
        private Button _btnConnect;
        private Label _lblClientStatus;
        private Label _lblGuideTitle;
        private RichTextBox _rtbGuide;

        // Automation controls - Client tab
        private GroupBox _gbDiscovery;
        private ComboBox _cboDiscovered;
        private Button _btnQuickConnect;
        private CheckBox _chkAutoConnect;

        // Captions measured by UpdateResponsiveLayout so fields can shrink to fit narrow screens.
        private Label _lblDiscoveryCaption;
        private Label _lblClientIpCaption;
        private Label _lblClientPortCaption;

        // Direct tab-page child whose width has to follow the window (it is not a group box).
        private FlowLayoutPanel _flowClientOpts;

        private readonly string _historyFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RemotePC_History.txt");

        #endregion

        #region Tray / Settings

        private NotifyIcon _trayIcon;
        private AppSettings _settings;

        // Automatic re-configuration when the laptop moves between networks.
        private NetworkChangeWatcher _networkWatcher;
        private System.Windows.Forms.Timer _internetRefreshTimer;
        private int _publicIpLookupBusy;
        private string _lastPublicIp;
        private int _advertisedPort;

        #endregion

        public MainForm()
        {
            // Scale the hand-written absolute layout with the monitor's DPI. Without this the
            // fonts grow at 125%/150% display scaling while every hardcoded offset stays put,
            // so the rows overlap and the right-hand buttons get clipped on high-DPI laptops.
            this.AutoScaleDimensions = new SizeF(96f, 96f);
            this.AutoScaleMode = AutoScaleMode.Dpi;

            _settings = AppSettings.Load();

            // Optional network.ini next to the executable overrides network-related settings,
            // so the same file can be copied to both PCs for identical network behaviour.
            string networkConfigPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "network.ini");
            AppSettings.ApplyNetworkConfigFile(_settings, networkConfigPath);
            AppSettings.CreateDefaultNetworkConfig(networkConfigPath);

            InitializeComponent();
            InitializeTrayIcon();
            InitializeHostTab();
            InitializeClientTab();
            LoadIpHistory();
            GenerateRandomPin();
            LoadSettingsIntoUI();
            InitializeNetworkAutomation();

            this.Resize += MainForm_Resize;
            this.Shown += MainForm_Shown;
        }

        /// <summary>
        /// Keeps the window inside the visible desktop. After DPI scaling the minimum size can
        /// exceed a small laptop screen, which would push the window's lower half off-screen.
        /// </summary>
        private void ClampToWorkingArea()
        {
            try
            {
                Rectangle wa = Screen.FromControl(this).WorkingArea;

                Size min = this.MinimumSize;
                if (min.Width > wa.Width) min.Width = wa.Width;
                if (min.Height > wa.Height) min.Height = wa.Height;

                int w = Math.Max(min.Width, Math.Min(this.Width, wa.Width));
                int h = Math.Max(min.Height, Math.Min(this.Height, wa.Height));

                this.MinimumSize = min;
                this.Size = new Size(w, h);
                this.Location = new Point(
                    wa.X + Math.Max(0, (wa.Width - w) / 2),
                    wa.Y + Math.Max(0, (wa.Height - h) / 2));
            }
            catch { }
        }

        private void InitializeComponent()
        {
            this.Text = "Remote PC - Work Across 2 PCs";
            this.Size = new Size(760, 680);
            this.MinimumSize = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(245, 247, 250);
            AppIcon.Apply(this);

            _tabControl = new TabControl();
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            _tabControl.Padding = new Point(16, 8);
            // Let the two long tab captions wrap onto a second row on a narrow/high-DPI
            // laptop instead of being cut off with the scroll arrows.
            _tabControl.Multiline = true;

            _tabHost = new TabPage("  Allow Remote Access (Host)  ");
            _tabHost.BackColor = Color.White;
            _tabHost.AutoScroll = true;
            _tabControl.TabPages.Add(_tabHost);

            _tabClient = new TabPage("  Connect to Remote PC (Client)  ");
            _tabClient.BackColor = Color.White;
            _tabClient.AutoScroll = true;
            _tabControl.TabPages.Add(_tabClient);

            this.Controls.Add(_tabControl);
        }

        #region Tray Icon

        private void InitializeTrayIcon()
        {
            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "Remote PC";
            // Branded 16x16 from the embedded icon, falling back to the executable's own icon
            try { _trayIcon.Icon = AppIcon.Small; }
            catch { _trayIcon.Icon = this.Icon ?? SystemIcons.Application; }

            ContextMenu trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Open Remote PC", delegate
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
            });
            trayMenu.MenuItems.Add("-");
            trayMenu.MenuItems.Add("Exit", delegate
            {
                _trayIcon.Visible = false;
                Application.Exit();
            });
            _trayIcon.ContextMenu = trayMenu;
            _trayIcon.DoubleClick += delegate
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
            };
        }

        #endregion

        #region Host Tab Setup

        private void InitializeHostTab()
        {
            int top = 15;

            // 1. Header Banner
            Label lblTitle = new Label();
            lblTitle.Text = "Host Mode: Let Another PC Control This PC";
            lblTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblTitle.Location = new Point(20, top);
            lblTitle.AutoSize = true;
            _tabHost.Controls.Add(lblTitle);

            // Brand logo badge in the header's top-right corner. Kept inside the 32px header
            // band (y=13, height 30 -> bottom 43) so it clears the status panel at y=47 and
            // never grows into it when the display scaling enlarges every control.
            _logoHost = AppIcon.CreateLogoBadge(30);
            _logoHost.Location = new Point(690, 13);
            _logoHost.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _tabHost.Controls.Add(_logoHost);

            top += 32;

            // 2. Status Panel
            _pnlHostStatus = new Panel();
            _pnlHostStatus.Location = new Point(20, top);
            _pnlHostStatus.Size = new Size(690, 36);
            _pnlHostStatus.BackColor = Color.FromArgb(240, 243, 246);
            _pnlHostStatus.BorderStyle = BorderStyle.FixedSingle;
            _tabHost.Controls.Add(_pnlHostStatus);

            _lblHostStatusIndicator = new Label();
            _lblHostStatusIndicator.Text = "●";
            _lblHostStatusIndicator.Font = new Font("Segoe UI", 14f);
            _lblHostStatusIndicator.ForeColor = Color.Red;
            _lblHostStatusIndicator.Location = new Point(10, 4);
            _lblHostStatusIndicator.Size = new Size(25, 25);
            _pnlHostStatus.Controls.Add(_lblHostStatusIndicator);

            _lblHostStatusText = new Label();
            _lblHostStatusText.Text = "Server Stopped. Click 'Start Server' to allow connections.";
            _lblHostStatusText.Location = new Point(35, 8);
            _lblHostStatusText.AutoSize = true;
            _pnlHostStatus.Controls.Add(_lblHostStatusText);
            top += 46;

            // 3. Network Configuration Card
            _gbHostNetwork = new GroupBox();
            _gbHostNetwork.Text = " Connection Credentials & Address ";
            _gbHostNetwork.Location = new Point(20, top);
            _gbHostNetwork.Size = new Size(690, 155);
            _gbHostNetwork.Padding = new Padding(12, 10, 12, 10);
            _tabHost.Controls.Add(_gbHostNetwork);

            _tlpHostNetwork = new TableLayoutPanel();
            _tlpHostNetwork.Dock = DockStyle.Fill;
            _tlpHostNetwork.ColumnCount = 2;
            _tlpHostNetwork.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _tlpHostNetwork.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _tlpHostNetwork.RowCount = 3;
            _tlpHostNetwork.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            _tlpHostNetwork.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            _tlpHostNetwork.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            _gbHostNetwork.Controls.Add(_tlpHostNetwork);

            // Row 0: IP Address
            Label lblIp = new Label() { Text = "This PC's IP:", AutoSize = true, Margin = new Padding(0, 8, 12, 0) };
            _tlpHostNetwork.Controls.Add(lblIp, 0, 0);

            FlowLayoutPanel flowIp = new FlowLayoutPanel() { AutoSize = false, Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 5, 0, 0) };
            _cboHostIps = new ComboBox() { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 460 };
            RefreshHostIps();
            flowIp.Controls.Add(_cboHostIps);

            _btnCopyIp = new Button() { Text = "Copy IP", Width = 88, Height = 27, Margin = new Padding(8, 0, 0, 0) };
            _btnCopyIp.Click += BtnCopyIp_Click;
            flowIp.Controls.Add(_btnCopyIp);

            _btnCopyCode = new Button() { Text = "Copy Code", Width = 88, Height = 27, Margin = new Padding(8, 0, 0, 0) };
            _btnCopyCode.Click += BtnCopyCode_Click;
            flowIp.Controls.Add(_btnCopyCode);
            _tlpHostNetwork.Controls.Add(flowIp, 1, 0);

            // Row 1: Port
            Label lblPort = new Label() { Text = "Port:", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };
            _tlpHostNetwork.Controls.Add(lblPort, 0, 1);

            _numHostPort = new NumericUpDown() { Width = 100, Minimum = 1024, Maximum = 65535, Value = 8888, Margin = new Padding(0, 3, 0, 0) };
            _tlpHostNetwork.Controls.Add(_numHostPort, 1, 1);

            // Row 2: Security PIN
            Label lblPass = new Label() { Text = "PIN / Password:", AutoSize = true, Margin = new Padding(0, 8, 12, 0) };
            _tlpHostNetwork.Controls.Add(lblPass, 0, 2);

            FlowLayoutPanel flowPass = new FlowLayoutPanel() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 3, 0, 0) };
            _txtHostPassword = new TextBox() { Width = 180, PasswordChar = '•' };
            flowPass.Controls.Add(_txtHostPassword);

            _btnToggleHostPass = new Button() { Text = "Show", Width = 65, Height = 27, Margin = new Padding(6, 0, 0, 0) };
            _btnToggleHostPass.Click += delegate
            {
                if (_txtHostPassword.PasswordChar == '•')
                {
                    _txtHostPassword.PasswordChar = '\0';
                    _btnToggleHostPass.Text = "Hide";
                }
                else
                {
                    _txtHostPassword.PasswordChar = '•';
                    _btnToggleHostPass.Text = "Show";
                }
            };
            flowPass.Controls.Add(_btnToggleHostPass);

            _btnGenPin = new Button() { Text = "Random PIN", Width = 105, Height = 27, Margin = new Padding(6, 0, 0, 0) };
            _btnGenPin.Click += delegate { GenerateRandomPin(); };
            flowPass.Controls.Add(_btnGenPin);
            _tlpHostNetwork.Controls.Add(flowPass, 1, 2);

            top += 165;

            // 4. Action Row (Checkboxes on Left, Start Server Button on Right)
            _pnlHostActions = new TableLayoutPanel();
            _pnlHostActions.Location = new Point(20, top);
            _pnlHostActions.Size = new Size(690, 44);
            _pnlHostActions.ColumnCount = 2;
            _pnlHostActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _pnlHostActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _pnlHostActions.RowCount = 1;
            _tabHost.Controls.Add(_pnlHostActions);

            FlowLayoutPanel flowCheckboxes = new FlowLayoutPanel() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = new Padding(0) };
            _chkAllowControl = new CheckBox() { Text = "Allow Mouse & Keyboard Control", Checked = true, AutoSize = true, Margin = new Padding(0, 8, 20, 0) };
            _chkAllowFiles = new CheckBox() { Text = "Allow Remote File Transfer", Checked = true, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
            flowCheckboxes.Controls.Add(_chkAllowControl);
            flowCheckboxes.Controls.Add(_chkAllowFiles);
            _pnlHostActions.Controls.Add(flowCheckboxes, 0, 0);

            _btnToggleServer = new Button();
            _btnToggleServer.Text = "Start Server";
            _btnToggleServer.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            _btnToggleServer.BackColor = Color.FromArgb(40, 167, 69);
            _btnToggleServer.ForeColor = Color.White;
            _btnToggleServer.FlatStyle = FlatStyle.Flat;
            _btnToggleServer.FlatAppearance.BorderSize = 0;
            _btnToggleServer.Size = new Size(185, 36);
            _btnToggleServer.Margin = new Padding(0);
            _btnToggleServer.Click += BtnToggleServer_Click;
            _pnlHostActions.Controls.Add(_btnToggleServer, 1, 0);

            top += 48;

            // 5. Zero-Work Automation GroupBox
            _gbHostAuto = new GroupBox();
            _gbHostAuto.Text = " ⚡ Zero-Work Automation & Shortcuts ";
            _gbHostAuto.Location = new Point(20, top);
            _gbHostAuto.Size = new Size(690, 148);
            _gbHostAuto.Padding = new Padding(12, 8, 12, 8);
            _tabHost.Controls.Add(_gbHostAuto);

            // Row 1: Options checkboxes
            FlowLayoutPanel flowAutoOpts = new FlowLayoutPanel();
            flowAutoOpts.Location = new Point(12, 22);
            flowAutoOpts.Size = new Size(660, 32);
            flowAutoOpts.FlowDirection = FlowDirection.LeftToRight;
            flowAutoOpts.WrapContents = false;
            _gbHostAuto.Controls.Add(flowAutoOpts);

            _chkAutoStartServer = new CheckBox() { Text = "⚡ Auto-Start Server on Launch", AutoSize = true, Margin = new Padding(0, 4, 24, 0) };
            _chkAutoStartServer.CheckedChanged += delegate { SaveSettingsFromUI(); };
            flowAutoOpts.Controls.Add(_chkAutoStartServer);

            _chkTrayOnClose = new CheckBox() { Text = "🔕 Minimize to Tray on Close", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
            _chkTrayOnClose.CheckedChanged += delegate { SaveSettingsFromUI(); };
            flowAutoOpts.Controls.Add(_chkTrayOnClose);

            // Row 2: Shortcut buttons
            FlowLayoutPanel flowShortcuts = new FlowLayoutPanel();
            flowShortcuts.Location = new Point(12, 60);
            flowShortcuts.Size = new Size(660, 38);
            flowShortcuts.FlowDirection = FlowDirection.LeftToRight;
            flowShortcuts.WrapContents = false;
            _gbHostAuto.Controls.Add(flowShortcuts);

            Button btnStartMenu = new Button() { Text = "📌 Start Menu", AutoSize = true, Height = 30, Margin = new Padding(0, 0, 10, 0) };
            btnStartMenu.FlatStyle = FlatStyle.Flat;
            btnStartMenu.Click += delegate { CreateShortcut("startmenu"); };
            flowShortcuts.Controls.Add(btnStartMenu);

            Button btnDesktop = new Button() { Text = "🖥 Desktop", AutoSize = true, Height = 30, Margin = new Padding(0, 0, 10, 0) };
            btnDesktop.FlatStyle = FlatStyle.Flat;
            btnDesktop.Click += delegate { CreateShortcut("desktop"); };
            flowShortcuts.Controls.Add(btnDesktop);

            Button btnStartup = new Button() { Text = "🚀 Start with Windows", AutoSize = true, Height = 30, Margin = new Padding(0, 0, 10, 0) };
            btnStartup.FlatStyle = FlatStyle.Flat;
            btnStartup.Click += delegate { CreateShortcut("startup"); };
            flowShortcuts.Controls.Add(btnStartup);

            Button btnFirewall = new Button() { Text = "🛡 Fix Firewall", AutoSize = true, Height = 30, Margin = new Padding(0) };
            btnFirewall.FlatStyle = FlatStyle.Flat;
            btnFirewall.Click += delegate { FixFirewallNow(); };
            flowShortcuts.Controls.Add(btnFirewall);

            Button btnRustDesk = new Button() { Text = "🪟 Launch RustDesk", AutoSize = true, Height = 30, Margin = new Padding(10, 0, 0, 0) };
            btnRustDesk.FlatStyle = FlatStyle.Flat;
            btnRustDesk.Click += delegate { LaunchRustDeskFallback(null); };
            flowShortcuts.Controls.Add(btnRustDesk);

            // Row 3: Status of the automatic setup
            _lblAutoStatus = new Label();
            _lblAutoStatus.Location = new Point(12, 104);
            _lblAutoStatus.AutoSize = true;
            _lblAutoStatus.ForeColor = Color.FromArgb(90, 90, 90);
            _lblAutoStatus.Text = "Automatic setup: checking...";
            _gbHostAuto.Controls.Add(_lblAutoStatus);

            top += 158;

            // 6. Activity Log Header
            _pnlLogHeader = new TableLayoutPanel();
            _pnlLogHeader.Location = new Point(20, top);
            _pnlLogHeader.Size = new Size(690, 32);
            _pnlLogHeader.ColumnCount = 2;
            _pnlLogHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _pnlLogHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _pnlLogHeader.RowCount = 1;
            _tabHost.Controls.Add(_pnlLogHeader);

            _lblHostLogTitle = new Label() { Text = "Activity & Connection Log:", AutoSize = true, Margin = new Padding(0, 5, 0, 0) };
            _pnlLogHeader.Controls.Add(_lblHostLogTitle, 0, 0);

            _btnHostChat = new Button() { Text = "Chat With Client", Width = 150, Height = 28, Enabled = false, Margin = new Padding(0) };
            _btnHostChat.Click += BtnHostChat_Click;
            _pnlLogHeader.Controls.Add(_btnHostChat, 1, 0);

            top += 34;

            // 7. Log Box
            _lbHostLog = new ListBox();
            _lbHostLog.Location = new Point(20, top);
            _lbHostLog.Size = new Size(690, 130);
            _lbHostLog.Font = new Font("Consolas", 9.5f);
            _lbHostLog.IntegralHeight = false;
            _tabHost.Controls.Add(_lbHostLog);
        }

        private void CreateShortcut(string target)
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                bool ok = false;
                string location = "";

                if (target == "startmenu")
                {
                    ok = AppShortcutHelper.CreateStartMenuShortcut(exePath, "Remote PC", out location);
                }
                else if (target == "desktop")
                {
                    ok = AppShortcutHelper.CreateDesktopShortcut(exePath, "Remote PC", out location);
                }
                else if (target == "startup")
                {
                    // Toggle: a second click removes it again.
                    if (AppShortcutHelper.IsStartWithWindowsEnabled())
                    {
                        string removeError;
                        ok = AppShortcutHelper.SetStartWithWindows(false, out removeError);
                        location = ok ? "Removed from Windows startup." : removeError;
                        if (_settings != null) { _settings.StartWithWindows = false; _settings.Save(); }
                    }
                    else
                    {
                        ok = AppShortcutHelper.CreateStartupShortcut(exePath, "Remote PC", out location);
                        if (ok && _settings != null) { _settings.StartWithWindows = true; _settings.Save(); }
                    }
                }

                if (ok)
                {
                    MessageBox.Show(
                        "Shortcut created successfully!\n\nLocation: " + location,
                        "Shortcut Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(
                        "Could not create shortcut. Please create it manually.",
                        "Shortcut Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error creating shortcut: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshHostIps()
        {
            string previous = null;
            NetworkAdapterInfo selected = _cboHostIps.SelectedItem as NetworkAdapterInfo;
            if (selected != null) previous = selected.IpAddress;

            _cboHostIps.Items.Clear();
            List<NetworkAdapterInfo> adapters = SystemInfo.GetLocalAddressesForRemoteAccess();
            for (int i = 0; i < adapters.Count; i++)
            {
                _cboHostIps.Items.Add(adapters[i]);
            }

            // Keep the user's choice across a network change when that address still exists.
            if (!string.IsNullOrEmpty(previous))
            {
                for (int i = 0; i < _cboHostIps.Items.Count; i++)
                {
                    NetworkAdapterInfo info = _cboHostIps.Items[i] as NetworkAdapterInfo;
                    if (info != null && string.Equals(info.IpAddress, previous, StringComparison.OrdinalIgnoreCase))
                    {
                        _cboHostIps.SelectedIndex = i;
                        return;
                    }
                }
            }

            if (_cboHostIps.Items.Count > 0)
            {
                _cboHostIps.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Generates the access PIN. Ten digits rather than six: now that the server can open
        /// its own port on the router it is reachable from the internet, and the search space
        /// has to be large enough that guessing is hopeless even before the lockout kicks in.
        /// Uses a cryptographic RNG so PINs are not predictable from one another.
        /// </summary>
        private void GenerateRandomPin()
        {
            _txtHostPassword.Text = GenerateSecurePin(10);
        }

        private static string GenerateSecurePin(int digits)
        {
            byte[] buffer = new byte[digits];
            using (System.Security.Cryptography.RandomNumberGenerator rng =
                System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                // Rejection sampling keeps every digit equally likely; a plain modulo would
                // bias the low digits.
                int filled = 0;
                while (filled < digits)
                {
                    rng.GetBytes(buffer);
                    for (int i = filled; i < digits; i++)
                    {
                        if (buffer[i] < 250)
                        {
                            buffer[filled] = (byte)(buffer[i] % 10);
                            filled++;
                        }
                    }
                }
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(digits);
            for (int i = 0; i < digits; i++) sb.Append((char)('0' + buffer[i]));
            return sb.ToString();
        }

        private void BtnCopyIp_Click(object sender, EventArgs e)
        {
            string ip = GetSelectedHostAddress();
            if (string.IsNullOrEmpty(ip)) return;

            Clipboard.SetText(ip);
            MessageBox.Show(
                string.Format("Copied IP '{0}' to clipboard!\n\nType this IP into the other PC, or use 'Copy Code' to copy the IP, port and PIN together.", ip),
                "IP Address Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Copies address + port + PIN in one string. The other PC can paste it straight into
        /// the "Remote PC Address" box; the port and PIN fields are filled automatically.
        /// This is what makes an IPv6 address or a changed public IP painless to send.
        /// </summary>
        private void BtnCopyCode_Click(object sender, EventArgs e)
        {
            string host = GetSelectedHostAddress();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("No address is available yet. Start the server and try again.",
                    "Nothing to Copy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int port = _advertisedPort > 0 ? _advertisedPort : (int)_numHostPort.Value;
            string pin = _txtHostPassword.Text.Trim();
            string code = ConnectionCode.Create(host, port, pin);

            try { Clipboard.SetText(code); }
            catch { }

            MessageBox.Show(
                "Connection code copied to the clipboard:\n\n" + code +
                "\n\nOn the other PC: open RemotePC, paste it into the 'Remote PC Address' box and click Connect. " +
                "The port and PIN are filled in automatically.",
                "Connection Code Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>The selected address, or the first usable one as a fallback.</summary>
        private string GetSelectedHostAddress()
        {
            NetworkAdapterInfo selected = _cboHostIps.SelectedItem as NetworkAdapterInfo;
            if (selected != null && !string.IsNullOrEmpty(selected.IpAddress)) return selected.IpAddress;

            for (int i = 0; i < _cboHostIps.Items.Count; i++)
            {
                NetworkAdapterInfo info = _cboHostIps.Items[i] as NetworkAdapterInfo;
                if (info != null && !string.IsNullOrEmpty(info.IpAddress) && info.IpAddress != "127.0.0.1")
                    return info.IpAddress;
            }

            return null;
        }

        private void BtnToggleServer_Click(object sender, EventArgs e)
        {
            if (_server == null || !_server.IsRunning)
            {
                // Start Server
                try
                {
                    _server = new RemoteServer();
                    _server.Password = _txtHostPassword.Text.Trim();
                    _server.AllowControl = _chkAllowControl.Checked;
                    _server.AllowFileTransfer = _chkAllowFiles.Checked;

                    _server.OnLog += Server_OnLog;
                    _server.OnStatusChanged += Server_OnStatusChanged;
                    _server.OnClientConnected += Server_OnClientConnected;
                    _server.OnClientDisconnected += Server_OnClientDisconnected;

                    int port = (int)_numHostPort.Value;
                    _server.Start(port, _settings != null ? _settings.BindAddress : null);

                    _btnToggleServer.Text = "Stop Server";
                    _btnToggleServer.BackColor = Color.FromArgb(220, 53, 69);
                    _numHostPort.Enabled = false;
                    _txtHostPassword.Enabled = false;
                    _btnGenPin.Enabled = false;

                    // Start LAN broadcasting so other PCs can discover us
                    if (_settings == null || _settings.EnableDiscovery)
                    {
                        try { NetworkDiscovery.StartBroadcasting(port); }
                        catch { }
                    }

                    // Ensure the firewall lets this port in, without ever asking the user to do it
                    if (_settings == null || _settings.EnableFirewallAuto)
                    {
                        EnsureFirewallRulesAsync(port);
                    }

                    // Open the router for internet access, hands-free. NAT-PMP is tried first
                    // because plenty of routers answer it while their UPnP service is off; UPnP
                    // is the fallback for routers that only speak IGD.
                    OpenInternetAccessAsync(port);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not start server: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                // Stop Server
                _server.Stop();
                _btnToggleServer.Text = "Start Server";
                _btnToggleServer.BackColor = Color.FromArgb(40, 167, 69);
                _numHostPort.Enabled = true;
                _txtHostPassword.Enabled = true;
                _btnGenPin.Enabled = true;
                _btnHostChat.Enabled = false;

                try { NetworkDiscovery.StopBroadcasting(); }
                catch { }

                StopInternetAccess();
            }
        }

        /// <summary>
        /// Makes this PC reachable from a different network with no router configuration.
        /// Tries NAT-PMP first, then UPnP, then falls back to reporting the public address
        /// (so a manual forward still works) or explaining why the network cannot accept
        /// inbound connections at all.
        /// </summary>
        private void OpenInternetAccessAsync(int port)
        {
            _advertisedPort = port;

            bool tryNat = _settings == null || _settings.EnableNatPmp;
            bool tryUpnp = _settings == null || _settings.EnableUpnp;

            if (!tryNat)
            {
                if (tryUpnp)
                {
                    UPnPPortForwarder.ForwardPortAsync(port, delegate(bool upnpOk, string upnpMessage)
                    {
                        LogHostLine("[UPnP] " + upnpMessage);

                        if (upnpOk && !string.IsNullOrEmpty(UPnPPortForwarder.ExternalPublicIp))
                        {
                            _advertisedPort = port;
                            _lastPublicIp = UPnPPortForwarder.ExternalPublicIp;
                            LogHostLine("[Internet] Ready from any network. Connect to "
                                + ConnectionCode.FormatEndpoint(UPnPPortForwarder.ExternalPublicIp, port));
                            ShowInternetAddress(port, UPnPPortForwarder.ExternalPublicIp, true);
                            VerifyMappedAddress(UPnPPortForwarder.ExternalPublicIp);
                            return;
                        }

                        LogHostLine("[Internet] No automatic router mapping available on this network.");
                        ResolveFallbackAddress(port);
                    });
                }
                else
                {
                    LogHostLine("[Internet] Automatic router mapping disabled by network.ini.");
                    ResolveFallbackAddress(port);
                }
                return;
            }

            NatPmpPortMapper.MapAsync(port, delegate(bool natOk, string natMessage)
            {
                LogHostLine("[NAT-PMP] " + natMessage);

                if (natOk && !string.IsNullOrEmpty(NatPmpPortMapper.ExternalPublicIp))
                {
                    int externalPort = NatPmpPortMapper.ExternalPort > 0 ? NatPmpPortMapper.ExternalPort : port;
                    _advertisedPort = externalPort;
                    _lastPublicIp = NatPmpPortMapper.ExternalPublicIp;

                    LogHostLine("[Internet] Ready from any network. Connect to "
                        + ConnectionCode.FormatEndpoint(NatPmpPortMapper.ExternalPublicIp, externalPort));
                    ShowInternetAddress(externalPort, NatPmpPortMapper.ExternalPublicIp, true);
                    VerifyMappedAddress(NatPmpPortMapper.ExternalPublicIp);
                    return;
                }

                if (!tryUpnp)
                {
                    LogHostLine("[Internet] UPnP disabled by network.ini.");
                    ResolveFallbackAddress(port);
                    return;
                }

                // NAT-PMP unavailable: fall back to UPnP IGD.
                UPnPPortForwarder.ForwardPortAsync(port, delegate(bool upnpOk, string upnpMessage)
                {
                    LogHostLine("[UPnP] " + upnpMessage);

                    if (upnpOk && !string.IsNullOrEmpty(UPnPPortForwarder.ExternalPublicIp))
                    {
                        _advertisedPort = port;
                        _lastPublicIp = UPnPPortForwarder.ExternalPublicIp;
                        LogHostLine("[Internet] Ready from any network. Connect to "
                            + ConnectionCode.FormatEndpoint(UPnPPortForwarder.ExternalPublicIp, port));
                        ShowInternetAddress(port, UPnPPortForwarder.ExternalPublicIp, true);
                        VerifyMappedAddress(UPnPPortForwarder.ExternalPublicIp);
                        return;
                    }

                    LogHostLine("[Internet] No automatic router mapping available on this network.");
                    ResolveFallbackAddress(port);
                });
            });
        }

        /// <summary>
        /// Re-checks the address the router handed us. A private address here means the ISP
        /// has already NATed the connection; no amount of port forwarding on this router can
        /// make it reachable. Saying so plainly saves hours of "why doesn't it connect?".
        /// </summary>
        private void VerifyMappedAddress(string mappedIp)
        {
            if (PublicIpResolver.IsPrivateOrShared(mappedIp))
            {
                LogHostLine("[Internet] WARNING: the router's public address is " + mappedIp +
                    " - this network is behind carrier-grade NAT. Direct connections will not work; " +
                    "use IPv6, Tailscale, or another network.");
            }

            PublicIpResolver.ResolveAsync(delegate(string observed)
            {
                if (string.IsNullOrEmpty(observed)) return;

                if (!string.Equals(observed, mappedIp, StringComparison.OrdinalIgnoreCase))
                {
                    LogHostLine("[Internet] WARNING: the router reports " + mappedIp +
                        " but the internet sees " + observed + ". This is double NAT; the automatic " +
                        "mapping may not be reachable. IPv6/Tailscale is the reliable fallback.");
                }
            });
        }

        /// <summary>
        /// When no mapping was possible, still find the network's public IPv4 address. If it
        /// is genuinely public, a manual port forward on the router will work and the user can
        /// copy that address. If it is carrier-grade NAT, say so instead of pretending.
        /// </summary>
        private void ResolveFallbackAddress(int port)
        {
            PublicIpResolver.ResolveAsync(delegate(string publicIp)
            {
                if (this.IsDisposed) return;

                if (string.IsNullOrEmpty(publicIp))
                {
                    LogHostLine("[Internet] Could not determine this network's public IP. " +
                        "Use IPv6, Tailscale, or a different network.");
                    return;
                }

                bool shared = PublicIpResolver.IsPrivateOrShared(publicIp);
                if (shared)
                {
                    LogHostLine("[Internet] Public address " + publicIp +
                        " is carrier-grade NAT; inbound connections cannot reach this PC.");
                    return;
                }

                LogHostLine("[Internet] Public address: " + ConnectionCode.FormatEndpoint(publicIp, port) +
                    ". If you manually forward TCP " + port +
                    " on the router, the other PC can use this address (or use IPv6/Tailscale).");

                try
                {
                    this.BeginInvoke(new Action(delegate
                    {
                        if (!this.IsDisposed) ShowInternetAddress(port, publicIp, false, false);
                    }));
                }
                catch { }
            });
        }

        private void ShowInternetAddress(int port, string publicIp, bool mapped)
        {
            ShowInternetAddress(port, publicIp, mapped, true);
        }

        /// <summary>
        /// Puts the internet address at the top of the Host tab's address list, so "Copy IP"
        /// and "Copy Code" hand the user the exact string the other PC has to dial.
        /// </summary>
        private void ShowInternetAddress(int port, string publicIp, bool mapped, bool select)
        {
            if (this.IsDisposed) return;

            if (this.InvokeRequired)
            {
                try { this.BeginInvoke(new Action<int, string, bool, bool>(ShowInternetAddress), port, publicIp, mapped, select); }
                catch { }
                return;
            }

            try
            {
                string label = mapped
                    ? "★ Internet (automatic)"
                    : "⚠ Internet (port forward needed)";

                RemoveInternetAddressEntries();

                NetworkAdapterInfo info = new NetworkAdapterInfo();
                info.Name = label;
                info.Description = mapped
                    ? "Works from another network - use Copy Code"
                    : "Public address found, but this router did not allow automatic mapping";
                info.IpAddress = publicIp;

                _cboHostIps.Items.Insert(0, info);
                if (select) _cboHostIps.SelectedIndex = 0;

                // Never overwrite the "Active Session" text while somebody is connected.
                if (!_btnHostChat.Enabled)
                {
                    if (mapped)
                    {
                        _lblHostStatusText.Text = string.Format(
                            "Server RUNNING on port {0}. From another network connect to {1}.",
                            port, ConnectionCode.FormatEndpoint(publicIp, port));
                    }
                    else
                    {
                        _lblHostStatusText.Text = string.Format(
                            "Server RUNNING on port {0}. Public IP {1} - automatic mapping unavailable; use IPv6/Tailscale.",
                            port, publicIp);
                    }
                }
            }
            catch { }
        }

        private void RemoveInternetAddressEntries()
        {
            for (int i = _cboHostIps.Items.Count - 1; i >= 0; i--)
            {
                NetworkAdapterInfo info = _cboHostIps.Items[i] as NetworkAdapterInfo;
                if (info == null || info.Name == null) continue;

                if (info.Name.StartsWith("★ Internet", StringComparison.Ordinal) ||
                    info.Name.StartsWith("⚠ Internet", StringComparison.Ordinal))
                {
                    _cboHostIps.Items.RemoveAt(i);
                }
            }
        }

        private void StopInternetAccess()
        {
            try { NatPmpPortMapper.Unmap(); }
            catch { }
            try { UPnPPortForwarder.Stop(); }
            catch { }

            _advertisedPort = 0;
            _lastPublicIp = null;
            try { RemoveInternetAddressEntries(); }
            catch { }
        }

        #region Automatic Network Adaptation

        /// <summary>
        /// Starts the "laptop moved to another Wi-Fi" machinery. Both the router mappings
        /// and Windows Firewall are profile-specific, so a network change must rebuild them;
        /// the periodic tick catches public IP changes that happen without an event.
        /// </summary>
        private void InitializeNetworkAutomation()
        {
            try
            {
                UPnPPortForwarder.PublicIpChanged += HandlePublicIpChanged;
                NatPmpPortMapper.PublicIpChanged += HandlePublicIpChanged;

                _networkWatcher = new NetworkChangeWatcher(HandleNetworkChanged, 2500);
                _networkWatcher.Start();

                _internetRefreshTimer = new System.Windows.Forms.Timer();
                _internetRefreshTimer.Interval = 5 * 60 * 1000;
                _internetRefreshTimer.Tick += delegate { RevalidateInternetAccess(); };
                _internetRefreshTimer.Start();
            }
            catch
            {
                // Automation is best-effort; the app still works on the LAN without it.
            }
        }

        private void HandleNetworkChanged()
        {
            if (this.IsDisposed) return;

            try { this.BeginInvoke(new Action(ApplyNetworkChange)); }
            catch { }
        }

        /// <summary>Runs on the UI thread after Windows reports a network change.</summary>
        private void ApplyNetworkChange()
        {
            if (this.IsDisposed) return;

            LogHostLine("[Network] This PC changed networks - re-configuring automatically.");
            RefreshHostIps();

            if (_server != null && _server.IsRunning)
            {
                int port = _server.Port;

                // The old subnet's broadcast address and router mapping are both stale.
                if (_settings == null || _settings.EnableDiscovery)
                {
                    try { NetworkDiscovery.StopBroadcasting(); }
                    catch { }
                    try { NetworkDiscovery.StartBroadcasting(port); }
                    catch { }
                }

                // A new network usually has a different firewall profile (Public/Private).
                if (_settings == null || _settings.EnableFirewallAuto)
                {
                    EnsureFirewallRulesAsync(port);
                }

                StopInternetAccess();
                OpenInternetAccessAsync(port);

                SetAutoStatus("Automatic setup: adapted to the new network (firewall, discovery, port mapping).");
            }
            else
            {
                RevalidateInternetAccess();
                SetAutoStatus("Automatic setup: adapted to the new network.");
            }
        }

        private void HandlePublicIpChanged(string publicIp)
        {
            if (this.IsDisposed || string.IsNullOrEmpty(publicIp)) return;

            try
            {
                this.BeginInvoke(new Action(delegate
                {
                    if (this.IsDisposed) return;

                    int port = _advertisedPort > 0
                        ? _advertisedPort
                        : (_server != null && _server.IsRunning ? _server.Port : (int)_numHostPort.Value);

                    bool mapped = NatPmpPortMapper.IsMapped || UPnPPortForwarder.IsPortMapped;
                    _lastPublicIp = publicIp;

                    ShowInternetAddress(port, publicIp, mapped, false);
                    LogHostLine("[Internet] Public address changed to "
                        + ConnectionCode.FormatEndpoint(publicIp, port) + ".");
                }));
            }
            catch { }
        }

        /// <summary>
        /// Periodic safety net. Router renewals handle mapped addresses; this catches a
        /// changed public IP on networks where no mapping is available.
        /// </summary>
        private void RevalidateInternetAccess()
        {
            if (this.IsDisposed) return;

            bool mapped = NatPmpPortMapper.IsMapped || UPnPPortForwarder.IsPortMapped;
            if (mapped)
            {
                string mappedIp = !string.IsNullOrEmpty(NatPmpPortMapper.ExternalPublicIp)
                    ? NatPmpPortMapper.ExternalPublicIp
                    : UPnPPortForwarder.ExternalPublicIp;

                if (!string.IsNullOrEmpty(mappedIp))
                {
                    int port = _advertisedPort > 0 ? _advertisedPort : (int)_numHostPort.Value;
                    if (!string.Equals(_lastPublicIp, mappedIp, StringComparison.OrdinalIgnoreCase))
                    {
                        _lastPublicIp = mappedIp;
                        ShowInternetAddress(port, mappedIp, true, false);
                        LogHostLine("[Internet] Public address is "
                            + ConnectionCode.FormatEndpoint(mappedIp, port) + ".");
                    }
                    return;
                }
            }

            if (System.Threading.Interlocked.Exchange(ref _publicIpLookupBusy, 1) == 1) return;

            bool serverRunning = _server != null && _server.IsRunning;
            int lookupPort = _advertisedPort > 0
                ? _advertisedPort
                : (serverRunning ? _server.Port : (int)_numHostPort.Value);

            PublicIpResolver.ResolveAsync(delegate(string publicIp)
            {
                System.Threading.Interlocked.Exchange(ref _publicIpLookupBusy, 0);
                if (this.IsDisposed) return;

                try
                {
                    this.BeginInvoke(new Action(delegate
                    {
                        if (this.IsDisposed || string.IsNullOrEmpty(publicIp)) return;

                        if (!string.Equals(_lastPublicIp, publicIp, StringComparison.OrdinalIgnoreCase))
                        {
                            _lastPublicIp = publicIp;
                            LogHostLine("[Internet] Current public IP: " + publicIp);
                        }

                        if (serverRunning && !PublicIpResolver.IsPrivateOrShared(publicIp))
                            ShowInternetAddress(lookupPort, publicIp, false, false);
                    }));
                }
                catch { }
            });
        }

        #endregion

        private void Server_OnStatusChanged(bool running)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<bool>(Server_OnStatusChanged), running);
                return;
            }

            if (running)
            {
                _lblHostStatusIndicator.ForeColor = Color.FromArgb(40, 167, 69);
                _lblHostStatusText.Text = string.Format("Server is RUNNING on port {0}. Ready for connections.", _server.Port);
            }
            else
            {
                _lblHostStatusIndicator.ForeColor = Color.Red;
                _lblHostStatusText.Text = "Server Stopped. Click 'Start Server' to allow connections.";
            }
        }

        private void Server_OnClientConnected(ServerSession session)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<ServerSession>(Server_OnClientConnected), session);
                return;
            }

            _lblHostStatusIndicator.ForeColor = Color.FromArgb(0, 122, 204);
            _lblHostStatusText.Text = string.Format("Active Session: Client '{0}' ({1}) connected!", session.ClientName, session.ClientEndPoint);
            _btnHostChat.Enabled = true;

            // Tray balloon notification
            if (_chkTrayOnClose != null && _chkTrayOnClose.Checked && _trayIcon != null)
            {
                try
                {
                    _trayIcon.BalloonTipTitle = "Remote PC";
                    _trayIcon.BalloonTipText = string.Format("Client '{0}' connected!", session.ClientName);
                    _trayIcon.ShowBalloonTip(3000);
                }
                catch { }
            }
        }

        private void Server_OnClientDisconnected(ServerSession session)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<ServerSession>(Server_OnClientDisconnected), session);
                return;
            }

            _lblHostStatusIndicator.ForeColor = Color.FromArgb(40, 167, 69);
            _lblHostStatusText.Text = string.Format("Server is RUNNING on port {0}. Ready for connections.", _server.Port);
            _btnHostChat.Enabled = false;
        }

        private void Server_OnLog(string msg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(Server_OnLog), msg);
                return;
            }

            _lbHostLog.Items.Add(msg);
            _lbHostLog.SelectedIndex = _lbHostLog.Items.Count - 1;
        }

        private void BtnHostChat_Click(object sender, EventArgs e)
        {
            if (_server != null && _server.IsRunning)
            {
                ChatForm cf = new ChatForm("Host", delegate(string msg) { _server.BroadcastChat("Host", msg); });
                cf.Show(this);
            }
        }

        #endregion

        #region Client Tab Setup

        private void InitializeClientTab()
        {
            int top = 15;

            // 1. Title
            Label lblTitle = new Label();
            lblTitle.Text = "Client Mode: Connect to & Control Remote PC";
            lblTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblTitle.Location = new Point(20, top);
            lblTitle.AutoSize = true;
            _tabClient.Controls.Add(lblTitle);

            // Same header-band badge on the client tab (y=13, height 30 -> bottom 43, which
            // clears the discovery box at y=50).
            _logoClient = AppIcon.CreateLogoBadge(30);
            _logoClient.Location = new Point(690, 13);
            _logoClient.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _tabClient.Controls.Add(_logoClient);

            top += 35;

            // 2. LAN Auto-Discovery GroupBox
            _gbDiscovery = new GroupBox();
            _gbDiscovery.Text = " 🔍 LAN Auto-Discovery (Same Network) ";
            _gbDiscovery.Location = new Point(20, top);
            _gbDiscovery.Size = new Size(690, 70);
            _gbDiscovery.Padding = new Padding(12, 8, 12, 8);
            _tabClient.Controls.Add(_gbDiscovery);

            FlowLayoutPanel flowDisc = new FlowLayoutPanel();
            flowDisc.Dock = DockStyle.Fill;
            flowDisc.FlowDirection = FlowDirection.LeftToRight;
            flowDisc.WrapContents = false;
            _gbDiscovery.Controls.Add(flowDisc);

            Label lblDisc = new Label() { Text = "Discovered PCs:", AutoSize = true, Margin = new Padding(0, 8, 8, 0) };
            _lblDiscoveryCaption = lblDisc;
            flowDisc.Controls.Add(lblDisc);

            _cboDiscovered = new ComboBox();
            _cboDiscovered.Width = 340;
            _cboDiscovered.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboDiscovered.Margin = new Padding(0, 4, 8, 0);
            flowDisc.Controls.Add(_cboDiscovered);

            _btnQuickConnect = new Button();
            _btnQuickConnect.Text = "⚡ Quick Connect";
            _btnQuickConnect.AutoSize = true;
            _btnQuickConnect.Height = 28;
            _btnQuickConnect.Margin = new Padding(0, 4, 0, 0);
            _btnQuickConnect.FlatStyle = FlatStyle.Flat;
            _btnQuickConnect.BackColor = Color.FromArgb(0, 122, 204);
            _btnQuickConnect.ForeColor = Color.White;
            _btnQuickConnect.FlatAppearance.BorderSize = 0;
            _btnQuickConnect.Click += BtnQuickConnect_Click;
            flowDisc.Controls.Add(_btnQuickConnect);

            top += 80;

            // Start listening for discoveries
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    NetworkDiscovery.StartListening(delegate(DiscoveredHost host)
                    {
                        if (this.IsDisposed) return;
                        try
                        {
                            this.BeginInvoke(new Action(delegate
                            {
                                string entry = string.Format("{0} ({1}:{2})", host.MachineName, host.IpAddress, host.Port);
                                if (!_cboDiscovered.Items.Contains(entry))
                                {
                                    _cboDiscovered.Items.Add(entry);
                                    if (_cboDiscovered.Items.Count == 1)
                                        _cboDiscovered.SelectedIndex = 0;
                                }
                            }));
                        }
                        catch { }
                    });
                }
                catch { }
            });

            // 3. Connection Details GroupBox
            _gbClientConnect = new GroupBox();
            _gbClientConnect.Text = " Remote Host Connection Details ";
            _gbClientConnect.Location = new Point(20, top);
            _gbClientConnect.Size = new Size(690, 168);
            _gbClientConnect.Padding = new Padding(12, 10, 12, 10);
            _tabClient.Controls.Add(_gbClientConnect);

            _tlpClientConnect = new TableLayoutPanel();
            _tlpClientConnect.Dock = DockStyle.Fill;
            _tlpClientConnect.ColumnCount = 2;
            _tlpClientConnect.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _tlpClientConnect.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _tlpClientConnect.RowCount = 3;
            _tlpClientConnect.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            _tlpClientConnect.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
            _tlpClientConnect.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            _gbClientConnect.Controls.Add(_tlpClientConnect);

            // Row 0: IP + Port
            Label lblIp = new Label() { Text = "Remote PC Address:", AutoSize = true, Margin = new Padding(0, 8, 12, 0) };
            _lblClientIpCaption = lblIp;
            _tlpClientConnect.Controls.Add(lblIp, 0, 0);

            FlowLayoutPanel flowIpPort = new FlowLayoutPanel() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 3, 0, 0) };
            _cboClientIp = new ComboBox() { Width = 260 };
            flowIpPort.Controls.Add(_cboClientIp);

            Label lblPort = new Label() { Text = "Port:", AutoSize = true, Margin = new Padding(15, 6, 6, 0) };
            _lblClientPortCaption = lblPort;
            flowIpPort.Controls.Add(lblPort);

            _numClientPort = new NumericUpDown() { Width = 90, Minimum = 1024, Maximum = 65535, Value = 8888 };
            flowIpPort.Controls.Add(_numClientPort);
            _tlpClientConnect.Controls.Add(flowIpPort, 1, 0);

            // Row 1: Access PIN
            Label lblPass = new Label() { Text = "PIN / Password:", AutoSize = true, Margin = new Padding(0, 8, 12, 0) };
            _tlpClientConnect.Controls.Add(lblPass, 0, 1);

            FlowLayoutPanel flowPass = new FlowLayoutPanel() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 3, 0, 0) };
            _txtClientPassword = new TextBox() { Width = 260, PasswordChar = '•' };
            flowPass.Controls.Add(_txtClientPassword);

            _btnToggleClientPass = new Button() { Text = "Show", Width = 65, Height = 27, Margin = new Padding(6, 0, 0, 0) };
            _btnToggleClientPass.Click += delegate
            {
                if (_txtClientPassword.PasswordChar == '•')
                {
                    _txtClientPassword.PasswordChar = '\0';
                    _btnToggleClientPass.Text = "Hide";
                }
                else
                {
                    _txtClientPassword.PasswordChar = '•';
                    _btnToggleClientPass.Text = "Show";
                }
            };
            flowPass.Controls.Add(_btnToggleClientPass);
            _tlpClientConnect.Controls.Add(flowPass, 1, 1);

            // Row 2: Connect Button & Status
            Label lblBlank = new Label() { Text = "", AutoSize = true, Margin = new Padding(0) };
            _tlpClientConnect.Controls.Add(lblBlank, 0, 2);

            FlowLayoutPanel flowConnect = new FlowLayoutPanel() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
            _btnConnect = new Button();
            _btnConnect.Text = "Connect to Remote PC";
            _btnConnect.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            _btnConnect.BackColor = Color.FromArgb(0, 122, 204);
            _btnConnect.ForeColor = Color.White;
            _btnConnect.FlatStyle = FlatStyle.Flat;
            _btnConnect.FlatAppearance.BorderSize = 0;
            _btnConnect.Size = new Size(260, 36);
            _btnConnect.Click += BtnConnect_Click;
            flowConnect.Controls.Add(_btnConnect);

            _lblClientStatus = new Label();
            _lblClientStatus.Text = "";
            _lblClientStatus.ForeColor = Color.FromArgb(80, 80, 80);
            _lblClientStatus.AutoSize = true;
            _lblClientStatus.Margin = new Padding(12, 10, 0, 0);
            flowConnect.Controls.Add(_lblClientStatus);
            _tlpClientConnect.Controls.Add(flowConnect, 1, 2);

            top += 178;

            // 4. Auto-Connect option
            FlowLayoutPanel flowClientOpts = new FlowLayoutPanel();
            _flowClientOpts = flowClientOpts;
            flowClientOpts.Location = new Point(20, top);
            flowClientOpts.Size = new Size(690, 32);
            flowClientOpts.FlowDirection = FlowDirection.LeftToRight;
            _tabClient.Controls.Add(flowClientOpts);

            _chkAutoConnect = new CheckBox() { Text = "⚡ Auto-Connect on App Start", AutoSize = true, Margin = new Padding(0, 4, 20, 0) };
            _chkAutoConnect.CheckedChanged += delegate { SaveSettingsFromUI(); };
            flowClientOpts.Controls.Add(_chkAutoConnect);

            Button btnAddStartMenu = new Button() { Text = "📌 Add to Start Menu", AutoSize = true, Height = 28, Margin = new Padding(0, 2, 0, 0) };
            btnAddStartMenu.FlatStyle = FlatStyle.Flat;
            btnAddStartMenu.Click += delegate { CreateShortcut("startmenu"); };
            flowClientOpts.Controls.Add(btnAddStartMenu);

            Button btnRustDeskClient = new Button() { Text = "🪟 Launch RustDesk", AutoSize = true, Height = 28, Margin = new Padding(10, 2, 0, 0) };
            btnRustDeskClient.FlatStyle = FlatStyle.Flat;
            btnRustDeskClient.Click += delegate { LaunchRustDeskFallback(_settings != null ? _settings.RustDeskId : null); };
            flowClientOpts.Controls.Add(btnRustDeskClient);

            top += 42;

            // 5. Connection Guide
            _lblGuideTitle = new Label() { Text = "Quick 2-PC Setup Guide:", Location = new Point(20, top), AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
            _tabClient.Controls.Add(_lblGuideTitle);
            top += 26;

            _rtbGuide = new RichTextBox();
            _rtbGuide.Location = new Point(20, top);
            _rtbGuide.Size = new Size(690, 180);
            _rtbGuide.ReadOnly = true;
            _rtbGuide.BackColor = Color.FromArgb(248, 249, 251);
            _rtbGuide.BorderStyle = BorderStyle.FixedSingle;
            _rtbGuide.Font = new Font("Segoe UI", 9.5f);
            _rtbGuide.Text =
                "HOW TO CONNECT YOUR 2 PCS:\n\n" +
                "SAME NETWORK (Home/Office Wi-Fi):\n" +
                "  PC 1 (Host): Run RemotePC.exe → 'Allow Remote Access' tab → Start Server\n" +
                "  PC 2 (Client): pick the PC from 'Discovered PCs' → Quick Connect\n\n" +
                "DIFFERENT NETWORKS - ZERO CONFIG ON THE HOST:\n" +
                "  1. PC 1: Start Server, then click 'Copy Code' (copies address + port + PIN).\n" +
                "  2. PC 2: paste the code into 'Remote PC Address' and click Connect.\n" +
                "     The port and PIN fields fill themselves in.\n" +
                "  The host automatically re-opens Windows Firewall, restarts discovery and\n" +
                "  recreates its router port mapping every time it joins a new Wi-Fi/hotspot.\n\n" +
                "IF THE HOST'S ADDRESS CHANGES:\n" +
                "  Reconnect with a fresh code. Addresses marked ★ are automatic (NAT-PMP/UPnP),\n" +
                "  IPv6 or Tailscale; the app prefers whichever works on the current network.\n\n" +
                "NETWORKS THAT BLOCK INBOUND CONNECTIONS:\n" +
                "  Hotel, cafe, airport and some mobile networks use carrier-grade NAT. No app can\n" +
                "  accept an inbound connection there without a relay. Use free Tailscale on both\n" +
                "  PCs (addresses are marked ★), or keep the host on a home/office network.\n\n" +
                "SECURITY: the PIN is locked out after " + AuthGuard.MaxFailuresPerAddress +
                " wrong attempts, so guessing it is not practical.\n" +
                "TIPS: No manual setup needed - the app adds itself to the Start Menu and opens the\n" +
                "Windows Firewall on first run (approve the single UAC prompt when asked).\n" +
                "Settings are auto-saved between sessions.";
            _tabClient.Controls.Add(_rtbGuide);
        }

        private void BtnQuickConnect_Click(object sender, EventArgs e)
        {
            if (_cboDiscovered.SelectedItem == null) return;

            string entry = _cboDiscovered.SelectedItem.ToString();
            // Parse "MachineName (IP:Port)"
            int parenOpen = entry.LastIndexOf('(');
            int parenClose = entry.LastIndexOf(')');
            if (parenOpen < 0 || parenClose < 0) return;

            string ipPort = entry.Substring(parenOpen + 1, parenClose - parenOpen - 1);
            string[] parts = ipPort.Split(':');
            if (parts.Length != 2) return;

            _cboClientIp.Text = parts[0].Trim();
            int port;
            if (int.TryParse(parts[1].Trim(), out port))
                _numClientPort.Value = Math.Max(1024, Math.Min(65535, port));

            // Switch to connection group and trigger connect
            _tabControl.SelectedTab = _tabClient;
            BtnConnect_Click(sender, e);
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            string host = _cboClientIp.Text.Trim();

            // Accept a full connection code (RPC1|host|port|pin), host:port, [v6]:port,
            // a plain host name, or a bare IPv4/IPv6 address.
            string parsedHost;
            int parsedPort;
            string parsedPin;
            if (ConnectionCode.TryParse(host, out parsedHost, out parsedPort, out parsedPin))
            {
                host = parsedHost;
                if (parsedPort >= 1024 && parsedPort <= 65535)
                    _numClientPort.Value = parsedPort;
                if (!string.IsNullOrEmpty(parsedPin))
                    _txtClientPassword.Text = parsedPin;

                if (!string.Equals(_cboClientIp.Text, host, StringComparison.Ordinal))
                    _cboClientIp.Text = host;
            }

            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Paste the code from the host, or enter the Remote PC's IP address.",
                    "Address Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int port = (int)_numClientPort.Value;
            string pass = _txtClientPassword.Text.Trim();

            _btnConnect.Enabled = false;
            _lblClientStatus.Text = "Connecting...";

            RemoteClient client = new RemoteClient();

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = client.Connect(host, port, pass, 5000);

                this.BeginInvoke(new Action(delegate
                {
                    _btnConnect.Enabled = true;
                    _lblClientStatus.Text = "";

                    if (ok)
                    {
                        SaveIpHistory(host);
                        SaveSettingsFromUI();
                        ViewerForm vf = new ViewerForm(client);
                        vf.Show();
                    }
                    else
                    {
                        string hint = BuildAddressHint(host);
                        string detail = string.IsNullOrEmpty(client.LastError)
                            ? ""
                            : "\n\nDetails: " + client.LastError;

                        MessageBox.Show(
                            string.Format(
                                "Could not connect to {0}.{1}\n\nEnsure:\n1. The server is running on the other PC (green status).\n2. If you are on different networks, ask for a fresh connection code - the host's address changes when it switches Wi-Fi.\n3. Hotel/cafe/public Wi-Fi usually blocks inbound connections; use Tailscale or a home/office network.\n4. The PIN is correct (the host may have generated a new one).{2}",
                                ConnectionCode.FormatEndpoint(host, port), hint, detail),
                            "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        if (RustDeskHelper.IsInstalled(_settings != null ? _settings.RustDeskPath : null))
                        {
                            DialogResult rd = MessageBox.Show(
                                "RustDesk is installed on this PC. Open it as a fallback for this connection?",
                                "Launch RustDesk?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            if (rd == DialogResult.Yes)
                            {
                                LaunchRustDeskFallback(_settings != null ? _settings.RustDeskId : null);
                            }
                        }
                    }
                }));
            });
        }

        /// <summary>
        /// Explains the most common cross-network mistake: dialling the host's private LAN
        /// address (192.168.x.x, 10.x.x.x, 172.16-31.x.x) from a different network. Returns an
        /// empty string for host names and public addresses.
        /// </summary>
        private static string BuildAddressHint(string host)
        {
            System.Net.IPAddress address;
            if (!System.Net.IPAddress.TryParse(host, out address)) return "";
            if (!PublicIpResolver.IsPrivateOrShared(address)) return "";

            if (System.Net.IPAddress.IsLoopback(address))
            {
                return "\n\n" + host + " is this PC's own loopback address. Enter the other PC's address instead.";
            }

            byte[] bytes = address.GetAddressBytes();
            bool carrierGradeNat =
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                && bytes.Length == 4 && bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127;

            if (carrierGradeNat)
            {
                return "\n\n" + host + " is a carrier-grade NAT (100.64.x.x) address, which cannot be reached from the internet. " +
                    "On the host, click 'Copy Code' and use the address marked \u2605 (router mapping, IPv6 or Tailscale). " +
                    "If the host is on hotel/cafe/mobile Wi-Fi, run Tailscale on both PCs.";
            }

            return "\n\n" + host + " is a private LAN address, so it only works while both PCs are on the same Wi-Fi/router. " +
                "On the host, click 'Copy Code' and paste that code here - it contains the host's current, reachable address. " +
                "If the host is on hotel/cafe/mobile Wi-Fi, run Tailscale on both PCs.";
        }

        private void LoadIpHistory()
        {
            try
            {
                if (File.Exists(_historyFile))
                {
                    string[] lines = File.ReadAllLines(_historyFile);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string ip = lines[i].Trim();
                        if (!string.IsNullOrEmpty(ip) && !_cboClientIp.Items.Contains(ip))
                        {
                            _cboClientIp.Items.Add(ip);
                        }
                    }
                }
            }
            catch { }

            // Also populate from saved LastHost setting
            if (!string.IsNullOrEmpty(_settings.LastHost))
            {
                if (!_cboClientIp.Items.Contains(_settings.LastHost))
                    _cboClientIp.Items.Insert(0, _settings.LastHost);
            }

            if (_cboClientIp.Items.Count > 0)
                _cboClientIp.SelectedIndex = 0;
            else
                _cboClientIp.Text = "192.168.1.";
        }

        private void SaveIpHistory(string newIp)
        {
            try
            {
                List<string> ips = new List<string>();
                ips.Add(newIp);

                for (int i = 0; i < _cboClientIp.Items.Count; i++)
                {
                    string existing = _cboClientIp.Items[i].ToString();
                    if (!string.Equals(existing, newIp, StringComparison.OrdinalIgnoreCase))
                    {
                        ips.Add(existing);
                    }
                    if (ips.Count >= 10) break;
                }

                File.WriteAllLines(_historyFile, ips.ToArray());

                if (!_cboClientIp.Items.Contains(newIp))
                {
                    _cboClientIp.Items.Insert(0, newIp);
                    _cboClientIp.SelectedIndex = 0;
                }
            }
            catch { }
        }

        #endregion

        #region Settings Persistence

        private void LoadSettingsIntoUI()
        {
            if (_settings == null) return;

            // Port
            if (_settings.ServerPort >= 1024 && _settings.ServerPort <= 65535)
            {
                _numHostPort.Value = _settings.ServerPort;
                _numClientPort.Value = _settings.ServerPort;
            }

            // Password
            if (!string.IsNullOrEmpty(_settings.Password))
                _txtHostPassword.Text = _settings.Password;

            // Checkboxes
            _chkAllowControl.Checked = _settings.AllowControl;
            _chkAllowFiles.Checked = _settings.AllowFileTransfer;
            _chkAutoStartServer.Checked = _settings.AutoStartServer;
            _chkTrayOnClose.Checked = _settings.MinimizeToTray;
            _chkAutoConnect.Checked = _settings.AutoConnect;

            // Last client IP
            if (!string.IsNullOrEmpty(_settings.LastHost))
                _cboClientIp.Text = _settings.LastHost;

            // Last client password
            if (!string.IsNullOrEmpty(_settings.LastPassword))
                _txtClientPassword.Text = _settings.LastPassword;
        }

        private void SaveSettingsFromUI()
        {
            if (_settings == null) return;

            _settings.ServerPort = (int)_numHostPort.Value;
            _settings.Password = _txtHostPassword.Text.Trim();
            _settings.AllowControl = _chkAllowControl.Checked;
            _settings.AllowFileTransfer = _chkAllowFiles.Checked;
            _settings.AutoStartServer = _chkAutoStartServer.Checked;
            _settings.MinimizeToTray = _chkTrayOnClose.Checked;
            _settings.AutoConnect = _chkAutoConnect.Checked;
            _settings.LastHost = _cboClientIp.Text.Trim();
            _settings.LastPassword = _txtClientPassword.Text.Trim();

            _settings.Save();
        }

        #endregion

        #region Responsive Layout

        private void MainForm_Resize(object sender, EventArgs e)
        {
            UpdateResponsiveLayout();
        }

        private void UpdateResponsiveLayout()
        {
            if (_tabHost == null || _tabClient == null) return;

            int hostW = _tabHost.ClientSize.Width;
            int hostH = _tabHost.ClientSize.Height;

            if (hostW > 60 && hostH > 60)
            {
                int cardW = Math.Max(200, hostW - 40);

                if (_pnlHostStatus != null) _pnlHostStatus.Width = cardW;
                if (_gbHostNetwork != null) _gbHostNetwork.Width = cardW;
                if (_pnlHostActions != null) _pnlHostActions.Width = cardW;
                if (_gbHostAuto != null) _gbHostAuto.Width = cardW;
                if (_pnlLogHeader != null) _pnlLogHeader.Width = cardW;
                if (_logoHost != null) _logoHost.Left = _tabHost.ClientSize.Width - _logoHost.Width - 22;

                // The address combo shares its row with both Copy buttons, so it has to give up
                // width first. Without this the buttons were pushed past the window edge.
                if (_tlpHostNetwork != null && _cboHostIps != null && _btnCopyIp != null && _btnCopyCode != null)
                {
                    int labelColW = GetFirstColumnWidth(_tlpHostNetwork, 100);
                    int innerW = Math.Max(120, cardW - 26);
                    int buttonsW = _btnCopyIp.Width + _btnCopyIp.Margin.Left
                                 + _btnCopyCode.Width + _btnCopyCode.Margin.Left + 4;
                    int comboW = innerW - labelColW - buttonsW;
                    _cboHostIps.Width = Math.Max(90, Math.Min(380, comboW));
                }

                if (_lbHostLog != null)
                {
                    _lbHostLog.Width = cardW;
                    int logHeight = hostH - _lbHostLog.Top - 15;
                    if (logHeight > 60) _lbHostLog.Height = logHeight;
                }
            }

            int clientW = _tabClient.ClientSize.Width;
            int clientH = _tabClient.ClientSize.Height;

            if (clientW > 60 && clientH > 60)
            {
                int cardW = Math.Max(200, clientW - 40);

                if (_gbDiscovery != null) _gbDiscovery.Width = cardW;
                if (_gbClientConnect != null) _gbClientConnect.Width = cardW;
                if (_flowClientOpts != null) _flowClientOpts.Width = cardW;
                if (_logoClient != null) _logoClient.Left = _tabClient.ClientSize.Width - _logoClient.Width - 22;

                int innerW = Math.Max(120, cardW - 26);

                // Discovery row: shrink the list so "Quick Connect" stays visible.
                if (_cboDiscovered != null && _btnQuickConnect != null)
                {
                    int captionW = _lblDiscoveryCaption != null
                        ? _lblDiscoveryCaption.Width + _lblDiscoveryCaption.Margin.Right
                        : 114;
                    int buttonW = _btnQuickConnect.Width + _btnQuickConnect.Margin.Left + 4;
                    int comboW = innerW - captionW - buttonW;
                    _cboDiscovered.Width = Math.Max(90, Math.Min(340, comboW));
                }

                // Connection details: shorten the address and PIN boxes instead of cutting off
                // the port field / Show button on the right.
                if (_tlpClientConnect != null)
                {
                    int labelColW = GetFirstColumnWidth(_tlpClientConnect, 150);
                    int rowW = Math.Max(120, innerW - labelColW);

                    if (_cboClientIp != null && _numClientPort != null)
                    {
                        int portW = 15 + 6 + 2 + _numClientPort.Width;
                        if (_lblClientPortCaption != null) portW += _lblClientPortCaption.Width;
                        int comboW = rowW - portW;
                        _cboClientIp.Width = Math.Max(100, Math.Min(300, comboW));
                    }

                    if (_txtClientPassword != null && _btnToggleClientPass != null)
                    {
                        int textW = rowW - _btnToggleClientPass.Width - _btnToggleClientPass.Margin.Left - 4;
                        _txtClientPassword.Width = Math.Max(100, Math.Min(300, textW));
                    }
                }

                if (_rtbGuide != null)
                {
                    _rtbGuide.Width = cardW;
                    int guideHeight = clientH - _rtbGuide.Top - 15;
                    if (guideHeight > 60) _rtbGuide.Height = guideHeight;
                }
            }
        }

        private int GetFirstColumnWidth(TableLayoutPanel table, int fallback)
        {
            try
            {
                int[] widths = table.GetColumnWidths();
                if (widths != null && widths.Length > 0 && widths[0] > 0) return widths[0];
            }
            catch { }
            return fallback;
        }

        #endregion

        #region Form Shown / Auto-Start

        /// <summary>
        /// Everything the app needs in order to work the moment RemotePC.exe is launched:
        /// Start Menu entry, Windows Firewall rules, and the server's own port. Runs once in
        /// the background so a UAC prompt or a slow netsh call never blocks the window.
        /// </summary>
        private void RunFirstRunSetupAsync()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // 1. Start Menu shortcut, created for the current user - no elevation needed.
                    if (!AppShortcutHelper.IsStartMenuShortcutPresent())
                    {
                        string error;
                        if (AppShortcutHelper.CreateStartMenuShortcut(out error))
                            LogHostLine("[Setup] Added 'Remote PC' to the Start Menu.");
                        else
                            LogHostLine("[Setup] Start Menu shortcut skipped: " + error);
                    }

                    // 2. Firewall rules so other PCs can reach this one.
                    int port = 8888;
                    try { port = (int)this.Invoke(new Func<int>(delegate { return (int)_numHostPort.Value; })); }
                    catch { }

                    bool fwOk = true;
                    string fwError = null;
                    if (_settings == null || _settings.EnableFirewallAuto)
                    {
                        List<int> ports = new List<int>();
                        ports.Add(port);
                        if (_settings != null && _settings.ServerPort != port) ports.Add(_settings.ServerPort);

                        fwOk = FirewallHelper.EnsureRules(ports, out fwError);
                    }
                    else
                    {
                        fwError = "disabled by network.ini";
                    }
                    if (fwOk)
                    {
                        LogHostLine("[Setup] Windows Firewall rules ready - no manual configuration needed.");
                        if (_settings != null && !_settings.FirstRunComplete)
                        {
                            _settings.FirstRunComplete = true;
                            _settings.Save();
                        }
                    }
                    else
                    {
                        LogHostLine("[Setup] Firewall rules not applied (" + fwError + "). LAN connections may be blocked.");
                    }

                    // 3. Windows startup entry, so the PC stays reachable after a reboot.
                    if (_settings != null && _settings.StartWithWindows && !AppShortcutHelper.IsStartWithWindowsEnabled())
                    {
                        string sLocation;
                        if (AppShortcutHelper.CreateStartupShortcut(Application.ExecutablePath, "Remote PC", out sLocation))
                            LogHostLine("[Setup] Remote PC will start with Windows.");
                        else
                            LogHostLine("[Setup] Windows startup entry skipped: " + sLocation);
                    }

                    SetAutoStatus(fwOk
                        ? "Automatic setup: firewall open, Start Menu entry ready."
                        : "Automatic setup: firewall NOT open - click 'Fix Firewall'.");
                }
                catch (Exception ex)
                {
                    LogHostLine("[Setup] Skipped: " + ex.Message);
                }
            });
        }

        /// <summary>Writes the automatic-setup summary line above the Shortcut buttons.</summary>
        private void SetAutoStatus(string text)
        {
            if (this.IsDisposed || _lblAutoStatus == null) return;
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action<string>(SetAutoStatus), text);
                    return;
                }
                _lblAutoStatus.Text = text;
            }
            catch { }
        }

        /// <summary>Manual re-run of the firewall step, for when the first UAC prompt was declined.</summary>
        private void FixFirewallNow()
        {
            int port = (int)_numHostPort.Value;
            SetAutoStatus("Automatic setup: configuring firewall...");

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                List<int> ports = new List<int>();
                ports.Add(port);
                ports.Add(NetworkDiscovery.DISCOVERY_PORT);

                string error;
                bool ok = FirewallHelper.EnsureRules(ports, out error);

                LogHostLine(ok
                    ? "[Firewall] Rules are in place for TCP " + port + " and UDP " + NetworkDiscovery.DISCOVERY_PORT + "."
                    : "[Firewall] Failed: " + error);

                SetAutoStatus(ok
                    ? "Automatic setup: firewall open, Start Menu entry ready."
                    : "Automatic setup: firewall NOT open - click 'Fix Firewall'.");
            });
        }

        /// <summary>Firewall rule check for a specific port, used when the user starts the server.</summary>
        private void EnsureFirewallRulesAsync(int port)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    List<int> ports = new List<int>();
                    ports.Add(port);
                    ports.Add(NetworkDiscovery.DISCOVERY_PORT);

                    string error;
                    if (!FirewallHelper.EnsureRules(ports, out error))
                        LogHostLine("[Firewall] Could not open port " + port + ": " + error);
                }
                catch { }
            });
        }

        private void LaunchRustDeskFallback(string peerId)
        {
            string error;
            if (RustDeskHelper.Launch(_settings != null ? _settings.RustDeskPath : null, peerId, out error))
            {
                LogHostLine("[RustDesk] Launched" + (string.IsNullOrEmpty(peerId) ? "." : " for id " + peerId + "."));
            }
            else
            {
                MessageBox.Show("Could not launch RustDesk: " + error,
                    "RustDesk", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Appends a line to the host activity log from any thread.</summary>
        private void LogHostLine(string message)
        {
            if (this.IsDisposed) return;
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action<string>(LogHostLine), message);
                    return;
                }
                Server_OnLog(message);
            }
            catch { }
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            // DPI scaling has been applied by now, so the scaled minimum size is known.
            ClampToWorkingArea();
            UpdateResponsiveLayout();

            // Handle -minimized arg
            string[] args = Environment.GetCommandLineArgs();
            bool startMinimized = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-minimized", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[i], "--minimized", StringComparison.OrdinalIgnoreCase))
                {
                    startMinimized = true;
                    break;
                }
            }

            // Zero-touch setup: firewall rules and a Start Menu entry, done automatically.
            RunFirstRunSetupAsync();

            // First launch ever: behave like the user pressed Start Server, so the app is
            // reachable the moment RemotePC.exe is double-clicked.
            if (_settings != null && !_settings.FirstRunComplete)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    System.Threading.Thread.Sleep(1200);
                    if (!this.IsDisposed)
                    {
                        try
                        {
                            this.BeginInvoke(new Action(delegate
                            {
                                if (_server == null || !_server.IsRunning)
                                    BtnToggleServer_Click(null, EventArgs.Empty);
                            }));
                        }
                        catch { }
                    }
                });
            }

            if (startMinimized && _chkTrayOnClose != null && _chkTrayOnClose.Checked)
            {
                this.Hide();
                _trayIcon.Visible = true;
                return;
            }

            // Auto-Start Server
            if (_settings != null && _settings.AutoStartServer)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    System.Threading.Thread.Sleep(300);
                    if (!this.IsDisposed)
                        this.BeginInvoke(new Action(delegate { BtnToggleServer_Click(null, EventArgs.Empty); }));
                });
            }

            // Auto-Connect (client)
            if (_settings != null && _settings.AutoConnect && !string.IsNullOrEmpty(_settings.LastHost))
            {
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    System.Threading.Thread.Sleep(600);
                    if (!this.IsDisposed)
                        this.BeginInvoke(new Action(delegate
                        {
                            _tabControl.SelectedTab = _tabClient;
                            BtnConnect_Click(null, EventArgs.Empty);
                        }));
                });
            }
        }

        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // If Minimize to Tray is enabled and user closed the window (not exiting from tray)
            if (_chkTrayOnClose != null && _chkTrayOnClose.Checked &&
                e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                _trayIcon.Visible = true;
                _trayIcon.BalloonTipTitle = "Remote PC";
                _trayIcon.BalloonTipText = "Still running in the background. Double-click to restore.";
                try { _trayIcon.ShowBalloonTip(2500); } catch { }
                return;
            }

            SaveSettingsFromUI();

            try
            {
                if (_networkWatcher != null) _networkWatcher.Stop();
            }
            catch { }
            try
            {
                if (_internetRefreshTimer != null) _internetRefreshTimer.Stop();
            }
            catch { }

            try { UPnPPortForwarder.PublicIpChanged -= HandlePublicIpChanged; }
            catch { }
            try { NatPmpPortMapper.PublicIpChanged -= HandlePublicIpChanged; }
            catch { }

            StopInternetAccess();

            if (_server != null && _server.IsRunning)
                _server.Stop();

            try { NetworkDiscovery.StopBroadcasting(); } catch { }
            try { NetworkDiscovery.StopListening(); } catch { }

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            base.OnFormClosing(e);
        }
    }
}
