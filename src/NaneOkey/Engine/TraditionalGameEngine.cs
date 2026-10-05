using System;
using System.Collections.Generic;
using System.Linq;
using NaneOkey.Domain;

namespace NaneOkey.Engine
{
    public sealed class TraditionalGameEngine
    {
        private readonly GameState _state;
        private readonly TraditionalRuleValidator _rules;

        public TraditionalGameEngine(GameState state)
        {
            _state = state;
            _rules = new TraditionalRuleValidator(state.Mode);
        }

        public static GameState CreateGame(GameSettings settings)
        {
            var state = new GameState { Mode = settings.Mode };
            var deck = DeckFactory.CreateShuffledDeck();
            state.Indicator = deck[0].Clone();
            deck.RemoveAt(0);
            var jokerNumber = state.Indicator.Number == 13 ? 1 : state.Indicator.Number + 1;
            foreach (var tile in deck)
                tile.IsJoker = tile.Color == state.Indicator.Color && tile.Number == jokerNumber;
            deck.Add(new Tile(105, state.Indicator.Color, jokerNumber) { IsFalseJoker = true });
            deck.Add(new Tile(106, state.Indicator.Color, jokerNumber) { IsFalseJoker = true });
            var random = new Random();
            for (var i = deck.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                var tile = deck[i]; deck[i] = deck[j]; deck[j] = tile;
            }
            foreach (Seat seat in Enum.GetValues(typeof(Seat)))
            {
                var setup = settings.Players[(int)seat];
                state.Players.Add(new PlayerState(seat, setup.Name, setup.Type, setup.Difficulty, setup.IsActive));
                state.DiscardPiles.Add(new DiscardPile(seat));
            }
            var active = state.Players.Where(x => x.IsActive).ToList();
            if (active.Count < 2) throw new InvalidOperationException("Oyun için en az iki oyuncu gerekir.");
            state.CurrentTurn = active[0].Seat;
            var handSize = settings.Mode == GameMode.ClassicOkey ? 14 : 21;
            foreach (var player in active)
                for (var count = 0; count < handSize + (player.Seat == state.CurrentTurn ? 1 : 0); count++)
                {
                    player.Hand.Add(deck[0]);
                    deck.RemoveAt(0);
                }
            state.Deck.AddRange(deck);
            state.HasDrawnThisTurn = true;
            state.LastAction = "Yeni " + (settings.Mode == GameMode.ClassicOkey ? "Klasik Okey" : "101 Okey") + " oyunu başladı. İlk oyuncu taş atar.";
            return state;
        }

        private PlayerState Player(Seat seat) { return _state.Players.First(x => x.Seat == seat); }

        private bool CanAct(Seat seat, out string message)
        {
            message = string.Empty;
            if (_state.IsGameOver) { message = "Bu el sona erdi."; return false; }
            if (_state.CurrentTurn != seat || !Player(seat).IsActive) { message = "Sıra bu oyuncuda değil."; return false; }
            return true;
        }

        public void BeginTurn(Seat seat)
        {
            string message;
            if (!CanAct(seat, out message)) throw new InvalidOperationException(message);
            if (_state.TurnInProgress) return;
            _state.TurnTable.Clear();
            _state.TurnTable.AddRange(_state.Table.Select(x => x.Clone()));
            _state.TurnHand.Clear();
            _state.TurnHand.AddRange(Player(seat).Hand.Select(x => x.Clone()));
            _state.OriginalHandIds.Clear();
            _state.OriginalHandIds.AddRange(Player(seat).Hand.Select(x => x.Id));
            _state.OriginalTableIds.Clear();
            _state.OriginalTableIds.AddRange(_state.Table.SelectMany(x => x.Tiles).Select(x => x.Id));
            _state.TurnInProgress = true;
        }

        public void UndoTurn(Seat seat)
        {
            string message;
            if (!CanAct(seat, out message)) return;
            ClearPending();
        }

        private void ClearPending()
        {
            _state.TurnTable.Clear();
            _state.TurnHand.Clear();
            _state.OriginalHandIds.Clear();
            _state.OriginalTableIds.Clear();
            _state.TurnInProgress = false;
        }

