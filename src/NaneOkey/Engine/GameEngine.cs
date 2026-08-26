using System;
using System.Collections.Generic;
using System.Linq;
using NaneOkey.Domain;

namespace NaneOkey.Engine
{
    public sealed class GameEngine
    {
        private readonly RuleValidator _validator;
        private readonly BotEngine _botEngine;

        public GameEngine()
        {
            _validator = new RuleValidator();
            _botEngine = new BotEngine(_validator);
            State = new GameState();
        }

        public GameState State { get; private set; }

        public int MaxBotThinkMilliseconds { get; set; } = 30000;

        public string LastBotDebugInfo { get; private set; }

        public void StartNewGame(GameSettings settings)
        {
            State = new GameState();
            var deck = DeckFactory.CreateShuffledDeck();
            foreach (Seat seat in Enum.GetValues(typeof(Seat)))
            {
                var setup = settings.Players[(int)seat];
                State.Players.Add(new PlayerState(seat, setup.Name, setup.Type, setup.Difficulty, setup.IsActive));
            }

            foreach (var player in State.Players)
            {
                if (!player.IsActive)
                {
                    continue;
                }

                for (var count = 0; count < settings.StartingHandSize; count++)
                {
                    player.Hand.Add(DrawForPlayer(deck, player, State.Table));
                }
            }

            State.Deck.AddRange(deck);
            State.CurrentTurn = State.Players.First(x => x.IsActive).Seat;
            State.LastAction = "Yeni oyun basladi.";
        }

        public void BeginTurn(Seat seat)
        {
            EnsureTurnOwner(seat);
            if (State.TurnInProgress)
            {
                return;
            }

            var player = GetPlayer(seat);
            State.TurnTable.Clear();
            State.TurnTable.AddRange(State.Table.Select(x => x.Clone()));
            State.TurnHand.Clear();
            State.TurnHand.AddRange(player.Hand.Select(x => x.Clone()));
            State.OriginalHandIds.Clear();
            State.OriginalHandIds.AddRange(player.Hand.Select(x => x.Id));
            State.OriginalTableIds.Clear();
            State.OriginalTableIds.AddRange(State.Table.SelectMany(x => x.Tiles).Select(x => x.Id));
            State.TurnInProgress = true;
        }

        public bool CreateMeldFromHand(Seat seat, IList<int> tileIds)
        {
            EnsureTurnOwner(seat);
            EnsureTurnBegun();
            if (tileIds == null || tileIds.Count < 3)
            {
                return false;
            }

            var selectedTiles = State.TurnHand.Where(x => tileIds.Contains(x.Id)).ToList();
            if (selectedTiles.Count != tileIds.Count)
            {
                return false;
            }

            foreach (var tile in selectedTiles)
            {
                State.TurnHand.RemoveAll(x => x.Id == tile.Id);
            }

            State.TurnTable.Add(new Meld(selectedTiles));
            return true;
        }

        public bool TryAddTileToMeld(Seat seat, int tileId, int meldIndex)
        {
            EnsureTurnOwner(seat);
            EnsureTurnBegun();
            if (meldIndex < 0 || meldIndex >= State.TurnTable.Count)
            {
                return false;
            }

            var tile = State.TurnHand.FirstOrDefault(x => x.Id == tileId);
            if (tile == null)
            {
                return false;
            }

            State.TurnTable[meldIndex].Tiles.Add(tile.Clone());
            if (_validator.IsValidMeld(State.TurnTable[meldIndex]))
            {
                State.TurnHand.RemoveAll(x => x.Id == tile.Id);
                return true;
            }

            State.TurnTable[meldIndex].Tiles.RemoveAll(x => x.Id == tile.Id);
            return false;
        }

        public bool RemoveTileFromMeld(Seat seat, int meldIndex, int tileId)
        {
            EnsureTurnOwner(seat);
            EnsureTurnBegun();
            if (meldIndex < 0 || meldIndex >= State.TurnTable.Count)
            {
                return false;
            }

            var meld = State.TurnTable[meldIndex];
            var tile = meld.Tiles.FirstOrDefault(x => x.Id == tileId);
            if (tile == null)
            {
                return false;
            }

            meld.Tiles.RemoveAll(x => x.Id == tileId);
            State.TurnHand.Add(tile.Clone());
            if (meld.Tiles.Count == 0)
            {
                State.TurnTable.RemoveAt(meldIndex);
            }

            return true;
        }

