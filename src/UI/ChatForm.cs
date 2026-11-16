using System;
using System.Drawing;
using System.Windows.Forms;
using RemotePC.Common;

namespace RemotePC.UI
{
    public class ChatForm : Form
    {
        private RichTextBox _rtbMessages;
        private TextBox _txtInput;
        private Button _btnSend;
        private Action<string> _sendCallback;

        public ChatForm(string title, Action<string> sendCallback)
        {
            _sendCallback = sendCallback;
            InitializeComponent(title);
        }

        private void InitializeComponent(string title)
        {
            this.AutoScaleDimensions = new SizeF(96f, 96f);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "Chat - " + title;
            this.Size = new Size(420, 500);
            AppIcon.Apply(this);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.FromArgb(245, 247, 250);

            _rtbMessages = new RichTextBox();
            _rtbMessages.Location = new Point(12, 12);
            _rtbMessages.Size = new Size(380, 390);
            _rtbMessages.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _rtbMessages.ReadOnly = true;
            _rtbMessages.BackColor = Color.White;
            _rtbMessages.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(_rtbMessages);

            _txtInput = new TextBox();
            _txtInput.Location = new Point(12, 415);
            _txtInput.Size = new Size(295, 26);
            _txtInput.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _txtInput.KeyDown += TxtInput_KeyDown;
            this.Controls.Add(_txtInput);

            _btnSend = new Button();
            _btnSend.Text = "Send";
            _btnSend.Location = new Point(315, 414);
            _btnSend.Size = new Size(77, 28);
            _btnSend.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnSend.BackColor = Color.FromArgb(0, 122, 204);
            _btnSend.ForeColor = Color.White;
            _btnSend.FlatStyle = FlatStyle.Flat;
            _btnSend.FlatAppearance.BorderSize = 0;
            _btnSend.Click += BtnSend_Click;
            this.Controls.Add(_btnSend);
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            SendMessage();
        }

        private void SendMessage()
        {
            string text = _txtInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            AppendMessage("Me", text, DateTime.Now.ToString("HH:mm:ss"), Color.FromArgb(0, 102, 204));
            _txtInput.Clear();

            if (_sendCallback != null)
            {
                _sendCallback(text);
            }
        }

        public void AppendMessage(string sender, string message, string time, Color senderColor)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string, string, string, Color>(AppendMessage), sender, message, time, senderColor);
                return;
            }

            _rtbMessages.SelectionStart = _rtbMessages.TextLength;
            _rtbMessages.SelectionLength = 0;

            _rtbMessages.SelectionColor = Color.Gray;
            _rtbMessages.AppendText("[" + time + "] ");

            _rtbMessages.SelectionColor = senderColor;
            _rtbMessages.SelectionFont = new Font(_rtbMessages.Font, FontStyle.Bold);
            _rtbMessages.AppendText(sender + ": ");

            _rtbMessages.SelectionColor = Color.Black;
            _rtbMessages.SelectionFont = new Font(_rtbMessages.Font, FontStyle.Regular);
            _rtbMessages.AppendText(message + Environment.NewLine);

            _rtbMessages.ScrollToCaret();
        }
    }
}
