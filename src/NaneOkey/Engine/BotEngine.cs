using System;
using System.Collections.Generic;
using System.Linq;
using NaneOkey.Domain;

namespace NaneOkey.Engine
{
    public sealed class BotEngine
    {
        private readonly RuleValidator _validator;

        public string LastDecisionDebugInfo { get; private set; }

        public BotEngine(RuleValidator validator)
        {
            _validator = validator;
        }

        public List<Meld> FindOpeningMelds(List<Tile> hand)
        {
            var candidates = GenerateCandidateMelds(hand);
            return FindDisjointOpening(candidates, 0, new List<Meld>(), new HashSet<int>());
        }

        public bool TryApplyTurn(GameEngine engine, Seat seat, BotDifficulty difficulty)
        {
            LastDecisionDebugInfo = string.Empty;
            var player = engine.State.Players.First(x => x.Seat == seat);
            engine.BeginTurn(seat);

            if (difficulty == BotDifficulty.Cheater)
            {
                if (TryApplyProgressiveBestTurn(engine, seat, difficulty, true))
                {
                    return true;
                }

                engine.UndoTurn(seat);
                engine.BeginTurn(seat);
            }
            else if (difficulty == BotDifficulty.UltraHard)
            {
                if (TryApplyProgressiveBestTurn(engine, seat, difficulty, false))
                {
                    return true;
                }

                engine.UndoTurn(seat);
                engine.BeginTurn(seat);
            }
            else if (difficulty == BotDifficulty.SmartHard)
            {
                if (TryApplyProgressiveBestTurn(engine, seat, difficulty, false))
                {
                    return true;
                }

                engine.UndoTurn(seat);
                engine.BeginTurn(seat);
            }
            else if (difficulty == BotDifficulty.VeryHard || difficulty == BotDifficulty.Impossible)
            {
                if (TryApplyBestTurn(engine, seat, BotDifficulty.VeryHard, false))
                {
                    return true;
                }

                engine.UndoTurn(seat);
                engine.BeginTurn(seat);
            }

            if (!player.HasOpened)
            {
                var opening = FindOpeningMelds(engine.State.TurnHand);
                if (opening != null && opening.Count >= 2)
                {
                    foreach (var meld in opening)
                    {
                        engine.CreateMeldFromHand(seat, meld.Tiles.Select(x => x.Id).ToList());
                    }

                    return engine.CommitTurn(seat, out _);
                }
            }

            foreach (var tile in engine.State.TurnHand.ToList())
            {
                for (var meldIndex = 0; meldIndex < engine.State.TurnTable.Count; meldIndex++)
                {
                    if (engine.TryAddTileToMeld(seat, tile.Id, meldIndex))
                    {
                        return engine.CommitTurn(seat, out _);
                    }
                }
            }

            if (difficulty != BotDifficulty.Easy && player.HasOpened)
            {
                var rearranged = TryRearrangeTable(engine, seat);
                if (rearranged)
                {
                    return true;
                }
            }

            var extraMeld = GenerateCandidateMelds(engine.State.TurnHand).FirstOrDefault();
            if (extraMeld != null)
            {
                engine.CreateMeldFromHand(seat, extraMeld.Tiles.Select(x => x.Id).ToList());
                if (engine.CommitTurn(seat, out _))
                {
                    return true;
                }

                engine.UndoTurn(seat);
                engine.BeginTurn(seat);
            }

            if ((difficulty == BotDifficulty.Hard || difficulty == BotDifficulty.VeryHard || difficulty == BotDifficulty.SmartHard || difficulty == BotDifficulty.UltraHard || difficulty == BotDifficulty.Cheater || difficulty == BotDifficulty.Impossible) && player.HasOpened)
            {
                if (TryRearrangeTable(engine, seat, true))
                {
                    return true;
                }
            }

            if ((engine.State.Deck.Count == 0 || engine.State.TurnHand.Count <= 5) &&
                (difficulty == BotDifficulty.VeryHard || difficulty == BotDifficulty.SmartHard || difficulty == BotDifficulty.UltraHard || difficulty == BotDifficulty.Cheater || difficulty == BotDifficulty.Impossible))
            {
                if (TryApplyBestTurn(engine, seat, difficulty == BotDifficulty.VeryHard ? BotDifficulty.SmartHard : difficulty, false))
                {
                    return true;
                }

                engine.UndoTurn(seat);
                engine.BeginTurn(seat);
            }

            engine.UndoTurn(seat);
            if (difficulty == BotDifficulty.Cheater || difficulty == BotDifficulty.UltraHard || difficulty == BotDifficulty.SmartHard)
            {
                LastDecisionDebugInfo = "Karar özeti: anlamlı hamle bulunamadı, çekme/pas yoluna düşüldü.";
            }
            return false;
        }

