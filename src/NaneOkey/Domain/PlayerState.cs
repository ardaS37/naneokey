using System;
using System.Collections.Generic;
using System.Linq;

namespace NaneOkey.Domain
{
    [Serializable]
    public sealed class PlayerState
    {
        public PlayerState(Seat seat, string name, PlayerType type, BotDifficulty difficulty, bool isActive = true)
        {
            Seat = seat;
            Name = name;
            Type = type;
            Difficulty = difficulty;
            IsActive = isActive;
            Hand = new List<Tile>();
        }

        public Seat Seat { get; private set; }

        public string Name { get; set; }

        public PlayerType Type { get; set; }

        public BotDifficulty Difficulty { get; set; }

        public bool IsActive { get; set; }

        public bool HasOpened { get; set; }

        public bool OpenedWithPairs { get; set; }

        public int RoundPenalty { get; set; }

        public List<Tile> Hand { get; private set; }

        public PlayerState Clone()
        {
            var clone = new PlayerState(Seat, Name, Type, Difficulty, IsActive);
            clone.HasOpened = HasOpened;
            clone.OpenedWithPairs = OpenedWithPairs;
            clone.RoundPenalty = RoundPenalty;
            clone.Hand.AddRange(Hand.Select(x => x.Clone()));
            return clone;
        }
    }
}
