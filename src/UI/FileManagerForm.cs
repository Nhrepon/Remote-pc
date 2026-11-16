using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using RemotePC.Client;
using RemotePC.Common;

namespace RemotePC.UI
{
    public class FileManagerForm : Form
    {
        private readonly RemoteClient _client;

        // Local pane
        private ComboBox _cboLocalDrives;
        private TextBox _txtLocalPath;
        private Button _btnLocalUp;
        private ListView _lvLocal;

        // Remote pane
        private ComboBox _cboRemoteDrives;
        private TextBox _txtRemotePath;
        private Button _btnRemoteUp;
        private ListView _lvRemote;

        // Actions
        private Button _btnUpload;
        private Button _btnDownload;
        private Button _btnRemoteNewFolder;
        private Button _btnRemoteDelete;
        private Button _btnRemoteRefresh;

        // Status
        private Label _lblStatus;
        private ProgressBar _progressBar;

        private string _currentLocalDir;
        private string _currentRemoteDir;

        public FileManagerForm(RemoteClient client)
        {
            _client = client;
            InitializeComponent();
            HookClientEvents();
            LoadLocalDrives();
            LoadLocalDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            // Request remote initial listing
            _client.RequestFileList("");
        }

        private void InitializeComponent()
        {
            this.AutoScaleDimensions = new SizeF(96f, 96f);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "Remote File Manager - Transfer Files";
            this.Size = new Size(950, 600);
            AppIcon.Apply(this);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            this.BackColor = Color.FromArgb(245, 247, 250);

            // Split Container: Left = Local, Right = Remote
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.SplitterDistance = 450;
            split.SplitterWidth = 8;
            this.Controls.Add(split);

            // Status strip at bottom
            Panel pnlBottom = new Panel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 40;
            pnlBottom.BackColor = Color.White;
            pnlBottom.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(pnlBottom);

            _lblStatus = new Label();
            _lblStatus.Text = "Ready";
            _lblStatus.Location = new Point(12, 11);
            _lblStatus.AutoSize = true;
            _lblStatus.ForeColor = Color.FromArgb(60, 60, 60);
            pnlBottom.Controls.Add(_lblStatus);

            _progressBar = new ProgressBar();
            _progressBar.Location = new Point(500, 10);
            _progressBar.Size = new Size(420, 20);
            _progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _progressBar.Visible = false;
            pnlBottom.Controls.Add(_progressBar);

            #region Local Pane Setup

            Panel pnlLocalTop = new Panel();
            pnlLocalTop.Dock = DockStyle.Top;
            pnlLocalTop.Height = 70;
            pnlLocalTop.Padding = new Padding(8);
            split.Panel1.Controls.Add(pnlLocalTop);

            Label lblLocalTitle = new Label();
            lblLocalTitle.Text = "Local PC (This Computer)";
            lblLocalTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblLocalTitle.Location = new Point(8, 8);
            lblLocalTitle.AutoSize = true;
            pnlLocalTop.Controls.Add(lblLocalTitle);

            _cboLocalDrives = new ComboBox();
            _cboLocalDrives.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboLocalDrives.Location = new Point(8, 35);
            _cboLocalDrives.Size = new Size(70, 25);
            _cboLocalDrives.SelectedIndexChanged += CboLocalDrives_SelectedIndexChanged;
            pnlLocalTop.Controls.Add(_cboLocalDrives);

            _btnLocalUp = new Button();
            _btnLocalUp.Text = "Up";
            _btnLocalUp.Location = new Point(84, 34);
            _btnLocalUp.Size = new Size(45, 27);
            _btnLocalUp.Click += BtnLocalUp_Click;
            pnlLocalTop.Controls.Add(_btnLocalUp);

            _txtLocalPath = new TextBox();
            _txtLocalPath.Location = new Point(135, 35);
            _txtLocalPath.Size = new Size(240, 25);
            _txtLocalPath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtLocalPath.KeyDown += TxtLocalPath_KeyDown;
            pnlLocalTop.Controls.Add(_txtLocalPath);

            Button btnLocalGo = new Button();
            btnLocalGo.Text = "Go";
            btnLocalGo.Location = new Point(382, 34);
            btnLocalGo.Size = new Size(45, 27);
            btnLocalGo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnLocalGo.Click += delegate { LoadLocalDirectory(_txtLocalPath.Text); };
            pnlLocalTop.Controls.Add(btnLocalGo);

            _lvLocal = new ListView();
            _lvLocal.Dock = DockStyle.Fill;
            _lvLocal.View = View.Details;
            _lvLocal.FullRowSelect = true;
            _lvLocal.GridLines = true;
            _lvLocal.Columns.Add("Name", 220);
            _lvLocal.Columns.Add("Size", 80, HorizontalAlignment.Right);
            _lvLocal.Columns.Add("Modified", 120);
            _lvLocal.DoubleClick += LvLocal_DoubleClick;
            split.Panel1.Controls.Add(_lvLocal);
            _lvLocal.BringToFront();

            #endregion

            #region Remote Pane Setup

            Panel pnlRemoteTop = new Panel();
            pnlRemoteTop.Dock = DockStyle.Top;
            pnlRemoteTop.Height = 70;
            pnlRemoteTop.Padding = new Padding(8);
            split.Panel2.Controls.Add(pnlRemoteTop);

            Label lblRemoteTitle = new Label();
            lblRemoteTitle.Text = "Remote PC (Host)";
            lblRemoteTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblRemoteTitle.Location = new Point(8, 8);
            lblRemoteTitle.AutoSize = true;
            pnlRemoteTop.Controls.Add(lblRemoteTitle);

            _cboRemoteDrives = new ComboBox();
            _cboRemoteDrives.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboRemoteDrives.Location = new Point(8, 35);
            _cboRemoteDrives.Size = new Size(70, 25);
            _cboRemoteDrives.SelectedIndexChanged += CboRemoteDrives_SelectedIndexChanged;
            pnlRemoteTop.Controls.Add(_cboRemoteDrives);

            _btnRemoteUp = new Button();
            _btnRemoteUp.Text = "Up";
            _btnRemoteUp.Location = new Point(84, 34);
            _btnRemoteUp.Size = new Size(45, 27);
            _btnRemoteUp.Click += BtnRemoteUp_Click;
            pnlRemoteTop.Controls.Add(_btnRemoteUp);

            _txtRemotePath = new TextBox();
            _txtRemotePath.Location = new Point(135, 35);
            _txtRemotePath.Size = new Size(240, 25);
            _txtRemotePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _txtRemotePath.KeyDown += TxtRemotePath_KeyDown;
            pnlRemoteTop.Controls.Add(_txtRemotePath);

            Button btnRemoteGo = new Button();
            btnRemoteGo.Text = "Go";
            btnRemoteGo.Location = new Point(382, 34);
            btnRemoteGo.Size = new Size(45, 27);
            btnRemoteGo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnRemoteGo.Click += delegate { _client.RequestFileList(_txtRemotePath.Text); };
            pnlRemoteTop.Controls.Add(btnRemoteGo);

            Panel pnlRemoteBar = new Panel();
            pnlRemoteBar.Dock = DockStyle.Bottom;
            pnlRemoteBar.Height = 35;
            split.Panel2.Controls.Add(pnlRemoteBar);

            _btnUpload = new Button();
            _btnUpload.Text = "▲ Upload";
            _btnUpload.Location = new Point(8, 4);
            _btnUpload.Size = new Size(85, 27);
            _btnUpload.BackColor = Color.FromArgb(0, 122, 204);
            _btnUpload.ForeColor = Color.White;
            _btnUpload.FlatStyle = FlatStyle.Flat;
            _btnUpload.FlatAppearance.BorderSize = 0;
            _btnUpload.Click += BtnUpload_Click;
            pnlRemoteBar.Controls.Add(_btnUpload);

            _btnDownload = new Button();
            _btnDownload.Text = "▼ Download";
            _btnDownload.Location = new Point(99, 4);
            _btnDownload.Size = new Size(95, 27);
            _btnDownload.BackColor = Color.FromArgb(40, 167, 69);
            _btnDownload.ForeColor = Color.White;
            _btnDownload.FlatStyle = FlatStyle.Flat;
            _btnDownload.FlatAppearance.BorderSize = 0;
            _btnDownload.Click += BtnDownload_Click;
            pnlRemoteBar.Controls.Add(_btnDownload);

            _btnRemoteNewFolder = new Button();
            _btnRemoteNewFolder.Text = "New Folder";
            _btnRemoteNewFolder.Location = new Point(200, 4);
            _btnRemoteNewFolder.Size = new Size(85, 27);
            _btnRemoteNewFolder.Click += BtnRemoteNewFolder_Click;
            pnlRemoteBar.Controls.Add(_btnRemoteNewFolder);

            _btnRemoteDelete = new Button();
            _btnRemoteDelete.Text = "Delete";
            _btnRemoteDelete.Location = new Point(291, 4);
            _btnRemoteDelete.Size = new Size(70, 27);
            _btnRemoteDelete.Click += BtnRemoteDelete_Click;
            pnlRemoteBar.Controls.Add(_btnRemoteDelete);

            _btnRemoteRefresh = new Button();
            _btnRemoteRefresh.Text = "Refresh";
            _btnRemoteRefresh.Location = new Point(367, 4);
            _btnRemoteRefresh.Size = new Size(70, 27);
            _btnRemoteRefresh.Click += delegate { _client.RequestFileList(_currentRemoteDir); };
            pnlRemoteBar.Controls.Add(_btnRemoteRefresh);

            _lvRemote = new ListView();
            _lvRemote.Dock = DockStyle.Fill;
            _lvRemote.View = View.Details;
            _lvRemote.FullRowSelect = true;
            _lvRemote.GridLines = true;
            _lvRemote.Columns.Add("Name", 220);
            _lvRemote.Columns.Add("Size", 80, HorizontalAlignment.Right);
            _lvRemote.Columns.Add("Modified", 120);
            _lvRemote.DoubleClick += LvRemote_DoubleClick;
            split.Panel2.Controls.Add(_lvRemote);
            _lvRemote.BringToFront();

            #endregion
        }

