using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;
using NaneOkey.Domain;

namespace NaneOkey.Network
{
    public sealed class LanHost : IDisposable
    {
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly Dictionary<TcpClient, string> _clientNames = new Dictionary<TcpClient, string>();
        private readonly Dictionary<TcpClient, Seat> _clientSeats = new Dictionary<TcpClient, Seat>();
        private readonly object _sync = new object();
        private TcpListener _listener;
        private Thread _acceptThread;
        private Thread _broadcastThread;
        private bool _running;
        private string _roomName;
        private int _port;
        private LanGameSnapshot _lastGameSnapshot;
        private List<Seat> _remoteSeats = new List<Seat>();
        private List<string> _localIpCandidates = new List<string>();

        public bool EnableLivePreview { get; set; }
        public GameMode Mode { get; set; }
        public bool UseNewAppearance { get; set; }
        public int TargetScore { get; set; } = 20;
        public string MatchId { get; set; }
        public bool EnableTurnTimer { get; set; }
        public int TurnSeconds { get; set; } = 30;
        public int BotThinkSeconds { get; set; } = 30;

        public event Action<string> LogReceived;
        public event Action<LanLobbySnapshot> LobbyChanged;
        public event Action<Seat, int, int> RemoteDrawRequested;
        public event Action<Seat> RemotePassRequested;
        public event Action<Seat, IList<Meld>, IList<int>> RemoteCommitRequested;
        public event Action<Seat, IList<Meld>, IList<int>> RemotePreviewRequested;
        public event Action<Seat, int, bool, IList<Meld>, IList<int>> RemoteDiscardRequested;
        public event Action<Seat> RemoteDiscardDrawRequested;
        public event Action<Seat, string> RemotePlayerNamed;
        public event Action<Seat, string> RemotePlayerDisconnected;

        public string LocalIpAddress { get; private set; }
        public IList<string> LocalIpCandidates
        {
            get { return _localIpCandidates.AsReadOnly(); }
        }

        public string RoomName
        {
            get { return _roomName; }
        }

        public int Port
        {
            get { return _port; }
        }

        public void Start(int port, string roomName, IEnumerable<PlayerState> players)
        {
            if (_running)
            {
                return;
            }

            _port = port;
            _roomName = roomName;
            _remoteSeats = BuildAssignableRemoteSeats(players);
            _localIpCandidates = ResolveLocalIpCandidates();
            LocalIpAddress = _localIpCandidates.FirstOrDefault() ?? "127.0.0.1";
            _listener = StartListenerWithFallback(port);
            _running = true;
            BroadcastLobby(players);
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
            _acceptThread.Start();
            _broadcastThread = new Thread(BroadcastRoomLoop) { IsBackground = true };
            _broadcastThread.Start();
            RaiseLog("LAN host açıldı. Oda: " + roomName + " IP: " + LocalIpAddress + " Port: " + _port);
            RaiseLog("LAN adres adayları: " + string.Join(", ", _localIpCandidates.ToArray()));
        }

        public void BroadcastLobby(IEnumerable<PlayerState> players)
        {
            var snapshot = new LanLobbySnapshot
            {
                ProtocolVersion = LanProtocol.Version,
                Mode = Mode,
                UseNewAppearance = Mode == GameMode.NaneOkey && UseNewAppearance,
                HostName = Dns.GetHostName(),
                HostIp = LocalIpAddress,
                HostIpCandidates = new List<string>(_localIpCandidates),
                RoomName = _roomName,
                Port = _port,
                Players = players.Select(x => new LanPlayerInfo
                {
                    Name = x.Name,
                    Seat = x.Seat.ToString(),
                    Connected = x.Type != PlayerType.Remote
                }).ToList()
            };

            LobbyChanged?.Invoke(snapshot);
            var message = new LanEnvelope { Type = "lobby", Payload = LanJson.Serialize(snapshot) };
            Broadcast(message);
        }

        public void BroadcastText(string text)
        {
            Broadcast(new LanEnvelope { Type = "text", Payload = text });
        }

