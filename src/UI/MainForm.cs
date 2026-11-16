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
        private ComboBox _cboHostIps;
        private Button _btnCopyIp;
        private NumericUpDown _numHostPort;
        private TextBox _txtHostPassword;
        private Button _btnToggleHostPass;
        private Button _btnGenPin;
        private CheckBox _chkAllowControl;
        private CheckBox _chkAllowFiles;
        private Button _btnToggleServer;
        private ListBox _lbHostLog;
        private Button _btnHostChat;

        private RemoteServer _server;

        #endregion

        #region Client Controls

        private ComboBox _cboClientIp;
        private NumericUpDown _numClientPort;
        private TextBox _txtClientPassword;
        private Button _btnToggleClientPass;
        private Button _btnConnect;
        private Label _lblClientStatus;
        private RichTextBox _rtbGuide;

        private readonly string _historyFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RemotePC_History.txt");

        #endregion

        public MainForm()
        {
            InitializeComponent();
            InitializeHostTab();
            InitializeClientTab();
            LoadIpHistory();
            GenerateRandomPin();
        }

        private void InitializeComponent()
        {
            this.Text = "Remote PC - Work Across 2 PCs";
            this.Size = new Size(680, 560);
            this.MinimumSize = new Size(650, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(245, 247, 250);

            _tabControl = new TabControl();
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            _tabControl.Padding = new Point(16, 8);

            _tabHost = new TabPage("  Allow Remote Access (Host)  ");
            _tabHost.BackColor = Color.White;
            _tabControl.TabPages.Add(_tabHost);

            _tabClient = new TabPage("  Connect to Remote PC (Client)  ");
            _tabClient.BackColor = Color.White;
            _tabControl.TabPages.Add(_tabClient);

            this.Controls.Add(_tabControl);
        }

        #region Host Tab Setup

        private void InitializeHostTab()
        {
            int top = 15;

            // Header Banner
            Label lblTitle = new Label();
            lblTitle.Text = "Host Mode: Allow Another PC to Control This PC";
            lblTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblTitle.Location = new Point(20, top);
            lblTitle.AutoSize = true;
            _tabHost.Controls.Add(lblTitle);
            top += 30;

            // Status Panel
            Panel pnlStatus = new Panel();
            pnlStatus.Location = new Point(20, top);
            pnlStatus.Size = new Size(620, 36);
            pnlStatus.BackColor = Color.FromArgb(240, 243, 246);
            pnlStatus.BorderStyle = BorderStyle.FixedSingle;
            _tabHost.Controls.Add(pnlStatus);

            _lblHostStatusIndicator = new Label();
            _lblHostStatusIndicator.Text = "●";
            _lblHostStatusIndicator.Font = new Font("Segoe UI", 14f);
            _lblHostStatusIndicator.ForeColor = Color.Red;
            _lblHostStatusIndicator.Location = new Point(10, 4);
            _lblHostStatusIndicator.Size = new Size(25, 25);
            pnlStatus.Controls.Add(_lblHostStatusIndicator);

            _lblHostStatusText = new Label();
            _lblHostStatusText.Text = "Server Stopped. Click 'Start Server' to allow connections.";
            _lblHostStatusText.Location = new Point(35, 8);
            _lblHostStatusText.AutoSize = true;
            pnlStatus.Controls.Add(_lblHostStatusText);
            top += 48;

            // Network Configuration Card
            GroupBox gbNetwork = new GroupBox();
            gbNetwork.Text = " Connection Credentials & Address ";
            gbNetwork.Location = new Point(20, top);
            gbNetwork.Size = new Size(620, 135);
            _tabHost.Controls.Add(gbNetwork);

            Label lblIp = new Label() { Text = "This PC's IPv4 Address:", Location = new Point(15, 26), AutoSize = true };
            gbNetwork.Controls.Add(lblIp);

            _cboHostIps = new ComboBox() { Location = new Point(165, 23), Size = new Size(340, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            RefreshHostIps();
            gbNetwork.Controls.Add(_cboHostIps);

            _btnCopyIp = new Button() { Text = "Copy IP", Location = new Point(515, 22), Size = new Size(90, 27) };
            _btnCopyIp.Click += BtnCopyIp_Click;
            gbNetwork.Controls.Add(_btnCopyIp);

            Label lblPort = new Label() { Text = "Port:", Location = new Point(15, 62), AutoSize = true };
            gbNetwork.Controls.Add(lblPort);

            _numHostPort = new NumericUpDown() { Location = new Point(165, 60), Size = new Size(90, 25), Minimum = 1024, Maximum = 65535, Value = 8888 };
            gbNetwork.Controls.Add(_numHostPort);

            Label lblPass = new Label() { Text = "Security PIN / Password:", Location = new Point(15, 98), AutoSize = true };
            gbNetwork.Controls.Add(lblPass);

            _txtHostPassword = new TextBox() { Location = new Point(165, 96), Size = new Size(200, 25), PasswordChar = '•' };
            gbNetwork.Controls.Add(_txtHostPassword);

            _btnToggleHostPass = new Button() { Text = "Show", Location = new Point(372, 95), Size = new Size(60, 27) };
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
            gbNetwork.Controls.Add(_btnToggleHostPass);

            _btnGenPin = new Button() { Text = "Random PIN", Location = new Point(440, 95), Size = new Size(95, 27) };
            _btnGenPin.Click += delegate { GenerateRandomPin(); };
            gbNetwork.Controls.Add(_btnGenPin);

            top += 145;

            // Permissions & Controls
            _chkAllowControl = new CheckBox() { Text = "Allow Mouse & Keyboard Control", Checked = true, Location = new Point(25, top), AutoSize = true };
            _tabHost.Controls.Add(_chkAllowControl);

            _chkAllowFiles = new CheckBox() { Text = "Allow Remote File Transfer", Checked = true, Location = new Point(265, top), AutoSize = true };
            _tabHost.Controls.Add(_chkAllowFiles);

            _btnToggleServer = new Button();
            _btnToggleServer.Text = "Start Server";
            _btnToggleServer.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            _btnToggleServer.BackColor = Color.FromArgb(40, 167, 69);
            _btnToggleServer.ForeColor = Color.White;
            _btnToggleServer.FlatStyle = FlatStyle.Flat;
            _btnToggleServer.FlatAppearance.BorderSize = 0;
            _btnToggleServer.Location = new Point(480, top - 6);
            _btnToggleServer.Size = new Size(160, 36);
            _btnToggleServer.Click += BtnToggleServer_Click;
            _tabHost.Controls.Add(_btnToggleServer);

            top += 42;

            // Session Activity Log
            Label lblLog = new Label() { Text = "Activity & Connection Log:", Location = new Point(20, top), AutoSize = true };
            _tabHost.Controls.Add(lblLog);

            _btnHostChat = new Button() { Text = "Chat With Client", Location = new Point(490, top - 4), Size = new Size(150, 26), Enabled = false };
            _btnHostChat.Click += BtnHostChat_Click;
            _tabHost.Controls.Add(_btnHostChat);

            top += 25;

            _lbHostLog = new ListBox();
            _lbHostLog.Location = new Point(20, top);
            _lbHostLog.Size = new Size(620, 150);
            _lbHostLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _lbHostLog.Font = new Font("Consolas", 9f);
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

            // Title
            Label lblTitle = new Label();
            lblTitle.Text = "Client Mode: Connect to & Control Remote PC";
            lblTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblTitle.Location = new Point(20, top);
            lblTitle.AutoSize = true;
            _tabClient.Controls.Add(lblTitle);
            top += 35;

            // Connection Box
            GroupBox gbConnect = new GroupBox();
            gbConnect.Text = " Remote Host Connection Details ";
            gbConnect.Location = new Point(20, top);
            gbConnect.Size = new Size(620, 155);
            _tabClient.Controls.Add(gbConnect);

            Label lblIp = new Label() { Text = "Remote PC IP Address:", Location = new Point(15, 30), AutoSize = true };
            gbConnect.Controls.Add(lblIp);

            _cboClientIp = new ComboBox() { Location = new Point(180, 27), Size = new Size(240, 25) };
            gbConnect.Controls.Add(_cboClientIp);

            Label lblPort = new Label() { Text = "Port:", Location = new Point(435, 30), AutoSize = true };
            gbConnect.Controls.Add(lblPort);

            _numClientPort = new NumericUpDown() { Location = new Point(475, 28), Size = new Size(80, 25), Minimum = 1024, Maximum = 65535, Value = 8888 };
            gbConnect.Controls.Add(_numClientPort);

            Label lblPass = new Label() { Text = "Access PIN / Password:", Location = new Point(15, 68), AutoSize = true };
            gbConnect.Controls.Add(lblPass);

            _txtClientPassword = new TextBox() { Location = new Point(180, 66), Size = new Size(240, 25), PasswordChar = '•' };
            gbConnect.Controls.Add(_txtClientPassword);

            _btnToggleClientPass = new Button() { Text = "Show", Location = new Point(430, 65), Size = new Size(60, 27) };
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
            gbConnect.Controls.Add(_btnToggleClientPass);

            _btnConnect = new Button();
            _btnConnect.Text = "Connect to Remote PC";
            _btnConnect.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            _btnConnect.BackColor = Color.FromArgb(0, 122, 204);
            _btnConnect.ForeColor = Color.White;
            _btnConnect.FlatStyle = FlatStyle.Flat;
            _btnConnect.FlatAppearance.BorderSize = 0;
            _btnConnect.Location = new Point(180, 105);
            _btnConnect.Size = new Size(240, 36);
            _btnConnect.Click += BtnConnect_Click;
            gbConnect.Controls.Add(_btnConnect);

            _lblClientStatus = new Label();
            _lblClientStatus.Text = "";
            _lblClientStatus.ForeColor = Color.FromArgb(80, 80, 80);
            _lblClientStatus.Location = new Point(430, 113);
            _lblClientStatus.AutoSize = true;
            gbConnect.Controls.Add(_lblClientStatus);

            top += 165;

            // Connection Guide
            Label lblGuide = new Label() { Text = "Quick 2-PC Setup Guide:", Location = new Point(20, top), AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
            _tabClient.Controls.Add(lblGuide);
            top += 25;

            _rtbGuide = new RichTextBox();
            _rtbGuide.Location = new Point(20, top);
            _rtbGuide.Size = new Size(620, 210);
            _rtbGuide.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _rtbGuide.ReadOnly = true;
            _rtbGuide.BackColor = Color.FromArgb(248, 249, 251);
            _rtbGuide.BorderStyle = BorderStyle.FixedSingle;
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
