using System;
using System.Collections.Generic;
using System.Linq;
using NaneOkey.Domain;

namespace NaneOkey.Engine
{
    public sealed class TraditionalBotEngine
    {
        public bool TryApplyTurn(GameEngine engine, Seat seat, out string message)
        {
            var state = engine.State;
            var player = state.Players.First(x => x.Seat == seat);
            var rules = new TraditionalRuleValidator(state.Mode);
            if (!state.HasDrawnThisTurn)
            {
                var previous = state.Players.Where(x => x.IsActive && x.Seat != seat)
                    .OrderBy(x => ((int)x.Seat - (int)seat + 4) % 4).FirstOrDefault();
                var pile = previous == null ? null : state.DiscardPiles.FirstOrDefault(x => x.Seat == previous.Seat);
                var tile = pile == null ? null : pile.Tiles.LastOrDefault();
                var takeDiscard = player.Difficulty != BotDifficulty.Easy && tile != null && CanUseDiscard(state, player, tile, rules);
                if (!(takeDiscard ? engine.DrawDiscard(seat, out message) : engine.DrawTile(seat, out message))) return false;
                if (state.IsGameOver) return true;
            }
            for (var attempt = 0; attempt < 2; attempt++)
            {
                engine.BeginTurn(seat);
                if (state.Mode == GameMode.ClassicOkey)
                {
                    foreach (var discard in state.TurnHand.OrderByDescending(x => x.IsJoker).ToList())
                    {
                        List<Meld> winning; bool pairs;
                        if (rules.TryFindWinningHand(state.TurnHand.Where(x => x.Id != discard.Id).ToList(), out winning, out pairs))
                            return engine.FinishClassic(seat, discard.Id, out message);
                    }
                }
                else Play101(engine, seat, rules);
                var discards = player.Difficulty == BotDifficulty.Easy
                    ? state.TurnHand.OrderBy(x => x.IsJoker).ThenByDescending(x => x.Number).ToList()
                    : state.TurnHand.OrderBy(x => TileUsefulness(x, state.TurnHand)).ThenByDescending(x => x.Number).ToList();
                foreach (var discard in discards)
                    if (engine.DiscardTile(seat, discard.Id, out message)) return true;
                engine.UndoTurn(seat);
                if (state.DrawnDiscardTileId > 0)
                {
                    if (!engine.DrawTile(seat, out message)) return false;
                    if (state.IsGameOver) return true;
                }
                else
                {
                    engine.BeginTurn(seat);
                    foreach (var discard in state.TurnHand.OrderBy(x => TileUsefulness(x, state.TurnHand)).ToList())
                        if (engine.DiscardTile(seat, discard.Id, out message)) return true;
                    break;
                }
            }
            message = "Bot geçerli bir hamle yapamadı.";
            return false;
        }

        private static bool CanUseDiscard(GameState state, PlayerState player, Tile tile, TraditionalRuleValidator rules)
        {
            var hand = player.Hand.Select(x => x.Clone()).ToList(); hand.Add(tile.Clone());
            if (state.Mode == GameMode.ClassicOkey)
                return TileUsefulness(tile, hand) > 45;
            if (player.HasOpened && !tile.IsJoker && state.Table.SelectMany(x => x.Tiles).Any(joker =>
                joker.IsJoker && joker.JokerColor == tile.Color && joker.JokerNumber == tile.Number)) return true;
            if (player.HasOpened && state.Table.Any(meld => !meld.IsPair && rules.IsValidMeld(new Meld(meld.Tiles.Concat(new[] { tile }))))) return true;
            if (player.HasOpened && !player.OpenedWithPairs && state.Players.Any(x => x.HasOpened && x.OpenedWithPairs) &&
                rules.FindBestMelds(hand, true, false).Any(pair => pair.Tiles.Any(x => x.Id == tile.Id))) return true;
            var pairs = player.HasOpened && player.OpenedWithPairs;
            var best = rules.FindBestMelds(hand, pairs, !player.HasOpened);
            if (!best.Any(x => x.Tiles.Any(t => t.Id == tile.Id))) return false;
            if (player.HasOpened) return true;
            if (best.Sum(rules.MeldValue) >= 101) return true;
            best = rules.FindBestMelds(hand, true, false);
            return best.Count >= 5 && best.Any(x => x.Tiles.Any(t => t.Id == tile.Id));
        }

        private static void Play101(GameEngine engine, Seat seat, TraditionalRuleValidator rules)
        {
            var state = engine.State;
            var player = state.Players.First(x => x.Seat == seat);
            if (player.HasOpened) RecoverTableJokers(engine, seat);
            var melds = rules.FindBestMelds(state.TurnHand, player.OpenedWithPairs, !player.HasOpened);
            if (!player.HasOpened && melds.Sum(rules.MeldValue) < 101)
            {
                melds = rules.FindBestMelds(state.TurnHand, true, false);
                if (melds.Count < 5) return;
            }
            if (melds.SelectMany(x => x.Tiles).Count() == state.TurnHand.Count && melds.Count > 0)
                melds.Remove(melds.OrderBy(rules.MeldValue).First());
            if (!player.HasOpened && (melds.Count == 0 || (melds.All(x => x.IsPair) ? melds.Count < 5 : melds.Sum(rules.MeldValue) < 101))) return;
            foreach (var meld in melds) engine.CreateMeldFromHand(seat, meld.Tiles.Select(x => x.Id).ToList());
            if (!player.HasOpened && melds.Count == 0) return;
            if (!player.HasOpened) RecoverTableJokers(engine, seat);
            var openingPairs = player.HasOpened ? player.OpenedWithPairs : melds.All(x => x.IsPair);
            if (!openingPairs && state.Players.Any(x => x.HasOpened && x.OpenedWithPairs))
                foreach (var pair in rules.FindBestMelds(state.TurnHand, true, false))
                {
                    if (state.TurnHand.Count <= 2) break;
                    engine.CreateMeldFromHand(seat, pair.Tiles.Select(x => x.Id).ToList());
                }
            foreach (var tile in state.TurnHand.ToList())
            {
                if (state.TurnHand.Count <= 1) break;
                for (var index = 0; index < state.TurnTable.Count; index++)
                    if (engine.TryAddTileToMeld(seat, tile.Id, index)) break;
            }
        }

        private static void RecoverTableJokers(GameEngine engine, Seat seat)
        {
            var state = engine.State;
            for (var index = 0; index < state.TurnTable.Count; index++)
                foreach (var joker in state.TurnTable[index].Tiles.Where(x => x.IsJoker).ToList())
                {
                    var replacement = state.TurnHand.FirstOrDefault(x => !x.IsJoker && x.Color == joker.JokerColor && x.Number == joker.JokerNumber);
                    if (replacement == null) continue;
                    string message;
                    engine.ReplaceTableJoker(seat, replacement.Id, index, joker.Id, out message);
                }
        }

        private static int TileUsefulness(Tile tile, IList<Tile> hand)
        {
            if (tile.IsJoker) return 10000;
            return hand.Where(x => x.Id != tile.Id).Sum(other =>
                other.IsJoker ? 5 :
                (other.Color == tile.Color && other.Number == tile.Number ? 14 :
                 other.Number == tile.Number && other.Color != tile.Color ? 24 :
                 other.Color == tile.Color && Math.Abs(other.Number - tile.Number) == 1 ? 30 :
                 other.Color == tile.Color && Math.Abs(other.Number - tile.Number) == 2 ? 9 : 0));
        }
    }
}