        public bool DrawTile(Seat seat, out string message)
        {
            if (!CanAct(seat, out message)) return false;
            if (_state.HasDrawnThisTurn)
            {
                // Returning a 101 discard restores the exact original pile before
                // the player draws from stock instead.
                if (_state.DrawnDiscardTileId > 0 && _state.Mode == GameMode.Okey101)
                    ReturnDiscard(seat, out message);
                else { message = "Bu tur zaten taş aldın; bir taş atmalısın."; return false; }
            }
            if (_state.Deck.Count == 0)
            {
                EndStock(); message = _state.LastAction; return true;
            }
            ClearPending();
            Player(seat).Hand.Add(_state.Deck[0].Clone());
            _state.Deck.RemoveAt(0);
            _state.HasDrawnThisTurn = true;
            _state.DrawnDiscardTileId = 0;
            _state.LastAction = Player(seat).Name + " ortadan taş çekti; taş atması gerekiyor.";
            message = _state.LastAction;
            return true;
        }

        private Seat PreviousSeat()
        {
            for (var offset = 1; offset <= 4; offset++)
            {
                var seat = (Seat)(((int)_state.CurrentTurn + offset) % 4);
                if (_state.Players.Any(x => x.Seat == seat && x.IsActive)) return seat;
            }
            return _state.CurrentTurn;
        }

        public bool DrawDiscard(Seat seat, out string message)
        {
            if (!CanAct(seat, out message)) return false;
            if (_state.HasDrawnThisTurn) { message = "Bu tur zaten taş aldın."; return false; }
            var pile = _state.DiscardPiles.FirstOrDefault(x => x.Seat == PreviousSeat());
            if (pile == null || pile.Tiles.Count == 0) { message = "Önceki oyuncunun alınabilecek taşı yok."; return false; }
            ClearPending();
            var tile = pile.Tiles[pile.Tiles.Count - 1];
            pile.Tiles.RemoveAt(pile.Tiles.Count - 1);
            Player(seat).Hand.Add(tile.Clone());
            _state.DrawnDiscardTileId = tile.Id;
            _state.HasDrawnThisTurn = true;
            _state.LastAction = Player(seat).Name + " önceki oyuncunun attığı taşı aldı.";
            message = _state.LastAction;
            return true;
        }

        public bool ReturnDiscard(Seat seat, out string message)
        {
            if (!CanAct(seat, out message)) return false;
            var tile = Player(seat).Hand.FirstOrDefault(x => x.Id == _state.DrawnDiscardTileId);
            if (tile == null || !_state.HasDrawnThisTurn) { message = "Geri bırakılacak alınmış taş yok."; return false; }
            ClearPending();
            Player(seat).Hand.RemoveAll(x => x.Id == tile.Id);
            _state.DiscardPiles.First(x => x.Seat == PreviousSeat()).Tiles.Add(tile.Clone());
            _state.DrawnDiscardTileId = 0;
            _state.HasDrawnThisTurn = false;
            message = "Alınan taş önceki oyuncunun atık yığınına geri bırakıldı.";
            _state.LastAction = message;
            return true;
        }

        public bool CreateMeldFromHand(Seat seat, IList<int> ids)
        {
            string message;
            if (!CanAct(seat, out message) || !_state.HasDrawnThisTurn || _state.Mode != GameMode.Okey101 || ids == null || ids.Count < 2 || ids.Distinct().Count() != ids.Count)
                return false;
            BeginTurn(seat);
            var tiles = _state.TurnHand.Where(x => ids.Contains(x.Id)).ToList();
            if (tiles.Count != ids.Count) return false;
            var meld = new Meld(tiles) { OwnerSeat = seat, IsPair = tiles.Count == 2 };
            Meld normalized;
            if (!_rules.TryNormalize(meld, out normalized)) return false;
            _state.TurnHand.RemoveAll(x => ids.Contains(x.Id));
            _state.TurnTable.Add(normalized);
            return true;
        }

