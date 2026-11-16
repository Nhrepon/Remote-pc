using System;
using System.Drawing;
using System.Windows.Forms;

namespace RemotePC.UI
{
    public class SystemInfoForm : Form
    {
        private TextBox _txtInfo;
        private Button _btnClose;

        public SystemInfoForm(string systemInfo)
        {
            InitializeComponent(systemInfo);
        }

        private void InitializeComponent(string systemInfo)
        {
            this.Text = "Remote System Information";
            this.Size = new Size(500, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(245, 247, 250);

            Label lblTitle = new Label();
            lblTitle.Text = "Remote PC Specifications";
            lblTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblTitle.Location = new Point(20, 15);
            lblTitle.AutoSize = true;
            this.Controls.Add(lblTitle);

            _txtInfo = new TextBox();
            _txtInfo.Multiline = true;
            _txtInfo.ReadOnly = true;
            _txtInfo.ScrollBars = ScrollBars.Vertical;
            _txtInfo.Location = new Point(20, 50);
            _txtInfo.Size = new Size(445, 270);
            _txtInfo.BackColor = Color.White;
            _txtInfo.Font = new Font("Consolas", 10f);
            _txtInfo.Text = systemInfo;
            this.Controls.Add(_txtInfo);

            _btnClose = new Button();
            _btnClose.Text = "Close";
            _btnClose.Size = new Size(100, 32);
            _btnClose.Location = new Point(365, 335);
            _btnClose.BackColor = Color.FromArgb(0, 122, 204);
            _btnClose.ForeColor = Color.White;
            _btnClose.FlatStyle = FlatStyle.Flat;
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.Click += delegate { this.Close(); };
            this.Controls.Add(_btnClose);
        }
    }
}
