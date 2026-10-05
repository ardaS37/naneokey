using System;
using System.Collections.Generic;
using System.Linq;
using NaneOkey.Domain;

namespace NaneOkey.Engine
{
    // Kept separate from RuleValidator: Nane Okey has no wild tiles and permits
    // rearrangement of the table, unlike the traditional games.
    public sealed class TraditionalRuleValidator
    {
        private readonly GameMode _mode;

        public TraditionalRuleValidator(GameMode mode)
        {
            _mode = mode;
        }

        public bool IsValidMeld(Meld meld)
        {
            Meld normalized;
            return TryNormalize(meld, out normalized);
        }

        public Meld NormalizeMeld(Meld meld)
        {
            Meld normalized;
            return TryNormalize(meld, out normalized) ? normalized : (meld == null ? null : meld.Clone());
        }

        public int MeldValue(Meld meld)
        {
            Meld normalized;
            return TryNormalize(meld, out normalized)
                ? normalized.Tiles.Sum(x => x.IsJoker ? x.JokerNumber : x.Number)
                : 0;
        }

        public bool TryNormalize(Meld meld, out Meld normalized)
        {
            normalized = null;
            if (meld == null || meld.Tiles.Select(x => x.Id).Distinct().Count() != meld.Tiles.Count)
                return false;
            if (meld.Tiles.Any(x => x.Number < 1 || x.Number > 13))
                return false;
            if (meld.Tiles.Count == 2)
                return TryPair(meld, out normalized);
            if (meld.IsPair || meld.Tiles.Count < 3 || meld.Tiles.Count > 13)
                return false;
            Meld set;
            Meld run;
            var isSet = TrySet(meld, out set);
            var isRun = TryRun(meld, out run);
            if (!isSet && !isRun) return false;
            normalized = !isSet ? run : (!isRun ? set :
                (run.Tiles.Sum(x => x.IsJoker ? x.JokerNumber : x.Number) > set.Tiles.Sum(x => x.IsJoker ? x.JokerNumber : x.Number) ? run : set));
            return true;
        }

        private static bool IsFree(Tile tile)
        {
            return tile.IsJoker && tile.JokerNumber == 0;
        }

        private static int Number(Tile tile)
        {
            return tile.IsJoker && tile.JokerNumber > 0 ? tile.JokerNumber : tile.Number;
        }

        private static TileColor Color(Tile tile)
        {
            return tile.IsJoker && tile.JokerNumber > 0 ? tile.JokerColor : tile.Color;
        }

        private static Tile Represent(Tile tile, TileColor color, int number)
        {
            var clone = tile.Clone();
            if (clone.IsJoker)
            {
                clone.JokerColor = color;
                clone.JokerNumber = number;
            }
            return clone;
        }

        private bool TryPair(Meld meld, out Meld normalized)
        {
            normalized = null;
            var fixedTiles = meld.Tiles.Where(x => !IsFree(x)).ToList();
            if (fixedTiles.Count == 2 && (Number(fixedTiles[0]) != Number(fixedTiles[1]) || Color(fixedTiles[0]) != Color(fixedTiles[1])))
                return false;
            var color = fixedTiles.Count == 0 ? TileColor.Red : Color(fixedTiles[0]);
            var number = fixedTiles.Count == 0 ? 13 : Number(fixedTiles[0]);
            normalized = meld.Clone();
            normalized.IsPair = true;
            normalized.Tiles.Clear();
            normalized.Tiles.AddRange(meld.Tiles.Select(x => Represent(x, color, number)));
            return true;
        }

        private bool TrySet(Meld meld, out Meld normalized)
        {
            normalized = null;
            if (meld.Tiles.Count > 4)
                return false;
            var fixedTiles = meld.Tiles.Where(x => !IsFree(x)).ToList();
            if (fixedTiles.Select(Number).Distinct().Count() > 1 || fixedTiles.Select(Color).Distinct().Count() != fixedTiles.Count)
                return false;
            var number = fixedTiles.Count == 0 ? 13 : Number(fixedTiles[0]);
            var colors = new Queue<TileColor>(Enum.GetValues(typeof(TileColor)).Cast<TileColor>().Except(fixedTiles.Select(Color)));
            normalized = meld.Clone();
            normalized.IsPair = false;
            normalized.Tiles.Clear();
            normalized.Tiles.AddRange(meld.Tiles.Select(x => IsFree(x) ? Represent(x, colors.Dequeue(), number) : x.Clone())
                .OrderBy(Color));
            return true;
        }

