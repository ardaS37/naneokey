using System;
using System.Collections.Generic;
using System.Linq;

namespace NaneOkey.Domain
{
    [Serializable]
    public sealed class GameState
    {
        public GameState()
        {
            Players = new List<PlayerState>();
            Table = new List<Meld>();
            Deck = new List<Tile>();
            TurnTable = new List<Meld>();
            TurnHand = new List<Tile>();
            LastAction = string.Empty;
            DiscardPiles = new List<DiscardPile>();
            RoundMultiplier = 1;
        }

        public List<PlayerState> Players { get; private set; }

        public List<Meld> Table { get; private set; }

        public List<Tile> Deck { get; private set; }

        public GameMode Mode { get; set; }

        public bool UseNewAppearance { get; set; }

        public Tile Indicator { get; set; }

        public List<DiscardPile> DiscardPiles { get; private set; }

        public bool HasDrawnThisTurn { get; set; }

        public int DrawnDiscardTileId { get; set; }

        public bool EndedByStock { get; set; }

        public int RoundMultiplier { get; set; }

        public Seat CurrentTurn { get; set; }

        public bool IsGameOver { get; set; }

        public string WinnerName { get; set; }

        public string LastAction { get; set; }

        public List<Meld> TurnTable { get; private set; }

        public List<Tile> TurnHand { get; private set; }

        public bool TurnInProgress { get; set; }

        public List<int> OriginalHandIds { get; private set; } = new List<int>();

        public List<int> OriginalTableIds { get; private set; } = new List<int>();

        public GameState Clone()
        {
            var clone = new GameState();
            clone.Players.AddRange(Players.Select(x => x.Clone()));
            clone.Table.AddRange(Table.Select(x => x.Clone()));
            clone.Deck.AddRange(Deck.Select(x => x.Clone()));
            clone.Mode = Mode;
            clone.UseNewAppearance = UseNewAppearance;
            clone.Indicator = Indicator == null ? null : Indicator.Clone();
            clone.DiscardPiles.AddRange(DiscardPiles.Select(x => x.Clone()));
            clone.HasDrawnThisTurn = HasDrawnThisTurn;
            clone.DrawnDiscardTileId = DrawnDiscardTileId;
            clone.EndedByStock = EndedByStock;
            clone.RoundMultiplier = RoundMultiplier;
            clone.CurrentTurn = CurrentTurn;
            clone.IsGameOver = IsGameOver;
            clone.WinnerName = WinnerName;
            clone.LastAction = LastAction;
            clone.TurnTable.AddRange(TurnTable.Select(x => x.Clone()));
            clone.TurnHand.AddRange(TurnHand.Select(x => x.Clone()));
            clone.TurnInProgress = TurnInProgress;
            clone.OriginalHandIds.AddRange(OriginalHandIds);
            clone.OriginalTableIds.AddRange(OriginalTableIds);
            return clone;
        }
    }
}