        public bool ReplaceTableJoker(Seat seat, int handTileId, int meldIndex, int jokerTileId, out string message)
        {
            if (!CanAct(seat, out message)) return false;
            if (_state.Mode != GameMode.Okey101 || !_state.HasDrawnThisTurn)
            { message = "Okey değiştirmeden önce bir taş almalısın."; return false; }
            BeginTurn(seat);
            if (meldIndex < 0 || meldIndex >= _state.TurnTable.Count)
            { message = "Değiştirilecek per bulunamadı."; return false; }
            var meld = _state.TurnTable[meldIndex];
            var joker = meld.Tiles.FirstOrDefault(x => x.Id == jokerTileId && x.IsJoker && x.JokerNumber > 0);
            var replacement = _state.TurnHand.FirstOrDefault(x => x.Id == handTileId && !x.IsJoker);
            if (joker == null || replacement == null || replacement.Number != joker.JokerNumber || replacement.Color != joker.JokerColor)
            { message = "Masadaki okeyin yerine temsil ettiği renkte ve sayıda taşı koymalısın."; return false; }
            bool pairs;
            if (!HasValidOpening(seat, _state.TurnTable, out pairs))
            { message = "Masadan okey almadan önce 101 puan veya 5 çift ile elini açmalısın."; return false; }
            var changed = meld.Clone();
            var position = changed.Tiles.FindIndex(x => x.Id == jokerTileId);
            changed.Tiles[position] = replacement.Clone();
            Meld normalized;
            if (!_rules.TryNormalize(changed, out normalized))
            { message = "Okey değişiminden sonra per geçerli olmalı."; return false; }
            _state.TurnTable[meldIndex] = normalized;
            _state.TurnHand.RemoveAll(x => x.Id == handTileId);
            var returned = joker.Clone();
            returned.JokerNumber = 0;
            returned.JokerColor = default(TileColor);
            _state.TurnHand.Add(returned);
            message = "Okeyin yerine gerçek taş kondu; okey eline alındı.";
            return true;
        }

        public bool TryAddTileToMeld(Seat seat, int tileId, int index)
        {
            string message;
            if (!CanAct(seat, out message) || !_state.HasDrawnThisTurn || _state.Mode != GameMode.Okey101) return false;
            BeginTurn(seat);
            if (index < 0 || index >= _state.TurnTable.Count || _state.TurnTable[index].IsPair) return false;
            var tile = _state.TurnHand.FirstOrDefault(x => x.Id == tileId);
            if (tile == null) return false;
            var expanded = _state.TurnTable[index].Clone();
            expanded.Tiles.Add(tile.Clone());
            if (!_rules.IsValidMeld(expanded)) return false;
            bool pairs;
            if (HasValidOpening(seat, _state.TurnTable, out pairs) && pairs)
            {
                var original = _state.Table.FirstOrDefault(x => ReferenceEquals(FindOriginalMeld(x, _state.TurnTable), _state.TurnTable[index]));
                if (original != null && expanded.Tiles.Count - original.Tiles.Count > 2) return false;
            }
            _state.TurnTable[index] = _rules.NormalizeMeld(expanded);
            _state.TurnHand.RemoveAll(x => x.Id == tileId);
            return true;
        }

        public bool RemoveTileFromMeld(Seat seat, int index, int tileId)
        {
            string message;
            if (!CanAct(seat, out message) || !_state.TurnInProgress || _state.Mode != GameMode.Okey101) return false;
            if (index < 0 || index >= _state.TurnTable.Count) return false;
            var meld = _state.TurnTable[index];
            if (_state.Table.Any(original => original.Tiles.Any(x => x.Id == tileId) && ReferenceEquals(FindOriginalMeld(original, _state.TurnTable), meld))) return false;
            var tile = meld.Tiles.FirstOrDefault(x => x.Id == tileId);
            if (tile == null) return false;
            meld.Tiles.RemoveAll(x => x.Id == tileId);
            var returned = tile.Clone();
            if (returned.IsJoker) { returned.JokerNumber = 0; returned.JokerColor = default(TileColor); }
            _state.TurnHand.Add(returned);
            if (meld.Tiles.Count == 0) _state.TurnTable.RemoveAt(index);
            return true;
        }

