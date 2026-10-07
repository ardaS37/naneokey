using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NaneOkey.Domain;
using NaneOkey.Network;
using NaneOkey.Engine;

namespace NaneOkey.UI
{
    public sealed partial class MainForm
    {
        private bool IsTraditionalGame { get { return _engine.State.Mode != GameMode.NaneOkey; } }

        private void ApplyNetworkMatchId(string matchId)
        {
            if (string.IsNullOrEmpty(matchId) || string.Equals(_matchId, matchId, StringComparison.Ordinal)) return;
            _matchId = matchId;
            _roundHistory.Clear();
            _totalScores.Clear();
            _roundScoreShownForCurrentGame = false;
        }

        private void RenamePlayerPreservingScores(PlayerState player, string requestedName)
        {
            var name = string.IsNullOrWhiteSpace(requestedName) ? SeatName(player.Seat) : requestedName;
            var original = name;
            var suffix = 2;
            while (_engine.State.Players.Any(x => x.IsActive && x.Seat != player.Seat && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                name = original + " (" + suffix++ + ")";
            var previous = player.Name;
            player.Name = name;
            if (string.Equals(previous, name, StringComparison.Ordinal)) return;
            int total;
            if (_totalScores.TryGetValue(previous, out total))
            {
                _totalScores.Remove(previous);
                _totalScores[name] = total;
            }
            foreach (var round in _roundHistory)
            {
                if (round.Scores == null || !round.Scores.TryGetValue(previous, out total)) continue;
                round.Scores.Remove(previous);
                round.Scores[name] = total;
            }
        }

        private GameSettings SelectNewGameSettings(GameSettings initialSettings)
        {
            using (var selection = new GameModeForm(initialSettings.Mode))
            {
                if (selection.ShowDialog(this) != DialogResult.OK) return null;
                var settings = CloneSettings(initialSettings);
                if (settings.Mode != selection.SelectedMode)
                    settings.TargetScore = selection.SelectedMode == GameMode.ClassicOkey ? 20 : 1000;
                settings.Mode = selection.SelectedMode;
                using (var dialog = new NewGameForm(settings))
                {
                    return dialog.ShowDialog(this) == DialogResult.OK ? dialog.CreateSettings() : null;
                }
            }
        }

        private List<List<Tile>> BuildTraditionalHandGroups(List<Tile> hand)
        {
            var rules = new TraditionalRuleValidator(_engine.State.Mode);
            var player = GetLocalPlayerOrFallback(_engine.State);
            var pairsOnly = _engine.State.Mode == GameMode.Okey101 && player != null && player.OpenedWithPairs;
            var melds = rules.FindBestMelds(hand, pairsOnly, _engine.State.Mode == GameMode.Okey101);
            if (!pairsOnly)
            {
                var pairs = rules.FindBestMelds(hand, true, false);
                if ((_engine.State.Mode == GameMode.ClassicOkey && pairs.Sum(x => x.Tiles.Count) >= melds.Sum(x => x.Tiles.Count)) ||
                    (_engine.State.Mode == GameMode.Okey101 && player != null && !player.HasOpened && melds.Sum(rules.MeldValue) < 101 && pairs.Count >= 5))
                    melds = pairs;
            }
            var original = hand.ToDictionary(x => x.Id);
            var used = new HashSet<int>(melds.SelectMany(x => x.Tiles).Select(x => x.Id));
            var groups = melds.Select(x => x.Tiles.Select(tile => original[tile.Id].Clone()).ToList()).ToList();
            var remainder = hand.Where(x => !used.Contains(x.Id)).OrderBy(x => x.IsJoker).ThenBy(x => x.Color).ThenBy(x => x.Number).Select(x => x.Clone()).ToList();
            if (remainder.Count > 0) groups.Add(remainder);
            return groups;
        }

        private void RefreshTraditionalUi()
        {
            Text = GameModeForm.ModeName(_engine.State.Mode) + " - v4.0.1.0";
            if (_tableLayoutMode != _engine.State.Mode || _tableLayoutAppearance != UsesNewTableAppearance) LayoutGameScreen();
            foreach (var button in _solMenusuPaneli.Controls.OfType<Button>())
            {
                if (button.Text == "Tahta Rengi" || button.Text == "Günlük / Sohbet" || button.Text == "Sohbet")
                    button.Text = UsesNewTableAppearance ? "Günlük / Sohbet" : "Tahta Rengi";
                if (button.Text == "Soldan Taş Al" || button.Text == "Yandan Taş Al")
                {
                    button.Text = "Yandan Taş Al";
                    button.Visible = IsTraditionalGame;
                }
                if (button.Text == "Hamleyi Oyna" || button.Text == "Taş At / Bitir" || button.Text == "Onayla")
                    button.Text = IsTraditionalGame ? "Taş At / Bitir" : "Hamleyi Oyna";
            }
            LayoutSideMenuSurface();
            if (!IsTraditionalGame) { SetTraditionalTableVisibility(IsNewNaneAppearance); if (IsNewNaneAppearance) RefreshTraditionalTable(); return; }
            RefreshTraditionalTable();
            var state = _engine.State;
            _durumEtiketi.Text = state.IsGameOver ? "Tur bitti" : state.HasDrawnThisTurn ? "Taş atmalısın" : "Taş çekmelisin";
            if (state.Indicator != null)
            {
                var jokerNumber = state.Indicator.Number == 13 ? 1 : state.Indicator.Number + 1;
                _desteEtiketi.Text = "Deste: " + state.Deck.Count + Environment.NewLine + "Gösterge: " + state.Indicator + Environment.NewLine + "Okey: " + jokerNumber + state.Indicator.ColorName;
            }
            foreach (var pile in state.DiscardPiles)
            {
                if (pile.Tiles.Count > 0 && _oyuncuEtiketleri.ContainsKey(pile.Seat))
                    _oyuncuEtiketleri[pile.Seat].Text += " · Atık: " + pile.Tiles.Last();
            }
        }

        private void DrawDiscardTile()
        {
            if (!IsTraditionalGame || !CanHumanAct(true)) return;
            if (_agIstemcisiModu)
            {
                if (_client != null) _client.SendDiscardDrawRequest();
                else if (_onlineClient != null) _onlineClient.SendEnvelope("discard_draw", string.Empty, false);
                return;
            }
            HandleRemoteDiscardDrawRequest(_engine.State.CurrentTurn);
        }

        private void HandleRemoteDiscardDrawRequest(Seat seat)
        {
            if (!_oyunBasladi || !IsTraditionalGame || _engine.State.IsGameOver || _engine.State.CurrentTurn != seat) return;
            string message;
            if (_engine.DrawDiscard(seat, out message)) AfterTraditionalAction(message);
            else ReportTraditionalError(seat, message);
        }

        private void ShowTraditionalDiscardDialog(int preferredTileId = -1)
        {
            if (!IsTraditionalGame || !CanHumanAct(true)) return;
            if (!_engine.State.HasDrawnThisTurn)
            {
                Log("Önce ortadan veya soldan bir taş çekmelisin.");
                return;
            }
            var tiles = _elSlotlari.Cast<Tile>().Where(x => x != null).ToList();
            if (tiles.Count == 0) { Log("Turu bitirmek için elinde atılacak bir taş kalmalı."); return; }
            using (var dialog = new Form())
            {
                dialog.Text = _engine.State.Mode == GameMode.ClassicOkey ? "Taş At / Bitir" : "Taş At ve Hamleyi Onayla";
                dialog.ClientSize = new Size(470, 270);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;
                dialog.Font = Font;
                dialog.BackColor = Color.FromArgb(233, 228, 219);
                var explanation = new Label { Left = 18, Top = 16, Width = 434, Height = 64,
                    Text = _engine.State.Mode == GameMode.ClassicOkey
                        ? "Normal hamlede bir taş at. Elinde kalan 14 taş perlerden veya 7 çiftten oluşuyorsa Bitir düğmesini kullan."
                        : "Ctrl+tık ile taşlarını seçip Per Aç veya Çift Aç alanını kullan. İlk açılış en az 101 puan veya 5 çift olmalı. Bir taş atarak turu tamamla." };
                var selection = new ComboBox { Left = 18, Top = 90, Width = 434, DropDownStyle = ComboBoxStyle.DropDownList };
                foreach (var tile in tiles) selection.Items.Add(tile);
                selection.SelectedIndex = Math.Max(0, tiles.FindIndex(x => x.Id == preferredTileId));
                var at = new Button { Text = "Taş At", Left = 112, Top = 218, Width = 100, Height = 32 };
                var finish = new Button { Text = "Bitir", Left = 222, Top = 218, Width = 100, Height = 32, Visible = _engine.State.Mode == GameMode.ClassicOkey };
                var cancel = new Button { Text = "Vazgeç", Left = 332, Top = 218, Width = 120, Height = 32, DialogResult = DialogResult.Cancel };
                var summary = new Label { Left = 18, Top = 132, Width = 434, Height = 68 };
                summary.Text = "Gösterge: " + _engine.State.Indicator + Environment.NewLine +
                    string.Join(" · ", _engine.State.DiscardPiles.Where(x => x.Tiles.Count > 0).Select(x => SeatName(x.Seat) + ": " + x.Tiles.Last()).ToArray());
                at.Click += (_, __) => { if (SubmitTraditionalDiscard(((Tile)selection.SelectedItem).Id, false)) dialog.DialogResult = DialogResult.OK; };
                finish.Click += (_, __) => { if (SubmitTraditionalDiscard(((Tile)selection.SelectedItem).Id, true)) dialog.DialogResult = DialogResult.OK; };
                dialog.Controls.AddRange(new Control[] { explanation, selection, summary, at, finish, cancel });
                dialog.AcceptButton = at;
                dialog.CancelButton = cancel;
                dialog.ShowDialog(this);
            }
            CheckForWinner();
        }

        private bool SubmitTraditionalDiscard(int tileId, bool finish)
        {
            if (!EnsureEditableTurn(true)) return false;
            var melds = BuildMeldsFromBoard();
            var handIds = _elSlotlari.Cast<Tile>().Where(x => x != null).Select(x => x.Id).ToList();
            if (_agIstemcisiModu)
            {
                if (_client != null) _client.SendDiscardRequest(_yerelKoltuk, tileId, finish, melds, handIds);
                else if (_onlineClient != null)
                {
                    var request = new LanDiscardRequest { Seat = _yerelKoltuk.ToString(), TileId = tileId, FinishClassic = finish,
                        Melds = melds.Select(LanMeldDto.FromDomain).ToList(), HandTileIds = handIds };
                    _onlineClient.SendEnvelope("discard", LanJson.Serialize(request), false);
                }
                Log("Hamle oda yöneticisine gönderildi.");
                return true;
            }
            return ApplyTraditionalDiscard(_engine.State.CurrentTurn, tileId, finish, melds, handIds);
        }

        private void HandleRemoteDiscardRequest(Seat seat, int tileId, bool finish, IList<Meld> melds, IList<int> handIds)
        {
            if (!_oyunBasladi || !IsTraditionalGame || _engine.State.IsGameOver || _engine.State.CurrentTurn != seat) return;
            ApplyTraditionalDiscard(seat, tileId, finish, melds, handIds);
            CheckForWinner();
        }

        private bool ApplyTraditionalDiscard(Seat seat, int tileId, bool finish, IList<Meld> melds, IList<int> handIds)
        {
            if (!_engine.State.HasDrawnThisTurn) { ReportTraditionalError(seat, "Önce taş çekmelisin."); return false; }
            if (!_engine.State.TurnInProgress) _engine.BeginTurn(seat);
            string message;
            if (!_engine.ReplaceTurnLayout(seat, melds, handIds, out message)) { ReportTraditionalError(seat, message); return false; }
            var success = finish ? _engine.FinishClassic(seat, tileId, out message) : _engine.DiscardTile(seat, tileId, out message);
            if (!success) { ReportTraditionalError(seat, message); return false; }
            AfterTraditionalAction(message);
            return true;
        }

        private void AfterTraditionalAction(string message)
        {
            LoadBoardFromMelds(_engine.State.Table);
            var player = GetLocalPlayerOrFallback(_engine.State);
            if (player != null) SyncHandSlots(player.Hand);
            Log(message);
            BroadcastTextToNetworks(message);
            BroadcastGameStateToNetworks();
            RefreshUi();
        }

        private void ReportTraditionalError(Seat seat, string message)
        {
            Log(message);
            _host?.SendTextToSeat(seat, message);
            if (_onlineClient != null && _onlineClient.InRoom && _onlineClient.IsMasterClient) _onlineClient.SendEnvelope("text", message, false);
            if (!_agIstemcisiModu && seat == _yerelKoltuk)
                MessageBox.Show(this, message, "Hamle Geçersiz", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CompleteTraditionalTimeout(Seat seat)
        {
            CancelActiveDragState();
            string message;
            if (_engine.CompleteTimeout(seat, out message)) AfterTraditionalAction("Tur süresi doldu. " + message);
            CheckForWinner();
        }

        private void ShowTraditionalRoundScoreDialog()
        {
            var players = _engine.State.Players.Where(x => x.IsActive).ToList();
            var scores = players.ToDictionary(x => x.Name, x => x.RoundPenalty);
            foreach (var player in players)
            {
                if (!_totalScores.ContainsKey(player.Name)) _totalScores[player.Name] = 0;
                _totalScores[player.Name] += player.RoundPenalty;
            }
            _roundHistory.Add(new RoundScoreRecord { RoundNumber = _roundHistory.Count + 1, Scores = scores });
            var totals = players.Select(x => new TotalScoreRecord { PlayerName = x.Name, TotalScore = _totalScores[x.Name] }).ToList();
            var target = _currentSettings != null ? _currentSettings.TargetScore : 1000;
            var overallWinner = totals.Any(x => x.TotalScore >= target) ? totals.OrderBy(x => x.TotalScore).First() : null;
            var text = _engine.State.EndedByStock ? "Deste bitti, tur tamamlandı." : _engine.State.WinnerName + " turu kazandı.";
            text += overallWinner != null ? " Genel kazanan: " + overallWinner.PlayerName : " En düşük ceza puanı önde.";
            var winner = players.FirstOrDefault(x => x.Name == _engine.State.WinnerName);
            if (winner != null) PlayRoundEndSound(winner, overallWinner);
            if (overallWinner != null) StartCelebration(overallWinner.PlayerName + " genel kazanan oldu.");
            RefreshUi();
            using (var dialog = new RoundScoreForm(players.Select(x => x.Name).ToList(), _roundHistory.ToList(), totals, !_agIstemcisiModu && overallWinner == null, text))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && !_agIstemcisiModu && overallWinner == null)
                    StartLocalGame(BuildSettingsFromCurrentPlayers(), false);
            }
        }
    }
}
