using System.Collections.Generic;

namespace NaneOkey.Domain
{
    public sealed class PlayerSetup
    {
        public string Name { get; set; }
        public PlayerType Type { get; set; }
        public BotDifficulty Difficulty { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class GameSettings
    {
        public GameSettings()
        {
            Players = new List<PlayerSetup>();
        }

        public List<PlayerSetup> Players { get; private set; }

        public int StartingHandSize { get; set; }

        public int LanPort { get; set; }

        public int ActivePlayerCount { get; set; } = 4;

        public int TargetScore { get; set; } = 1000;

        public bool EnableLivePreview { get; set; } = false;

        public bool EnableTurnTimer { get; set; } = false;

        public int TurnSeconds { get; set; } = 30;

        public int BotThinkSeconds { get; set; } = 30;
    }
}