        public bool ReplaceTurnLayout(Seat seat, IList<Meld> melds, IList<int> handIds, out string message)
        {
            if (!CanAct(seat, out message)) return false;
            if (melds == null || handIds == null) { message = "Hamle düzeni eksik."; return false; }
            if (melds.Any(x => x == null || x.Tiles == null || x.Tiles.Any(t => t == null)))
            { message = "Hamlede eksik per veya taş var."; return false; }
            BeginTurn(seat);
            var tileMap = Player(seat).Hand.Concat(_state.Table.SelectMany(x => x.Tiles)).ToDictionary(x => x.Id, x => x);
            var requested = melds.SelectMany(x => x.Tiles).Select(x => x.Id).Concat(handIds).OrderBy(x => x).ToList();
            if (!tileMap.Keys.OrderBy(x => x).SequenceEqual(requested)) { message = "Hamlede taş kaybı veya kopyalanması var."; return false; }
            if (_state.Mode == GameMode.ClassicOkey && melds.Count > 0) { message = "Klasik Okey'de perler elde tutulur."; return false; }
            var layout = new List<Meld>();
            var returnedJokers = new HashSet<int>();
            foreach (var original in _state.Table)
            {
                var preserved = FindOriginalMeld(original, melds);
                if (preserved == null) continue;
                foreach (var joker in original.Tiles.Where(x => x.IsJoker))
                {
                    var requestedJoker = preserved.Tiles.FirstOrDefault(x => x.Id == joker.Id);
                    if (requestedJoker == null || ((requestedJoker.JokerNumber != joker.JokerNumber || requestedJoker.JokerColor != joker.JokerColor) &&
                        preserved.Tiles.Any(x => tileMap.ContainsKey(x.Id) && !tileMap[x.Id].IsJoker && _state.OriginalHandIds.Contains(x.Id) &&
                            tileMap[x.Id].Number == joker.JokerNumber && tileMap[x.Id].Color == joker.JokerColor)))
                        returnedJokers.Add(joker.Id);
                }
            }
            foreach (var meld in melds)
            {
                var original = _state.Table.FirstOrDefault(x => ReferenceEquals(FindOriginalMeld(x, melds), meld));
                layout.Add(new Meld(meld.Tiles.Select(x => FreeReturnedJoker(tileMap[x.Id], returnedJokers)))
                {
                    BoardRow = meld.BoardRow, StartColumn = meld.StartColumn,
                    OwnerSeat = original == null ? seat : original.OwnerSeat,
                    IsPair = original == null ? meld.Tiles.Count == 2 : original.IsPair
                });
            }
            if (!PreservesTable(layout, out message)) return false;
            if (returnedJokers.Count > 0)
            {
                bool pairs;
                if (!_state.HasDrawnThisTurn || !HasValidOpening(seat, layout, out pairs))
                { message = "Masadan okey almadan önce elini açmalısın."; return false; }
            }
            if (!_state.HasDrawnThisTurn && !SameTableTiles(layout)) { message = "Önce bir taş almalısın."; return false; }
            _state.TurnTable.Clear(); _state.TurnTable.AddRange(layout);
            _state.TurnHand.Clear(); _state.TurnHand.AddRange(handIds.Select(x => FreeReturnedJoker(tileMap[x], returnedJokers)));
            message = string.Empty;
            return true;
        }

        private static Tile FreeReturnedJoker(Tile tile, ISet<int> returnedJokers)
        {
            var clone = tile.Clone();
            if (returnedJokers.Contains(clone.Id))
            { clone.JokerNumber = 0; clone.JokerColor = default(TileColor); }
            return clone;
        }