        private void HookClientEvents()
        {
            _client.OnFileListReceived += Client_OnFileListReceived;
            _client.OnDownloadProgress += Client_OnDownloadProgress;
            _client.OnDownloadComplete += Client_OnDownloadComplete;
            _client.OnUploadProgress += Client_OnUploadProgress;
            _client.OnUploadComplete += Client_OnUploadComplete;
            _client.OnFileActionResponse += Client_OnFileActionResponse;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _client.OnFileListReceived -= Client_OnFileListReceived;
            _client.OnDownloadProgress -= Client_OnDownloadProgress;
            _client.OnDownloadComplete -= Client_OnDownloadComplete;
            _client.OnUploadProgress -= Client_OnUploadProgress;
            _client.OnUploadComplete -= Client_OnUploadComplete;
            _client.OnFileActionResponse -= Client_OnFileActionResponse;
            base.OnFormClosing(e);
        }

        #region Local Operations

        private void LoadLocalDrives()
        {
            _cboLocalDrives.Items.Clear();
            DriveInfo[] drives = DriveInfo.GetDrives();
            for (int i = 0; i < drives.Length; i++)
            {
                if (drives[i].IsReady)
                {
                    _cboLocalDrives.Items.Add(drives[i].Name);
                }
            }
            if (_cboLocalDrives.Items.Count > 0)
            {
                _cboLocalDrives.SelectedIndex = 0;
            }
        }

