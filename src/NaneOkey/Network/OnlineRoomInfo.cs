using System;
using NaneOkey.Domain;
using Photon.Realtime;

namespace NaneOkey.Network
{
    public sealed class OnlineRoomInfo
    {
        public const string ModeProperty = "mode";
        public const string AppearanceProperty = "newAppearance";
        public const string TargetScoreProperty = "targetScore";
        public const string ProtocolProperty = "protocol";

        public OnlineRoomInfo(RoomInfo room)
        {
            Name = room != null ? room.Name : string.Empty;
            PlayerCount = room != null ? room.PlayerCount : 0;
            MaxPlayers = room != null ? room.MaxPlayers : 0;
            IsOpen = room != null && room.IsOpen;
            IsVisible = room != null && room.IsVisible;
            var mode = ReadInt(room, ModeProperty, 0);
            Mode = Enum.IsDefined(typeof(GameMode), mode) ? (GameMode)mode : GameMode.NaneOkey;
            UseNewAppearance = Mode == GameMode.NaneOkey && ReadBool(room, AppearanceProperty);
            TargetScore = ReadInt(room, TargetScoreProperty, 20);
            ProtocolVersion = ReadInt(room, ProtocolProperty, 0);
        }

        public string Name { get; private set; }

        public int PlayerCount { get; private set; }

        public int MaxPlayers { get; private set; }

        public bool IsOpen { get; private set; }

        public bool IsVisible { get; private set; }

        public GameMode Mode { get; private set; }

        public bool UseNewAppearance { get; private set; }

        public int TargetScore { get; private set; }

        public int ProtocolVersion { get; private set; }

        private static bool ReadBool(RoomInfo room, string key)
        {
            if (room == null || room.CustomProperties == null || !room.CustomProperties.ContainsKey(key)) return false;
            return room.CustomProperties[key] is bool && (bool)room.CustomProperties[key];
        }

        private static int ReadInt(RoomInfo room, string key, int fallback)
        {
            if (room == null || room.CustomProperties == null || !room.CustomProperties.ContainsKey(key))
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(room.CustomProperties[key]);
            }
            catch (FormatException) { return fallback; }
            catch (InvalidCastException) { return fallback; }
            catch (OverflowException) { return fallback; }
        }

        public override string ToString()
        {
            return Name + " - " + LanProtocol.ModeDisplayName(Mode) + " (" + PlayerCount + "/" + MaxPlayers + ")";
        }
    }
}