        private Meld FindOriginalMeld(Meld original, IList<Meld> layout)
        {
            var fixedIds = original.Tiles.Where(x => !x.IsJoker).Select(x => x.Id).ToList();
            var candidates = layout.Where(x => fixedIds.All(id => x.Tiles.Any(t => t.Id == id))).ToList();
            if (fixedIds.Count == 0)
                candidates = candidates.Where(x => x.Tiles.Count == original.Tiles.Count &&
                    original.Tiles.All(joker => x.Tiles.Any(tile => tile.Id == joker.Id ||
                        (!tile.IsJoker && tile.Color == joker.JokerColor && tile.Number == joker.JokerNumber)))).ToList();
            return candidates.Count == 1 ? candidates[0] : null;
        }

        private List<Meld> NewMelds(IList<Meld> layout)
        {
            var preserved = _state.Table.Select(x => FindOriginalMeld(x, layout)).Where(x => x != null).ToList();
            return layout.Where(x => !preserved.Contains(x)).ToList();
        }

        private bool HasValidOpening(Seat seat, IList<Meld> layout, out bool pairs)
        {
            var player = Player(seat);
            pairs = player.OpenedWithPairs;
            if (player.HasOpened) return true;
            var added = NewMelds(layout);
            if (added.Count == 0 || added.Any(x => !_rules.IsValidMeld(x))) return false;
            pairs = added.All(x => x.IsPair);
            return pairs ? added.Count >= 5 : added.Where(x => !x.IsPair).Sum(_rules.MeldValue) >= 101;
        }

        private bool SameTableTiles(IList<Meld> layout)
        {
            return _state.Table.SelectMany(x => x.Tiles).Select(x => x.Id).OrderBy(x => x)
                .SequenceEqual(layout.SelectMany(x => x.Tiles).Select(x => x.Id).OrderBy(x => x));
        }

        private bool PreservesTable(IList<Meld> layout, out string message)
        {
            var matched = new Dictionary<Meld, Meld>();
            foreach (var original in _state.Table)
            {
                var containing = FindOriginalMeld(original, layout);
                if (containing == null || matched.Values.Contains(containing))
                { message = "101 Okey'de açılmış perler bölünemez, birleştirilemez veya ele geri alınamaz."; return false; }
                matched[original] = containing;
                if (original.IsPair && containing.Tiles.Count != 2)
                { message = "Açılmış çifte taş eklenemez."; return false; }
                var replacementIds = new HashSet<int>();
                foreach (var joker in original.Tiles.Where(x => x.IsJoker))
                {
                    var requested = containing.Tiles.FirstOrDefault(x => x.Id == joker.Id);
                    if (requested == null || requested.JokerNumber != joker.JokerNumber || requested.JokerColor != joker.JokerColor)
                    {
                        var replacement = containing.Tiles.FirstOrDefault(x => !x.IsJoker && !replacementIds.Contains(x.Id) &&
                            _state.OriginalHandIds.Contains(x.Id) && x.Number == joker.JokerNumber && x.Color == joker.JokerColor);
                        if (replacement == null)
                        { message = "Masadaki okey yalnız temsil ettiği gerçek taşla değiştirilebilir."; return false; }
                        replacementIds.Add(replacement.Id);
                        continue;
                    }
                }
            }
            var lockedIds = new HashSet<int>(_state.Table.SelectMany(x => x.Tiles).Where(x => !x.IsJoker).Select(x => x.Id));
            foreach (var match in matched)
                foreach (var joker in match.Key.Tiles.Where(x => x.IsJoker && match.Value.Tiles.Any(t => t.Id == x.Id && t.JokerNumber == x.JokerNumber && t.JokerColor == x.JokerColor)))
                    lockedIds.Add(joker.Id);
            foreach (var match in matched)
                if (match.Value.Tiles.Any(x => lockedIds.Contains(x.Id) && !match.Key.Tiles.Any(t => t.Id == x.Id)))
                { message = "101 Okey'de açılmış perler bölünemez veya birleştirilemez."; return false; }
            message = string.Empty;
            return true;
        }

        public bool CommitTurn(Seat seat, out string message)
        {
            if (!CanAct(seat, out message)) return false;
            message = "Turu bitirmek için elinden bir taş seçip taş atmalısın.";
            return false;
        }

