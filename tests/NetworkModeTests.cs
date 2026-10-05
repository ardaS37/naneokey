using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using NaneOkey.Domain;
using NaneOkey.Network;

internal static class NetworkModeTests
{
    private static int _checks;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }

    private static GameState Sample(GameMode mode)
    {
        var state = new GameState
        {
            Mode = mode,
            Indicator = new Tile(1, TileColor.Blue, 13),
            CurrentTurn = Seat.West,
            HasDrawnThisTurn = true,
            DrawnDiscardTileId = 8,
            EndedByStock = true,
            RoundMultiplier = 2,
            TurnInProgress = true,
            LastAction = "Ağ denemesi"
        };
        var player = new PlayerState(Seat.West, "Garp", PlayerType.Remote, BotDifficulty.Hard)
        {
            HasOpened = true,
            OpenedWithPairs = true,
            RoundPenalty = 404
        };
        player.Hand.Add(new Tile(2, TileColor.Blue, 1) { IsJoker = true, JokerColor = TileColor.Yellow, JokerNumber = 7 });
        player.Hand.Add(new Tile(3, TileColor.Black, 0) { IsFalseJoker = true });
        state.Players.Add(player);
        var meld = new Meld(player.Hand) { OwnerSeat = Seat.West, IsPair = true, BoardRow = 2, StartColumn = 4 };
        state.Table.Add(meld);
        state.TurnTable.Add(meld.Clone());
        state.TurnHand.Add(player.Hand[0].Clone());
        state.Deck.Add(new Tile(4, TileColor.Red, 9));
        var pile = new DiscardPile(Seat.North);
        pile.Tiles.Add(new Tile(8, TileColor.Yellow, 7));
        state.DiscardPiles.Add(pile);
        state.OriginalHandIds.AddRange(new[] { 2, 3 });
        state.OriginalTableIds.AddRange(new[] { 5, 6, 7 });
        return state;
    }

    private static void RoundTrip()
    {
        foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
        {
            var source = new LanGameSnapshot
            {
                State = LanGameStateDto.FromDomain(Sample(mode)),
                TargetScore = 543,
                TurnSeconds = 40,
                BotThinkSeconds = 20,
                EnableTurnTimer = true
            };
            var received = LanJson.Deserialize<LanGameSnapshot>(LanJson.Serialize(source));
            var state = received.State.ToDomain();
            Check(received.ProtocolVersion == 4 && received.TargetScore == 543, "Sürüm/hedef puan kayboldu.");
            Check(state.Mode == mode && state.Indicator.Number == 13, "Oyun modu/gösterge kayboldu.");
            Check(state.HasDrawnThisTurn && state.DrawnDiscardTileId == 8 && state.EndedByStock && state.RoundMultiplier == 2, "Tur metadata kayboldu.");
            Check(state.DiscardPiles.Count == 1 && state.DiscardPiles[0].Seat == Seat.North && state.DiscardPiles[0].Tiles[0].Id == 8, "Atılan taşlar kayboldu.");
            Check(state.Players[0].OpenedWithPairs && state.Players[0].RoundPenalty == 404 && state.Players[0].HasOpened, "Oyuncu açılış/ceza kayboldu.");
            Check(state.Players[0].Hand[0].IsJoker && state.Players[0].Hand[0].JokerColor == TileColor.Yellow && state.Players[0].Hand[0].JokerNumber == 7, "Joker temsili kayboldu.");
            Check(state.Players[0].Hand[1].IsFalseJoker, "Sahte okey kayboldu.");
            Check(state.Table[0].IsPair && state.Table[0].OwnerSeat == Seat.West && state.Table[0].BoardRow == 2 && state.Table[0].StartColumn == 4, "Per metadata kayboldu.");
            Check(LanJson.Serialize(LanGameStateDto.FromDomain(state)) == LanJson.Serialize(source.State), "State roundtrip eksik alan içeriyor.");
        }
    }

    private static void LanActionsAndVersion()
    {
        var players = new[]
        {
            new PlayerState(Seat.South, "Şark", PlayerType.Human, BotDifficulty.Easy),
            new PlayerState(Seat.West, "Garp", PlayerType.Remote, BotDifficulty.Easy),
            new PlayerState(Seat.North, "Şimal", PlayerType.Remote, BotDifficulty.Easy)
        };
        using (var host = new LanHost { Mode = GameMode.ClassicOkey, EnableLivePreview = true, TargetScore = 543 })
        using (var client = new LanClient())
        using (var assigned = new ManualResetEvent(false))
        using (var discarded = new ManualResetEvent(false))
        using (var discardDrawn = new ManualResetEvent(false))
        using (var snapshotReceived = new ManualResetEvent(false))
        using (var previewReceived = new ManualResetEvent(false))
        {
            Seat discardSeat = Seat.South;
            Seat drawSeat = Seat.South;
            int tileId = -1;
            bool finish = false;
            LanGameSnapshot lastSnapshot = null;
            client.SeatAssigned += seat => assigned.Set();
            client.GameStateReceived += (state, snapshot) => { lastSnapshot = snapshot; snapshotReceived.Set(); };
            host.RemoteDiscardRequested += (seat, tile, classic, melds, hand) =>
            {
                discardSeat = seat;
                tileId = tile;
                finish = classic;
                discarded.Set();
            };
            host.RemoteDiscardDrawRequested += seat => { drawSeat = seat; discardDrawn.Set(); };
            host.RemotePreviewRequested += (seat, melds, hand) => previewReceived.Set();
            host.Start(0, "Test Masa", players);
            host.BroadcastGameState(Sample(GameMode.ClassicOkey));
            client.Connect("127.0.0.1", host.Port, "Ağ Oyuncusu", "Test Masa", 3000);
            Check(assigned.WaitOne(3000), "LAN koltuk atamadı.");
            Check(snapshotReceived.WaitOne(3000), "LAN state göndermedi.");
            Check(client.Mode == GameMode.ClassicOkey && !lastSnapshot.EnableLivePreview && lastSnapshot.TargetScore == 543, "Klasik preview/hedef puan yanlış.");
            client.SendDiscardRequest(Seat.South, 88, true, new List<Meld>(), new List<int>());
            Check(discarded.WaitOne(3000), "LAN taş atma aksiyonu taşınmadı.");
            Check(discardSeat == Seat.West && tileId == 88 && finish, "LAN payload koltuğuna güvenildi veya taş/bitirme kayboldu.");
            client.SendDiscardDrawRequest();
            Check(discardDrawn.WaitOne(3000) && drawSeat == Seat.West, "LAN yerden çekme koltuğu yanlış.");
            client.SendPreviewRequest(Seat.West, new List<Meld>(), new List<int>());
            Check(!previewReceived.WaitOne(100), "Klasik okey gizli eli preview ile sızdı.");

            using (var rawClient = new TcpClient("127.0.0.1", host.Port))
            {
                rawClient.ReceiveTimeout = 3000;
                var writer = new StreamWriter(rawClient.GetStream()) { AutoFlush = true };
                discarded.Reset();
                writer.WriteLine(LanJson.Serialize(new LanEnvelope
                {
                    Type = "discard",
                    Payload = LanJson.Serialize(new LanDiscardRequest { Seat = "South", TileId = 99 })
                }));
                Check(!discarded.WaitOne(100), "Koltuk atanmadan LAN hamlesi kabul edildi.");
                writer.WriteLine(LanJson.Serialize(new LanEnvelope
                {
                    Type = "hello",
                    Payload = LanJson.Serialize(new LanHello { PlayerName = "Ham İstemci", RoomName = "Test Masa", ProtocolVersion = 4 })
                }));
                var reader = new StreamReader(rawClient.GetStream());
                LanEnvelope reply;
                do { reply = LanJson.Deserialize<LanEnvelope>(reader.ReadLine()); } while (reply.Type != "assign");
                writer.WriteLine(LanJson.Serialize(new LanEnvelope
                {
                    Type = "preview",
                    Payload = LanJson.Serialize(new LanTurnPreview { Seat = "North", Melds = new List<LanMeldDto>(), HandTileIds = new List<int> { 99 } })
                }));
                Check(!previewReceived.WaitOne(100), "Klasik preview sunucuda engellenmedi.");
            }

            using (var oldClient = new TcpClient("127.0.0.1", host.Port))
            {
                oldClient.ReceiveTimeout = 3000;
                var writer = new StreamWriter(oldClient.GetStream()) { AutoFlush = true };
                writer.WriteLine(LanJson.Serialize(new LanEnvelope
                {
                    Type = "hello",
                    Payload = LanJson.Serialize(new LanHello { PlayerName = "Eski Sürüm", RoomName = "Test Masa", ProtocolVersion = 0 })
                }));
                var reader = new StreamReader(oldClient.GetStream());
                var reply = LanJson.Deserialize<LanEnvelope>(reader.ReadLine());
                Check(reply.Type == "text" && reply.Payload.Contains("4.0.0.0"), "Eski LAN sürümü anlaşılır şekilde reddedilmedi.");
            }
        }
    }

    public static int Main()
    {
        try
        {
            RoundTrip();
            LanActionsAndVersion();
            Console.WriteLine("NetworkModeTests: " + _checks + " kontrol geçti.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