        private bool TryRun(Meld meld, out Meld normalized)
        {
            normalized = null;
            var fixedTiles = meld.Tiles.Where(x => !IsFree(x)).ToList();
            if (fixedTiles.Select(Color).Distinct().Count() > 1 || fixedTiles.Select(Number).Distinct().Count() != fixedTiles.Count)
                return false;
            var color = fixedTiles.Count == 0 ? TileColor.Red : Color(fixedTiles[0]);
            var highest = _mode == GameMode.ClassicOkey ? 14 : 13;
            for (var start = highest - meld.Tiles.Count + 1; start >= 1; start--)
            {
                // A classic run may use 1 above 13, but never wrap onwards to 2.
                var numbers = Enumerable.Range(start, meld.Tiles.Count).Select(x => x == 14 ? 1 : x).ToList();
                if (numbers.Distinct().Count() != numbers.Count || fixedTiles.Any(x => !numbers.Contains(Number(x))))
                    continue;
                var wild = new Queue<Tile>(meld.Tiles.Where(IsFree));
                var ordered = new List<Tile>();
                foreach (var number in numbers)
                {
                    var tile = fixedTiles.FirstOrDefault(x => Number(x) == number);
                    ordered.Add(tile == null ? Represent(wild.Dequeue(), color, number) : tile.Clone());
                }
                normalized = meld.Clone();
                normalized.IsPair = false;
                normalized.Tiles.Clear();
                normalized.Tiles.AddRange(ordered);
                return true;
            }
            return false;
        }

        public bool TryFindWinningHand(IList<Tile> hand, out List<Meld> melds, out bool pairs)
        {
            melds = null;
            pairs = false;
            if (hand.Count != 14 || hand.Select(x => x.Id).Distinct().Count() != 14)
                return false;
            // Prefer the higher scoring pair finish if a hand can be interpreted
            // both as seven pairs and as two copies of the same runs.
            pairs = true;
            if (TryPartition(hand, GenerateCandidates(hand, true), out melds)) return true;
            pairs = false;
            return TryPartition(hand, GenerateCandidates(hand, false), out melds);
        }

        private static bool TryPartition(IList<Tile> hand, IList<Meld> candidates, out List<Meld> result)
        {
            var map = hand.Select((tile, index) => new { tile.Id, Index = index }).ToDictionary(x => x.Id, x => x.Index);
            var masks = candidates.Select(x => x.Tiles.Aggregate(0, (mask, tile) => mask | (1 << map[tile.Id]))).ToList();
            var failed = new HashSet<int>();
            var chosen = new List<Meld>();
            Func<int, bool> solve = null;
            solve = remaining =>
            {
                if (remaining == 0) return true;
                if (failed.Contains(remaining)) return false;
                var first = remaining & -remaining;
                for (var i = 0; i < candidates.Count; i++)
                {
                    if ((masks[i] & first) == 0 || (remaining & masks[i]) != masks[i]) continue;
                    chosen.Add(candidates[i]);
                    if (solve(remaining ^ masks[i])) return true;
                    chosen.RemoveAt(chosen.Count - 1);
                }
                failed.Add(remaining);
                return false;
            };
            result = solve((1 << hand.Count) - 1) ? chosen.Select(x => x.Clone()).ToList() : null;
            return result != null;
        }