        public bool DiscardTile(Seat seat, int tileId, bool finishClassic, out string message)
        {
            if (!CanAct(seat, out message)) return false;
            if (!_state.HasDrawnThisTurn) { message = "Taş atmadan önce ortadan veya önceki oyuncudan bir taş almalısın."; return false; }
            BeginTurn(seat);
            var discard = _state.TurnHand.FirstOrDefault(x => x.Id == tileId);
            if (discard == null) { message = "Atılacak taş elinde bulunamadı."; return false; }
            var player = Player(seat);
            var afterHand = _state.TurnHand.Where(x => x.Id != tileId).Select(x => x.Clone()).ToList();
            var before = player.Hand.Select(x => x.Id).Concat(_state.Table.SelectMany(x => x.Tiles).Select(x => x.Id)).OrderBy(x => x);
            var after = _state.TurnHand.Select(x => x.Id).Concat(_state.TurnTable.SelectMany(x => x.Tiles).Select(x => x.Id)).OrderBy(x => x);
            if (!before.SequenceEqual(after)) { message = "Hamlede taş kaybı veya kopyalanması var."; return false; }
            var normalized = new List<Meld>();
            var openedNow = false;
            var openedPairs = player.OpenedWithPairs;
            var classicWin = false;
            var classicPairs = false;
            if (_state.Mode == GameMode.ClassicOkey)
            {
                if (_state.TurnTable.Count > 0 || afterHand.Count != 14) { message = "Klasik Okey'de taş attıktan sonra elde 14 taş kalmalı."; return false; }
                if (finishClassic)
                {
                    if (!_rules.TryFindWinningHand(afterHand, out normalized, out classicPairs))
                    { message = "Bitmek için eldeki 14 taşın tamamı geçerli perler veya 7 çift oluşturmalı."; return false; }
                    foreach (var meld in normalized) meld.OwnerSeat = seat;
                    classicWin = true;
                }
            }
            else
            {
                if (!PreservesTable(_state.TurnTable, out message)) return false;
                foreach (var meld in _state.TurnTable)
                {
                    Meld normalizedMeld;
                    if (!_rules.TryNormalize(meld, out normalizedMeld)) { message = "Masadaki tüm perler ve çiftler geçerli olmalı."; return false; }
                    normalized.Add(normalizedMeld);
                }
                var addedMelds = NewMelds(normalized);
                var placedCount = _state.TurnTable.SelectMany(x => x.Tiles).Count(x => _state.OriginalHandIds.Contains(x.Id));
                if (!player.HasOpened && placedCount > 0)
                {
                    if (addedMelds.Count == 0) { message = "Taş işlemeden önce en az 101 puan veya 5 çift ile açılmalısın."; return false; }
                    openedPairs = addedMelds.All(x => x.IsPair);
                    if (openedPairs && addedMelds.Count < 5) { message = "Çift açmak için aynı turda en az 5 çift gerekir."; return false; }
                    if (!openedPairs && addedMelds.Where(x => !x.IsPair).Sum(_rules.MeldValue) < 101)
                    { message = "Per açmak için aynı turda en az 101 puan gerekir; işlenen çiftler açılış puanına eklenmez."; return false; }
                    openedNow = true;
                }
                if (player.HasOpened || openedNow)
                {
                    if (openedPairs && addedMelds.Any(x => !x.IsPair)) { message = "Çift açan oyuncu yeni seri veya grup açamaz."; return false; }
                    if (openedPairs && _state.Table.Any(original =>
                    {
                        var expanded = FindOriginalMeld(original, normalized);
                        return expanded != null && !original.IsPair && expanded.Tiles.Count - original.Tiles.Count > 2;
                    }))
                    { message = "Çift açan oyuncu aynı turda bir pere en fazla 2 taş işleyebilir."; return false; }
                    if (!openedPairs && addedMelds.Any(x => x.IsPair) && !_state.Players.Any(x => x.HasOpened && x.OpenedWithPairs))
                    { message = "Çift açan bir oyuncu olmadan çift koyamazsın."; return false; }
                }
                if (_state.DrawnDiscardTileId > 0 && !_state.TurnTable.SelectMany(x => x.Tiles).Any(x => x.Id == _state.DrawnDiscardTileId))
                { message = "Önceki oyuncudan aldığın taşı bu tur açılan veya işlenen bir perde kullanmalısın. Ortadan çekerek geri bırakabilirsin."; return false; }
                if (afterHand.Count == 0 && !(player.HasOpened || openedNow)) { message = "Açılmadan el bitirilemez."; return false; }
            }
            var fromHandFinish = _state.Mode == GameMode.Okey101 && openedNow && afterHand.Count == 0 &&
                !_state.Players.Any(x => x.HasOpened);
            player.Hand.Clear(); player.Hand.AddRange(afterHand);
            if (openedNow) { player.HasOpened = true; player.OpenedWithPairs = openedPairs; }
            if (_state.Mode == GameMode.Okey101 || classicWin)
            { _state.Table.Clear(); _state.Table.AddRange(normalized.Select(x => x.Clone())); }
            if (classicWin) player.Hand.Clear();
            _state.DiscardPiles.First(x => x.Seat == seat).Tiles.Add(discard.Clone());
            ClearPending();
            _state.LastAction = player.Name + " " + discard + " taşını attı.";
            if (classicWin || (_state.Mode == GameMode.Okey101 && afterHand.Count == 0))
            {
                _state.IsGameOver = true;
                _state.WinnerName = player.Name;
                _state.RoundMultiplier = (discard.IsJoker ? 2 : 1) * ((classicPairs || player.OpenedWithPairs) ? 2 : 1) * (fromHandFinish ? 2 : 1);
                ScoreRound(player);
                _state.LastAction = player.Name + " eli bitirdi.";
            }
            else AdvanceTurn();
            message = _state.LastAction;
            return true;
        }

