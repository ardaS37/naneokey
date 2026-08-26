using System.Drawing;
using System.Windows.Forms;
using NaneOkey.Domain;

namespace NaneOkey.UI
{
    public sealed class NewGameForm : Form
    {
        private readonly TextBox[] _nameBoxes = new TextBox[4];
        private readonly ComboBox[] _typeBoxes = new ComboBox[4];
        private readonly ComboBox[] _difficultyBoxes = new ComboBox[4];
        private readonly Label[] _seatLabels = new Label[4];
        private readonly ComboBox _playerCountBox = new ComboBox();
        private readonly NumericUpDown _targetScoreBox = new NumericUpDown();
        private readonly CheckBox _livePreviewBox = new CheckBox();
        private readonly CheckBox _turnTimerEnabledBox = new CheckBox();
        private readonly NumericUpDown _turnSecondsBox = new NumericUpDown();
        private readonly NumericUpDown _botThinkSecondsBox = new NumericUpDown();

        public NewGameForm()
            : this(null)
        {
        }

        public NewGameForm(GameSettings initialSettings)
        {
            Text = "Oyun Ayarları";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Width = 640;
            Height = 520;
            BackColor = Color.FromArgb(233, 228, 219);

            var seats = new[] { "Aşağı", "Sol", "Yukarı", "Sağ" };
            var defaultNames = new[] { "Nurhan", "Kemal", "Sarp", "Şükrü" };

            var playerCountLabel = new Label
            {
                Left = 30,
                Top = 18,
                Width = 170,
                Text = "Oyuncu Sayısı"
            };

            _playerCountBox.Left = 210;
            _playerCountBox.Top = 15;
            _playerCountBox.Width = 150;
            _playerCountBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _playerCountBox.Items.Add("2");
            _playerCountBox.Items.Add("3");
            _playerCountBox.Items.Add("4");
            _playerCountBox.SelectedItem = "4";
            _playerCountBox.SelectedIndexChanged += (_, __) => ApplyPlayerCountLayout();

            Controls.Add(playerCountLabel);
            Controls.Add(_playerCountBox);

            var targetScoreLabel = new Label
            {
                Left = 30,
                Top = 52,
                Width = 170,
                Text = "Hedef Puan"
            };

            _targetScoreBox.Left = 210;
            _targetScoreBox.Top = 49;
            _targetScoreBox.Width = 150;
            _targetScoreBox.Minimum = 100;
            _targetScoreBox.Maximum = 100000;
            _targetScoreBox.Increment = 100;
            _targetScoreBox.Value = 1000;

            Controls.Add(targetScoreLabel);
            Controls.Add(_targetScoreBox);

            _livePreviewBox.Left = 210;
            _livePreviewBox.Top = 82;
            _livePreviewBox.Width = 320;
            _livePreviewBox.Text = "Canlı önizleme (LAN)";
            _livePreviewBox.Checked = false;
            Controls.Add(_livePreviewBox);

            _turnTimerEnabledBox.Left = 210;
            _turnTimerEnabledBox.Top = 106;
            _turnTimerEnabledBox.Width = 140;
            _turnTimerEnabledBox.Text = "Tur süresi";
            _turnTimerEnabledBox.Checked = false;
            _turnTimerEnabledBox.CheckedChanged += (_, __) => _turnSecondsBox.Enabled = _turnTimerEnabledBox.Checked;
            Controls.Add(_turnTimerEnabledBox);

            _turnSecondsBox.Left = 360;
            _turnSecondsBox.Top = 104;
            _turnSecondsBox.Width = 70;
            _turnSecondsBox.Minimum = 5;
            _turnSecondsBox.Maximum = 300;
            _turnSecondsBox.Increment = 5;
            _turnSecondsBox.Value = 30;
            _turnSecondsBox.Enabled = false;
            Controls.Add(_turnSecondsBox);

            var botThinkLabel = new Label
            {
                Left = 30,
                Top = 136,
                Width = 170,
                Text = "Bot düşünme süresi"
            };

            _botThinkSecondsBox.Left = 210;
            _botThinkSecondsBox.Top = 133;
            _botThinkSecondsBox.Width = 70;
            _botThinkSecondsBox.Minimum = 1;
            _botThinkSecondsBox.Maximum = 180;
            _botThinkSecondsBox.Increment = 1;
            _botThinkSecondsBox.Value = 30;

            var botThinkSuffix = new Label
            {
                Left = 290,
                Top = 136,
                Width = 50,
                Text = "sn"
            };

            Controls.Add(botThinkLabel);
            Controls.Add(_botThinkSecondsBox);
            Controls.Add(botThinkSuffix);

            var playerTop = 170;
            for (var index = 0; index < 4; index++)
            {
                var label = new Label
                {
                    Left = 30,
                    Top = playerTop + index * 45,
                    Width = 80,
                    Text = seats[index]
                };

                var nameBox = new TextBox
                {
                    Left = 120,
                    Top = label.Top - 3,
                    Width = 150,
                    Text = defaultNames[index]
                };

                var typeBox = new ComboBox
                {
                    Left = 290,
                    Top = label.Top - 3,
                    Width = 140,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                if (index == 0)
                {
                    typeBox.Items.Add("İnsan");
                    typeBox.SelectedIndex = 0;
                }
                else
                {
                    typeBox.Items.Add("Bilgisayar");
                    typeBox.Items.Add("Ağ Oyuncusu");
                    typeBox.SelectedIndex = 0;
                }

                var difficultyBox = new ComboBox
                {
                    Left = 445,
                    Top = label.Top - 3,
                    Width = 140,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                difficultyBox.Items.Add("Çok Kolay");
                difficultyBox.Items.Add("Kolay");
                difficultyBox.Items.Add("Orta");
                difficultyBox.Items.Add("Zor");
                difficultyBox.Items.Add("Çok Zor");
                difficultyBox.Items.Add("Akıllı");
                difficultyBox.Items.Add("Elebakan");
                difficultyBox.Items.Add("Taşçalan");
                difficultyBox.SelectedIndex = 5;
                difficultyBox.Enabled = typeBox.SelectedItem as string == "Bilgisayar";
                typeBox.SelectedIndexChanged += (_, __) => difficultyBox.Enabled = typeBox.SelectedItem as string == "Bilgisayar";

                Controls.Add(label);
                Controls.Add(nameBox);
                Controls.Add(typeBox);
                Controls.Add(difficultyBox);
                _nameBoxes[index] = nameBox;
                _typeBoxes[index] = typeBox;
                _difficultyBoxes[index] = difficultyBox;
                _seatLabels[index] = label;
            }

            var startButton = new Button
            {
                Text = "Tamam",
                Left = 280,
                Top = 420,
                Width = 90,
                DialogResult = DialogResult.OK
            };

            var cancelButton = new Button
            {
                Text = "Vazgeç",
                Left = 380,
                Top = 420,
                Width = 90,
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(startButton);
            Controls.Add(cancelButton);
            AcceptButton = startButton;
            CancelButton = cancelButton;
            ApplyInitialSettings(initialSettings);
            ApplyPlayerCountLayout();
        }

        private void ApplyInitialSettings(GameSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            _playerCountBox.SelectedItem = settings.ActivePlayerCount.ToString();
            _targetScoreBox.Value = ClampNumericValue(_targetScoreBox, settings.TargetScore);
            _livePreviewBox.Checked = settings.EnableLivePreview;
            _turnTimerEnabledBox.Checked = settings.EnableTurnTimer;
            _turnSecondsBox.Value = ClampNumericValue(_turnSecondsBox, settings.TurnSeconds);
            _turnSecondsBox.Enabled = _turnTimerEnabledBox.Checked;
            _botThinkSecondsBox.Value = ClampNumericValue(_botThinkSecondsBox, settings.BotThinkSeconds);

            for (var index = 0; index < 4 && index < settings.Players.Count; index++)
            {
                var player = settings.Players[index];
                _nameBoxes[index].Text = player.Name;
                _typeBoxes[index].SelectedItem = ToPlayerTypeText(player.Type, index);
                _difficultyBoxes[index].SelectedItem = ToDifficultyText(player.Difficulty);
            }
        }

        private static decimal ClampNumericValue(NumericUpDown box, int value)
        {
            if (value < box.Minimum)
            {
                return box.Minimum;
            }

            if (value > box.Maximum)
            {
                return box.Maximum;
            }

            return value;
        }

        public GameSettings CreateSettings()
        {
            var settings = new GameSettings
            {
                StartingHandSize = 15,
                LanPort = 51234,
                ActivePlayerCount = int.Parse(_playerCountBox.SelectedItem as string ?? "4"),
                TargetScore = (int)_targetScoreBox.Value,
                EnableLivePreview = _livePreviewBox.Checked,
                EnableTurnTimer = _turnTimerEnabledBox.Checked,
                TurnSeconds = (int)_turnSecondsBox.Value,
                BotThinkSeconds = (int)_botThinkSecondsBox.Value
            };

            for (var index = 0; index < 4; index++)
            {
                settings.Players.Add(new PlayerSetup
                {
                    Name = _nameBoxes[index].Text.Trim(),
                    Type = ToPlayerType(_typeBoxes[index].SelectedItem as string),
                    Difficulty = ToBotDifficulty(_difficultyBoxes[index].SelectedItem as string),
                    IsActive = IsSeatActive(index, settings.ActivePlayerCount)
                });
            }

            return settings;
        }

        private void ApplyPlayerCountLayout()
        {
            var count = int.Parse(_playerCountBox.SelectedItem as string ?? "4");
            for (var index = 0; index < 4; index++)
            {
                var active = IsSeatActive(index, count);
                _seatLabels[index].Enabled = active;
                _nameBoxes[index].Enabled = active;
                _typeBoxes[index].Enabled = active;
                _difficultyBoxes[index].Enabled = active && (_typeBoxes[index].SelectedItem as string == "Bilgisayar");
                if (!active)
                {
                    _typeBoxes[index].SelectedIndex = index == 0 ? 0 : 0;
                }
            }
        }

        private static bool IsSeatActive(int index, int playerCount)
        {
            if (playerCount <= 2)
            {
                return index == 0 || index == 2;
            }

            if (playerCount == 3)
            {
                return index == 0 || index == 1 || index == 3;
            }

            return true;
        }

        private static PlayerType ToPlayerType(string text)
        {
            switch (text)
            {
                case "Bilgisayar":
                    return PlayerType.Bot;
                case "Ağ Oyuncusu":
                    return PlayerType.Remote;
                default:
                    return PlayerType.Human;
            }
        }

        private static BotDifficulty ToBotDifficulty(string text)
        {
            switch (text)
            {
                case "Çok Kolay":
                    return BotDifficulty.Easy;
                case "Kolay":
                    return BotDifficulty.Medium;
                case "Orta":
                    return BotDifficulty.Hard;
                case "Akıllı":
                    return BotDifficulty.SmartHard;
                case "Çok Zor":
                    return BotDifficulty.UltraHard;
                case "Elebakan":
                    return BotDifficulty.Cheater;
                case "Taşçalan":
                    return BotDifficulty.Impossible;
                case "Zor":
                    return BotDifficulty.VeryHard;
                default:
                    return BotDifficulty.SmartHard;
            }
        }

        private static string ToPlayerTypeText(PlayerType type, int index)
        {
            if (index == 0)
            {
                return "İnsan";
            }

            switch (type)
            {
                case PlayerType.Remote:
                    return "Ağ Oyuncusu";
                case PlayerType.Human:
                    return "İnsan";
                default:
                    return "Bilgisayar";
            }
        }

        private static string ToDifficultyText(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return "Çok Kolay";
                case BotDifficulty.Medium:
                    return "Kolay";
                case BotDifficulty.Hard:
                    return "Orta";
                case BotDifficulty.VeryHard:
                    return "Zor";
                case BotDifficulty.UltraHard:
                    return "Çok Zor";
                case BotDifficulty.Cheater:
                    return "Elebakan";
                case BotDifficulty.Impossible:
                    return "Taşçalan";
                default:
                    return "Akıllı";
            }
        }
    }
}