        public List<Meld> GenerateCandidates(IList<Tile> hand, bool pairs)
        {
            var candidates = new Dictionary<string, Meld>();
            Action<IList<Tile>> add = tiles =>
            {
                Meld normalized;
                var meld = new Meld(tiles) { IsPair = pairs };
                if (!TryNormalize(meld, out normalized)) return;
                var key = string.Join(",", tiles.Select(x => x.Id).OrderBy(x => x));
                Meld previous;
                if (!candidates.TryGetValue(key, out previous) || MeldValue(normalized) > MeldValue(previous))
                    candidates[key] = normalized;
            };
            if (pairs)
            {
                for (var a = 0; a < hand.Count; a++)
                    for (var b = a + 1; b < hand.Count; b++) add(new[] { hand[a], hand[b] });
                return candidates.Values.ToList();
            }
            // Sets are at most four tiles. Enumerating these small combinations
            // also covers short runs and both copies of any physical tile.
            for (var a = 0; a < hand.Count; a++)
                for (var b = a + 1; b < hand.Count; b++)
                    for (var c = b + 1; c < hand.Count; c++)
                    {
                        add(new[] { hand[a], hand[b], hand[c] });
                        for (var d = c + 1; d < hand.Count; d++) add(new[] { hand[a], hand[b], hand[c], hand[d] });
                    }
            var highest = _mode == GameMode.ClassicOkey ? 14 : 13;
            foreach (TileColor color in Enum.GetValues(typeof(TileColor)))
                for (var length = 5; length <= Math.Min(13, hand.Count); length++)
                    for (var start = 1; start <= highest - length + 1; start++)
                    {
                        var numbers = Enumerable.Range(start, length).Select(x => x == 14 ? 1 : x).ToList();
                        var selected = new List<Tile>();
                        var used = new HashSet<int>();
                        Action<int> build = null;
                        build = position =>
                        {
                            if (position == numbers.Count) { add(selected); return; }
                            foreach (var tile in hand.Where(x => !used.Contains(x.Id) &&
                                (IsFree(x) || (Color(x) == color && Number(x) == numbers[position]))))
                            {
                                used.Add(tile.Id);
                                selected.Add(tile);
                                build(position + 1);
                                selected.RemoveAt(selected.Count - 1);
                                used.Remove(tile.Id);
                            }
                        };
                        build(0);
                    }
            return candidates.Values.OrderByDescending(x => x.Tiles.Count).ThenByDescending(MeldValue).ToList();
        }

        public List<Meld> FindBestMelds(IList<Tile> hand, bool pairs, bool maximizeValue)
        {
            if (hand.Count == 0 || hand.Count > 30) return new List<Meld>();
            var candidates = GenerateCandidates(hand, pairs);
            var map = hand.Select((tile, index) => new { tile.Id, Index = index }).ToDictionary(x => x.Id, x => x.Index);
            var masks = candidates.Select(x => x.Tiles.Aggregate(0, (mask, tile) => mask | (1 << map[tile.Id]))).ToList();
            var memo = new Dictionary<int, int>();
            var choices = new Dictionary<int, int>();
            Func<int, int> solve = null;
            solve = remaining =>
            {
                if (remaining == 0) return 0;
                int known;
                if (memo.TryGetValue(remaining, out known)) return known;
                var first = remaining & -remaining;
                var best = solve(remaining ^ first);
                var choice = -1;
                for (var i = 0; i < candidates.Count; i++)
                {
                    if ((masks[i] & first) == 0 || (remaining & masks[i]) != masks[i]) continue;
                    var weight = maximizeValue ? MeldValue(candidates[i]) * 100 + candidates[i].Tiles.Count : candidates[i].Tiles.Count * 100 + MeldValue(candidates[i]);
                    var value = weight + solve(remaining ^ masks[i]);
                    if (value > best) { best = value; choice = i; }
                }
                memo[remaining] = best;
                choices[remaining] = choice;
                return best;
            };
            var maskLeft = (1 << hand.Count) - 1;
            solve(maskLeft);
            var result = new List<Meld>();
            while (maskLeft != 0)
            {
                var choice = choices[maskLeft];
                if (choice < 0) maskLeft ^= maskLeft & -maskLeft;
                else { result.Add(candidates[choice].Clone()); maskLeft ^= masks[choice]; }
            }
            return result;
        }
    }
}
