using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace NaneOkey.UI
{
    internal sealed class AboutForm : Form
    {
        private int _f1PressCount;
        private bool _f1Down;
        private readonly Label _debugStatus;
        public event Action BotDebugUnlocked;

        public AboutForm()
        {
            Text = "Hakkında";
            ClientSize = new Size(390, 205);
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Tahoma", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            KeyPreview = true;
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var information = new Label
            {
                Bounds = new Rectangle(20, 18, 350, 110),
                Text = "Nane Okey v" + (version != null ? version.ToString() : "?") + Environment.NewLine +
                    "Copyright © Arda Saplıoğlu 2026" + Environment.NewLine + "Arda" + Environment.NewLine +
                    "sapliogluarda@gmail.com"
            };
            _debugStatus = new Label { Bounds = new Rectangle(20, 131, 350, 22), Text = string.Empty };
            var close = new Button { Text = "Tamam", Bounds = new Rectangle(280, 165, 90, 28), DialogResult = DialogResult.OK };
            Controls.Add(information);
            Controls.Add(_debugStatus);
            Controls.Add(close);
            AcceptButton = close;
            CancelButton = close;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData != Keys.F1) return base.ProcessCmdKey(ref msg, keyData);
            if (!_f1Down)
            {
                _f1Down = true;
                if (++_f1PressCount == 3)
                {
                    _debugStatus.Text = "Bot Debug etkinleştirildi.";
                    if (BotDebugUnlocked != null) BotDebugUnlocked();
                }
            }
            return true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F1) _f1Down = false;
            base.OnKeyUp(e);
        }
    }
}