        public void BroadcastPreview(Seat seat, IList<Meld> melds, IList<int> handTileIds)
        {
            if (Mode != GameMode.NaneOkey || !EnableLivePreview)
            {
                return;
            }

            var payload = new LanTurnPreview
            {
                Seat = seat.ToString(),
                Melds = melds == null ? new List<LanMeldDto>() : melds.Select(LanMeldDto.FromDomain).ToList(),
                HandTileIds = handTileIds == null ? new List<int>() : new List<int>(handTileIds)
            };
            Broadcast(new LanEnvelope { Type = "preview", Payload = LanJson.Serialize(payload) });
        }

        public void SendTextToSeat(Seat seat, string text)
        {
            List<TcpClient> targets;
            lock (_sync)
            {
                targets = _clientSeats
                    .Where(x => x.Value == seat)
                    .Select(x => x.Key)
                    .ToList();
            }

            foreach (var client in targets)
            {
                try
                {
                    var writer = new StreamWriter(client.GetStream());
                    writer.AutoFlush = true;
                    writer.WriteLine(LanJson.Serialize(new LanEnvelope { Type = "text", Payload = text }));
                }
                catch (IOException)
                {
                }
            }
        }

        public void BroadcastGameState(GameState state)
        {
            if (state != null)
            {
                Mode = state.Mode;
                UseNewAppearance = state.Mode == GameMode.NaneOkey && state.UseNewAppearance;
            }
            _lastGameSnapshot = new LanGameSnapshot
            {
                MatchId = MatchId,
                State = state == null ? null : LanGameStateDto.FromDomain(state.Clone()),
                EnableLivePreview = EnableLivePreview && Mode == GameMode.NaneOkey,
                EnableTurnTimer = EnableTurnTimer,
                TurnSeconds = TurnSeconds,
                BotThinkSeconds = BotThinkSeconds,
                TargetScore = TargetScore
            };
            Broadcast(new LanEnvelope
            {
                Type = "game",
                Payload = LanJson.Serialize(_lastGameSnapshot)
            });
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    lock (_sync)
                    {
                        _clients.Add(client);
                        _clientNames[client] = "Misafir";
                    }

                    RaiseLog("Yeni istemci baglandi.");
                    var thread = new Thread(() => HandleClient(client)) { IsBackground = true };
                    thread.Start();
                }
                catch (SocketException)
                {
                    return;
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                using (var reader = new StreamReader(client.GetStream()))
                {
                    while (_running && client.Connected)
                    {
                        var line = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            break;
                        }

                        var envelope = LanJson.Deserialize<LanEnvelope>(line);
                        if (envelope == null)
                        {
                            continue;
                        }
                        if (envelope.Type == "hello")
                        {
                            var hello = LanJson.Deserialize<LanHello>(envelope.Payload);
                            if (hello == null || hello.ProtocolVersion != LanProtocol.Version)
                            {
                                SendToClient(client, new LanEnvelope
                                {
                                    Type = "text",
                                    Payload = LanProtocol.VersionMismatchMessage(hello != null ? hello.ProtocolVersion : 0)
                                });
                                break;
                            }
                            if (hello == null || !string.Equals(hello.RoomName, _roomName, StringComparison.OrdinalIgnoreCase))
                            {
                                var writer = new StreamWriter(client.GetStream());
                                writer.AutoFlush = true;
                                writer.WriteLine(LanJson.Serialize(new LanEnvelope
                                {
                                    Type = "text",
                                    Payload = "Oda adı uyuşmuyor."
                                }));
                                break;
                            }

                            var normalizedName = NormalizePlayerName(hello.PlayerName);
                            lock (_sync)
                            {
                                _clientNames[client] = normalizedName;
                                if (!_clientSeats.ContainsKey(client))
                                {
                                    Seat availableSeat;
                                    if (!TryGetAvailableSeat(out availableSeat))
                                    {
                                        var writer = new StreamWriter(client.GetStream());
                                        writer.AutoFlush = true;
                                        writer.WriteLine(LanJson.Serialize(new LanEnvelope
                                        {
                                            Type = "text",
                                            Payload = "Bu odada boş ağ oyuncusu koltuğu kalmadı."
                                        }));
                                        break;
                                    }

                                    _clientSeats[client] = availableSeat;
                                }
                            }

                            RaiseLog("İstemci bağlandı: " + normalizedName);
                            var assigned = _clientSeats[client];
                            RemotePlayerNamed?.Invoke(assigned, normalizedName);
                            var assignmentWriter = new StreamWriter(client.GetStream());
                            assignmentWriter.AutoFlush = true;
                            assignmentWriter.WriteLine(LanJson.Serialize(new LanEnvelope
                            {
                                Type = "assign",
                                Payload = LanJson.Serialize(new LanSeatAssignment { ProtocolVersion = LanProtocol.Version, Seat = assigned.ToString() })
                            }));
                            if (_lastGameSnapshot != null)
                            {
                                assignmentWriter.WriteLine(LanJson.Serialize(new LanEnvelope
                                {
                                    Type = "game",
                                    Payload = LanJson.Serialize(_lastGameSnapshot)
                                }));
                            }
                        }
                        else if (envelope.Type == "text")
                        {
                            Seat chatSeat;
                            if (!TryGetClientSeat(client, out chatSeat))
                            {
                                continue;
                            }
                            string playerName;
                            lock (_sync)
                            {
                                playerName = _clientNames.ContainsKey(client) ? _clientNames[client] : "Misafir";
                            }

                            var chatText = "[" + playerName + "] " + envelope.Payload;
                            RaiseLog(chatText);
                            Broadcast(new LanEnvelope { Type = "text", Payload = chatText });
                        }
                        else if (envelope.Type == "draw")
                        {
                            Seat assignedSeat;
                            if (TryGetClientSeat(client, out assignedSeat))
                            {
                                var request = LanJson.Deserialize<LanDrawRequest>(envelope.Payload);
                                RemoteDrawRequested?.Invoke(
                                    assignedSeat,
                                    request != null ? request.TargetRow : -1,
                                    request != null ? request.TargetColumn : -1);
                            }
                        }
                        else if (envelope.Type == "pass")
                        {
                            Seat assignedSeat;
                            if (TryGetClientSeat(client, out assignedSeat))
                            {
                                RemotePassRequested?.Invoke(assignedSeat);
                            }
                        }
                        else if (envelope.Type == "commit")
                        {
                            Seat assignedSeat;
                            if (TryGetClientSeat(client, out assignedSeat))
                            {
                                var layout = LanJson.Deserialize<LanTurnLayout>(envelope.Payload);
                                RemoteCommitRequested?.Invoke(
                                    assignedSeat,
                                    layout != null && layout.Melds != null
                                        ? layout.Melds.ConvertAll(x => x.ToDomain())
                                        : new List<Meld>(),
                                    layout != null && layout.HandTileIds != null ? layout.HandTileIds : new List<int>());
                            }
                        }
                        else if (envelope.Type == "discard")
                        {
                            Seat assignedSeat;
                            if (TryGetClientSeat(client, out assignedSeat))
                            {
                                var request = LanJson.Deserialize<LanDiscardRequest>(envelope.Payload);
                                if (request != null)
                                {
                                    RemoteDiscardRequested?.Invoke(
                                        assignedSeat,
                                        request.TileId,
                                        request.FinishClassic,
                                        request.Melds != null ? request.Melds.ConvertAll(x => x.ToDomain()) : new List<Meld>(),
                                        request.HandTileIds ?? new List<int>());
                                }
                            }
                        }
                        else if (envelope.Type == "discard_draw")
                        {
                            Seat assignedSeat;
                            if (TryGetClientSeat(client, out assignedSeat))
                            {
                                RemoteDiscardDrawRequested?.Invoke(assignedSeat);
                            }
                        }
                        else if (envelope.Type == "preview")
                        {
                            Seat assignedSeat;
                            if (Mode == GameMode.NaneOkey && EnableLivePreview && TryGetClientSeat(client, out assignedSeat))
                            {
                                var preview = LanJson.Deserialize<LanTurnPreview>(envelope.Payload);
                                RemotePreviewRequested?.Invoke(
                                    assignedSeat,
                                    preview != null && preview.Melds != null
                                        ? preview.Melds.ConvertAll(x => x.ToDomain())
                                        : new List<Meld>(),
                                    preview != null && preview.HandTileIds != null ? preview.HandTileIds : new List<int>());
                            }
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (ArgumentException)
            {
                RaiseLog("LAN istemcisinden geçersiz ileti alındı; bağlantı kapatıldı.");
            }
            catch (InvalidOperationException)
            {
                RaiseLog("LAN istemcisinden geçersiz ileti alındı; bağlantı kapatıldı.");
            }
            finally
            {
                Seat disconnectedSeat;
                string disconnectedName;
                var hadSeat = false;
                lock (_sync)
                {
                    hadSeat = _clientSeats.TryGetValue(client, out disconnectedSeat);
                    disconnectedName = _clientNames.ContainsKey(client) ? _clientNames[client] : "Misafir";
                    _clients.Remove(client);
                    _clientNames.Remove(client);
                    _clientSeats.Remove(client);
                }

                try
                {
                    client.Close();
                }
                catch (IOException)
                {
                }

                if (hadSeat)
                {
                    RemotePlayerDisconnected?.Invoke(disconnectedSeat, disconnectedName);
                }
            }
        }

        private bool TryGetClientSeat(TcpClient client, out Seat seat)
        {
            lock (_sync)
            {
                return _clientSeats.TryGetValue(client, out seat);
            }
        }

        private static void SendToClient(TcpClient client, LanEnvelope envelope)
        {
            var writer = new StreamWriter(client.GetStream());
            writer.AutoFlush = true;
            writer.WriteLine(LanJson.Serialize(envelope));
        }

        private void Broadcast(LanEnvelope envelope)
        {
            var line = LanJson.Serialize(envelope);
            lock (_sync)
            {
                foreach (var client in _clients.ToList())
                {
                    if (!_clientSeats.ContainsKey(client))
                    {
                        continue;
                    }
                    try
                    {
                        var writer = new StreamWriter(client.GetStream());
                        writer.AutoFlush = true;
                        writer.WriteLine(line);
                    }
                    catch (IOException)
                    {
                        _clients.Remove(client);
                    }
                }
            }
        }

        private void RaiseLog(string text)
        {
            LogReceived?.Invoke(text);
        }

        private void BroadcastRoomLoop()
        {
            while (_running)
            {
                try
                {
                    using (var client = new UdpClient())
                    {
                        client.EnableBroadcast = true;
                        var room = new LanRoomAnnouncement
                        {
                            ProtocolVersion = LanProtocol.Version,
                            Mode = Mode,
                            UseNewAppearance = Mode == GameMode.NaneOkey && UseNewAppearance,
                            HostName = Dns.GetHostName(),
                            HostIp = LocalIpAddress,
                            HostIpCandidates = new List<string>(_localIpCandidates),
                            RoomName = _roomName,
                            Port = _port
                        };
                        var payload = System.Text.Encoding.UTF8.GetBytes(LanJson.Serialize(room));
                        foreach (var endpoint in LanDiscoveryService.GetBroadcastEndpoints())
                        {
                            try
                            {
                                client.Send(payload, payload.Length, endpoint);
                            }
                            catch (SocketException)
                            {
                            }
                        }
                    }
                    Thread.Sleep(3000);
                }
                catch (SocketException)
                {
                    return;
                }
            }
        }

        public void RespondToDiscovery(IPEndPoint remoteEndPoint)
        {
            if (!_running || remoteEndPoint == null)
            {
                return;
            }

            var room = new LanRoomAnnouncement
            {
                ProtocolVersion = LanProtocol.Version,
                Mode = Mode,
                UseNewAppearance = Mode == GameMode.NaneOkey && UseNewAppearance,
                HostName = Dns.GetHostName(),
                HostIp = LocalIpAddress,
                HostIpCandidates = new List<string>(_localIpCandidates),
                RoomName = _roomName,
                Port = _port
            };
            var payload = System.Text.Encoding.UTF8.GetBytes(LanJson.Serialize(room));
            using (var client = new UdpClient())
            {
                client.Send(payload, payload.Length, new IPEndPoint(remoteEndPoint.Address, LanDiscoveryService.DiscoveryPort));
            }
        }

        private TcpListener StartListenerWithFallback(int requestedPort)
        {
            SocketException lastError = null;
            for (var port = requestedPort; port < requestedPort + 128; port++)
            {
                var listener = new TcpListener(IPAddress.Any, port);
                try
                {
                    listener.Start();
                    _port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    return listener;
                }
                catch (SocketException ex)
                {
                    lastError = ex;
                    try
                    {
                        listener.Stop();
                    }
                    catch (SocketException)
                    {
                    }
                }
            }

            try
            {
                var listener = new TcpListener(IPAddress.Any, 0);
                listener.Start();
                _port = ((IPEndPoint)listener.LocalEndpoint).Port;
                return listener;
            }
            catch (SocketException ex)
            {
                lastError = ex;
            }

            throw lastError ?? new SocketException((int)SocketError.AddressAlreadyInUse);
        }

        private static List<Seat> BuildAssignableRemoteSeats(IEnumerable<PlayerState> players)
        {
            var result = new List<Seat>();
            if (players == null)
            {
                return result;
            }

            foreach (var player in players
                .Where(x => x.IsActive && x.Seat != Seat.South && x.Type == PlayerType.Remote)
                .OrderBy(x => (int)x.Seat))
            {
                if (!result.Contains(player.Seat))
                {
                    result.Add(player.Seat);
                }
            }

            foreach (var player in players
                .Where(x => x.IsActive && x.Seat != Seat.South && x.Type == PlayerType.Bot)
                .OrderBy(x => (int)x.Seat))
            {
                if (!result.Contains(player.Seat))
                {
                    result.Add(player.Seat);
                }
            }

            return result;
        }

        private bool TryGetAvailableSeat(out Seat seat)
        {
            var usedSeats = new HashSet<Seat>(_clientSeats.Values);
            foreach (var candidate in _remoteSeats)
            {
                if (!usedSeats.Contains(candidate))
                {
                    seat = candidate;
                    return true;
                }
            }

            seat = Seat.South;
            return false;
        }

        private static List<string> ResolveLocalIpCandidates()
        {
            var addresses = new List<string>();
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var endPoint = socket.LocalEndPoint as IPEndPoint;
                    if (endPoint != null && endPoint.Address != null && !IPAddress.IsLoopback(endPoint.Address))
                    {
                        addresses.Add(endPoint.Address.ToString());
                    }
                }
            }
            catch (SocketException)
            {
            }

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel ||
                    IsVirtualInterface(networkInterface))
                {
                    continue;
                }