        private void AdvanceTurn()
        {
            _state.HasDrawnThisTurn = false;
            _state.DrawnDiscardTileId = 0;
            for (var offset = 1; offset <= 4; offset++)
            {
                // Seat values run South, West, North, East; traditional play
                // passes to the player on the right (South -> East -> North -> West).
                var next = (Seat)(((int)_state.CurrentTurn - offset + 4) % 4);
                if (_state.Players.Any(x => x.Seat == next && x.IsActive)) { _state.CurrentTurn = next; return; }
            }
        }

        private void ScoreRound(PlayerState winner)
        {
            foreach (var player in _state.Players.Where(x => x.IsActive))
            {
                if (_state.Mode == GameMode.ClassicOkey)
                    player.RoundPenalty = player.Seat == winner.Seat ? 0 : 2 * _state.RoundMultiplier;
                else
                {
                    var penalty = player.Seat == winner.Seat ? -101 : HandPenalty(player);
                    player.RoundPenalty = penalty * _state.RoundMultiplier;
                }
            }
        }

        private static int HandPenalty(PlayerState player)
        {
            var penalty = !player.HasOpened ? 202 : player.Hand.Sum(x => x.IsJoker ? 0 : x.Number);
            penalty += player.Hand.Count(x => x.IsJoker) * 101;
            return player.OpenedWithPairs ? penalty * 2 : penalty;
        }

        private void EndStock()
        {
            ClearPending();
            _state.IsGameOver = true;
            _state.EndedByStock = true;
            _state.WinnerName = null;
            foreach (var player in _state.Players.Where(x => x.IsActive))
                player.RoundPenalty = _state.Mode == GameMode.Okey101 ? HandPenalty(player) : 0;
            _state.LastAction = "Ortadaki taşlar bitti; el sona erdi.";
        }

        public bool PassTurn(Seat seat, out string message)
        {
            if (!CanAct(seat, out message)) return false;
            if (_state.HasDrawnThisTurn) { message = "Bu tur bir taş atmalısın."; return false; }
            if (_state.Deck.Count > 0) { message = "Ortada taş varken pas geçemezsin."; return false; }
            EndStock(); message = _state.LastAction; return true;
        }
    }
}
