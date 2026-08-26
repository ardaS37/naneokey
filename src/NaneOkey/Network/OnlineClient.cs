using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ExitGames.Client.Photon;
using NaneOkey.Domain;
using Photon.Realtime;
using Hashtable = System.Collections.Hashtable;

namespace NaneOkey.Network
{
    public sealed class OnlineClient : IDisposable, IConnectionCallbacks, ILobbyCallbacks, IInRoomCallbacks, IMatchmakingCallbacks, IOnEventCallback
    {
        private const byte EnvelopeEventCode = 21;
        private readonly LoadBalancingClient _client = new LoadBalancingClient(ConnectionProtocol.Udp);
        private readonly Timer _serviceTimer = new Timer();
        private readonly Dictionary<string, OnlineRoomInfo> _rooms = new Dictionary<string, OnlineRoomInfo>();
        private bool _createRequested;
        private string _pendingRoomName;

        public OnlineClient()
        {
            _client.AddCallbackTarget(this);
            _serviceTimer.Interval = 40;
            _serviceTimer.Tick += (_, __) => Service();
        }

        public event Action<string> LogReceived;
        public event Action<IList<OnlineRoomInfo>> RoomsUpdated;
        public event Action RoomJoined;
        public event Action RoomCreated;
        public event Action<int, string> PlayerEntered;
        public event Action<int> PlayerLeft;
        public event Action<int> MasterClientChanged;
        public event Action<int, LanEnvelope> EnvelopeReceived;

        public bool IsConnected
        {
            get { return _client.State != ClientState.PeerCreated && _client.State != ClientState.Disconnected; }
        }

        public bool InRoom
        {
            get { return _client.InRoom; }
        }

        public bool IsMasterClient
        {
            get { return _client.LocalPlayer != null && _client.LocalPlayer.IsMasterClient; }
        }

        public int LocalActorNumber
        {
            get { return _client.LocalPlayer != null ? _client.LocalPlayer.ActorNumber : 0; }
        }

        public string CurrentRoomName
        {
            get { return _client.CurrentRoom != null ? _client.CurrentRoom.Name : string.Empty; }
        }

        public string GetPlayerName(int actorNumber)
        {
            if (_client.CurrentRoom == null || _client.CurrentRoom.Players == null)
            {
                return string.Empty;
            }

            Player player;
            if (!_client.CurrentRoom.Players.TryGetValue(actorNumber, out player) || player == null)
            {
                return string.Empty;
            }

            return player.NickName ?? string.Empty;
        }

        public void Connect(string appId, string playerName)
        {
            if (IsConnected)
            {
                return;
            }

            SetPlayerName(playerName);
            _client.AppId = appId;
            _client.AppVersion = "3.0.5.0";
            _serviceTimer.Start();
            if (!_client.ConnectToRegionMaster("eu"))
            {
                RaiseLog("Photon bağlantısı başlatılamadı.");
            }
        }

        public void SetPlayerName(string playerName)
        {
            _client.NickName = string.IsNullOrWhiteSpace(playerName) ? "Misafir" : playerName.Trim();
        }

        public void CreateRoom(string roomName)
        {
            _createRequested = true;
            _pendingRoomName = NormalizeRoomName(roomName);
            TryCreateOrJoinPendingRoom();
        }

        public void JoinRoom(string roomName)
        {
            _createRequested = false;
            _pendingRoomName = NormalizeRoomName(roomName);
            TryCreateOrJoinPendingRoom();
        }

        public void LeaveRoom()
        {
            if (_client.InRoom)
            {
                _client.OpLeaveRoom(false);
            }
        }

        public void SendEnvelope(string type, string payload, bool includeSelf)
        {
            if (!_client.InRoom)
            {
                return;
            }

            var envelope = new LanEnvelope { Type = type, Payload = payload ?? string.Empty };
            var options = new RaiseEventOptions { Receivers = includeSelf ? ReceiverGroup.All : ReceiverGroup.Others };
            _client.OpRaiseEvent(EnvelopeEventCode, LanJson.Serialize(envelope), options, SendOptions.SendReliable);
            _client.LoadBalancingPeer.SendOutgoingCommands();
        }

