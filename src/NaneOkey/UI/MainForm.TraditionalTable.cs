using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NaneOkey.Domain;
using NaneOkey.Engine;

namespace NaneOkey.UI
{
    public sealed partial class MainForm
    {
        private TraditionalTableView _traditionalTable;
        private GameMode? _tableLayoutMode;
        private bool _tableLayoutAppearance;
        private bool IsNewNaneAppearance { get { return _engine.State.Mode == GameMode.NaneOkey && _engine.State.UseNewAppearance; } }
        private bool UsesNewTableAppearance { get { return IsTraditionalGame || IsNewNaneAppearance; } }
        private readonly HashSet<Control> _compactSideMenuItems = new HashSet<Control>();
        private readonly HashSet<int> _selectedHandIds = new HashSet<int>();

        private void EnsureTraditionalTable()
        {
            if (_traditionalTable != null) return;
            _traditionalTable = new TraditionalTableView();
            _traditionalTable.TargetClicked += TraditionalTargetClicked;
            _traditionalTable.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (!UsesNewTableAppearance || e.Button != MouseButtons.Left) return;
                var target = _traditionalTable.HitTest(e.Location);
                if (target != null && target.Kind == TraditionalTargetKind.Stock)
                    DeckLabelMouseDown(sender, new MouseEventArgs(MouseButtons.Left, 1, GetTileWidth() / 2, GetTileHeight() / 2, 0));
            };
            _masaPaneli.Controls.Add(_traditionalTable);
        }

