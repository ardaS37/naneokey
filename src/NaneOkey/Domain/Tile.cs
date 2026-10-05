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

        public bool IsFalseJoker { get; set; }

        public bool IsJoker { get; set; }

        // A joker's represented value is fixed once its meld is committed in 101.
        public TileColor JokerColor { get; set; }

        public int JokerNumber { get; set; }

        public Tile Clone()
        {
            return new Tile(Id, Color, Number)
            {
                IsFalseJoker = IsFalseJoker,
                IsJoker = IsJoker,
                JokerColor = JokerColor,
                JokerNumber = JokerNumber
            };
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
            return IsFalseJoker ? "Sahte okey" : Number + ColorSymbol() + (IsJoker ? " (Okey)" : string.Empty);
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
