using System;
using System.Collections.Generic;
using NaneOkey.Domain;

namespace NaneOkey.Network
{
    public static class LanProtocol
    {
        public const int Version = 4;
        public const string AppVersion = "4.0.0.0";

        public static string ModeDisplayName(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.ClassicOkey: return "Klasik Okey";
                case GameMode.Okey101: return "101 Okey";
                default: return "Nane Okey";
            }
        }

        public static string VersionMismatchMessage(int version)
        {
            return "Oyun sürümleri uyuşmuyor. Bu masa için tüm oyuncularda Nane Okey v" + AppVersion + " bulunmalıdır.";
        }
    }

    [Serializable]
    public sealed class LanEnvelope
    {
        public string Type { get; set; }
        public string Payload { get; set; }
    }

    [Serializable]
    public sealed class LanHello
    {
        public int ProtocolVersion { get; set; }
        public string PlayerName { get; set; }
        public string RoomName { get; set; }
    }

    [Serializable]
    public sealed class LanPlayerInfo
    {
        public string Name { get; set; }
        public string Seat { get; set; }
        public bool Connected { get; set; }
    }

    [Serializable]
    public sealed class LanLobbySnapshot
    {
        public int ProtocolVersion { get; set; }
        public GameMode Mode { get; set; }
        public bool UseNewAppearance { get; set; }
        public string HostName { get; set; }
        public string HostIp { get; set; }
        public List<string> HostIpCandidates { get; set; }
        public string RoomName { get; set; }
        public int Port { get; set; }
        public List<LanPlayerInfo> Players { get; set; }
    }

    [Serializable]
    public sealed class LanRoomAnnouncement
    {
        public int ProtocolVersion { get; set; }
        public GameMode Mode { get; set; }
        public bool UseNewAppearance { get; set; }
        public string HostName { get; set; }
        public string HostIp { get; set; }
        public List<string> HostIpCandidates { get; set; }
        public string RoomName { get; set; }
        public int Port { get; set; }

        public override string ToString()
        {
            var versionWarning = ProtocolVersion == LanProtocol.Version ? string.Empty : " - farklı sürüm";
            return RoomName + " - " + LanProtocol.ModeDisplayName(Mode) + " (" + HostIp + ")" + versionWarning;
        }
    }

    [Serializable]
    public sealed class LanDiscoveryQuery
    {
        public string App { get; set; }
        public int ProtocolVersion { get; set; }
    }

    [Serializable]
    public sealed class LanGameSnapshot
    {
        public string MatchId { get; set; }
        public int ProtocolVersion { get; set; } = LanProtocol.Version;
        public LanGameStateDto State { get; set; }
        public bool EnableLivePreview { get; set; }
        public bool EnableTurnTimer { get; set; }
        public int TurnSeconds { get; set; }
        public int BotThinkSeconds { get; set; }
        public int TargetScore { get; set; }
    }

    [Serializable]
    public sealed class LanSeatAssignment
    {
        public int ProtocolVersion { get; set; }
        public string Seat { get; set; }
    }

    [Serializable]
    public sealed class OnlineSeatAssignment
    {
        public int ActorNumber { get; set; }
        public string Seat { get; set; }
    }

    [Serializable]
    public sealed class LanTurnLayout
    {
        public string Seat { get; set; }
        public List<LanMeldDto> Melds { get; set; }
        public List<int> HandTileIds { get; set; }
    }

    [Serializable]
    public sealed class LanTurnPreview
    {
        public string Seat { get; set; }
        public List<LanMeldDto> Melds { get; set; }
        public List<int> HandTileIds { get; set; }
    }

    [Serializable]
    public sealed class LanDrawRequest
    {
        public int TargetRow { get; set; }
        public int TargetColumn { get; set; }
    }

    [Serializable]
    public sealed class LanDiscardRequest
    {
        public string Seat { get; set; }
        public int TileId { get; set; }
        public bool FinishClassic { get; set; }
        public List<LanMeldDto> Melds { get; set; }
        public List<int> HandTileIds { get; set; }
    }

    [Serializable]
    public sealed class LanTileDto
    {
        public int Id { get; set; }
        public TileColor Color { get; set; }
        public int Number { get; set; }
        public bool IsFalseJoker { get; set; }
        public bool IsJoker { get; set; }
        public TileColor JokerColor { get; set; }
        public int JokerNumber { get; set; }

        public static LanTileDto FromDomain(Tile tile)
        {
            return new LanTileDto
            {
                Id = tile.Id, Color = tile.Color, Number = tile.Number,
                IsFalseJoker = tile.IsFalseJoker, IsJoker = tile.IsJoker,
                JokerColor = tile.JokerColor, JokerNumber = tile.JokerNumber
            };
        }

        public Tile ToDomain()
        {
            return new Tile(Id, Color, Number)
            {
                IsFalseJoker = IsFalseJoker, IsJoker = IsJoker,
                JokerColor = JokerColor, JokerNumber = JokerNumber
            };
        }
    }

    [Serializable]
    public sealed class LanMeldDto
    {
        public List<LanTileDto> Tiles { get; set; }
        public int BoardRow { get; set; }
        public int StartColumn { get; set; }
        public Seat OwnerSeat { get; set; }
        public bool IsPair { get; set; }

        public static LanMeldDto FromDomain(Meld meld)
        {
            return new LanMeldDto
            {
                Tiles = meld.Tiles.ConvertAll(LanTileDto.FromDomain),
                BoardRow = meld.BoardRow,
                StartColumn = meld.StartColumn,
                OwnerSeat = meld.OwnerSeat,
                IsPair = meld.IsPair
            };
        }

        public Meld ToDomain()
        {
            var meld = new Meld((Tiles ?? new List<LanTileDto>()).ConvertAll(x => x.ToDomain()));
            meld.BoardRow = BoardRow;
            meld.StartColumn = StartColumn;
            meld.OwnerSeat = OwnerSeat;
            meld.IsPair = IsPair;
            return meld;
        }
    }

    [Serializable]
    public sealed class LanPlayerStateDto
    {
        public string Seat { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Difficulty { get; set; }
        public bool IsActive { get; set; }
        public bool HasOpened { get; set; }
        public bool OpenedWithPairs { get; set; }
        public int RoundPenalty { get; set; }
        public List<LanTileDto> Hand { get; set; }

        public static LanPlayerStateDto FromDomain(PlayerState player)
        {
            return new LanPlayerStateDto
            {
                Seat = player.Seat.ToString(),
                Name = player.Name,
                Type = player.Type.ToString(),
                Difficulty = player.Difficulty.ToString(),
                IsActive = player.IsActive,
                HasOpened = player.HasOpened,
                OpenedWithPairs = player.OpenedWithPairs,
                RoundPenalty = player.RoundPenalty,
                Hand = player.Hand.ConvertAll(LanTileDto.FromDomain)
            };
        }

        public PlayerState ToDomain()
        {
            Seat seat;
            PlayerType type;
            BotDifficulty difficulty;
            Enum.TryParse(Seat, out seat);
            Enum.TryParse(Type, out type);
            Enum.TryParse(Difficulty, out difficulty);
            var player = new PlayerState(seat, Name, type, difficulty, IsActive);
            player.HasOpened = HasOpened;
            player.OpenedWithPairs = OpenedWithPairs;
            player.RoundPenalty = RoundPenalty;
            player.Hand.AddRange((Hand ?? new List<LanTileDto>()).ConvertAll(x => x.ToDomain()));
            return player;
        }
    }

    [Serializable]
    public sealed class LanDiscardPileDto
    {
        public Seat Seat { get; set; }
        public List<LanTileDto> Tiles { get; set; }

        public static LanDiscardPileDto FromDomain(DiscardPile pile)
        {
            return new LanDiscardPileDto { Seat = pile.Seat, Tiles = pile.Tiles.ConvertAll(LanTileDto.FromDomain) };
        }

        public DiscardPile ToDomain()
        {
            var pile = new DiscardPile(Seat);
            pile.Tiles.AddRange((Tiles ?? new List<LanTileDto>()).ConvertAll(x => x.ToDomain()));
            return pile;
        }
    }

    [Serializable]
    public sealed class LanGameStateDto
    {
        public GameMode Mode { get; set; }
        public bool UseNewAppearance { get; set; }
        public LanTileDto Indicator { get; set; }
        public List<LanDiscardPileDto> DiscardPiles { get; set; }
        public bool HasDrawnThisTurn { get; set; }
        public int DrawnDiscardTileId { get; set; }
        public bool EndedByStock { get; set; }
        public int RoundMultiplier { get; set; }
        public List<LanPlayerStateDto> Players { get; set; }
        public List<LanMeldDto> Table { get; set; }
        public List<LanTileDto> Deck { get; set; }
        public string CurrentTurn { get; set; }
        public bool IsGameOver { get; set; }
        public string WinnerName { get; set; }
        public string LastAction { get; set; }
        public List<LanMeldDto> TurnTable { get; set; }
        public List<LanTileDto> TurnHand { get; set; }
        public bool TurnInProgress { get; set; }
        public List<int> OriginalHandIds { get; set; }
        public List<int> OriginalTableIds { get; set; }

        public static LanGameStateDto FromDomain(GameState state)
        {
            return new LanGameStateDto
            {
                Mode = state.Mode,
                UseNewAppearance = state.Mode == GameMode.NaneOkey && state.UseNewAppearance,
                Indicator = state.Indicator == null ? null : LanTileDto.FromDomain(state.Indicator),
                DiscardPiles = state.DiscardPiles.ConvertAll(LanDiscardPileDto.FromDomain),
                HasDrawnThisTurn = state.HasDrawnThisTurn,
                DrawnDiscardTileId = state.DrawnDiscardTileId,
                EndedByStock = state.EndedByStock,
                RoundMultiplier = state.RoundMultiplier,
                Players = state.Players.ConvertAll(LanPlayerStateDto.FromDomain),
                Table = state.Table.ConvertAll(LanMeldDto.FromDomain),
                Deck = state.Deck.ConvertAll(LanTileDto.FromDomain),
                CurrentTurn = state.CurrentTurn.ToString(),
                IsGameOver = state.IsGameOver,
                WinnerName = state.WinnerName,
                LastAction = state.LastAction,
                TurnTable = state.TurnTable.ConvertAll(LanMeldDto.FromDomain),
                TurnHand = state.TurnHand.ConvertAll(LanTileDto.FromDomain),
                TurnInProgress = state.TurnInProgress,
                OriginalHandIds = new List<int>(state.OriginalHandIds),
                OriginalTableIds = new List<int>(state.OriginalTableIds)
            };
        }

        public GameState ToDomain()
        {
            var state = new GameState();
            state.Mode = Mode;
            state.UseNewAppearance = Mode == GameMode.NaneOkey && UseNewAppearance;
            state.Indicator = Indicator == null ? null : Indicator.ToDomain();
            state.DiscardPiles.AddRange((DiscardPiles ?? new List<LanDiscardPileDto>()).ConvertAll(x => x.ToDomain()));
            state.HasDrawnThisTurn = HasDrawnThisTurn;
            state.DrawnDiscardTileId = DrawnDiscardTileId;
            state.EndedByStock = EndedByStock;
            state.RoundMultiplier = RoundMultiplier;
            state.Players.AddRange((Players ?? new List<LanPlayerStateDto>()).ConvertAll(x => x.ToDomain()));
            state.Table.AddRange((Table ?? new List<LanMeldDto>()).ConvertAll(x => x.ToDomain()));
            state.Deck.AddRange((Deck ?? new List<LanTileDto>()).ConvertAll(x => x.ToDomain()));
            Seat currentTurn;
            Enum.TryParse(CurrentTurn, out currentTurn);
            state.CurrentTurn = currentTurn;
            state.IsGameOver = IsGameOver;
            state.WinnerName = WinnerName;
            state.LastAction = LastAction ?? string.Empty;
            state.TurnTable.AddRange((TurnTable ?? new List<LanMeldDto>()).ConvertAll(x => x.ToDomain()));
            state.TurnHand.AddRange((TurnHand ?? new List<LanTileDto>()).ConvertAll(x => x.ToDomain()));
            state.TurnInProgress = TurnInProgress;
            state.OriginalHandIds.AddRange(OriginalHandIds ?? new List<int>());
            state.OriginalTableIds.AddRange(OriginalTableIds ?? new List<int>());
            return state;
        }
    }
}
