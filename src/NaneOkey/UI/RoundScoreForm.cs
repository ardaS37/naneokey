using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NaneOkey.UI
{
    public sealed class RoundScoreRecord
    {
        public int RoundNumber { get; set; }
        public Dictionary<string, int> Scores { get; set; }
    }

    public sealed class TotalScoreRecord
    {
        public string PlayerName { get; set; }
        public int TotalScore { get; set; }
    }

    public sealed class RoundScoreForm : Form
    {
        public RoundScoreForm(
            IList<string> playerNames,
            IList<RoundScoreRecord> roundHistory,
            IList<TotalScoreRecord> totals,
            bool canStartNextRound,
            string winnerText)
        {
            Text = "Tur Puanları";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 760;
            Height = 520;
            BackColor = Color.FromArgb(238, 229, 212);

            var winnerLabel = new Label
            {
                Left = 16,
                Top = 16,
                Width = 710,
                Height = 28,
                Font = new Font("Tahoma", 10F, FontStyle.Bold),
                Text = winnerText
            };

            var roundLabel = new Label
            {
                Left = 16,
                Top = 54,
                Width = 320,
                Height = 22,
                Font = new Font("Tahoma", 9F, FontStyle.Bold),
                Text = "Tur Puanları"
            };

            var roundList = new ListView
            {
                Left = 16,
                Top = 80,
                Width = 710,
                Height = 250,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            roundList.Columns.Add("Tur", 60);
            foreach (var playerName in playerNames)
            {
                roundList.Columns.Add(playerName, 160);
            }

            foreach (var round in roundHistory)
            {
                var row = new ListViewItem(round.RoundNumber.ToString());
                foreach (var playerName in playerNames)
                {
                    var score = round.Scores != null && round.Scores.ContainsKey(playerName) ? round.Scores[playerName] : 0;
                    row.SubItems.Add(score.ToString());
                }
                roundList.Items.Add(row);
            }

            var totalLabel = new Label
            {
                Left = 16,
                Top = 342,
                Width = 320,
                Height = 22,
                Font = new Font("Tahoma", 9F, FontStyle.Bold),
                Text = "Toplam Puan"
            };

            var totalList = new ListView
            {
                Left = 16,
                Top = 368,
                Width = 710,
                Height = 78,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            totalList.Columns.Add("Oyuncu", 360);
            totalList.Columns.Add("Toplam", 120);

            foreach (var total in totals.OrderBy(x => x.TotalScore))
            {
                var row = new ListViewItem(total.PlayerName);
                row.SubItems.Add(total.TotalScore.ToString());
                totalList.Items.Add(row);
            }

            var nextRoundButton = new Button
            {
                Text = "Sonraki Tur",
                Left = 500,
                Top = 455,
                Width = 110,
                Height = 30,
                DialogResult = canStartNextRound ? DialogResult.OK : DialogResult.None,
                Enabled = canStartNextRound
            };

            var closeButton = new Button
            {
                Text = "Kapat",
                Left = 616,
                Top = 455,
                Width = 110,
                Height = 30,
                DialogResult = DialogResult.Cancel
            };

            Controls.Add(winnerLabel);
            Controls.Add(roundLabel);
            Controls.Add(roundList);
            Controls.Add(totalLabel);
            Controls.Add(totalList);
            Controls.Add(nextRoundButton);
            Controls.Add(closeButton);

            AcceptButton = canStartNextRound ? nextRoundButton : closeButton;
            CancelButton = closeButton;
        }
    }
}