        public void UndoTurn(Seat seat)
        {
            EnsureTurnOwner(seat);
            State.TurnTable.Clear();
            State.TurnHand.Clear();
            State.OriginalHandIds.Clear();
            State.OriginalTableIds.Clear();
            State.TurnInProgress = false;
        }

        public bool CommitTurn(Seat seat, out string error)
        {
            EnsureTurnOwner(seat);
            EnsureTurnBegun();

            var normalizedTurnTable = State.TurnTable
                .Select(x => _validator.NormalizeMeld(x))
                .ToList();

            if (!_validator.AreAllMeldsValid(normalizedTurnTable))
            {
                error = "Masadaki tum seriler ve gruplar gecerli olmali.";
                return false;
            }

            if (!HasSameTilesBeforeAndAfter())
            {
                error = "Hamlede tas kaybi veya kopyalanmasi var.";
                return false;
            }

            if (State.TurnHand.Count >= State.OriginalHandIds.Count)
            {
                error = "Hamleyi onaylamak için elinden en az bir taş koymalısın.";
                return false;
            }

            var player = GetPlayer(seat);
            if (!player.HasOpened)
            {
                var openingCount = normalizedTurnTable.Count(IsPureOpeningMeld);
                var minimumOpeningCount = HasAnyPlayerOpenedBefore(player.Seat) ? 1 : 2;
                if (openingCount < minimumOpeningCount)
                {
                    error = minimumOpeningCount == 2
                        ? "Masada ilk acilisi yapan oyuncu elinden en az iki gecerli per acmalidir."
                        : "Acilmak icin elinizden en az bir gecerli per koymalisiniz.";
                    return false;
                }
            }

            player.Hand.Clear();
            player.Hand.AddRange(State.TurnHand.Select(x => x.Clone()));
            player.HasOpened = true;

            State.Table.Clear();
            State.Table.AddRange(normalizedTurnTable.Select(x => x.Clone()));
            State.TurnTable.Clear();
            State.TurnHand.Clear();
            State.TurnInProgress = false;
            State.LastAction = player.Name + " hamlesini onayladi.";

            if (player.Hand.Count == 0)
            {
                State.IsGameOver = true;
                State.WinnerName = player.Name;
                error = string.Empty;
                return true;
            }

            AdvanceTurn();
            error = string.Empty;
            return true;
        }

        public bool ReplaceTurnLayout(Seat seat, IList<Meld> melds, IList<int> handTileIds, out string error)
        {
            EnsureTurnOwner(seat);
            EnsureTurnBegun();

            var tileMap = State.TurnHand
                .Concat(State.TurnTable.SelectMany(x => x.Tiles))
                .GroupBy(x => x.Id)
                .ToDictionary(x => x.Key, x => x.First().Clone());

            var requestedIds = new List<int>();
            foreach (var meld in melds)
            {
                requestedIds.AddRange(meld.Tiles.Select(x => x.Id));
            }
            requestedIds.AddRange(handTileIds);

            var before = State.OriginalHandIds.Concat(State.OriginalTableIds).OrderBy(x => x).ToArray();
            var after = requestedIds.OrderBy(x => x).ToArray();
            if (!before.SequenceEqual(after))
            {
                error = "Hamlede taş kaybı veya fazlalığı var.";
                return false;
            }

            var previousTable = State.TurnTable.Select(x => x.Clone()).ToList();
            State.TurnTable.Clear();
            foreach (var meld in melds)
            {
                var clone = new Meld(meld.Tiles.Select(x => tileMap[x.Id]));
                if (meld.BoardRow >= 0 && meld.StartColumn >= 0)
                {
                    clone.BoardRow = meld.BoardRow;
                    clone.StartColumn = meld.StartColumn;
                }
                else
                {
                    var meldTileIds = new HashSet<int>(meld.Tiles.Select(x => x.Id));
                    var previousMeld = previousTable
                        .Where(x => x.BoardRow >= 0 && x.StartColumn >= 0)
                        .Where(x => x.Tiles.All(tile => meldTileIds.Contains(tile.Id)))
                        .OrderByDescending(x => x.Tiles.Count)
                        .FirstOrDefault();
                    if (previousMeld != null)
                    {
                        clone.BoardRow = previousMeld.BoardRow;
                        clone.StartColumn = previousMeld.StartColumn;
                    }
                }
                State.TurnTable.Add(clone);
            }

            State.TurnHand.Clear();
            foreach (var tileId in handTileIds)
            {
                State.TurnHand.Add(tileMap[tileId].Clone());
            }

            error = string.Empty;
            return true;
        }

