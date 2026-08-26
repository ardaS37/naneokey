using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using NaneOkey.Domain;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NaneOkey.Network
{
    public sealed class LanClient : IDisposable
    {
        private TcpClient _client;
        private Thread _listenThread;

        public event Action<string> LogReceived;
        public event Action<LanLobbySnapshot> LobbyReceived;
        public event Action<GameState, LanGameSnapshot> GameStateReceived;
        public event Action<Seat> SeatAssigned;
        public event Action<Seat, IList<Meld>, IList<int>> PreviewReceived;

        public bool IsConnected
        {
            get { return _client != null && _client.Connected; }
        }

        public void Connect(string host, int port, string playerName, string roomName)
        {
            Connect(host, port, playerName, roomName, 800);
        }

        public void Connect(string host, int port, string playerName, string roomName, int timeoutMilliseconds)
        {
            if (IsConnected)
            {
                return;
            }

            LogReceived?.Invoke("Bağlantı deneniyor: " + host + ":" + port);
            _client = new TcpClient();
            var asyncResult = _client.BeginConnect(host, port, null, null);
            if (!asyncResult.AsyncWaitHandle.WaitOne(timeoutMilliseconds))
            {
                try
                {
                    _client.Close();
                }
                catch (IOException)
                {
                }

                _client = null;
                LogReceived?.Invoke("Bağlantı zaman aşımına uğradı: " + host + ":" + port);
                throw new SocketException((int)SocketError.TimedOut);
            }

            _client.EndConnect(asyncResult);
            var remoteEndPoint = _client.Client.RemoteEndPoint as IPEndPoint;
            Send(new LanEnvelope
            {
                Type = "hello",
                Payload = LanJson.Serialize(new LanHello
                {
                    PlayerName = playerName,
                    RoomName = roomName
                })
            });
            _listenThread = new Thread(ListenLoop) { IsBackground = true };
            _listenThread.Start();
            LogReceived?.Invoke("LAN host'a baglanildi: " + (remoteEndPoint != null ? remoteEndPoint.ToString() : host + ":" + port));
        }

        public void SendText(string text)
        {
            if (!IsConnected)
            {
                return;
            }

            Send(new LanEnvelope { Type = "text", Payload = text });
        }

        public void SendDrawRequest(int targetRow, int targetColumn)
        {
            if (!IsConnected)
            {
                return;
            }

            Send(new LanEnvelope
            {
                Type = "draw",
                Payload = LanJson.Serialize(new LanDrawRequest
                {
                    TargetRow = targetRow,
                    TargetColumn = targetColumn
                })
            });
        }

        public void SendPassRequest()
        {
            if (!IsConnected)
            {
                return;
            }

            Send(new LanEnvelope { Type = "pass", Payload = string.Empty });
        }

        public void SendCommitRequest(Seat seat, IList<Meld> melds, IList<int> handTileIds)
        {
            if (!IsConnected)
            {
                return;
            }

            Send(new LanEnvelope
            {
                Type = "commit",
                Payload = LanJson.Serialize(new LanTurnLayout
                {
                    Seat = seat.ToString(),
                    Melds = melds == null ? new List<LanMeldDto>() : melds.Select(LanMeldDto.FromDomain).ToList(),
                    HandTileIds = handTileIds == null ? new List<int>() : new List<int>(handTileIds)
                })
            });
        }

        public void SendPreviewRequest(Seat seat, IList<Meld> melds, IList<int> handTileIds)
        {
            if (!IsConnected)
            {
                return;
            }

            Send(new LanEnvelope
            {
                Type = "preview",
                Payload = LanJson.Serialize(new LanTurnPreview
                {
                    Seat = seat.ToString(),
                    Melds = melds == null ? new List<LanMeldDto>() : melds.Select(LanMeldDto.FromDomain).ToList(),
                    HandTileIds = handTileIds == null ? new List<int>() : new List<int>(handTileIds)
                })
            });
        }

        private void ListenLoop()
        {
            try
            {
                using (var reader = new StreamReader(_client.GetStream()))
                {
                    while (_client.Connected)
                    {
                        var line = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            break;
                        }

                        var envelope = LanJson.Deserialize<LanEnvelope>(line);
                        if (envelope.Type == "lobby")
                        {
                            LobbyReceived?.Invoke(LanJson.Deserialize<LanLobbySnapshot>(envelope.Payload));
                        }
                        else if (envelope.Type == "assign")
                        {
                            var assignment = LanJson.Deserialize<LanSeatAssignment>(envelope.Payload);
                            Seat seat;
                            if (assignment != null && Enum.TryParse(assignment.Seat, out seat))
                            {
                                SeatAssigned?.Invoke(seat);
                            }
                        }
                        else if (envelope.Type == "game")
                        {
                            var snapshot = LanJson.Deserialize<LanGameSnapshot>(envelope.Payload);
                            if (snapshot != null && snapshot.State != null)
                            {
                                GameStateReceived?.Invoke(snapshot.State.ToDomain(), snapshot);
                            }
                        }
                        else if (envelope.Type == "preview")
                        {
                            var preview = LanJson.Deserialize<LanTurnPreview>(envelope.Payload);
                            Seat seat;
                            if (preview != null && Enum.TryParse(preview.Seat, out seat))
                            {
                                var melds = preview.Melds != null
                                    ? preview.Melds.ConvertAll(x => x.ToDomain())
                                    : new List<Meld>();
                                PreviewReceived?.Invoke(seat, melds, preview.HandTileIds ?? new List<int>());
                            }
                        }
                        else if (envelope.Type == "text")
                        {
                            LogReceived?.Invoke(envelope.Payload);
                        }
                    }
                }
            }
            catch (IOException)
            {
                LogReceived?.Invoke("LAN baglantisi koptu.");
            }
        }

        private void Send(LanEnvelope envelope)
        {
            var writer = new StreamWriter(_client.GetStream());
            writer.AutoFlush = true;
            writer.WriteLine(LanJson.Serialize(envelope));
        }

        public void Dispose()
        {
            try
            {
                if (_client != null)
                {
                    _client.Close();
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
