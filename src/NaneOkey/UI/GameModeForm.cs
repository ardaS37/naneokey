using System.Drawing;
using System.Windows.Forms;
using NaneOkey.Domain;

namespace NaneOkey.UI
{
    public sealed class GameModeForm : Form
    {
        public GameModeForm(GameMode initialMode)
        {
            Text = "Yeni Oyun";
            ClientSize = new Size(400, 280);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(233, 228, 219);
            Font = new Font("Tahoma", 9F);
            SelectedMode = initialMode;
            Controls.Add(new Label { Text = "Oynamak istediğin oyunu seç", Left = 24, Top = 20, Width = 350, Height = 24 });
            var modes = new[] { GameMode.NaneOkey, GameMode.ClassicOkey, GameMode.Okey101 };
            var names = new[] { "Nane Okey", "Klasik Okey", "101 Okey" };
            for (var index = 0; index < modes.Length; index++)
            {
                var mode = modes[index];
                var button = new Button { Text = names[index], Left = 24, Top = 54 + index * 52, Width = 350, Height = 42 };
                button.Click += (_, __) => { SelectedMode = mode; DialogResult = DialogResult.OK; };
                Controls.Add(button);
                if (mode == initialMode) AcceptButton = button;
            }
            var cancel = new Button { Text = "Vazgeç", Left = 274, Top = 222, Width = 100, Height = 30, DialogResult = DialogResult.Cancel };
            Controls.Add(cancel);
            CancelButton = cancel;
        }

        public GameMode SelectedMode { get; private set; }

        public static string ModeName(GameMode mode)
        {
            return mode == GameMode.ClassicOkey ? "Klasik Okey" : mode == GameMode.Okey101 ? "101 Okey" : "Nane Okey";
        }
    }
}