        public bool DrawTile(Seat seat, out string message)
        {
            EnsureTurnOwner(seat);
            if (State.Deck.Count == 0)
            {
                message = "Ortada cekilecek tas kalmadi.";
                return false;
            }

            if (State.TurnInProgress)
            {
                UndoTurn(seat);
            }

            var player = GetPlayer(seat);
            player.Hand.Add(DrawForPlayer(State.Deck, player, State.Table));
            State.LastAction = player.Name + " ortadan tas cekti.";
            message = State.LastAction;
            AdvanceTurn();
            return true;
        }

        public bool PassTurn(Seat seat, out string message)
        {
            EnsureTurnOwner(seat);
            if (State.Deck.Count > 0)
            {
                message = "Ortada taş varken pas geçemezsin.";
                return false;
            }

            if (State.TurnInProgress)
            {
                UndoTurn(seat);
            }

            var player = GetPlayer(seat);
            State.LastAction = player.Name + " pas gecti.";
            message = State.LastAction;
            AdvanceTurn();
            return true;
        }

        public bool RunBotTurnIfNeeded(out string message)
        {
            message = string.Empty;
            LastBotDebugInfo = string.Empty;
            if (State.IsGameOver)
            {
                return false;
            }

            var player = GetPlayer(State.CurrentTurn);
            if (player.Type != PlayerType.Bot)
            {
                return false;
            }

            if (_botEngine.TryApplyTurn(this, player.Seat, player.Difficulty))
            {
                message = State.LastAction;
                LastBotDebugInfo = _botEngine.LastDecisionDebugInfo ?? string.Empty;
                return true;
            }

            LastBotDebugInfo = _botEngine.LastDecisionDebugInfo ?? string.Empty;

            if (State.Deck.Count == 0)
            {
                return PassTurn(player.Seat, out message);
            }

            return DrawTile(player.Seat, out message);
        }

        public GameState Snapshot()
        {
            return State.Clone();
        }

        public void Restore(GameState state)
        {
            State = state == null ? new GameState() : state.Clone();
        }

        private bool IsPureOpeningMeld(Meld meld)
        {
            return meld.Tiles.All(x => State.OriginalHandIds.Contains(x.Id) && !State.OriginalTableIds.Contains(x.Id));
        }

        private bool HasAnyPlayerOpenedBefore(Seat currentSeat)
        {
            return State.Players.Any(x => x.IsActive && x.Seat != currentSeat && x.HasOpened) || State.Table.Count > 0;
        }

        private bool HasSameTilesBeforeAndAfter()
        {
            var before = State.OriginalHandIds.Concat(State.OriginalTableIds).OrderBy(x => x).ToArray();
            var after = State.TurnHand.Select(x => x.Id)
                .Concat(State.TurnTable.SelectMany(x => x.Tiles).Select(x => x.Id))
                .OrderBy(x => x)
                .ToArray();

            return before.SequenceEqual(after);
        }

        private Tile DrawTop(List<Tile> deck)
        {
            var tile = deck[0];
            deck.RemoveAt(0);
            return tile;
        }

