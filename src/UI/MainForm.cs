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

        private readonly string _historyFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemotePC_History.txt");

        #endregion

        public MainForm()
        {
            this.AutoScaleMode = AutoScaleMode.None;
            InitializeComponent();
            InitializeHostTab();
            InitializeClientTab();
            LoadIpHistory();
            GenerateRandomPin();

            this.Resize += MainForm_Resize;
            this.Shown += delegate { UpdateResponsiveLayout(); };
        }

        private void InitializeComponent()
        {
            this.Text = "Remote PC - Work Across 2 PCs";
            this.Size = new Size(760, 640);
            this.MinimumSize = new Size(700, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(245, 247, 250);

            _tabControl = new TabControl();
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            _tabControl.Padding = new Point(16, 8);

            _tabHost = new TabPage("  Allow Remote Access (Host)  ");
            _tabHost.BackColor = Color.White;
            _tabHost.AutoScroll = false;
            _tabControl.TabPages.Add(_tabHost);

            _tabClient = new TabPage("  Connect to Remote PC (Client)  ");
            _tabClient.BackColor = Color.White;
            _tabClient.AutoScroll = false;
            _tabControl.TabPages.Add(_tabClient);

            this.Controls.Add(_tabControl);
        }

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

            // 3. Network Configuration Card (TableLayoutPanel with AutoSize columns - immune to DPI overlap!)
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

            // 5. Activity Log Header
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

            // 6. Log Box
            _lbHostLog = new ListBox();
            _lbHostLog.Location = new Point(20, top);
            _lbHostLog.Size = new Size(690, 160);
            _lbHostLog.Font = new Font("Consolas", 9.5f);
            _lbHostLog.IntegralHeight = false;
            _tabHost.Controls.Add(_lbHostLog);
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
                    MessageBox.Show(string.Format("Copied IP '{0}' to clipboard!\n\nType this IP into PC 2 to connect.", info.IpAddress),
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

            // 2. Connection Details GroupBox (TableLayoutPanel with AutoSize columns - immune to DPI overlap!)
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

            // 3. Connection Guide
            _lblGuideTitle = new Label() { Text = "Quick 2-PC Setup Guide:", Location = new Point(20, top), AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
            _tabClient.Controls.Add(_lblGuideTitle);
            top += 26;

            _rtbGuide = new RichTextBox();
            _rtbGuide.Location = new Point(20, top);
            _rtbGuide.Size = new Size(690, 220);
            _rtbGuide.ReadOnly = true;
            _rtbGuide.BackColor = Color.FromArgb(248, 249, 251);
            _rtbGuide.BorderStyle = BorderStyle.FixedSingle;
            _rtbGuide.Font = new Font("Segoe UI", 9.5f);
            _rtbGuide.Text =
                "HOW TO CONNECT YOUR 2 PCS:\n\n" +
                "1. ON PC 1 (The PC to control/host):\n" +
                "   • Run RemotePC.exe and stay on the 'Allow Remote Access' tab.\n" +
                "   • Look at 'This PC's IPv4 Address' (e.g. 192.168.1.105) and note down the Security PIN.\n" +
                "   • Click the green 'Start Server' button.\n\n" +
                "2. ON PC 2 (The PC you are working from):\n" +
                "   • Run RemotePC.exe and switch to this 'Connect to Remote PC' tab.\n" +
                "   • Enter the IP address and PIN from PC 1.\n" +
                "   • Click 'Connect to Remote PC'!\n" +
                "   • The Remote Desktop window will open with live screen, full mouse & keyboard control,\n" +
                "     file transfer, clipboard sync, and chat!\n\n" +
                "NETWORK TIPS:\n" +
                "• Same Wi-Fi / LAN: Direct connection works instantly out-of-the-box.\n" +
                "• Windows Firewall: If prompted, check 'Allow access on Private networks'.\n" +
                "• Over Internet (Different networks): Install free Tailscale or ZeroTier on both PCs,\n" +
                "  or forward port 8888 on your router.";
            _tabClient.Controls.Add(_rtbGuide);
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

            // Run connection on background thread to keep UI completely responsive
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
                        ViewerForm vf = new ViewerForm(client);
                        vf.Show();
                    }
                    else
                    {
                        MessageBox.Show(
                            string.Format("Could not connect to {0}:{1}.\n\nEnsure:\n1. Server is running on PC 1.\n2. Both PCs are on the same Wi-Fi/network or VPN.\n3. The IP address and PIN are correct.\n4. Windows Firewall is not blocking port {1}.", host, port),
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

            if (_cboClientIp.Items.Count > 0)
            {
                _cboClientIp.SelectedIndex = 0;
            }
            else
            {
                _cboClientIp.Text = "192.168.1.";
            }
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
                    if (ips.Count >= 10) break; // Keep last 10
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_server != null && _server.IsRunning)
            {
                _server.Stop();
            }
            base.OnFormClosing(e);
        }
    }
}