        private void LoadLocalDirectory(string dirPath)
        {
            try
            {
                if (!Directory.Exists(dirPath)) return;

                _currentLocalDir = dirPath;
                _txtLocalPath.Text = dirPath;
                _lvLocal.Items.Clear();

                DirectoryInfo di = new DirectoryInfo(dirPath);
                DirectoryInfo[] subDirs = di.GetDirectories();
                for (int i = 0; i < subDirs.Length; i++)
                {
                    if ((subDirs[i].Attributes & FileAttributes.Hidden) != 0) continue;
                    ListViewItem lvi = new ListViewItem("[Folder] " + subDirs[i].Name);
                    lvi.SubItems.Add("");
                    lvi.SubItems.Add(subDirs[i].LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                    lvi.Tag = "DIR";
                    _lvLocal.Items.Add(lvi);
                }

                FileInfo[] files = di.GetFiles();
                for (int i = 0; i < files.Length; i++)
                {
                    if ((files[i].Attributes & FileAttributes.Hidden) != 0) continue;
                    ListViewItem lvi = new ListViewItem(files[i].Name);
                    lvi.SubItems.Add(SystemInfo.FormatFileSize(files[i].Length));
                    lvi.SubItems.Add(files[i].LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                    lvi.Tag = "FILE";
                    _lvLocal.Items.Add(lvi);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error opening folder: " + ex.Message, "Local Explorer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CboLocalDrives_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cboLocalDrives.SelectedItem != null)
            {
                LoadLocalDirectory(_cboLocalDrives.SelectedItem.ToString());
            }
        }

        private void BtnLocalUp_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentLocalDir))
            {
                DirectoryInfo parent = Directory.GetParent(_currentLocalDir);
                if (parent != null)
                {
                    LoadLocalDirectory(parent.FullName);
                }
            }
        }

