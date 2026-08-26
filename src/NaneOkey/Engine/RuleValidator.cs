using System.Collections.Generic;
using System.Linq;
using NaneOkey.Domain;

namespace NaneOkey.Engine
{
    public sealed class RuleValidator
    {
        public bool IsValidMeld(Meld meld)
        {
            if (meld == null || meld.Tiles.Count < 3)
            {
                return false;
            }

            return IsValidSet(meld.Tiles) || IsValidRun(meld.Tiles);
        }

        public bool AreAllMeldsValid(IEnumerable<Meld> melds)
        {
            return melds.All(IsValidMeld);
        }

        public Meld NormalizeMeld(Meld meld)
        {
            if (meld == null)
            {
                return null;
            }

            var clone = meld.Clone();
            List<Tile> orderedRun;
            if (TryGetOrderedRunTiles(clone.Tiles, out orderedRun))
            {
                clone.Tiles.Clear();
                clone.Tiles.AddRange(orderedRun.Select(x => x.Clone()));
                return clone;
            }

            if (IsValidSet(clone.Tiles))
            {
                clone.Tiles.Clear();
                clone.Tiles.AddRange(meld.Tiles.OrderBy(x => (int)x.Color).ThenBy(x => x.Number).Select(x => x.Clone()));
                return clone;
            }

            return clone;
        }

        public bool IsValidSet(IEnumerable<Tile> tiles)
        {
            var list = tiles.ToList();
            if (list.Count < 3 || list.Count > 4)
            {
                return false;
            }

            var number = list[0].Number;
            if (list.Any(x => x.Number != number))
            {
                return false;
            }

            return list.Select(x => x.Color).Distinct().Count() == list.Count;
        }

        public bool IsValidRun(IEnumerable<Tile> tiles)
        {
            List<Tile> ordered;
            return TryGetOrderedRunTiles(tiles, out ordered);
        }

        private bool TryGetOrderedRunTiles(IEnumerable<Tile> tiles, out List<Tile> orderedTiles)
        {
            var list = tiles.ToList();
            orderedTiles = null;
            if (list.Count < 3)
            {
                return false;
            }

            var color = list[0].Color;
            if (list.Any(x => x.Color != color))
            {
                return false;
            }

            if (TryBuildRunOrder(list, false, out orderedTiles))
            {
                return true;
            }

            if (list.Count(x => x.Number == 1) > 1)
            {
                return false;
            }

            return TryBuildRunOrder(list, true, out orderedTiles);
        }

        private bool TryBuildRunOrder(IEnumerable<Tile> tiles, bool aceHigh, out List<Tile> orderedTiles)
        {
            orderedTiles = tiles
                .OrderBy(x => aceHigh && x.Number == 1 ? 14 : x.Number)
                .ToList();

            for (var i = 1; i < orderedTiles.Count; i++)
            {
                var previous = aceHigh && orderedTiles[i - 1].Number == 1 ? 14 : orderedTiles[i - 1].Number;
                var current = aceHigh && orderedTiles[i].Number == 1 ? 14 : orderedTiles[i].Number;
                if (current != previous + 1)
                {
                    orderedTiles = null;
                    return false;
                }
            }

            return true;
        }

        public int NormalizeRunOrder(int number)
        {
            return number == 1 ? 14 : number;
        }
    }
}