        private bool TryApplyProgressiveBestTurn(GameEngine engine, Seat seat, BotDifficulty difficulty, bool useOpponentPressure)
        {
            var player = engine.State.Players.First(x => x.Seat == seat);
            var tableTiles = engine.State.TurnTable.SelectMany(x => x.Tiles).ToList();
            var handTiles = engine.State.TurnHand.ToList();
            var allTiles = tableTiles.Concat(handTiles).ToList();
            if (allTiles.Count > GetMaxRearrangeTileCount(difficulty))
            {
                return false;
            }

            var handIds = new HashSet<int>(handTiles.Select(x => x.Id));
            var handTileMap = handTiles.ToDictionary(x => x.Id, x => x.Clone());
            var tableIds = new HashSet<int>(tableTiles.Select(x => x.Id));
            var minimumOpeningCount = player.HasOpened ? 0 : DetermineMinimumOpeningCount(engine.State, seat);
            var deadlineTick = Environment.TickCount + System.Math.Max(100, ResolveSearchMilliseconds(engine, difficulty));
            var allCandidates = BuildCandidateEntries(allTiles, handIds, tableIds, difficulty);
            if (allCandidates.Count == 0)
            {
                return false;
            }

            var requiredOrder = tableIds.OrderBy(x => x).ToList();
            var shortlist = useOpponentPressure ? new List<SearchResult>() : null;
            SearchResult best = null;

            foreach (var candidateLimit in GetProgressiveCandidateLimits(difficulty, allCandidates.Count))
            {
                if (Environment.TickCount > deadlineTick)
                {
                    break;
                }

                var stageCandidates = candidateLimit >= allCandidates.Count
                    ? allCandidates
                    : allCandidates.Take(candidateLimit).ToList();
                best = FindBestLayout(
                    stageCandidates,
                    requiredOrder,
                    0,
                    new HashSet<int>(),
                    new List<Meld>(),
                    handIds,
                    handTileMap,
                    minimumOpeningCount,
                    engine.State,
                    seat,
                    useOpponentPressure,
                    shortlist,
                    4,
                    best,
                    deadlineTick,
                    true,
                    false);

                if (best != null && best.HandUsedCount >= handIds.Count)
                {
                    break;
                }
            }

            if (best == null || best.HandUsedCount == 0)
            {
                return false;
            }

            if (useOpponentPressure && shortlist != null && shortlist.Count > 0)
            {
                best = SelectBestLookaheadResult(engine, seat, shortlist, best);
                LastDecisionDebugInfo = BuildDebugSummary(engine, seat, shortlist, best, difficulty);
            }
            else if (difficulty == BotDifficulty.SmartHard)
            {
                LastDecisionDebugInfo = "Karar özeti: kademeli derin arama ile " + best.HandUsedCount + " taşlık hamle seçildi.";
            }

            var remainingHandIds = handIds.Where(x => !best.UsedIds.Contains(x)).OrderBy(x => x).ToList();
            string error;
            if (!engine.ReplaceTurnLayout(seat, best.SelectedMelds, remainingHandIds, out error))
            {
                return false;
            }

            return engine.CommitTurn(seat, out _);
        }

        private bool TryApplyBestTurn(GameEngine engine, Seat seat, BotDifficulty difficulty, bool useOpponentPressure)
        {
            var player = engine.State.Players.First(x => x.Seat == seat);
            var tableTiles = engine.State.TurnTable.SelectMany(x => x.Tiles).ToList();
            var handTiles = engine.State.TurnHand.ToList();
            var allTiles = tableTiles.Concat(handTiles).ToList();
            if (allTiles.Count > GetMaxRearrangeTileCount(difficulty))
            {
                return false;
            }

            var handIds = new HashSet<int>(handTiles.Select(x => x.Id));
            var handTileMap = handTiles.ToDictionary(x => x.Id, x => x.Clone());
            var tableIds = new HashSet<int>(tableTiles.Select(x => x.Id));
            var minimumOpeningCount = player.HasOpened ? 0 : DetermineMinimumOpeningCount(engine.State, seat);
            var deadlineTick = Environment.TickCount + System.Math.Max(100, ResolveSearchMilliseconds(engine, difficulty));

            var candidates = BuildCandidateEntries(allTiles, handIds, tableIds, difficulty);
            if (candidates.Count == 0)
            {
                return false;
            }

            var requiredOrder = tableIds.OrderBy(x => x).ToList();
            var shortlist = useOpponentPressure ? new List<SearchResult>() : null;
            var best = FindBestLayout(
                candidates,
                requiredOrder,
                0,
                new HashSet<int>(),
                new List<Meld>(),
                handIds,
                handTileMap,
                minimumOpeningCount,
                engine.State,
                seat,
                useOpponentPressure,
                shortlist,
                4,
                null,
                deadlineTick,
                true,
                false);
            if (best == null || best.HandUsedCount == 0)
            {
                return false;
            }

            if (useOpponentPressure && shortlist != null && shortlist.Count > 0)
            {
                best = SelectBestLookaheadResult(engine, seat, shortlist, best);
                LastDecisionDebugInfo = BuildDebugSummary(engine, seat, shortlist, best, difficulty);
            }

            var remainingHandIds = handIds.Where(x => !best.UsedIds.Contains(x)).OrderBy(x => x).ToList();
            string error;
            if (!engine.ReplaceTurnLayout(seat, best.SelectedMelds, remainingHandIds, out error))
            {
                return false;
            }

            return engine.CommitTurn(seat, out _);
        }

        private bool TryRearrangeTable(GameEngine engine, Seat seat)
        {
            return TryRearrangeTable(engine, seat, false);
        }

        private bool TryRearrangeTable(GameEngine engine, Seat seat, bool requireBetterUsage)
        {
            return TryRearrangeTable(engine, seat, requireBetterUsage, ResolveSearchMilliseconds(engine, engine.State.Players.First(x => x.Seat == seat).Difficulty));
        }

        private int ResolveSearchMilliseconds(GameEngine engine, BotDifficulty difficulty)
        {
            var configuredMaximum = engine != null ? System.Math.Max(100, engine.MaxBotThinkMilliseconds) : int.MaxValue;
            return System.Math.Min(GetSearchMilliseconds(difficulty), configuredMaximum);
        }