                var properties = networkInterface.GetIPProperties();
                foreach (var unicast in properties.UnicastAddresses)
                {
                    var address = unicast.Address;
                    if (address == null ||
                        address.AddressFamily != AddressFamily.InterNetwork ||
                        IPAddress.IsLoopback(address))
                    {
                        continue;
                    }

                    var addressText = address.ToString();
                    if (!addresses.Contains(addressText))
                    {
                        addresses.Add(addressText);
                    }
                }
            }

            if (addresses.Count == 0)
            {
                try
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (var address in host.AddressList)
                    {
                        if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                        {
                            var addressText = address.ToString();
                            if (!addresses.Contains(addressText))
                            {
                                addresses.Add(addressText);
                            }
                        }
                    }
                }
                catch (SocketException)
                {
                }
            }

            if (addresses.Count == 0)
            {
                addresses.Add("127.0.0.1");
            }

            return addresses;
        }

        private static bool IsVirtualInterface(NetworkInterface networkInterface)
        {
            var text = ((networkInterface.Name ?? string.Empty) + " " + (networkInterface.Description ?? string.Empty)).ToLowerInvariant();
            return text.Contains("virtual") ||
                   text.Contains("hyper-v") ||
                   text.Contains("vmware") ||
                   text.Contains("vbox") ||
                   text.Contains("vethernet") ||
                   text.Contains("wsl") ||
                   text.Contains("docker") ||
                   text.Contains("loopback") ||
                   text.Contains("tap-") ||
                   text.Contains("tailscale") ||
                   text.Contains("zerotier");
        }

        private static string NormalizePlayerName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Misafir";
            }

            var normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length == 0)
            {
                return "Misafir";
            }

            if (normalized.Length > 24)
            {
                normalized = normalized.Substring(0, 24);
                while (normalized.Length > 0 && normalized[normalized.Length - 1] == ' ')
                {
                    normalized = normalized.Substring(0, normalized.Length - 1);
                }
            }

            return normalized;
        }

        public void Dispose()
        {
            _running = false;
            try
            {
                if (_listener != null)
                {
                    _listener.Stop();
                }
            }
            catch (SocketException)
            {
            }

            lock (_sync)
            {
                foreach (var client in _clients)
                {
                    client.Close();
                }

                _clients.Clear();
            }
        }
    }
}
