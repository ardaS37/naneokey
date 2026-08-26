using System;
using System.Collections.Generic;
using System.Linq;

namespace NaneOkey.Domain
{
    [Serializable]
    public sealed class Meld
    {
        public Meld()
        {
            Tiles = new List<Tile>();
            BoardRow = -1;
            StartColumn = -1;
        }

        public Meld(IEnumerable<Tile> tiles)
            : this()
        {
            Tiles.AddRange(tiles.Select(x => x.Clone()));
        }

        public List<Tile> Tiles { get; private set; }

        public int BoardRow { get; set; }

        public int StartColumn { get; set; }

        public Meld Clone()
        {
            var clone = new Meld(Tiles);
            clone.BoardRow = BoardRow;
            clone.StartColumn = StartColumn;
            return clone;
        }

        public override string ToString()
        {
            return string.Join(" ", Tiles.Select(x => x.ToString()).ToArray());
        }
    }
}
