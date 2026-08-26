namespace NaneOkey.Domain
{
    public enum TileColor
    {
        Red,
        Blue,
        Yellow,
        Black
    }

    public enum MeldKind
    {
        Run,
        Set
    }

    public enum Seat
    {
        South = 0,
        West = 1,
        North = 2,
        East = 3
    }

    public enum PlayerType
    {
        Human,
        Bot,
        Remote
    }

    public enum BotDifficulty
    {
        Easy,
        Medium,
        Hard,
        VeryHard,
        SmartHard,
        UltraHard,
        Cheater,
        Impossible
    }
}
