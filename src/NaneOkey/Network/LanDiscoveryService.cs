using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;

namespace NaneOkey.Network
{
    public sealed class LanDiscoveryService : IDisposable
    {
        public const string DiscoveryAppName = "NaneOkey";
        public const int DiscoveryPort = 51235;
        private static readonly TimeSpan RoomEntryLifetime = TimeSpan.FromSeconds(10);
        private readonly Dictionary<string, LanRoomAnnouncement> _rooms = new Dictionary<string, LanRoomAnnouncement>();
        private readonly Dictionary<string, DateTime> _roomLastSeen = new Dictionary<string, DateTime>();
        private UdpClient _listener;
        private Thread _listenThread;
        private bool _running;

        public event Action<List<LanRoomAnnouncement>> RoomsUpdated;
        public event Action<IPEndPoint> QueryReceived;

        public bool IsListening
        {
            get { return _running; }
        }

        public void StartListening()
        {
            if (_running)
            {
                return;
            }

            _listener = new UdpClient();
            _listener.Client.ExclusiveAddressUse = false;
            _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
            _listener.EnableBroadcast = true;
            _running = true;
            _listenThread = new Thread(ListenLoop) { IsBackground = true };
            _listenThread.Start();
        }

        public void QueryRooms()
        {
            PublishFreshRooms();
            var payload = new LanEnvelope
            {
                Type = "discover",
                Payload = LanJson.Serialize(new LanDiscoveryQuery { App = DiscoveryAppName, ProtocolVersion = LanProtocol.Version })
            };
            BroadcastPayload(LanJson.Serialize(payload));
        }

        public void BroadcastRoom(LanRoomAnnouncement room)
        {
            BroadcastPayload(LanJson.Serialize(room));
        }

        private void ListenLoop()
        {
            var endpoint = new IPEndPoint(IPAddress.Any, DiscoveryPort);
            while (_running)
            {
                try
                {
                    var bytes = _listener.Receive(ref endpoint);
                    var payload = Encoding.UTF8.GetString(bytes);
                    var envelope = LanJson.Deserialize<LanEnvelope>(payload);
                    if (envelope != null && string.Equals(envelope.Type, "discover", StringComparison.OrdinalIgnoreCase))
                    {
                        var query = LanJson.Deserialize<LanDiscoveryQuery>(envelope.Payload);
                        if (query != null && string.Equals(query.App, DiscoveryAppName, StringComparison.OrdinalIgnoreCase))
                        {
                            QueryReceived?.Invoke(new IPEndPoint(endpoint.Address, endpoint.Port));
                        }
                        continue;
                    }

                    var room = LanJson.Deserialize<LanRoomAnnouncement>(payload);
                    if (room == null || string.IsNullOrWhiteSpace(room.RoomName) || string.IsNullOrWhiteSpace(room.HostIp))
                    {
                        continue;
                    }

                    if (endpoint != null &&
                        endpoint.Address != null &&
                        endpoint.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(endpoint.Address))
                    {
                        var endpointIp = endpoint.Address.ToString();
                        room.HostIp = endpointIp;
                        if (room.HostIpCandidates == null)
                        {
                            room.HostIpCandidates = new List<string>();
                        }

                        if (!room.HostIpCandidates.Contains(endpointIp))
                        {
                            room.HostIpCandidates.Insert(0, endpointIp);
                        }
                    }

                    lock (_rooms)
                    {
                        var roomKey = BuildRoomKey(room);
                        _rooms[roomKey] = room;
                        _roomLastSeen[roomKey] = DateTime.UtcNow;
                        RoomsUpdated?.Invoke(GetFreshRoomsLocked());
                    }
                }
                catch (SocketException)
                {
                    return;
                }
                catch (ArgumentException)
                {
                    // Discovery shares a UDP port with all machines on the LAN.
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        private static string BuildRoomKey(LanRoomAnnouncement room)
        {
            return room.HostIp + ":" + room.RoomName;
        }

        private void PublishFreshRooms()
        {
            lock (_rooms)
            {
                RoomsUpdated?.Invoke(GetFreshRoomsLocked());
            }
        }

        private List<LanRoomAnnouncement> GetFreshRoomsLocked()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _roomLastSeen
                .Where(x => now - x.Value > RoomEntryLifetime)
                .Select(x => x.Key)
                .ToList();

            foreach (var expiredKey in expiredKeys)
            {
                _roomLastSeen.Remove(expiredKey);
                _rooms.Remove(expiredKey);
            }

            return _rooms.Values
                .OrderBy(x => x.RoomName)
                .ThenBy(x => x.HostIp)
                .ToList();
        }

        private static void BroadcastPayload(string payload)
        {
            using (var client = new UdpClient())
            {
                client.EnableBroadcast = true;
                var bytes = Encoding.UTF8.GetBytes(payload);
                foreach (var endpoint in GetBroadcastEndpoints())
                {
                    try
                    {
                        client.Send(bytes, bytes.Length, endpoint);
                    }
                    catch (SocketException)
                    {
                    }
                }
            }
        }

        public static List<IPEndPoint> GetBroadcastEndpoints()
        {
            var endpoints = new Dictionary<string, IPEndPoint>();
            endpoints[IPAddress.Broadcast + ":" + DiscoveryPort] = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                var properties = networkInterface.GetIPProperties();
                foreach (var unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address == null ||
                        unicast.IPv4Mask == null ||
                        unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    var broadcast = GetBroadcastAddress(unicast.Address, unicast.IPv4Mask);
                    if (broadcast == null)
                    {
                        continue;
                    }

                    endpoints[broadcast + ":" + DiscoveryPort] = new IPEndPoint(broadcast, DiscoveryPort);
                }
            }

            return endpoints.Values.ToList();
        }

        private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
        {
            var addressBytes = address.GetAddressBytes();
            var maskBytes = subnetMask.GetAddressBytes();
            if (addressBytes.Length != maskBytes.Length)
            {
                return null;
            }

            var broadcastBytes = new byte[addressBytes.Length];
            for (var index = 0; index < addressBytes.Length; index++)
            {
                broadcastBytes[index] = (byte)(addressBytes[index] | (maskBytes[index] ^ 255));
            }

            return new IPAddress(broadcastBytes);
        }

        public void Dispose()
        {
            _running = false;
            try
            {
                if (_listener != null)
                {
                    _listener.Close();
                }
            }
            catch (SocketException)
            {
            }
        }
    }
}