        private Tile DrawForPlayer(List<Tile> deck, PlayerState player, IList<Meld> table)
        {
            if (deck == null || deck.Count == 0)
            {
                throw new InvalidOperationException("Destede tas yok.");
            }

            if (player == null || player.Type != PlayerType.Bot || player.Difficulty != BotDifficulty.Impossible)
            {
                return DrawTop(deck);
            }

            var bestIndex = 0;
            var bestScore = int.MinValue;
            for (var index = 0; index < deck.Count; index++)
            {
                var score = ScoreDeckTileForImpossibleBot(deck[index], player, table);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = index;
                }
            }

            var tile = deck[bestIndex];
            deck.RemoveAt(bestIndex);
            return tile;
        }

        private int ScoreDeckTileForImpossibleBot(Tile tile, PlayerState player, IList<Meld> table)
        {
            var hand = player.Hand.Select(x => x.Clone()).ToList();
            hand.Add(tile.Clone());

            var score = tile.Number;

            var sameNumberDistinctColors = hand
                .Where(x => x.Number == tile.Number)
                .Select(x => x.Color)
                .Distinct()
                .Count();
            score += sameNumberDistinctColors * sameNumberDistinctColors * 30;

            var sameColorNumbers = hand
                .Where(x => x.Color == tile.Color)
                .Select(x => _validator.NormalizeRunOrder(x.Number))
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            if (sameColorNumbers.Contains(_validator.NormalizeRunOrder(tile.Number) - 1))
            {
                score += 35;
            }

            if (sameColorNumbers.Contains(_validator.NormalizeRunOrder(tile.Number) + 1))
            {
                score += 35;
            }

            if (sameColorNumbers.Contains(_validator.NormalizeRunOrder(tile.Number) - 2))
            {
                score += 12;
            }

            if (sameColorNumbers.Contains(_validator.NormalizeRunOrder(tile.Number) + 2))
            {
                score += 12;
            }

            var handMeldCount = CountHandMelds(hand);
            score += handMeldCount * 70;

            if (table != null && table.Any(meld => CanAddTileToMeld(tile, meld)))
            {
                score += 80;
            }

            return score;
        }

        private int CountHandMelds(IList<Tile> hand)
        {
            var result = 0;

            foreach (var numberGroup in hand.GroupBy(x => x.Number))
            {
                var colorCount = numberGroup.Select(x => x.Color).Distinct().Count();
                if (colorCount >= 3)
                {
                    result++;
                }
            }

            foreach (var colorGroup in hand.GroupBy(x => x.Color))
            {
                var ordered = colorGroup
                    .Select(x => _validator.NormalizeRunOrder(x.Number))
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
                var streak = 1;
                for (var index = 1; index < ordered.Count; index++)
                {
                    if (ordered[index] == ordered[index - 1] + 1)
                    {
                        streak++;
                        if (streak >= 3)
                        {
                            result++;
                        }
                    }
                    else
                    {
                        streak = 1;
                    }
                }
            }

            return result;
        }

        private bool CanAddTileToMeld(Tile tile, Meld meld)
        {
            if (tile == null || meld == null)
            {
                return false;
            }

            var expanded = meld.Clone();
            expanded.Tiles.Add(tile.Clone());
            return _validator.IsValidMeld(expanded);
        }

        private void AdvanceTurn()
        {
            for (var offset = 1; offset <= 4; offset++)
            {
                var nextSeat = (Seat)(((int)State.CurrentTurn + offset) % 4);
                if (State.Players.Any(x => x.Seat == nextSeat && x.IsActive))
                {
                    State.CurrentTurn = nextSeat;
                    return;
                }
            }
        }

        private PlayerState GetPlayer(Seat seat)
        {
            return State.Players.First(x => x.Seat == seat);
        }

        private void EnsureTurnOwner(Seat seat)
        {
            if (State.CurrentTurn != seat)
            {
                throw new InvalidOperationException("Sira bu oyuncuda degil.");
            }
        }

        private void EnsureTurnBegun()
        {
            if (!State.TurnInProgress)
            {
                throw new InvalidOperationException("Tur duzenleme modu baslatilmadi.");
            }
        }
    }
}