        private bool TryRearrangeTable(GameEngine engine, Seat seat, bool requireBetterUsage, int maxSearchMilliseconds)
        {
            var player = engine.State.Players.First(x => x.Seat == seat);
            var tableTiles = engine.State.TurnTable.SelectMany(x => x.Tiles).ToList();
            var handTiles = engine.State.TurnHand.ToList();
            var allTiles = tableTiles.Concat(handTiles).ToList();
            if (allTiles.Count > GetMaxRearrangeTileCount(player.Difficulty))
            {
                return false;
            }

            var tableIds = new HashSet<int>(tableTiles.Select(x => x.Id));
            var handIds = new HashSet<int>(handTiles.Select(x => x.Id));
            var handTileMap = handTiles.ToDictionary(x => x.Id, x => x.Clone());
            var candidates = BuildCandidateEntries(allTiles, handIds, tableIds, player.Difficulty);
            if (candidates.Count > GetMaxCandidateCount(player.Difficulty))
            {
                return false;
            }

            var requiredOrder = tableIds.ToList();
            var deadlineTick = Environment.TickCount + System.Math.Max(100, maxSearchMilliseconds);
            var best = FindBestLayout(
                candidates,
                requiredOrder,
                0,
                new HashSet<int>(),
                new List<Meld>(),
                handIds,
                handTileMap,
                0,
                engine.State,
                seat,
                false,
                null,
                0,
                null,
                deadlineTick,
                player.Difficulty == BotDifficulty.VeryHard || player.Difficulty == BotDifficulty.SmartHard || player.Difficulty == BotDifficulty.UltraHard || player.Difficulty == BotDifficulty.Cheater,
                requireBetterUsage);
            if (best == null || best.HandUsedCount == 0)
            {
                return false;
            }

            if (requireBetterUsage && best.HandUsedCount < 2)
            {
                return false;
            }

            var remainingHandIds = handIds.Where(x => !best.UsedIds.Contains(x)).ToList();
            string error;
            if (!engine.ReplaceTurnLayout(seat, best.SelectedMelds, remainingHandIds, out error))
            {
                return false;
            }

            return engine.CommitTurn(seat, out _);
        }

        private int GetSearchMilliseconds(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return 500;
                case BotDifficulty.Medium:
                    return 5000;
                case BotDifficulty.Hard:
                    return 30000;
                case BotDifficulty.SmartHard:
                    return 45000;
                case BotDifficulty.UltraHard:
                    return 60000;
                case BotDifficulty.Impossible:
                    return 30000;
                case BotDifficulty.Cheater:
                    return 120000;
                default:
                    return 180000;
            }
        }

