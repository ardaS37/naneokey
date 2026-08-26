using System;

namespace NaneOkey.Domain
{
    [Serializable]
    public sealed class Tile
    {
        public Tile(int id, TileColor color, int number)
        {
            Id = id;
            Color = color;
            Number = number;
        }

        public int Id { get; private set; }

        public TileColor Color { get; private set; }

        public int Number { get; private set; }

        public Tile Clone()
        {
            return new Tile(Id, Color, Number);
        }

        public string ColorName
        {
            get
            {
                switch (Color)
                {
                    case TileColor.Red:
                        return "K";
                    case TileColor.Blue:
                        return "M";
                    case TileColor.Yellow:
                        return "Y";
                    default:
                        return "Siy";
                }
            }
        }

        public override string ToString()
        {
            return Number + ColorSymbol();
        }

        private string ColorSymbol()
        {
            switch (Color)
            {
                case TileColor.Red:
                    return "K";
                case TileColor.Blue:
                    return "M";
                case TileColor.Yellow:
                    return "Y";
                default:
                    return "Y";
            }
        }
    }
}
