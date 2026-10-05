using System;
using System.Collections.Generic;
using System.Linq;

namespace NaneOkey.Domain
{
    [Serializable]
    public sealed class DiscardPile
    {
        public DiscardPile(Seat seat)
        {
            Seat = seat;
            Tiles = new List<Tile>();
        }

        public Seat Seat { get; private set; }

        public List<Tile> Tiles { get; private set; }

        public DiscardPile Clone()
        {
            var clone = new DiscardPile(Seat);
            clone.Tiles.AddRange(Tiles.Select(x => x.Clone()));
            return clone;
        }
    }
}