        private int GetMaxRearrangeTileCount(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return 18;
                case BotDifficulty.Medium:
                    return 28;
                case BotDifficulty.Hard:
                    return 44;
                case BotDifficulty.SmartHard:
                    return 72;
                case BotDifficulty.UltraHard:
                    return 88;
                case BotDifficulty.Impossible:
                    return 64;
                case BotDifficulty.Cheater:
                    return 104;
                default:
                    return 64;
            }
        }

        private int GetMaxCandidateCount(BotDifficulty difficulty)
        {
            switch (difficulty)
            {
                case BotDifficulty.Easy:
                    return 45;
                case BotDifficulty.Medium:
                    return 90;
                case BotDifficulty.Hard:
                    return 320;
                case BotDifficulty.SmartHard:
                    return 1400;
                case BotDifficulty.UltraHard:
                    return 2400;
                case BotDifficulty.Impossible:
                    return 1200;
                case BotDifficulty.Cheater:
                    return 4000;
                default:
                    return 1000;
            }
        }

        private SearchResult FindBestLayout(
            List<CandidateEntry> candidates,
            List<int> requiredOrder,
            int startIndex,
            HashSet<int> usedIds,
            List<Meld> selected,
            HashSet<int> handIds,
            Dictionary<int, Tile> handTileMap,
            int minimumPureHandMeldCount,
            GameState state,
            Seat actingSeat,
            bool useOpponentPressure,
            List<SearchResult> shortlistedResults,
            int shortlistLimit,
            SearchResult best,
            int deadlineTick,
            bool allowHandOnlyExpansion,
            bool requireAtLeastTwoHandTiles)
        {
            if (Environment.TickCount > deadlineTick)
            {
                return best;
            }

            while (startIndex < requiredOrder.Count && usedIds.Contains(requiredOrder[startIndex]))
            {
                startIndex++;
            }

            if (startIndex >= requiredOrder.Count)
            {
                best = ConsiderBestResult(selected, usedIds, handIds, handTileMap, minimumPureHandMeldCount, state, actingSeat, useOpponentPressure, requireAtLeastTwoHandTiles, shortlistedResults, shortlistLimit, best);

                if (!allowHandOnlyExpansion)
                {
                    return best;
                }

                for (var i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (!candidate.IsPureHand || candidate.TileIds.Any(usedIds.Contains))
                    {
                        continue;
                    }

                    foreach (var tileId in candidate.TileIds)
                    {
                        usedIds.Add(tileId);
                    }

                    selected.Add(candidate.Meld);
                    best = FindBestLayout(candidates, requiredOrder, startIndex, usedIds, selected, handIds, handTileMap, minimumPureHandMeldCount, state, actingSeat, useOpponentPressure, shortlistedResults, shortlistLimit, best, deadlineTick, true, requireAtLeastTwoHandTiles);
                    selected.RemoveAt(selected.Count - 1);

                    foreach (var tileId in candidate.TileIds)
                    {
                        usedIds.Remove(tileId);
                    }
                }

                return best;
            }

            var targetId = requiredOrder[startIndex];
            foreach (var candidate in candidates.Where(x => x.TileIds.Contains(targetId)))
            {
                if (candidate.TileIds.Any(usedIds.Contains))
                {
                    continue;
                }

                foreach (var tileId in candidate.TileIds)
                {
                    usedIds.Add(tileId);
                }

                selected.Add(candidate.Meld);
                best = FindBestLayout(candidates, requiredOrder, startIndex + 1, usedIds, selected, handIds, handTileMap, minimumPureHandMeldCount, state, actingSeat, useOpponentPressure, shortlistedResults, shortlistLimit, best, deadlineTick, allowHandOnlyExpansion, requireAtLeastTwoHandTiles);
                selected.RemoveAt(selected.Count - 1);

                foreach (var tileId in candidate.TileIds)
                {
                    usedIds.Remove(tileId);
                }
            }

            return best;
        }

        private SearchResult ConsiderBestResult(
            List<Meld> selected,
            HashSet<int> usedIds,
            HashSet<int> handIds,
            Dictionary<int, Tile> handTileMap,
            int minimumPureHandMeldCount,
            GameState state,
            Seat actingSeat,
            bool useOpponentPressure,
            bool requireAtLeastTwoHandTiles,
            List<SearchResult> shortlistedResults,
            int shortlistLimit,
            SearchResult best)
        {
            var handUsed = usedIds.Count(x => handIds.Contains(x));
            if (handUsed == 0 || (requireAtLeastTwoHandTiles && handUsed < 2))
            {
                return best;
            }

            var pureHandMeldCount = selected.Count(x => x.Tiles.All(t => handIds.Contains(t.Id)));
            if (pureHandMeldCount < minimumPureHandMeldCount)
            {
                return best;
            }

            var remainingHandTiles = handTileMap
                .Where(x => !usedIds.Contains(x.Key))
                .Select(x => x.Value.Clone())
                .ToList();
            var handValueUsed = usedIds.Where(handIds.Contains).Sum(x => handTileMap[x].Number);
            var remainingHandValue = remainingHandTiles.Sum(x => x.Number);
            var residualPotential = EvaluateResidualHandPotential(remainingHandTiles);
            var opponentThreatScore = useOpponentPressure
                ? EvaluateOpponentThreatScore(state, actingSeat, selected)
                : int.MaxValue;
            var current = new SearchResult
            {
                HandUsedCount = handUsed,
                PureHandMeldCount = pureHandMeldCount,
                HandValueUsed = handValueUsed,
                RemainingHandValue = remainingHandValue,
                ResidualPotential = residualPotential,
                OpponentThreatScore = opponentThreatScore,
                UsedIds = new HashSet<int>(usedIds),
                SelectedMelds = selected.Select(x => x.Clone()).ToList()
            };

            if (shortlistedResults != null && shortlistLimit > 0)
            {
                InsertShortlistedResult(shortlistedResults, current, shortlistLimit);
            }

            return IsBetterSearchResult(current, best, useOpponentPressure) ? current : best;
        }

        private List<Meld> GenerateCandidateMelds(List<Tile> hand)
        {
            var result = new List<Meld>();
            AppendSetCandidates(hand, result);
            AppendRunCandidates(hand, result);

            return result
                .Where(_validator.IsValidMeld)
                .GroupBy(CreateCandidateKey)
                .Select(x => x.First())
                .ToList();
        }

        private List<Meld> FindDisjointOpening(List<Meld> candidates, int index, List<Meld> chosen, HashSet<int> usedTiles)
        {
            if (chosen.Count >= 2)
            {
                return chosen.Select(x => x.Clone()).ToList();
            }

            for (var i = index; i < candidates.Count; i++)
            {
                var meld = candidates[i];
                if (meld.Tiles.Any(x => usedTiles.Contains(x.Id)))
                {
                    continue;
                }

                foreach (var tile in meld.Tiles)
                {
                    usedTiles.Add(tile.Id);
                }

                chosen.Add(meld);
                var result = FindDisjointOpening(candidates, i + 1, chosen, usedTiles);
                if (result != null)
                {
                    return result;
                }

                chosen.RemoveAt(chosen.Count - 1);
                foreach (var tile in meld.Tiles)
                {
                    usedTiles.Remove(tile.Id);
                }
            }

            return null;
        }

        private List<Meld> FindBestOpeningMelds(List<Tile> hand)
        {
            var candidates = GenerateCandidateMelds(hand);
            OpeningSearchResult best = null;
            FindBestOpening(candidates, 0, new List<Meld>(), new HashSet<int>(), ref best);
            return best != null ? best.SelectedMelds.Select(x => x.Clone()).ToList() : null;
        }

        private List<CandidateEntry> BuildCandidateEntries(List<Tile> allTiles, HashSet<int> handIds, HashSet<int> tableIds, BotDifficulty difficulty)
        {
            return GenerateCandidateMelds(allTiles)
                .Select(meld => new CandidateEntry
                {
                    Meld = meld,
                    TileIds = new HashSet<int>(meld.Tiles.Select(x => x.Id)),
                    HandTileCount = meld.Tiles.Count(x => handIds.Contains(x.Id)),
                    TableTileCount = meld.Tiles.Count(x => tableIds.Contains(x.Id))
                })
                .Where(x => x.HandTileCount > 0 || x.TableTileCount > 0)
                .OrderByDescending(x => x.HandTileCount)
                .ThenByDescending(x => x.TableTileCount)
                .ThenByDescending(x => x.Meld.Tiles.Count)
                .Take(GetMaxCandidateCount(difficulty))
                .ToList();
        }

        private int DetermineMinimumOpeningCount(GameState state, Seat currentSeat)
        {
            return state.Players.Any(x => x.IsActive && x.Seat != currentSeat && x.HasOpened) || state.Table.Count > 0
                ? 1
                : 2;
        }

        private int EvaluateResidualHandPotential(List<Tile> remainingHandTiles)
        {
            if (remainingHandTiles.Count < 2)
            {
                return 0;
            }

            var pairPotential = remainingHandTiles
                .GroupBy(x => x.Number)
                .Sum(group => System.Math.Min(2, group.Select(x => x.Color).Distinct().Count()));

            var runPotential = 0;
            foreach (var colorGroup in remainingHandTiles.GroupBy(x => x.Color))
            {
                var orderedNumbers = colorGroup
                    .Select(x => _validator.NormalizeRunOrder(x.Number))
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
                for (var index = 1; index < orderedNumbers.Count; index++)
                {
                    if (orderedNumbers[index] == orderedNumbers[index - 1] + 1)
                    {
                        runPotential++;
                    }
                }
            }

            return pairPotential + runPotential;
        }

        private SearchResult SelectBestLookaheadResult(GameEngine engine, Seat seat, IList<SearchResult> shortlist, SearchResult fallback)
        {
            if (shortlist == null || shortlist.Count == 0)
            {
                return fallback;
            }

            SearchResult best = fallback;
            var bestScore = int.MinValue;
            foreach (var candidate in shortlist)
            {
                var score = EvaluateLookaheadScore(engine, seat, candidate);
                if (score > bestScore || (score == bestScore && IsBetterSearchResult(candidate, best, true)))
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private int EvaluateLookaheadScore(GameEngine engine, Seat seat, SearchResult candidate)
        {
            var simulation = new GameEngine();
            simulation.MaxBotThinkMilliseconds = System.Math.Max(1000, engine.MaxBotThinkMilliseconds / 3);
            simulation.Restore(engine.State);
            simulation.BeginTurn(seat);

            var player = simulation.State.Players.First(x => x.Seat == seat);
            var remainingHandIds = player.Hand
                .Select(x => x.Id)
                .Where(x => !candidate.UsedIds.Contains(x))
                .OrderBy(x => x)
                .ToList();

            string error;
            if (!simulation.ReplaceTurnLayout(seat, candidate.SelectedMelds, remainingHandIds, out error) ||
                !simulation.CommitTurn(seat, out error))
            {
                return int.MinValue;
            }

            if (simulation.State.IsGameOver)
            {
                return int.MaxValue / 2;
            }

            var scoreAfterOwnMove = EvaluateStateForSeat(simulation.State, seat);
            var opponentSeat = simulation.State.CurrentTurn;
            if (opponentSeat != seat)
            {
                SimulateSeatTurn(simulation, opponentSeat);
            }

            if (simulation.State.IsGameOver)
            {
                return string.Equals(simulation.State.WinnerName, player.Name, StringComparison.Ordinal)
                    ? int.MaxValue / 3
                    : int.MinValue / 3;
            }

            var scoreAfterOpponent = EvaluateStateForSeat(simulation.State, seat);
            if (simulation.State.CurrentTurn == seat)
            {
                SimulateSeatTurn(simulation, seat);
            }

            if (simulation.State.IsGameOver)
            {
                return string.Equals(simulation.State.WinnerName, player.Name, StringComparison.Ordinal)
                    ? int.MaxValue / 4
                    : int.MinValue / 4;
            }

            var scoreAfterSelfReply = EvaluateStateForSeat(simulation.State, seat);
            var selfReplyPotential = EstimateSeatTurnPotential(simulation.State, seat);
            return scoreAfterOwnMove + scoreAfterOpponent + scoreAfterSelfReply + selfReplyPotential * 12;
        }

        private void SimulateSeatTurn(GameEngine engine, Seat seat)
        {
            if (engine == null || engine.State.IsGameOver || engine.State.CurrentTurn != seat)
            {
                return;
            }

            var player = engine.State.Players.FirstOrDefault(x => x.Seat == seat);
            if (player == null || !player.IsActive)
            {
                return;
            }

            if (TryApplyBestTurn(engine, seat, BotDifficulty.VeryHard, false))
            {
                return;
            }

            engine.UndoTurn(seat);
            engine.BeginTurn(seat);

            if (!player.HasOpened)
            {
                var opening = FindOpeningMelds(engine.State.TurnHand);
                if (opening != null && opening.Count >= 2)
                {
                    foreach (var meld in opening)
                    {
                        engine.CreateMeldFromHand(seat, meld.Tiles.Select(x => x.Id).ToList());
                    }

                    if (engine.CommitTurn(seat, out _))
                    {
                        return;
                    }
                }

                engine.UndoTurn(seat);
                engine.BeginTurn(seat);
            }

            string message;
            if (engine.State.Deck.Count == 0)
            {
                engine.PassTurn(seat, out message);
            }
            else
            {
                engine.DrawTile(seat, out message);
            }
        }

        private int EstimateSeatTurnPotential(GameState state, Seat seat)
        {
            var player = state.Players.FirstOrDefault(x => x.Seat == seat && x.IsActive);
            if (player == null)
            {
                return 0;
            }

            var allTiles = state.Table.SelectMany(x => x.Tiles).Concat(player.Hand).Select(x => x.Clone()).ToList();
            var handIds = new HashSet<int>(player.Hand.Select(x => x.Id));
            var tableIds = new HashSet<int>(state.Table.SelectMany(x => x.Tiles).Select(x => x.Id));
            var handTileMap = player.Hand.ToDictionary(x => x.Id, x => x.Clone());
            var candidates = BuildCandidateEntries(allTiles, handIds, tableIds, BotDifficulty.VeryHard);
            if (candidates.Count == 0)
            {
                return 0;
            }

            var best = FindBestLayout(
                candidates,
                tableIds.OrderBy(x => x).ToList(),
                0,
                new HashSet<int>(),
                new List<Meld>(),
                handIds,
                handTileMap,
                player.HasOpened ? 0 : DetermineMinimumOpeningCount(state, seat),
                state,
                seat,
                false,
                null,
                0,
                null,
                Environment.TickCount + 150,
                true,
                false);

            return best != null ? best.HandUsedCount * 10 + best.ResidualPotential * 2 - best.RemainingHandValue : 0;
        }

        private int EvaluateStateForSeat(GameState state, Seat seat)
        {
            var player = state.Players.FirstOrDefault(x => x.Seat == seat && x.IsActive);
            if (player == null)
            {
                return int.MinValue / 4;
            }

            var ownHand = player.Hand.Select(x => x.Clone()).ToList();
            var ownPotential = EvaluateResidualHandPotential(ownHand);
            var ownValue = ownHand.Sum(x => x.Number);
            var opponentValue = state.Players
                .Where(x => x.IsActive && x.Seat != seat)
                .Sum(x => x.Hand.Sum(t => t.Number));
            var opponentThreat = EvaluateOpponentThreatScore(state, seat, state.Table);
            var boardOpenness = CountBoardOpenness(state.Table);

            return
                (player.Hand.Count == 0 ? 50000 : 0) +
                ownPotential * 18 -
                ownValue * 10 -
                ownHand.Count * 14 +
                opponentValue * 2 -
                opponentThreat * 6 -
                boardOpenness * 5 +
                (player.HasOpened ? 120 : -120);
        }

        private int CountBoardOpenness(IList<Meld> table)
        {
            if (table == null)
            {
                return 0;
            }

            var openness = 0;
            foreach (var meld in table)
            {
                foreach (TileColor color in Enum.GetValues(typeof(TileColor)))
                {
                    for (var number = 1; number <= 13; number++)
                    {
                        var probe = new Tile(-(number * 10 + (int)color + 1), color, number);
                        if (CanAddTileToMeld(probe, meld))
                        {
                            openness++;
                        }
                    }
                }
            }

            return openness;
        }

        private void InsertShortlistedResult(List<SearchResult> shortlistedResults, SearchResult current, int shortlistLimit)
        {
            var key = CreateSearchResultKey(current);
            if (shortlistedResults.Any(x => CreateSearchResultKey(x) == key))
            {
                return;
            }

            shortlistedResults.Add(current);
            shortlistedResults.Sort((left, right) => CompareSearchResults(right, left, true));
            if (shortlistedResults.Count > shortlistLimit)
            {
                shortlistedResults.RemoveRange(shortlistLimit, shortlistedResults.Count - shortlistLimit);
            }
        }

        private bool IsBetterSearchResult(SearchResult candidate, SearchResult best, bool useOpponentPressure)
        {
            if (candidate == null)
            {
                return false;
            }

            if (best == null)
            {
                return true;
            }

            return CompareSearchResults(candidate, best, useOpponentPressure) > 0;
        }

        private int CompareSearchResults(SearchResult left, SearchResult right, bool useOpponentPressure)
        {
            if (left.HandUsedCount != right.HandUsedCount)
            {
                return left.HandUsedCount.CompareTo(right.HandUsedCount);
            }

            if (left.PureHandMeldCount != right.PureHandMeldCount)
            {
                return left.PureHandMeldCount.CompareTo(right.PureHandMeldCount);
            }

            if (left.HandValueUsed != right.HandValueUsed)
            {
                return left.HandValueUsed.CompareTo(right.HandValueUsed);
            }

            if (useOpponentPressure && left.OpponentThreatScore != right.OpponentThreatScore)
            {
                return right.OpponentThreatScore.CompareTo(left.OpponentThreatScore);
            }

            if (left.ResidualPotential != right.ResidualPotential)
            {
                return left.ResidualPotential.CompareTo(right.ResidualPotential);
            }

            if (left.RemainingHandValue != right.RemainingHandValue)
            {
                return right.RemainingHandValue.CompareTo(left.RemainingHandValue);
            }

            if (left.SelectedMelds.Count != right.SelectedMelds.Count)
            {
                return left.SelectedMelds.Count.CompareTo(right.SelectedMelds.Count);
            }

            return 0;
        }

        private string CreateSearchResultKey(SearchResult result)
        {
            return string.Join("|", result.SelectedMelds.Select(CreateCandidateKey).OrderBy(x => x).ToArray());
        }

        private List<int> GetProgressiveCandidateLimits(BotDifficulty difficulty, int totalCandidateCount)
        {
            var maximum = System.Math.Min(totalCandidateCount, GetMaxCandidateCount(difficulty));
            var limits = new List<int>();
            int[] seeds;

            switch (difficulty)
            {
                case BotDifficulty.SmartHard:
                    seeds = new[] { 80, 160, 320, 640, maximum };
                    break;
                case BotDifficulty.UltraHard:
                    seeds = new[] { 120, 240, 480, 960, 1600, maximum };
                    break;
                case BotDifficulty.Cheater:
                    seeds = new[] { 160, 320, 640, 1200, 2400, maximum };
                    break;
                default:
                    seeds = new[] { maximum };
                    break;
            }

            foreach (var seed in seeds)
            {
                var value = System.Math.Min(maximum, seed);
                if (value > 0 && !limits.Contains(value))
                {
                    limits.Add(value);
                }
            }

            if (limits.Count == 0 && maximum > 0)
            {
                limits.Add(maximum);
            }

            return limits;
        }

        private string BuildDebugSummary(GameEngine engine, Seat seat, IList<SearchResult> shortlist, SearchResult best, BotDifficulty difficulty)
        {
            var player = engine.State.Players.FirstOrDefault(x => x.Seat == seat);
            var lines = new List<string>();
            lines.Add("Bot Debug [" + (player != null ? player.Name : seat.ToString()) + "]");
            lines.Add("Zorluk: " + GetDifficultyDebugLabel(difficulty));
            lines.Add("Maks düşünme: " + System.Math.Max(1, engine.MaxBotThinkMilliseconds / 1000) + " sn");
            lines.Add("Aday sayısı: " + shortlist.Count);
            lines.Add("Seçilen hamle: " + DescribeSearchResult(best));
            lines.Add("İlk 3 aday:");

            foreach (var candidate in shortlist.Take(3))
            {
                lines.Add("- " + DescribeSearchResult(candidate));
            }

            return string.Join(Environment.NewLine, lines.ToArray());
        }

        private string GetDifficultyDebugLabel(BotDifficulty difficulty)
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
                case BotDifficulty.SmartHard:
                    return "Akıllı";
                case BotDifficulty.UltraHard:
                    return "Çok Zor";
                case BotDifficulty.Cheater:
                    return "Elebakan";
                case BotDifficulty.Impossible:
                    return "Taşçalan";
                default:
                    return difficulty.ToString();
            }
        }

        private string DescribeSearchResult(SearchResult result)
        {
            if (result == null)
            {
                return "yok";
            }

            return string.Format(
                "elde {0} taş kullandı, {1} saf per, elde kalan değer {2}, potansiyel {3}, rakip tehdit {4}, diziler [{5}]",
                result.HandUsedCount,
                result.PureHandMeldCount,
                result.RemainingHandValue,
                result.ResidualPotential,
                result.OpponentThreatScore,
                string.Join(" | ", result.SelectedMelds.Select(x => x.ToString()).ToArray()));
        }

        private int EvaluateOpponentThreatScore(GameState state, Seat actingSeat, IList<Meld> resultingTable)
        {
            if (state == null)
            {
                return int.MaxValue;
            }

            var score = 0;
            var nextSeat = GetNextActiveSeat(state, actingSeat);
            foreach (var opponent in state.Players.Where(x => x.IsActive && x.Seat != actingSeat))
            {
                var handTiles = opponent.Hand.Select(x => x.Clone()).ToList();
                var handOnlyUsage = FindBestHandOnlyUsage(handTiles, DetermineMinimumOpeningCount(state, opponent.Seat));
                var extensionCount = CountDirectTableExtensions(handTiles, resultingTable);
                var threat = handOnlyUsage * 20 + extensionCount * 3 - handTiles.Sum(x => x.Number);
                if (opponent.Seat == nextSeat)
                {
                    threat += 25;
                }

                score += threat;
            }

            return score;
        }

        private int CountDirectTableExtensions(IList<Tile> handTiles, IList<Meld> table)
        {
            if (handTiles == null || table == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var tile in handTiles)
            {
                if (table.Any(meld => CanAddTileToMeld(tile, meld)))
                {
                    count++;
                }
            }

            return count;
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

        private int FindBestHandOnlyUsage(List<Tile> handTiles, int minimumMeldCount)
        {
            if (handTiles == null || handTiles.Count < 3)
            {
                return 0;
            }

            var candidates = GenerateCandidateMelds(handTiles);
            var best = 0;
            SearchBestHandOnlyUsage(candidates, 0, new HashSet<int>(), 0, 0, minimumMeldCount, ref best);
            return best;
        }

        private void SearchBestHandOnlyUsage(
            List<Meld> candidates,
            int index,
            HashSet<int> usedIds,
            int usedTileCount,
            int meldCount,
            int minimumMeldCount,
            ref int best)
        {
            if (meldCount >= minimumMeldCount && usedTileCount > best)
            {
                best = usedTileCount;
            }

            for (var i = index; i < candidates.Count; i++)
            {
                var meld = candidates[i];
                if (meld.Tiles.Any(x => usedIds.Contains(x.Id)))
                {
                    continue;
                }

                foreach (var tile in meld.Tiles)
                {
                    usedIds.Add(tile.Id);
                }

                SearchBestHandOnlyUsage(candidates, i + 1, usedIds, usedTileCount + meld.Tiles.Count, meldCount + 1, minimumMeldCount, ref best);

                foreach (var tile in meld.Tiles)
                {
                    usedIds.Remove(tile.Id);
                }
            }
        }

        private Seat GetNextActiveSeat(GameState state, Seat currentSeat)
        {
            for (var offset = 1; offset <= 4; offset++)
            {
                var nextSeat = (Seat)(((int)currentSeat + offset) % 4);
                if (state.Players.Any(x => x.IsActive && x.Seat == nextSeat))
                {
                    return nextSeat;
                }
            }

            return currentSeat;
        }

        private void AppendSetCandidates(List<Tile> tiles, List<Meld> result)
        {
            foreach (var numberGroup in tiles.GroupBy(x => x.Number))
            {
                var tilesByColor = numberGroup
                    .GroupBy(x => x.Color)
                    .OrderBy(x => (int)x.Key)
                    .ToList();
                if (tilesByColor.Count < 3)
                {
                    continue;
                }

                var colorIndices = Enumerable.Range(0, tilesByColor.Count).ToList();
                foreach (var colorSelection in BuildIndexSelections(colorIndices, 3))
                {
                    AppendSetSelectionVariants(tilesByColor, colorSelection, 0, new List<Tile>(), result);
                }

                if (tilesByColor.Count >= 4)
                {
                    foreach (var colorSelection in BuildIndexSelections(colorIndices, 4))
                    {
                        AppendSetSelectionVariants(tilesByColor, colorSelection, 0, new List<Tile>(), result);
                    }
                }
            }
        }

        private void AppendSetSelectionVariants(
            IList<IGrouping<TileColor, Tile>> tilesByColor,
            IList<int> colorSelection,
            int selectionIndex,
            List<Tile> current,
            List<Meld> result)
        {
            if (selectionIndex >= colorSelection.Count)
            {
                result.Add(new Meld(current));
                return;
            }

            foreach (var tile in tilesByColor[colorSelection[selectionIndex]])
            {
                current.Add(tile);
                AppendSetSelectionVariants(tilesByColor, colorSelection, selectionIndex + 1, current, result);
                current.RemoveAt(current.Count - 1);
            }
        }

        private void AppendRunCandidates(List<Tile> tiles, List<Meld> result)
        {
            foreach (var colorGroup in tiles.GroupBy(x => x.Color))
            {
                var numberGroups = colorGroup
                    .GroupBy(x => _validator.NormalizeRunOrder(x.Number))
                    .OrderBy(x => x.Key)
                    .ToList();
                for (var start = 0; start < numberGroups.Count; start++)
                {
                    var currentGroups = new List<IGrouping<int, Tile>> { numberGroups[start] };
                    for (var index = start + 1; index < numberGroups.Count; index++)
                    {
                        if (numberGroups[index].Key != numberGroups[index - 1].Key + 1)
                        {
                            break;
                        }

                        currentGroups.Add(numberGroups[index]);
                        if (currentGroups.Count >= 3)
                        {
                            for (var length = 3; length <= currentGroups.Count; length++)
                            {
                                AppendRunSelectionVariants(currentGroups.Take(length).ToList(), 0, new List<Tile>(), result);
                            }
                        }
                    }
                }
            }
        }

        private void AppendRunSelectionVariants(
            IList<IGrouping<int, Tile>> groups,
            int groupIndex,
            List<Tile> current,
            List<Meld> result)
        {
            if (groupIndex >= groups.Count)
            {
                result.Add(new Meld(current));
                return;
            }

            foreach (var tile in groups[groupIndex])
            {
                current.Add(tile);
                AppendRunSelectionVariants(groups, groupIndex + 1, current, result);
                current.RemoveAt(current.Count - 1);
            }
        }

        private IEnumerable<List<int>> BuildIndexSelections(IList<int> values, int choose)
        {
            return BuildIndexSelections(values, choose, 0, new List<int>());
        }

        private IEnumerable<List<int>> BuildIndexSelections(IList<int> values, int choose, int startIndex, List<int> current)
        {
            if (current.Count == choose)
            {
                yield return new List<int>(current);
                yield break;
            }

            for (var index = startIndex; index < values.Count; index++)
            {
                current.Add(values[index]);
                foreach (var selection in BuildIndexSelections(values, choose, index + 1, current))
                {
                    yield return selection;
                }

                current.RemoveAt(current.Count - 1);
            }
        }

        private string CreateCandidateKey(Meld meld)
        {
            return string.Join("-", meld.Tiles.Select(x => x.Id).OrderBy(x => x).ToArray());
        }

        private void FindBestOpening(List<Meld> candidates, int index, List<Meld> chosen, HashSet<int> usedTiles, ref OpeningSearchResult best)
        {
            if (chosen.Count >= 2)
            {
                var usedCount = usedTiles.Count;
                if (best == null ||
                    usedCount > best.HandUsedCount ||
                    (usedCount == best.HandUsedCount && chosen.Count > best.SelectedMelds.Count))
                {
                    best = new OpeningSearchResult
                    {
                        HandUsedCount = usedCount,
                        SelectedMelds = chosen.Select(x => x.Clone()).ToList()
                    };
                }
            }

            for (var i = index; i < candidates.Count; i++)
            {
                var meld = candidates[i];
                if (meld.Tiles.Any(x => usedTiles.Contains(x.Id)))
                {
                    continue;
                }

                foreach (var tile in meld.Tiles)
                {
                    usedTiles.Add(tile.Id);
                }

                chosen.Add(meld);
                FindBestOpening(candidates, i + 1, chosen, usedTiles, ref best);
                chosen.RemoveAt(chosen.Count - 1);

                foreach (var tile in meld.Tiles)
                {
                    usedTiles.Remove(tile.Id);
                }
            }
        }

        private sealed class SearchResult
        {
            public int HandUsedCount { get; set; }
            public int PureHandMeldCount { get; set; }
            public int HandValueUsed { get; set; }
            public int RemainingHandValue { get; set; }
            public int ResidualPotential { get; set; }
            public int OpponentThreatScore { get; set; }
            public HashSet<int> UsedIds { get; set; }
            public List<Meld> SelectedMelds { get; set; }
        }

        private sealed class OpeningSearchResult
        {
            public int HandUsedCount { get; set; }
            public List<Meld> SelectedMelds { get; set; }
        }

        private sealed class CandidateEntry
        {
            public Meld Meld { get; set; }
            public HashSet<int> TileIds { get; set; }
            public int HandTileCount { get; set; }
            public int TableTileCount { get; set; }

            public bool IsPureHand
            {
                get { return TableTileCount == 0; }
            }
        }
    }
}
