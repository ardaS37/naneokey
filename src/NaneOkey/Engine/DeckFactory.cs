using System;
using System.Collections.Generic;
using NaneOkey.Domain;

namespace NaneOkey.Engine
{
    public static class DeckFactory
    {
        public static List<Tile> CreateShuffledDeck()
        {
            var random = new Random();
            var deck = new List<Tile>();
            var id = 1;

            for (var copy = 0; copy < 2; copy++)
            {
                foreach (TileColor color in Enum.GetValues(typeof(TileColor)))
                {
                    for (var number = 1; number <= 13; number++)
                    {
                        deck.Add(new Tile(id++, color, number));
                    }
                }
            }

            for (var index = deck.Count - 1; index > 0; index--)
            {
                var swapIndex = random.Next(index + 1);
                var temp = deck[index];
                deck[index] = deck[swapIndex];
                deck[swapIndex] = temp;
            }

            return deck;
        }
    }
}
