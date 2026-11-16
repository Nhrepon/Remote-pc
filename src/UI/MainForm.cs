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

        private readonly string _historyFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RemotePC_History.txt");

        #endregion

        #region Tray / Settings

        private NotifyIcon _trayIcon;
        private AppSettings _settings;

        #endregion

        public MainForm()
        {
            this.AutoScaleMode = AutoScaleMode.None;

            _settings = AppSettings.Load();

            InitializeComponent();
            InitializeTrayIcon();
            InitializeHostTab();
            InitializeClientTab();
            LoadIpHistory();
            GenerateRandomPin();
            LoadSettingsIntoUI();

            this.Resize += MainForm_Resize;
            this.Shown += MainForm_Shown;
        }

        private void InitializeComponent()
        {
            this.Text = "Remote PC - Work Across 2 PCs";
            this.Size = new Size(760, 680);
            this.MinimumSize = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(245, 247, 250);

            _tabControl = new TabControl();
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            _tabControl.Padding = new Point(16, 8);

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
            // Use a simple built-in icon if no custom icon is embedded
            try { _trayIcon.Icon = this.Icon ?? SystemIcons.Application; }
            catch { _trayIcon.Icon = SystemIcons.Application; }

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
            lblTitle.Text = "Host Mode: Allow Another PC to Control This PC";
            lblTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblTitle.Location = new Point(20, top);
            lblTitle.AutoSize = true;
            _tabHost.Controls.Add(lblTitle);
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
            Label lblIp = new Label() { Text = "This PC's IPv4 Address:", AutoSize = true, Margin = new Padding(0, 8, 15, 0) };
            _tlpHostNetwork.Controls.Add(lblIp, 0, 0);

            FlowLayoutPanel flowIp = new FlowLayoutPanel() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 3, 0, 0) };
            _cboHostIps = new ComboBox() { Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
            RefreshHostIps();
            flowIp.Controls.Add(_cboHostIps);

            _btnCopyIp = new Button() { Text = "Copy IP", Width = 95, Height = 27, Margin = new Padding(8, 0, 0, 0) };
            _btnCopyIp.Click += BtnCopyIp_Click;
            flowIp.Controls.Add(_btnCopyIp);
            _tlpHostNetwork.Controls.Add(flowIp, 1, 0);

            // Row 1: Port
            Label lblPort = new Label() { Text = "Port:", AutoSize = true, Margin = new Padding(0, 6, 15, 0) };
            _tlpHostNetwork.Controls.Add(lblPort, 0, 1);

            _numHostPort = new NumericUpDown() { Width = 100, Minimum = 1024, Maximum = 65535, Value = 8888, Margin = new Padding(0, 3, 0, 0) };
            _tlpHostNetwork.Controls.Add(_numHostPort, 1, 1);

            // Row 2: Security PIN
            Label lblPass = new Label() { Text = "Security PIN / Password:", AutoSize = true, Margin = new Padding(0, 8, 15, 0) };
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

            Button btnStartMenu = new Button() { Text = "📌 Add to Start Menu", AutoSize = true, Height = 30, Margin = new Padding(0, 0, 10, 0) };
            btnStartMenu.FlatStyle = FlatStyle.Flat;
            btnStartMenu.Click += delegate { CreateShortcut("startmenu"); };
            flowShortcuts.Controls.Add(btnStartMenu);

            Button btnDesktop = new Button() { Text = "🖥 Desktop Shortcut", AutoSize = true, Height = 30, Margin = new Padding(0, 0, 10, 0) };
            btnDesktop.FlatStyle = FlatStyle.Flat;
            btnDesktop.Click += delegate { CreateShortcut("desktop"); };
            flowShortcuts.Controls.Add(btnDesktop);

            Button btnStartup = new Button() { Text = "🚀 Start with Windows", AutoSize = true, Height = 30, Margin = new Padding(0, 0, 10, 0) };
            btnStartup.FlatStyle = FlatStyle.Flat;
            btnStartup.Click += delegate { CreateShortcut("startup"); };
            flowShortcuts.Controls.Add(btnStartup);

            Button btnFirewall = new Button() { Text = "🛡 Auto-Configure Firewall", AutoSize = true, Height = 30, Margin = new Padding(0) };
            btnFirewall.FlatStyle = FlatStyle.Flat;
            btnFirewall.Click += delegate { FixFirewallNow(); };
            flowShortcuts.Controls.Add(btnFirewall);

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
            _cboHostIps.Items.Clear();
            List<NetworkAdapterInfo> adapters = SystemInfo.GetLocalIPv4Addresses();
            for (int i = 0; i < adapters.Count; i++)
            {
                _cboHostIps.Items.Add(adapters[i]);
            }
            if (_cboHostIps.Items.Count > 0)
            {
                _cboHostIps.SelectedIndex = 0;
            }
        }

        private void GenerateRandomPin()
        {
            Random r = new Random();
            _txtHostPassword.Text = r.Next(100000, 999999).ToString();
        }

        private void BtnCopyIp_Click(object sender, EventArgs e)
        {
            if (_cboHostIps.SelectedItem != null)
            {
                NetworkAdapterInfo info = _cboHostIps.SelectedItem as NetworkAdapterInfo;
                if (info != null)
                {
                    Clipboard.SetText(info.IpAddress);
                    MessageBox.Show(
                        string.Format("Copied IP '{0}' to clipboard!\n\nType this IP into PC 2 to connect.", info.IpAddress),
                        "IP Address Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
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
                    _server.Start(port);

                    _btnToggleServer.Text = "Stop Server";
                    _btnToggleServer.BackColor = Color.FromArgb(220, 53, 69);
                    _numHostPort.Enabled = false;
                    _txtHostPassword.Enabled = false;
                    _btnGenPin.Enabled = false;

                    // Start LAN broadcasting so other PCs can discover us
                    try { NetworkDiscovery.StartBroadcasting(port); }
                    catch { }

                    // Ensure the firewall lets this port in, without ever asking the user to do it
                    EnsureFirewallRulesAsync(port);

                    // Try UPnP port forward in background (non-blocking)
                    UPnPPortForwarder.ForwardPortAsync(port, delegate(bool ok, string message)
                    {
                        LogHostLine(ok ? "[UPnP] " + message : "[UPnP] " + message);
                        if (ok && !string.IsNullOrEmpty(UPnPPortForwarder.ExternalPublicIp))
                        {
                            LogHostLine(string.Format("[UPnP] Share this public address with PC 2: {0}:{1}",
                                UPnPPortForwarder.ExternalPublicIp, port));
                        }
                    });
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
            }
        }

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
            Label lblIp = new Label() { Text = "Remote PC IP Address:", AutoSize = true, Margin = new Padding(0, 8, 15, 0) };
            _tlpClientConnect.Controls.Add(lblIp, 0, 0);

            FlowLayoutPanel flowIpPort = new FlowLayoutPanel() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 3, 0, 0) };
            _cboClientIp = new ComboBox() { Width = 260 };
            flowIpPort.Controls.Add(_cboClientIp);

            Label lblPort = new Label() { Text = "Port:", AutoSize = true, Margin = new Padding(15, 6, 6, 0) };
            flowIpPort.Controls.Add(lblPort);

            _numClientPort = new NumericUpDown() { Width = 90, Minimum = 1024, Maximum = 65535, Value = 8888 };
            flowIpPort.Controls.Add(_numClientPort);
            _tlpClientConnect.Controls.Add(flowIpPort, 1, 0);

            // Row 1: Access PIN
            Label lblPass = new Label() { Text = "Access PIN / Password:", AutoSize = true, Margin = new Padding(0, 8, 15, 0) };
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
                "  PC 2 (Client): Run RemotePC.exe → 'Connect to Remote PC' tab → pick IP from dropdown OR type it in\n\n" +
                "DIFFERENT NETWORKS (Home ↔ Office):\n" +
                "  Option A: Install free Tailscale on both PCs → use the 100.x.x.x IP shown in this app (★ marked)\n" +
                "  Option B: This app auto-tries UPnP port forwarding on your router (no manual config needed)\n" +
                "  Option C: Forward TCP port 8888 manually on your router → use your public IP\n\n" +
                "TIPS: No manual setup needed - the app adds itself to the Start Menu and opens the\n" +
                "Windows Firewall on first run (approve the single UAC prompt when asked).\n" +
                "Settings are auto-saved between sessions. Shortcuts: use the 'Zero-Work Automation' buttons on the Host tab.";
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
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show("Please enter the Remote PC's IP address.", "IP Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                        MessageBox.Show(
                            string.Format(
                                "Could not connect to {0}:{1}.\n\nEnsure:\n1. Server is running on PC 1.\n2. Both PCs are on the same Wi-Fi/network, or use Tailscale (100.x.x.x IP).\n3. The IP address and PIN are correct.\n4. Windows Firewall is not blocking port {1}.",
                                host, port),
                            "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }));
            });
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

                if (_tlpHostNetwork != null && _cboHostIps != null)
                {
                    int labelColW = _tlpHostNetwork.GetColumnWidths().Length > 0 ? _tlpHostNetwork.GetColumnWidths()[0] : 180;
                    int maxCboW = cardW - labelColW - 130;
                    _cboHostIps.Width = Math.Max(180, Math.Min(380, maxCboW));
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

                if (_rtbGuide != null)
                {
                    _rtbGuide.Width = cardW;
                    int guideHeight = clientH - _rtbGuide.Top - 15;
                    if (guideHeight > 60) _rtbGuide.Height = guideHeight;
                }
            }
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

                    List<int> ports = new List<int>();
                    ports.Add(port);
                    if (_settings != null && _settings.ServerPort != port) ports.Add(_settings.ServerPort);

                    string fwError;
                    bool fwOk = FirewallHelper.EnsureRules(ports, out fwError);
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
                        : "Automatic setup: firewall NOT open - click 'Auto-Configure Firewall'.");
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
                    : "Automatic setup: firewall NOT open - click 'Auto-Configure Firewall'.");
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