        private Point GetTraditionalTargetCenter(TraditionalTargetKind kind)
        {
            var bounds = _traditionalTable.GetTargetBounds(new TraditionalTableTarget(kind));
            return _traditionalTable.PointToScreen(new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));
        }

        private Point GetTraditionalSeatCenter(Seat seat)
        {
            var w = _traditionalTable.ClientSize.Width;
            var h = _traditionalTable.ClientSize.Height;
            var relative = ((int)seat - (int)_yerelKoltuk + 4) % 4;
            var point = relative == 0 ? new Point(w / 2, h - 20)
                : relative == 1 ? new Point(28, h / 3)
                : relative == 2 ? new Point(w / 2, 35) : new Point(w - 28, h / 3);
            return _traditionalTable.PointToScreen(point);
        }

        private void EnsureDetachedChatInput()
        {
            if (_logPenceresi == null || _logPenceresi.Controls.ContainsKey("traditionalChat")) return;
            var panel = new Panel { Name = "traditionalChat", Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(6) };
            var send = new Button { Text = "Gönder", Dock = DockStyle.Right, Width = 76 };
            var input = new TextBox { Dock = DockStyle.Fill, Font = new Font("Tahoma", 9F), MaxLength = 500 };
            Action submit = delegate
            {
                _sohbetGirdiKutusu.Text = input.Text;
                SendChatMessage();
                input.Text = _sohbetGirdiKutusu.Text;
            };
            send.Click += delegate { submit(); };
            input.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                submit(); e.SuppressKeyPress = true;
            };
            panel.Controls.Add(input);
            panel.Controls.Add(send);
            _logPenceresi.Controls.Add(panel);
            _logPenceresi.Text = "Günlük / Sohbet";
        }

        private void ConfigureTraditionalSideMenu()
        {
            _compactSideMenuItems.Clear();
            var compact = _oyunPaneli.ClientSize.Height < 480;
            foreach (var button in _solMenusuPaneli.Controls.OfType<Button>())
            {
                if (button.Text == "Tahta Rengi" || button.Text == "Günlük / Sohbet" || button.Text == "Sohbet")
                    button.Text = UsesNewTableAppearance || !_gunlukPaneli.Visible ? compact ? "Sohbet" : "Günlük / Sohbet" : "Tahta Rengi";
                var redundant = button.Text == "Taş At / Bitir" || button.Text == "Ortadan Taş Çek" ||
                    button.Text == "Yandan Taş Al" || button.Text == "Soldan Taş Al";
                if (button.Text == "Hamleyi Oyna" || button.Text == "Onayla")
                    button.Text = compact && IsNewNaneAppearance ? "Onayla" : "Hamleyi Oyna";
                var hidden = compact && redundant || button.Text == "Günlük / Sohbet" || button.Text == "Sohbet" ||
                    !IsTraditionalGame && (button.Text == "Yandan Taş Al" || button.Text == "Soldan Taş Al");
                if (hidden) _compactSideMenuItems.Add(button);
                button.Visible = !hidden &&
                    (IsTraditionalGame || button.Text != "Yandan Taş Al" && button.Text != "Soldan Taş Al");
            }
        }

        private void SetTraditionalTableVisibility(bool traditional)
        {
            if (traditional) EnsureTraditionalTable();
            _matrisPaneli.Visible = !traditional || IsNewNaneAppearance;
            _boardViewport.Visible = !traditional || IsNewNaneAppearance;
            _desteEtiketi.Visible = !traditional;
            foreach (var label in _oyuncuEtiketleri.Values) label.Visible = !traditional;
            if (_traditionalTable != null) _traditionalTable.Visible = traditional;
            var matrixColor = IsNewNaneAppearance ? Color.FromArgb(24, 91, 61) : _matrisArkaRenk;
            var slotColor = IsNewNaneAppearance ? Color.Transparent : _slotArkaRenk;
            var border = IsNewNaneAppearance ? BorderStyle.None : BorderStyle.FixedSingle;
            if (_matrisPaneli.BackColor != matrixColor) _matrisPaneli.BackColor = matrixColor;
            if (_matrisPaneli.BorderStyle != border) _matrisPaneli.BorderStyle = border;
            foreach (var slot in _masaSlotPanelleri)
                if (slot != null && slot.BackColor != slotColor) slot.BackColor = slotColor;
        }

        private void PaintNewNaneBoardBackground(object sender, PaintEventArgs e)
        {
            if (!IsNewNaneAppearance) return;
            var origin = _masaPaneli.PointToClient(((Control)sender).PointToScreen(Point.Empty));
            DrawNewNaneFelt(e.Graphics, new Rectangle(-origin.X, -origin.Y,
                _masaPaneli.ClientSize.Width, _masaPaneli.ClientSize.Height), e.ClipRectangle);
        }

        private static void DrawNewNaneFelt(Graphics graphics, Rectangle bounds, Rectangle clip)
        {
            if (bounds.Width < 1 || bounds.Height < 1) return;
            using (var felt = new System.Drawing.Drawing2D.LinearGradientBrush(bounds,
                Color.FromArgb(32, 107, 72), Color.FromArgb(18, 78, 51), 90F)) graphics.FillRectangle(felt, bounds);
            using (var weave = new Pen(Color.FromArgb(11, 213, 231, 171)))
            {
                var first = bounds.Top + 10 + Math.Max(0, (clip.Top - bounds.Top - 10) / 8) * 8;
                for (var y = first; y < Math.Min(bounds.Bottom, clip.Bottom); y += 8)
                    graphics.DrawLine(weave, bounds.Left + 8, y, bounds.Right - 8, y);
            }
        }

        private void LayoutTraditionalTable()
        {
            SetTraditionalTableVisibility(true);
            _traditionalTable.Bounds = _masaPaneli.ClientRectangle;
            _traditionalTable.BringToFront();
            RefreshTraditionalTable();
        }

        private float CalculateTraditionalUiScale(int width, int height)
        {
            // Eighteen visible positions leave room to arrange a hand without making every piece tiny.
            // Extra positions and Nane's full board use the scroll viewports.
            var low = 0.84D;
            var high = 1.55D;
            var tableBudget = Math.Min(_engine.State.Mode == GameMode.Okey101 ? 260 : 240, Math.Max(120, height * 0.55));
            for (var i = 0; i < 24; i++)
            {
                var scale = (low + high) / 2;
                var tileWidth = Math.Max(1, (int)Math.Round(TileWidth * scale));
                var tileHeight = Math.Max(1, (int)Math.Round(TileHeight * scale));
                var gap = Math.Max(1, (int)Math.Round(HandGapX * scale));
                if (18 * tileWidth + 17 * gap + 36 <= width &&
                    HandRows * tileHeight + gap + 24 + SystemInformation.HorizontalScrollBarHeight + tableBudget <= height) low = scale;
                else high = scale;
            }
            return (float)Math.Max(0.84D, low - 0.00001D);
        }

        private void RefreshTraditionalTable()
        {
            if (!UsesNewTableAppearance || _masaPaneli == null) return;
            EnsureTraditionalTable();
            SetTraditionalTableVisibility(true);
            _traditionalTable.Bounds = _masaPaneli.ClientRectangle;
            var state = _engine.State;
            var melds = state.TurnInProgress ? state.TurnTable : state.Table;
            var originalIds = new HashSet<int>(state.Table.SelectMany(x => x.Tiles).Select(x => x.Id));
            var newlyOpened = melds.Where(x => x.OwnerSeat == _yerelKoltuk && !x.Tiles.Any(t => originalIds.Contains(t.Id))).ToList();
            var rules = new TraditionalRuleValidator(state.Mode);
            _traditionalTable.State = state;
            _traditionalTable.LocalSeat = _yerelKoltuk;
            _traditionalTable.SeatScores = state.Players.ToDictionary(x => x.Seat,
                x => _totalScores.ContainsKey(x.Name) ? _totalScores[x.Name] : 0);
            _traditionalTable.DisplayMelds = melds.Select(x => x.Clone()).ToList();
            _traditionalTable.Pending = state.TurnInProgress && !SameTraditionalTable(state.Table, state.TurnTable);
            _traditionalTable.OpeningValue = newlyOpened.Where(x => !x.IsPair).Sum(rules.MeldValue);
            _traditionalTable.PairCount = newlyOpened.Count(x => x.IsPair);
            _traditionalTable.CanAct = _oyunBasladi && CanHumanAct(false);
            _traditionalTable.NaneCanDraw = IsNewNaneAppearance && _traditionalTable.CanAct;
            if (IsNewNaneAppearance)
            {
                _traditionalTable.NaneStockBounds = _desteEtiketi.Bounds;
                _traditionalTable.SendToBack();
                _matrisPaneli.BringToFront();
                _boardViewport.BringToFront();
                return;
            }
            var player = GetLocalPlayerOrFallback(state);
            var status = state.IsGameOver ? "El sona erdi" : state.CurrentTurn != _yerelKoltuk
                ? "Sıra " + state.Players.First(x => x.Seat == state.CurrentTurn).Name + " oyuncusunda"
                : state.HasDrawnThisTurn ? "Bir taşı yana bırakarak turunu tamamla" : "Ortadan veya yandan bir taş al";
            if (state.Mode == GameMode.Okey101 && player != null && !player.HasOpened)
                status += " · Açılış: 101 puan / 5 çift";
            _traditionalTable.StatusText = status + (_selectedHandIds.Count > 0 ? " · " + _selectedHandIds.Count + " taş seçili" : string.Empty);
            var handIds = new HashSet<int>(_elSlotlari.Cast<Tile>().Where(x => x != null).Select(x => x.Id));
            _selectedHandIds.IntersectWith(handIds);
            UpdateTraditionalSelectionViews();
        }

        private static bool SameTraditionalTable(IList<Meld> first, IList<Meld> second)
        {
            return first.Count == second.Count && first.SelectMany(x => x.Tiles).Select(x => x.Id)
                .SequenceEqual(second.SelectMany(x => x.Tiles).Select(x => x.Id));
        }

        private void ToggleTraditionalSelection(int id)
        {
            if (!_selectedHandIds.Add(id)) _selectedHandIds.Remove(id);
            RefreshTraditionalTable();
        }

        private void UpdateTraditionalSelectionViews()
        {
            for (var row = 0; row < HandRows; row++)
                for (var col = 0; col < HandCols; col++)
                {
                    var view = _elTasGorunumleri[row, col];
                    if (view == null) continue;
                    var selected = IsTraditionalGame && view.Tile != null && _selectedHandIds.Contains(view.Tile.Id);
                    if (view.Selected == selected) continue;
                    view.Selected = selected;
                    view.Invalidate();
                }
        }

        private void TraditionalTargetClicked(TraditionalTableTarget target)
        {
            if (_aktifSurukleme != null) return;
            if (IsNewNaneAppearance)
            {
                if (target.Kind == TraditionalTargetKind.Stock) DrawTile();
                return;
            }
            if (!IsTraditionalGame) return;
            switch (target.Kind)
            {
                case TraditionalTargetKind.Stock:
                    _selectedHandIds.Clear();
                    DrawTile();
                    CheckForWinner();
                    break;
                case TraditionalTargetKind.PreviousDiscard:
                    _selectedHandIds.Clear();
                    DrawDiscardTile();
                    break;
                case TraditionalTargetKind.OpenRuns:
                    SubmitSelectedTraditionalMelds(false);
                    break;
                case TraditionalTargetKind.OpenPairs:
                    SubmitSelectedTraditionalMelds(true);
                    break;
                case TraditionalTargetKind.Meld:
                    if (_selectedHandIds.Count == 1) ProcessTraditionalTile(_selectedHandIds.First(), target.MeldIndex);
                    else Log("İşlemek için Ctrl+tık ile tek taş seç veya taşı perin üzerine sürükle.");
                    break;
                case TraditionalTargetKind.OwnDiscard:
                case TraditionalTargetKind.Finish:
                    if (_selectedHandIds.Count == 1)
                    {
                        if (SubmitTraditionalDiscard(_selectedHandIds.First(), target.Kind == TraditionalTargetKind.Finish))
                            _selectedHandIds.Clear();
                        CheckForWinner();
                    }
                    else Log("Atacağın taşı yana sürükle veya Ctrl+tık ile tek taş seç.");
                    break;
            }
            RefreshTraditionalTable();
        }

        private bool SubmitSelectedTraditionalMelds(bool pairs)
        {
            return StageTraditionalMelds(_selectedHandIds.ToList(), pairs);
        }

        private bool StageTraditionalMelds(IList<int> ids, bool pairs)
        {
            if (_engine.State.Mode != GameMode.Okey101 || !EnsureEditableTurn(true)) return false;
            if (ids.Count < (pairs ? 2 : 3))
            {
                Log("Ctrl+tık ile taşları seç; ardından " + (pairs ? "Çift Aç" : "Per Aç") + " alanına tıkla. Sağ tuşla yan yana taşları da sürükleyebilirsin.");
                return false;
            }
            var state = _engine.State;
            var player = GetLocalPlayerOrFallback(state);
            if (player.OpenedWithPairs && !pairs) { Log("Çift açtıktan sonra seri açamazsın; açık serilere taş işleyebilirsin."); return false; }
            var oldIds = new HashSet<int>(state.Table.SelectMany(x => x.Tiles).Select(x => x.Id));
            var pending = state.TurnTable.Where(x => x.Tiles.Any(t => !oldIds.Contains(t.Id))).ToList();
            var rules = new TraditionalRuleValidator(GameMode.Okey101);
            var canProcessPairsAfterOpening = pairs && pending.Where(x => !x.IsPair).Sum(rules.MeldValue) >= 101 &&
                state.Players.Any(x => x.HasOpened && x.OpenedWithPairs);
            if (!player.HasOpened && pending.Any(x => x.IsPair != pairs) && !canProcessPairsAfterOpening)
            { Log("İlk açılışta perlerle çiftleri karıştıramazsın. Geri Al ile açılışı yeniden düzenleyebilirsin."); return false; }
            if (player.HasOpened && !player.OpenedWithPairs && pairs && !state.Players.Any(x => x.OpenedWithPairs))
            { Log("Çift işlemek için masada çift açmış bir oyuncu bulunmalı."); return false; }
            var tiles = state.TurnHand.Where(x => ids.Contains(x.Id)).Select(x => x.Clone()).ToList();
            if (tiles.Count != ids.Distinct().Count() || state.TurnHand.Count <= tiles.Count)
            { Log("Turu bitirmek için elinde atılacak bir taş bırakmalısın."); return false; }
            Meld normalized;
            List<Meld> melds;
            if (rules.TryNormalize(new Meld(tiles) { IsPair = pairs }, out normalized) && normalized.IsPair == pairs)
                melds = new List<Meld> { normalized };
            else melds = rules.FindBestMelds(tiles, pairs, true).Where(x => x.IsPair == pairs).ToList();
            if (!melds.SelectMany(x => x.Tiles).Select(x => x.Id).OrderBy(x => x).SequenceEqual(tiles.Select(x => x.Id).OrderBy(x => x)))
            { Log("Seçilen taşların tamamı geçerli " + (pairs ? "çiftlerden" : "perlerden") + " oluşmalı."); return false; }
            var beforeTable = state.TurnTable.Select(x => x.Clone()).ToList();
            var beforeHand = state.TurnHand.Select(x => x.Clone()).ToList();
            foreach (var meld in melds)
            {
                if (_engine.CreateMeldFromHand(state.CurrentTurn, meld.Tiles.Select(x => x.Id).ToList())) continue;
                state.TurnTable.Clear(); state.TurnTable.AddRange(beforeTable);
                state.TurnHand.Clear(); state.TurnHand.AddRange(beforeHand);
                Log("Bu taşlar açılamadı."); return false;
            }
            _selectedHandIds.Clear();
            RefreshStagedTraditionalLayout();
            Log("Taşlar masaya yerleştirildi. Taş atınca açılış ve işleme onaylanır; Geri Al ile geri alabilirsin.");
            return true;
        }

        private bool ProcessTraditionalTile(int tileId, int index)
        {
            if (_engine.State.Mode != GameMode.Okey101 || !EnsureEditableTurn(true)) return false;
            var state = _engine.State;
            if (index < 0 || index >= state.TurnTable.Count || state.TurnHand.Count <= 1) return false;
            var player = GetLocalPlayerOrFallback(state);
            var meld = state.TurnTable[index];
            var rules = new TraditionalRuleValidator(GameMode.Okey101);
            var newMelds = state.TurnTable.Where(x => x.OwnerSeat == state.CurrentTurn &&
                !x.Tiles.Any(t => state.OriginalTableIds.Contains(t.Id))).ToList();
            var ownPendingMeld = meld.OwnerSeat == state.CurrentTurn && !meld.Tiles.Any(t => state.OriginalTableIds.Contains(t.Id));
            if (!player.HasOpened && !ownPendingMeld && newMelds.Where(x => !x.IsPair).Sum(rules.MeldValue) < 101 && newMelds.Count(x => x.IsPair) < 5)
            { Log("Taş işlemeden önce 101 puan veya 5 çift ile elini açmalısın."); return false; }
            string message = string.Empty;
            var tile = state.TurnHand.FirstOrDefault(x => x.Id == tileId);
            var joker = tile == null || tile.IsJoker ? null : meld.Tiles.FirstOrDefault(x => x.IsJoker && x.JokerNumber == tile.Number && x.JokerColor == tile.Color);
            var success = joker != null
                ? _engine.ReplaceTableJoker(state.CurrentTurn, tileId, index, joker.Id, out message)
                : _engine.TryAddTileToMeld(state.CurrentTurn, tileId, index);
            if (!success) { Log(string.IsNullOrEmpty(message) ? "Bu taş bu pere işlenemiyor." : message); return false; }
            _selectedHandIds.Clear();
            RefreshStagedTraditionalLayout();
            Log(string.IsNullOrEmpty(message) ? "Taş pere işlendi. Bir taş atarak turunu tamamla." : message);
            return true;
        }

        private void RefreshStagedTraditionalLayout()
        {
            SyncHandSlots(_engine.State.TurnHand);
            RefreshUi();
            PlayEmbeddedMoveSound();
        }

        private bool TryTraditionalDrop(TileDragData data, Point screenLocation)
        {
            if (data == null || _traditionalTable == null || !_traditionalTable.Visible) return false;
            var target = _traditionalTable.HitTest(_traditionalTable.PointToClient(screenLocation));
            if (target == null || data.SourceKind != TileSourceKind.Hand) return false;
            var ids = data.IsGroupDrag ? data.GroupTiles.Select(x => x.Id).ToList() : new List<int> { data.Tile.Id };
            // The drag preview removes its source; restore it before serializing/staging the canonical turn.
            EndCustomDrag(true);
            switch (target.Kind)
            {
                case TraditionalTargetKind.OwnDiscard:
                case TraditionalTargetKind.Finish:
                    if (ids.Count == 1) SubmitTraditionalDiscard(ids[0], target.Kind == TraditionalTargetKind.Finish);
                    else Log("Bir seferde yalnızca bir taş atabilirsin.");
                    CheckForWinner();
                    break;
                case TraditionalTargetKind.OpenRuns:
                case TraditionalTargetKind.OpenPairs:
                    StageTraditionalMelds(ids, target.Kind == TraditionalTargetKind.OpenPairs);
                    break;
                case TraditionalTargetKind.Meld:
                    if (ids.Count == 1) ProcessTraditionalTile(ids[0], target.MeldIndex);
                    else Log("İşlenecek taşı tek olarak perin üzerine bırak.");
                    break;
                default:
                    Log("Taşı kendi atık alanına bırak; 101'de per veya çift açma alanını da kullanabilirsin.");
                    break;
            }
            RefreshUi();
            return true;
        }
    }
}
