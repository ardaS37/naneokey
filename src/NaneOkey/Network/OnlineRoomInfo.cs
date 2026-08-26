using Photon.Realtime;

namespace NaneOkey.Network
{
    public sealed class OnlineRoomInfo
    {
        public OnlineRoomInfo(RoomInfo room)
        {
            Name = room != null ? room.Name : string.Empty;
            PlayerCount = room != null ? room.PlayerCount : 0;
            MaxPlayers = room != null ? room.MaxPlayers : 0;
            IsOpen = room != null && room.IsOpen;
            IsVisible = room != null && room.IsVisible;
        }

        public string Name { get; private set; }

        public int PlayerCount { get; private set; }

        public int MaxPlayers { get; private set; }

        public bool IsOpen { get; private set; }

        public bool IsVisible { get; private set; }

        public override string ToString()
        {
            return Name + " (" + PlayerCount + "/" + MaxPlayers + ")";
        }
    }
}