        private void Service()
        {
            _client.LoadBalancingPeer.DispatchIncomingCommands();
            _client.LoadBalancingPeer.SendOutgoingCommands();
        }

        private void TryCreateOrJoinPendingRoom()
        {
            if (string.IsNullOrWhiteSpace(_pendingRoomName) || _client.State != ClientState.JoinedLobby)
            {
                return;
            }

            var parameters = new EnterRoomParams
            {
                RoomName = _pendingRoomName,
                RoomOptions = new RoomOptions { MaxPlayers = 4, IsVisible = true, IsOpen = true }
            };

            if (_createRequested)
            {
                _client.OpCreateRoom(parameters);
            }
            else
            {
                _client.OpJoinRoom(parameters);
            }
        }

        private static string NormalizeRoomName(string value)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Nane Masa" : value.Trim();
            return normalized.Length > 32 ? normalized.Substring(0, 32) : normalized;
        }

        private void RaiseLog(string text)
        {
            LogReceived?.Invoke(text);
        }

        public void OnConnected()
        {
            RaiseLog("Photon bağlantısı kuruldu.");
        }

        public void OnConnectedToMaster()
        {
            RaiseLog("Photon master sunucusuna bağlandı.");
            _client.OpJoinLobby(null);
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            _serviceTimer.Stop();
            RaiseLog("Photon bağlantısı koptu: " + cause);
        }

        public void OnRegionListReceived(RegionHandler regionHandler)
        {
        }

        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
        }

        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            RaiseLog("Photon kimlik doğrulama hatası: " + debugMessage);
        }

        public void OnJoinedLobby()
        {
            RaiseLog("Online lobiye girildi.");
            TryCreateOrJoinPendingRoom();
        }

        public void OnLeftLobby()
        {
        }

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            foreach (var room in roomList)
            {
                if (!room.IsOpen || !room.IsVisible || room.RemovedFromList)
                {
                    _rooms.Remove(room.Name);
                    continue;
                }

                _rooms[room.Name] = new OnlineRoomInfo(room);
            }

            RoomsUpdated?.Invoke(_rooms.Values.OrderBy(x => x.Name).ToList());
        }

        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
        {
        }

        public void OnCreatedRoom()
        {
            RaiseLog("Online oda kuruldu: " + CurrentRoomName);
            RoomCreated?.Invoke();
        }

        public void OnCreateRoomFailed(short returnCode, string message)
        {
            RaiseLog("Online oda kurulamadı: " + message);
            _pendingRoomName = null;
        }

        public void OnJoinedRoom()
        {
            RaiseLog("Online odaya girildi: " + CurrentRoomName);
            _pendingRoomName = null;
            RoomJoined?.Invoke();
        }

        public void OnJoinRoomFailed(short returnCode, string message)
        {
            RaiseLog("Online odaya katılınamadı: " + message);
            _pendingRoomName = null;
        }

        public void OnJoinRandomFailed(short returnCode, string message)
        {
        }

        public void OnLeftRoom()
        {
            RaiseLog("Online odadan çıkıldı.");
        }

        public void OnFriendListUpdate(List<FriendInfo> friendList)
        {
        }

        public void OnPlayerEnteredRoom(Player newPlayer)
        {
            PlayerEntered?.Invoke(newPlayer.ActorNumber, newPlayer.NickName);
        }

        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            PlayerLeft?.Invoke(otherPlayer.ActorNumber);
        }

        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
        }

        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
        }

        public void OnMasterClientSwitched(Player newMasterClient)
        {
            MasterClientChanged?.Invoke(newMasterClient != null ? newMasterClient.ActorNumber : 0);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent == null || photonEvent.Code != EnvelopeEventCode)
            {
                return;
            }

            var payload = photonEvent.CustomData as string;
            if (string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            var envelope = LanJson.Deserialize<LanEnvelope>(payload);
            if (envelope != null)
            {
                EnvelopeReceived?.Invoke(photonEvent.Sender, envelope);
            }
        }

        public void Dispose()
        {
            _serviceTimer.Stop();
            _client.RemoveCallbackTarget(this);
            if (_client.IsConnected)
            {
                _client.Disconnect();
            }
        }
    }
}