        private void TxtLocalPath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                LoadLocalDirectory(_txtLocalPath.Text);
            }
        }

        private void LvLocal_DoubleClick(object sender, EventArgs e)
        {
            if (_lvLocal.SelectedItems.Count == 0) return;
            ListViewItem item = _lvLocal.SelectedItems[0];
            if ((string)item.Tag == "DIR")
            {
                string folderName = item.Text.Substring(9); // remove "[Folder] "
                string targetPath = Path.Combine(_currentLocalDir, folderName);
                LoadLocalDirectory(targetPath);
            }
        }

        #endregion

        #region Remote Operations

        private void Client_OnFileListReceived(string fileListText)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(Client_OnFileListReceived), fileListText);
                return;
            }

            try
            {
                _lvRemote.Items.Clear();
                string[] lines = fileListText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string[] parts = line.Split('|');
                    if (parts.Length < 2) continue;

                    string type = parts[0];
                    if (type == "PATH")
                    {
                        _currentRemoteDir = parts[1];
                        _txtRemotePath.Text = _currentRemoteDir;
                    }
                    else if (type == "DRIVES")
                    {
                        _cboRemoteDrives.Items.Clear();
                        string[] drives = parts[1].Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int d = 0; d < drives.Length; d++)
                        {
                            _cboRemoteDrives.Items.Add(drives[d]);
                        }
                    }
                    else if (type == "DIR" && parts.Length >= 3)
                    {
                        string dirName = parts[1];
                        long ticks = 0;
                        long.TryParse(parts[2], out ticks);
                        DateTime dt = ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime() : DateTime.MinValue;

                        ListViewItem lvi = new ListViewItem("[Folder] " + dirName);
                        lvi.SubItems.Add("");
                        lvi.SubItems.Add(dt != DateTime.MinValue ? dt.ToString("yyyy-MM-dd HH:mm") : "");
                        lvi.Tag = "DIR";
                        _lvRemote.Items.Add(lvi);
                    }
                    else if (type == "FILE" && parts.Length >= 4)
                    {
                        string fileName = parts[1];
                        long size = 0;
                        long.TryParse(parts[2], out size);
                        long ticks = 0;
                        long.TryParse(parts[3], out ticks);
                        DateTime dt = ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime() : DateTime.MinValue;

                        ListViewItem lvi = new ListViewItem(fileName);
                        lvi.SubItems.Add(SystemInfo.FormatFileSize(size));
                        lvi.SubItems.Add(dt != DateTime.MinValue ? dt.ToString("yyyy-MM-dd HH:mm") : "");
                        lvi.Tag = "FILE";
                        _lvRemote.Items.Add(lvi);
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatus("Error parsing remote files: " + ex.Message);
            }
        }

        private void CboRemoteDrives_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_cboRemoteDrives.SelectedItem != null)
            {
                _client.RequestFileList(_cboRemoteDrives.SelectedItem.ToString());
            }
        }

        private void BtnRemoteUp_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentRemoteDir))
            {
                string parent = Path.GetDirectoryName(_currentRemoteDir.TrimEnd('\\', '/'));
                if (!string.IsNullOrEmpty(parent))
                {
                    _client.RequestFileList(parent);
                }
                else
                {
                    _client.RequestFileList("DRIVES");
                }
            }
        }

        private void TxtRemotePath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _client.RequestFileList(_txtRemotePath.Text);
            }
        }

        private void LvRemote_DoubleClick(object sender, EventArgs e)
        {
            if (_lvRemote.SelectedItems.Count == 0) return;
            ListViewItem item = _lvRemote.SelectedItems[0];
            if ((string)item.Tag == "DIR")
            {
                string folderName = item.Text.Substring(9); // remove "[Folder] "
                string targetPath = Path.Combine(_currentRemoteDir, folderName);
                _client.RequestFileList(targetPath);
            }
        }

        private void BtnRemoteNewFolder_Click(object sender, EventArgs e)
        {
            string folderName = ShowInputDialog("Enter new folder name:", "Create Remote Folder", "NewFolder");
            if (!string.IsNullOrEmpty(folderName))
            {
                string targetPath = Path.Combine(_currentRemoteDir, folderName);
                _client.SendFileAction(3, targetPath);
                _client.RequestFileList(_currentRemoteDir);
            }
        }

        private static string ShowInputDialog(string text, string caption, string defaultValue)
        {
            using (Form prompt = new Form())
            {
                prompt.Width = 360;
                prompt.Height = 160;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.Text = caption;
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;

                Label lbl = new Label() { Left = 20, Top = 15, Text = text, AutoSize = true };
                TextBox txt = new TextBox() { Left = 20, Top = 40, Width = 300, Text = defaultValue };
                Button btnOk = new Button() { Text = "OK", Left = 150, Width = 80, Top = 75, DialogResult = DialogResult.OK };
                Button btnCancel = new Button() { Text = "Cancel", Left = 240, Width = 80, Top = 75, DialogResult = DialogResult.Cancel };

                prompt.Controls.Add(lbl);
                prompt.Controls.Add(txt);
                prompt.Controls.Add(btnOk);
                prompt.Controls.Add(btnCancel);
                prompt.AcceptButton = btnOk;
                prompt.CancelButton = btnCancel;

                return prompt.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : "";
            }
        }

        private void BtnRemoteDelete_Click(object sender, EventArgs e)
        {
            if (_lvRemote.SelectedItems.Count == 0) return;
            ListViewItem item = _lvRemote.SelectedItems[0];
            bool isDir = (string)item.Tag == "DIR";
            string name = isDir ? item.Text.Substring(9) : item.Text;
            string targetPath = Path.Combine(_currentRemoteDir, name);

            DialogResult dr = MessageBox.Show(string.Format("Are you sure you want to delete '{0}' on remote PC?", name), "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr == DialogResult.Yes)
            {
                _client.SendFileAction(isDir ? 2 : 1, targetPath);
                _client.RequestFileList(_currentRemoteDir);
            }
        }

        #endregion

        #region Upload & Download Operations

        private void BtnUpload_Click(object sender, EventArgs e)
        {
            if (_lvLocal.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a local file to upload.", "Upload", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ListViewItem item = _lvLocal.SelectedItems[0];
            if ((string)item.Tag != "FILE")
            {
                MessageBox.Show("Only files can be uploaded directly. Please select a file.", "Upload", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string localPath = Path.Combine(_currentLocalDir, item.Text);
            string remoteDestPath = Path.Combine(_currentRemoteDir, item.Text);

            SetStatus("Uploading " + item.Text + "...");
            _progressBar.Value = 0;
            _progressBar.Visible = true;

            _client.UploadFile(localPath, remoteDestPath);
        }

        private void BtnDownload_Click(object sender, EventArgs e)
        {
            if (_lvRemote.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a remote file to download.", "Download", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ListViewItem item = _lvRemote.SelectedItems[0];
            if ((string)item.Tag != "FILE")
            {
                MessageBox.Show("Only files can be downloaded directly. Please select a file.", "Download", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string remotePath = Path.Combine(_currentRemoteDir, item.Text);
            string localSavePath = Path.Combine(_currentLocalDir, item.Text);

            SetStatus("Downloading " + item.Text + "...");
            _progressBar.Value = 0;
            _progressBar.Visible = true;

            _client.RequestFileDownload(remotePath, localSavePath);
        }

        private void Client_OnDownloadProgress(string fileName, int curChunk, int totalChunks, int percent)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string, int, int, int>(Client_OnDownloadProgress), fileName, curChunk, totalChunks, percent);
                return;
            }
            SetStatus(string.Format("Downloading {0}: {1}%", fileName, percent));
            _progressBar.Value = Math.Min(100, Math.Max(0, percent));
        }

        private void Client_OnDownloadComplete(string fileName, bool success)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string, bool>(Client_OnDownloadComplete), fileName, success);
                return;
            }
            _progressBar.Visible = false;
            if (success)
            {
                SetStatus("Downloaded " + fileName + " successfully.");
                LoadLocalDirectory(_currentLocalDir); // Refresh local view
            }
            else
            {
                SetStatus("Download failed for " + fileName);
            }
        }

        private void Client_OnUploadProgress(string fileName, int curChunk, int totalChunks, int percent)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string, int, int, int>(Client_OnUploadProgress), fileName, curChunk, totalChunks, percent);
                return;
            }
            SetStatus(string.Format("Uploading {0}: {1}%", fileName, percent));
            _progressBar.Value = Math.Min(100, Math.Max(0, percent));
        }

        private void Client_OnUploadComplete(string fileName, bool success)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string, bool>(Client_OnUploadComplete), fileName, success);
                return;
            }
            _progressBar.Visible = false;
            if (success)
            {
                SetStatus("Uploaded " + fileName + " successfully.");
                _client.RequestFileList(_currentRemoteDir); // Refresh remote view
            }
            else
            {
                SetStatus("Upload failed for " + fileName);
            }
        }

        private void Client_OnFileActionResponse(bool success, string msg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<bool, string>(Client_OnFileActionResponse), success, msg);
                return;
            }
            SetStatus(msg);
        }

        private void SetStatus(string text)
        {
            _lblStatus.Text = text;
        }

        #endregion
    }
}
