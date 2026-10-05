using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NaneOkey.Domain;
using NaneOkey.Engine;

internal static class TraditionalEngineTests
{
    private static int _id = 200;
    private static int _assertions;
    private static Tile T(TileColor color, int number) { return new Tile(_id++, color, number); }
    private static Tile R(int number) { return T(TileColor.Red, number); }
    private static Meld M(params Tile[] tiles) { return new Meld(tiles); }
    private static void Check(bool condition, string name)
    {
        _assertions++;
        if (!condition) throw new Exception(name);
    }
    private static GameSettings Settings(GameMode mode, bool bots)
    {
        var settings = new GameSettings { Mode = mode, StartingHandSize = 15 };
        foreach (Seat seat in Enum.GetValues(typeof(Seat)))
            settings.Players.Add(new PlayerSetup { Name = seat.ToString(), Type = bots ? PlayerType.Bot : PlayerType.Human, Difficulty = BotDifficulty.Medium, IsActive = true });
        return settings;
    }
    private static GameEngine Controlled(GameMode mode, IEnumerable<Tile> hand)
    {
        var state = new GameState { Mode = mode, CurrentTurn = Seat.South, HasDrawnThisTurn = true, Indicator = R(4) };
        foreach (Seat seat in Enum.GetValues(typeof(Seat)))
        {
            state.Players.Add(new PlayerState(seat, seat.ToString(), PlayerType.Human, BotDifficulty.Medium));
            state.DiscardPiles.Add(new DiscardPile(seat));
        }
        state.Players[0].Hand.AddRange(hand);
        state.Deck.AddRange(new[] { R(2), R(3), R(4) });
        var engine = new GameEngine(); engine.Restore(state); return engine;
    }
    private static List<Tile> Exact101()
    {
        var tiles = new List<Tile>();
        foreach (TileColor color in Enum.GetValues(typeof(TileColor))) tiles.Add(T(color, 13));
        foreach (TileColor color in Enum.GetValues(typeof(TileColor))) tiles.Add(T(color, 7));
        tiles.AddRange(new[] { T(TileColor.Blue, 6), T(TileColor.Blue, 7), T(TileColor.Blue, 8) });
        return tiles;
    }
    private static void Stage101(GameEngine engine, List<Tile> tiles)
    {
        engine.BeginTurn(Seat.South);
        Check(engine.CreateMeldFromHand(Seat.South, tiles.Take(4).Select(x => x.Id).ToList()), "13 group");
        Check(engine.CreateMeldFromHand(Seat.South, tiles.Skip(4).Take(4).Select(x => x.Id).ToList()), "7 group");
        Check(engine.CreateMeldFromHand(Seat.South, tiles.Skip(8).Select(x => x.Id).ToList()), "6-7-8 run");
    }
    public static int Main()
    {
        try
        {
            TestRules(); TestDeal(); TestTurnPhases(); TestRightwardTurns(); TestClassicFinish(); Test101Opening(); Test101Table(); Test101JokerReplacement(); Test101PairProcessing(); TestDiscardAndTimeout(); TestStock(); TestNaneRegression(); TestBotJokerReplacement(); TestBots();
            Console.WriteLine("PASS: " + _assertions + " assertions."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void TestRules()
    {
        var classic = new TraditionalRuleValidator(GameMode.ClassicOkey);
        var rules101 = new TraditionalRuleValidator(GameMode.Okey101);
        Check(classic.IsValidMeld(M(R(12), R(13), R(1))), "classic ace high");
        Check(!rules101.IsValidMeld(M(R(12), R(13), R(1))), "101 no ace high");
        Check(!classic.IsValidMeld(M(R(13), R(1), R(2))), "no wrap to two");
        Check(classic.IsValidMeld(M(R(1), R(2), R(3))), "ace low");
        Check(!classic.IsValidMeld(M(R(7), R(7), T(TileColor.Blue, 7))), "distinct colors required");
        var joker = R(5); joker.IsJoker = true;
        var fake = R(5); fake.IsFalseJoker = true;
        Check(classic.IsValidMeld(M(R(6), joker, R(8))), "true joker wild");
        Check(!classic.IsValidMeld(M(R(6), fake, R(8))), "false joker not wild");
        Check(classic.IsValidMeld(M(R(4), fake, R(6))), "false joker takes okey value");
        var secondJoker = R(5); secondJoker.IsJoker = true;
        Check(rules101.MeldValue(M(R(1), joker, secondJoker)) == 6, "ambiguous two jokers choose opening's greatest valid score");
        var same = R(2); Check(!classic.IsValidMeld(M(same, same, R(3))), "same physical tile rejected");
        Check(!rules101.IsValidMeld(M(R(4), T(TileColor.Blue, 4))), "pair needs same color");
        Check(rules101.IsValidMeld(M(R(4), joker)), "joker pair");
        var locked = classic.NormalizeMeld(M(R(6), joker, R(8)));
        Check(locked.Tiles.Single(x => x.IsJoker).JokerNumber == 7, "joker representation normalized");
        Check(!rules101.IsValidMeld(M(locked.Tiles.Single(x => x.IsJoker), R(9), R(10))), "fixed joker cannot move");
    }
    private static void TestDeal()
    {
        foreach (var mode in new[] { GameMode.ClassicOkey, GameMode.Okey101 })
        {
            var engine = new GameEngine(); engine.StartNewGame(Settings(mode, false));
            var state = engine.State;
            Check(state.Players[0].Hand.Count == (mode == GameMode.ClassicOkey ? 15 : 22), "starter count");
            Check(state.Players.Skip(1).All(x => x.Hand.Count == (mode == GameMode.ClassicOkey ? 14 : 21)), "other counts");
            var tiles = state.Players.SelectMany(x => x.Hand).Concat(state.Deck).Concat(new[] { state.Indicator }).ToList();
            Check(tiles.Count == 106 && tiles.Select(x => x.Id).Distinct().Count() == 106, "106 unique tiles including indicator");
            Check(tiles.Count(x => x.IsFalseJoker) == 2 && tiles.Count(x => x.IsJoker) == 2, "2 false and true jokers");
            Check(state.DiscardPiles.Count == 4 && state.HasDrawnThisTurn, "initial phase and piles");
            var clone = state.Clone(); clone.Indicator.IsJoker = true;
            Check(!state.Indicator.IsJoker && clone.Mode == mode, "deep clone metadata");
        }
    }
    private static void TestTurnPhases()
    {
        var engine = new GameEngine(); engine.StartNewGame(Settings(GameMode.ClassicOkey, false));
        string message;
        Check(!engine.DrawTile(Seat.South, out message), "starter cannot draw twice");
        Check(engine.DiscardTile(Seat.South, engine.State.Players[0].Hand[0].Id, out message), "starter discard");
        Check(engine.State.CurrentTurn == Seat.East && !engine.State.HasDrawnThisTurn, "discard advances right once");
        Check(!engine.DiscardTile(Seat.East, engine.State.Players[3].Hand[0].Id, out message), "must draw first");
        Check(engine.DrawTile(Seat.East, out message) && engine.State.CurrentTurn == Seat.East, "draw retains turn");
        Check(!engine.DrawTile(Seat.East, out message), "one draw only");
        Check(engine.DiscardTile(Seat.East, engine.State.Players[3].Hand[0].Id, out message), "mandatory discard");
        Check(!engine.DrawTile(Seat.East, out message), "wrong owner rejected");
    }

    private static void TestRightwardTurns()
    {
        foreach (var mode in new[] { GameMode.ClassicOkey, GameMode.Okey101 })
        foreach (Seat start in Enum.GetValues(typeof(Seat)))
        {
            var engine = new GameEngine(); engine.StartNewGame(Settings(mode, false));
            var state = engine.State;
            var baseSize = mode == GameMode.ClassicOkey ? 14 : 21;
            foreach (var player in state.Players)
            {
                while (player.Hand.Count > baseSize) { state.Deck.Add(player.Hand.Last()); player.Hand.RemoveAt(player.Hand.Count - 1); }
            }
            state.CurrentTurn = start; state.HasDrawnThisTurn = false;
            string message;
            for (var step = 0; step < 4; step++)
            {
                var seat = (Seat)(((int)start - step + 4) % 4);
                Check(state.CurrentTurn == seat, mode + " rotates South/East/North/West from " + start);
                Check(engine.DrawTile(seat, out message), mode + " next right player draws");
                var discard = state.Players[(int)seat].Hand.Last();
                Check(engine.DiscardTile(seat, discard.Id, out message), mode + " rightward turn commits discard");
                Check(state.DiscardPiles[(int)seat].Tiles.Last().Id == discard.Id, mode + " preserves discard owner");
            }
            Check(state.CurrentTurn == start, mode + " returns to first player after rightward cycle");
            var previous = (Seat)(((int)start + 1) % 4);
            var taken = state.DiscardPiles[(int)previous].Tiles.Last();
            Check(engine.DrawDiscard(start, out message) && state.DrawnDiscardTileId == taken.Id,
                mode + " takes left player's discard for seat " + start);

            // Empty seats do not become a turn or an inaccessible discard source.
            engine = new GameEngine(); engine.StartNewGame(Settings(mode, false)); state = engine.State;
            state.CurrentTurn = start;
            var missingRight = (Seat)(((int)start + 3) % 4);
            state.Players[(int)missingRight].IsActive = false;
            var starter = state.Players[(int)start];
            if (starter.Hand.Count == baseSize)
            { starter.Hand.Add(state.Deck[0]); state.Deck.RemoveAt(0); }
            state.HasDrawnThisTurn = true;
            var starterDiscard = starter.Hand[0];
            Check(engine.DiscardTile(start, starterDiscard.Id, out message) && state.CurrentTurn == (Seat)(((int)start + 2) % 4),
                mode + " skips inactive right seat " + missingRight);
            Check(engine.DrawDiscard(state.CurrentTurn, out message) && state.DrawnDiscardTileId == starterDiscard.Id && state.DiscardPiles[(int)start].Tiles.Count == 0,
                mode + " takes prior active player's discard across empty seat");
        }
    }
    private static void TestClassicFinish()
    {
        var hand = new List<Tile>();
        for (var n = 1; n <= 7; n++) { hand.Add(R(n)); hand.Add(R(n)); }
        var discard = T(TileColor.Blue, 13); hand.Add(discard);
        var engine = Controlled(GameMode.ClassicOkey, hand); string message;
        Check(engine.FinishClassic(Seat.South, discard.Id, out message), "seven pairs finish");
        Check(engine.State.IsGameOver && engine.State.Table.Count == 7 && engine.State.Players[1].RoundPenalty == 4, "seven pairs score");
        Check(!engine.DiscardTile(Seat.South, hand[0].Id, out message), "cannot act after gameover");
        hand = new List<Tile>();
        hand.AddRange(Enumerable.Range(1, 5).Select(R));
        hand.AddRange(Enumerable.Range(4, 3).Select(x => T(TileColor.Blue, x)));
        hand.AddRange(new[] { T(TileColor.Red, 9), T(TileColor.Blue, 9), T(TileColor.Black, 9) });
        hand.AddRange(new[] { T(TileColor.Red, 12), T(TileColor.Yellow, 12), T(TileColor.Black, 12) });
        discard = R(5); discard.IsJoker = true; hand.Add(discard);
        engine = Controlled(GameMode.ClassicOkey, hand);
        Check(engine.FinishClassic(Seat.South, discard.Id, out message), "per finish with joker discard");
        Check(engine.State.RoundMultiplier == 2 && engine.State.Players[1].RoundPenalty == 4, "joker discard score");
        hand[0] = R(13); engine = Controlled(GameMode.ClassicOkey, hand);
        Check(!engine.FinishClassic(Seat.South, discard.Id, out message) && !engine.State.IsGameOver, "invalid finish stays live");
    }
    private static void Test101Opening()
    {
        var opening = Exact101(); var discard = R(2);
        var engine = Controlled(GameMode.Okey101, opening.Concat(new[] { discard })); string message;
        Stage101(engine, opening);
        Check(!engine.CommitTurn(Seat.South, out message), "101 commit cannot omit discard");
        Check(engine.DiscardTile(Seat.South, discard.Id, out message), "exact 101 opening plus discard");
        Check(engine.State.IsGameOver && engine.State.Players[0].HasOpened && engine.State.Players[0].RoundPenalty == -202 && engine.State.Players[1].RoundPenalty == 404, "101 hand finish doubles unopened penalties");
        opening = Exact101(); discard = R(2);
        engine = Controlled(GameMode.Okey101, opening.Concat(new[] { discard })); engine.State.Players[1].HasOpened = true;
        Stage101(engine, opening);
        Check(engine.DiscardTile(Seat.South, discard.Id, out message) && engine.State.Players[0].RoundPenalty == -101 && engine.State.Players[2].RoundPenalty == 202, "opening and finish after another opening is ordinary finish");
        var low = new List<Tile>();
        foreach (var n in new[] { 13, 12, 8 })
            low.AddRange(new[] { T(TileColor.Red, n), T(TileColor.Blue, n), T(TileColor.Yellow, n) });
        discard = R(2); engine = Controlled(GameMode.Okey101, low.Concat(new[] { discard })); engine.BeginTurn(Seat.South);
        for (var i = 0; i < 3; i++) Check(engine.CreateMeldFromHand(Seat.South, low.Skip(i * 3).Take(3).Select(x => x.Id).ToList()), "99 staging");
        Check(!engine.DiscardTile(Seat.South, discard.Id, out message), "99 does not open");
        Check(engine.State.Table.Count == 0 && !engine.State.Players[0].HasOpened && engine.State.CurrentTurn == Seat.South, "rejected opening atomic");
        var pairs = new List<Tile>();
        for (var n = 1; n <= 5; n++) { pairs.Add(R(n)); pairs.Add(R(n)); }
        discard = R(10); engine = Controlled(GameMode.Okey101, pairs.Concat(new[] { discard, R(11) })); engine.BeginTurn(Seat.South);
        for (var i = 0; i < 5; i++) Check(engine.CreateMeldFromHand(Seat.South, pairs.Skip(i * 2).Take(2).Select(x => x.Id).ToList()), "pair stage");
        Check(engine.DiscardTile(Seat.South, discard.Id, out message) && engine.State.Players[0].OpenedWithPairs, "five pairs opens");
    }
    private static void Test101JokerReplacement()
    {
        string message;
        var joker = R(5); joker.IsJoker = true;
        var old = new TraditionalRuleValidator(GameMode.Okey101).NormalizeMeld(M(R(6), joker, R(8))); old.OwnerSeat = Seat.West;
        var replacement = R(7); var discard = R(12); var bad = T(TileColor.Blue, 7);
        var engine = Controlled(GameMode.Okey101, new[] { replacement, discard, bad }); engine.State.Table.Add(old);
        Check(!engine.ReplaceTableJoker(Seat.South, replacement.Id, 0, joker.Id, out message), "closed player cannot take table joker");
        engine.State.Players[0].HasOpened = true;
        Check(!engine.ReplaceTableJoker(Seat.South, bad.Id, 0, joker.Id, out message), "joker replacement must match color and number");
        Check(engine.ReplaceTableJoker(Seat.South, replacement.Id, 0, joker.Id, out message), "opened player can replace table joker");
        Check(engine.State.TurnHand.Single(x => x.Id == joker.Id).JokerNumber == 0 && engine.State.TurnTable[0].Tiles.Any(x => x.Id == replacement.Id), "taken joker is free in hand");
        var clone = engine.State.Clone();
        Check(clone.TurnHand.Single(x => x.Id == joker.Id).JokerNumber == 0 && clone.TurnTable[0].OwnerSeat == Seat.West, "joker exchange deep clone retains state and owner");
        Check(engine.DiscardTile(Seat.South, discard.Id, out message) && engine.State.Table[0].OwnerSeat == Seat.West && engine.State.Players[0].Hand.Any(x => x.Id == joker.Id), "exchange commits with discard");

        replacement = R(7); discard = R(12); var extraSpare = R(10);
        engine = Controlled(GameMode.Okey101, new[] { replacement, discard, extraSpare }); engine.State.Table.Add(old); engine.State.Players[0].HasOpened = true;
        Check(engine.ReplaceTableJoker(Seat.South, replacement.Id, 0, joker.Id, out message) && engine.TryAddTileToMeld(Seat.South, joker.Id, 0), "returned joker can extend its former run with a new represented value");
        Check(engine.DiscardTile(Seat.South, discard.Id, out message) && engine.State.Table[0].Tiles.Single(x => x.IsJoker).JokerNumber == 9, "same run joker replacement and reuse commits");
        engine = Controlled(GameMode.Okey101, new[] { replacement, discard, extraSpare }); engine.State.Table.Add(old); engine.State.Players[0].HasOpened = true;
        var freeJoker = joker.Clone(); freeJoker.JokerNumber = 0;
        var sameRun = M(old.Tiles.First(x => x.Number == 6 && !x.IsJoker), replacement, old.Tiles.First(x => x.Number == 8 && !x.IsJoker), freeJoker);
        Check(engine.ReplaceTurnLayout(Seat.South, new[] { sameRun }, new[] { discard.Id, extraSpare.Id }, out message) && engine.DiscardTile(Seat.South, discard.Id, out message), "network permits safe replacement and reuse in the same run");
        Check(engine.State.Table[0].Tiles.Single(x => x.IsJoker).JokerNumber == 9, "network re-normalizes released joker instead of trusting requested value");

        replacement = R(7); discard = R(12); var one = T(TileColor.Blue, 1); var two = T(TileColor.Blue, 2); var spare = R(10);
        engine = Controlled(GameMode.Okey101, new[] { replacement, one, two, discard, spare }); engine.State.Table.Add(old); engine.State.Players[0].HasOpened = true;
        var changed = M(old.Tiles.First(x => x.Number == 6 && !x.IsJoker), replacement, old.Tiles.First(x => x.Number == 8 && !x.IsJoker));
        var reused = M(one, two, joker);
        Check(engine.ReplaceTurnLayout(Seat.South, new[] { changed, reused }, new[] { discard.Id, spare.Id }, out message), "network layout can exchange and reuse joker");
        Check(engine.DiscardTile(Seat.South, discard.Id, out message), "network joker reuse commits");
        Check(engine.State.Table[0].OwnerSeat == Seat.West && engine.State.Table[1].OwnerSeat == Seat.South && engine.State.Table[1].Tiles.Single(x => x.IsJoker).JokerNumber == 3, "returned joker gets new represented value and old owner remains");

        engine = Controlled(GameMode.Okey101, new[] { replacement, one, two, discard, spare }); engine.State.Table.Add(old);
        Check(!engine.ReplaceTurnLayout(Seat.South, new[] { changed, reused }, new[] { discard.Id, spare.Id }, out message), "network cannot exchange joker without valid opening");
        engine.State.Players[0].HasOpened = true;
        var wrong = M(old.Tiles.First(x => x.Number == 6 && !x.IsJoker), one, old.Tiles.First(x => x.Number == 8 && !x.IsJoker));
        Check(!engine.ReplaceTurnLayout(Seat.South, new[] { wrong }, new[] { replacement.Id, two.Id, discard.Id, spare.Id, joker.Id }, out message), "network cannot steal joker with wrong replacement");

        var opening = Exact101(); replacement = R(7); discard = R(12); spare = R(2);
        engine = Controlled(GameMode.Okey101, opening.Concat(new[] { replacement, discard, spare })); engine.State.Table.Add(old);
        Stage101(engine, opening);
        Check(engine.ReplaceTableJoker(Seat.South, replacement.Id, 0, joker.Id, out message), "valid staged opening permits joker exchange in same turn");
        Check(engine.DiscardTile(Seat.South, discard.Id, out message) && engine.State.Players[0].HasOpened, "opening and joker exchange commit atomically");

        engine = Controlled(GameMode.ClassicOkey, new[] { replacement });
        Check(!engine.ReplaceTableJoker(Seat.South, replacement.Id, 0, joker.Id, out message), "classic has no table joker exchange");
    }
    private static void Test101PairProcessing()
    {
        string message;
        var old = M(R(4), R(5), R(6)); old.OwnerSeat = Seat.West;
        var seven = R(7); var eight = R(8); var nine = R(9); var discard = R(12); var spare = R(2);
        var engine = Controlled(GameMode.Okey101, new[] { seven, eight, nine, discard, spare }); engine.State.Table.Add(old);
        engine.State.Players[0].HasOpened = true; engine.State.Players[0].OpenedWithPairs = true;
        Check(engine.TryAddTileToMeld(Seat.South, seven.Id, 0) && engine.TryAddTileToMeld(Seat.South, eight.Id, 0), "pair opener can process two tiles into a run");
        Check(!engine.TryAddTileToMeld(Seat.South, nine.Id, 0), "pair opener cannot process third tile into same run this turn");
        Check(engine.DiscardTile(Seat.South, discard.Id, out message), "pair opener processing commits");
        engine = Controlled(GameMode.Okey101, new[] { seven, eight, nine, discard, spare }); engine.State.Table.Add(old);
        engine.State.Players[0].HasOpened = true; engine.State.Players[0].OpenedWithPairs = true;
        var expanded = M(old.Tiles.Concat(new[] { seven, eight, nine }).ToArray());
        Check(engine.ReplaceTurnLayout(Seat.South, new[] { expanded }, new[] { discard.Id, spare.Id }, out message), "network can stage expanded run");
        Check(!engine.DiscardTile(Seat.South, discard.Id, out message), "network cannot bypass pair processing limit");
        Check(engine.State.Table[0].Tiles.Count == 3 && engine.State.CurrentTurn == Seat.South, "invalid pair processing remains atomic");

        var pair = new[] { R(11), R(11) };
        engine = Controlled(GameMode.Okey101, pair.Concat(new[] { discard, spare })); engine.State.Players[0].HasOpened = true;
        Check(engine.CreateMeldFromHand(Seat.South, pair.Select(x => x.Id).ToList()), "series opener stages pair");
        Check(!engine.DiscardTile(Seat.South, discard.Id, out message), "series opener cannot process pairs before anyone opens pairs");
        engine.State.Players[1].HasOpened = true; engine.State.Players[1].OpenedWithPairs = true;
        Check(engine.DiscardTile(Seat.South, discard.Id, out message), "series opener processes pair after another pair opening");

        var opening = Exact101(); pair = new[] { R(11), R(11) };
        engine = Controlled(GameMode.Okey101, opening.Concat(pair).Concat(new[] { discard, spare }));
        engine.State.Players[1].HasOpened = true; engine.State.Players[1].OpenedWithPairs = true;
        Stage101(engine, opening);
        Check(engine.CreateMeldFromHand(Seat.South, pair.Select(x => x.Id).ToList()) && engine.DiscardTile(Seat.South, discard.Id, out message), "valid series opening can process a pair in the same turn");
        Check(engine.State.Players[0].HasOpened && !engine.State.Players[0].OpenedWithPairs, "processed pair does not change series opening type");

        var low = new[] { T(TileColor.Red, 13), T(TileColor.Blue, 13), T(TileColor.Yellow, 13) };
        engine = Controlled(GameMode.Okey101, low.Concat(pair).Concat(new[] { discard, spare }));
        engine.State.Players[1].HasOpened = true; engine.State.Players[1].OpenedWithPairs = true;
        Check(engine.CreateMeldFromHand(Seat.South, low.Select(x => x.Id).ToList()) && engine.CreateMeldFromHand(Seat.South, pair.Select(x => x.Id).ToList()), "series and pair process can be staged");
        Check(!engine.DiscardTile(Seat.South, discard.Id, out message), "processed pairs do not contribute to initial 101 threshold");
    }
    private static void Test101Table()
    {
        var old = M(R(4), R(5), R(6)); old.OwnerSeat = Seat.West;
        var extra = R(7); var discard = R(10);
        var engine = Controlled(GameMode.Okey101, new[] { extra, discard, R(11) }); engine.State.Table.Add(old); engine.State.Players[0].HasOpened = true;
        engine.BeginTurn(Seat.South); string message;
        Check(!engine.ReplaceTurnLayout(Seat.South, new Meld[] { null }, engine.State.Players[0].Hand.Select(x => x.Id).ToList(), out message), "malformed network meld rejected without throwing");
        Check(!engine.RemoveTileFromMeld(Seat.South, 0, old.Tiles[0].Id), "committed tile cannot return to hand");
        var split1 = M(old.Tiles[0], old.Tiles[1]); var split2 = M(old.Tiles[2], extra);
        Check(!engine.ReplaceTurnLayout(Seat.South, new[] { split1, split2 }, new[] { discard.Id, engine.State.Players[0].Hand.Last().Id }, out message), "cannot split old meld");
        Check(engine.TryAddTileToMeld(Seat.South, extra.Id, 0), "can extend old run");
        Check(engine.DiscardTile(Seat.South, discard.Id, out message), "extension committed");
        Check(engine.State.Table[0].OwnerSeat == Seat.West && engine.State.Table[0].Tiles.Count == 4, "owner preserved");
        engine = Controlled(GameMode.Okey101, new[] { extra, discard }); engine.State.Table.Add(old); engine.BeginTurn(Seat.South);
        Check(engine.TryAddTileToMeld(Seat.South, extra.Id, 0), "stage closed player extension");
        Check(!engine.DiscardTile(Seat.South, discard.Id, out message), "cannot only extend before opening");
        var joker = R(5); joker.IsJoker = true;
        old = engine.NormalizeMeld(M(R(6), joker, R(8))); old.OwnerSeat = Seat.East;
        extra = R(9); discard = R(10);
        engine = Controlled(GameMode.Okey101, new[] { extra, discard, R(12) }); engine.State.Table.Add(old); engine.State.Players[0].HasOpened = true;
        var forged = old.Clone(); forged.Tiles.Single(x => x.IsJoker).JokerNumber = 12;
        forged.Tiles.Single(x => x.IsJoker).IsFalseJoker = true; forged.OwnerSeat = Seat.South;
        Check(engine.ReplaceTurnLayout(Seat.South, new[] { forged }, engine.State.Players[0].Hand.Select(x => x.Id).ToList(), out message), "layout canonicalizes forged metadata");
        Check(engine.State.TurnTable[0].OwnerSeat == Seat.East && engine.State.TurnTable[0].Tiles.Single(x => x.IsJoker).JokerNumber == 7 && !engine.State.TurnTable[0].Tiles.Single(x => x.IsJoker).IsFalseJoker, "canonical owner and fixed joker retained");
        Check(engine.TryAddTileToMeld(Seat.South, extra.Id, 0) && engine.DiscardTile(Seat.South, discard.Id, out message), "can extend committed joker run without changing representation");
        Check(engine.State.Table[0].Tiles.Single(x => x.IsJoker).JokerNumber == 7, "joker stays represented after commit");
    }
    private static void TestDiscardAndTimeout()
    {
        var engine = new GameEngine(); engine.StartNewGame(Settings(GameMode.Okey101, false)); string message;
        var dropped = engine.State.Players[0].Hand[0];
        Check(engine.DiscardTile(Seat.South, dropped.Id, out message), "101 initial discard");
        Check(engine.DrawDiscard(Seat.East, out message), "take previous discard");
        Check(!engine.DiscardTile(Seat.East, engine.State.Players[3].Hand.First(x => x.Id != dropped.Id).Id, out message), "taken101 discard must be melded");
        Check(engine.CompleteTimeout(Seat.East, out message), "timeout safely returns unusable discard");
        Check(engine.State.CurrentTurn == Seat.North && engine.State.DiscardPiles[0].Tiles.Last().Id == dropped.Id, "timeout restores source pile and advances");
        var opening = Exact101(); var discard = R(3);
        engine = Controlled(GameMode.Okey101, opening.Skip(1).Concat(new[] { discard, R(2) }));
        engine.State.HasDrawnThisTurn = false; engine.State.DiscardPiles[1].Tiles.Add(opening[0].Clone());
        Check(engine.DrawDiscard(Seat.South, out message), "101 take discard for immediate opening");
        Stage101(engine, opening);
        Check(engine.DiscardTile(Seat.South, discard.Id, out message) && engine.State.Players[0].HasOpened && engine.State.Table.SelectMany(x => x.Tiles).Any(x => x.Id == opening[0].Id), "taken discard can be used in the 101 opening");
        engine = Controlled(GameMode.ClassicOkey, Enumerable.Range(1, 15).Select(x => R((x % 13) + 1)));
        Check(engine.CompleteTimeout(Seat.South, out message) && engine.State.CurrentTurn == Seat.East, "starter timeout discards without draw");
    }
    private static void TestStock()
    {
        var engine = Controlled(GameMode.ClassicOkey, Enumerable.Range(1, 14).Select(x => R((x % 13) + 1)));
        engine.State.Deck.Clear(); engine.State.HasDrawnThisTurn = false; string message;
        Check(engine.DrawTile(Seat.South, out message) && engine.State.IsGameOver && engine.State.EndedByStock && engine.State.WinnerName == null, "classic stock draw ends round");
        Check(engine.State.Players.All(x => x.RoundPenalty == 0), "classic stock no score");
        var joker = R(5); joker.IsJoker = true;
        engine = Controlled(GameMode.Okey101, new[] { joker }); engine.State.Deck.Clear(); engine.State.HasDrawnThisTurn = false;
        Check(engine.PassTurn(Seat.South, out message) && engine.State.Players[0].RoundPenalty == 303 && engine.State.Players[1].RoundPenalty == 202, "101 stock unopened and joker penalties");
        engine = Controlled(GameMode.Okey101, new[] { R(5), R(6), joker }); engine.State.Players[0].HasOpened = true; engine.State.Players[0].OpenedWithPairs = true;
        engine.State.Deck.Clear(); engine.State.HasDrawnThisTurn = false;
        Check(engine.PassTurn(Seat.South, out message) && engine.State.Players[0].RoundPenalty == 224, "stock opened pair penalty doubles hand total and joker penalty");
    }
    private static void TestNaneRegression()
    {
        var engine = new GameEngine(); engine.StartNewGame(Settings(GameMode.NaneOkey, false)); string message;
        Check(engine.State.Players.All(x => x.Hand.Count == 15), "Nane15 each");
        Check(engine.State.Players.SelectMany(x => x.Hand).Concat(engine.State.Deck).Count() == 104 && engine.State.Indicator == null && engine.State.DiscardPiles.Count == 0, "Nane104 no joker/piles");
        Check(engine.DrawTile(Seat.South, out message) && engine.State.CurrentTurn == Seat.West && engine.State.Players[0].Hand.Count == 16, "Nane draw directly advances");
        Check(!engine.DiscardTile(Seat.West, engine.State.Players[1].Hand[0].Id, out message), "Nane no discard");
        var validator = new RuleValidator(); Check(validator.IsValidMeld(M(R(12), R(13), R(1))), "Nane ace high unchanged");
        var opening = new[] { R(3), R(4), R(5), T(TileColor.Blue, 8), T(TileColor.Yellow, 8), T(TileColor.Black, 8), R(12) };
        engine = Controlled(GameMode.NaneOkey, opening); engine.BeginTurn(Seat.South);
        Check(engine.CreateMeldFromHand(Seat.South, opening.Take(3).Select(x => x.Id).ToList()), "Nane first per");
        Check(!engine.CommitTurn(Seat.South, out message), "Nane first opening still needs two pers");
        Check(engine.CreateMeldFromHand(Seat.South, opening.Skip(3).Take(3).Select(x => x.Id).ToList()), "Nane second per");
        Check(engine.CommitTurn(Seat.South, out message) && engine.State.CurrentTurn == Seat.West && engine.State.Players[0].Hand.Count == 1, "Nane commit no discard advances");
    }
    private static void TestBots()
    {
        foreach (var mode in new[] { GameMode.ClassicOkey, GameMode.Okey101 })
        {
            var engine = new GameEngine(); engine.StartNewGame(Settings(mode, true));
            var watch = Stopwatch.StartNew();
            for (var turn = 0; turn < 160 && !engine.State.IsGameOver; turn++)
            {
                var seat = engine.State.CurrentTurn; string message;
                Check(engine.RunBotTurnIfNeeded(out message), mode + " bot can complete turn: " + message);
                Check(engine.State.IsGameOver || engine.State.CurrentTurn != seat, mode + " bot advances");
                var all = engine.State.Players.SelectMany(x => x.Hand).Concat(engine.State.Deck).Concat(engine.State.Table.SelectMany(x => x.Tiles))
                    .Concat(engine.State.DiscardPiles.SelectMany(x => x.Tiles)).Concat(new[] { engine.State.Indicator }).Select(x => x.Id).ToList();
                Check(all.Count == 106 && all.Distinct().Count() == 106, mode + " bot conserves all physical tiles");
            }
            Check(watch.Elapsed.TotalSeconds < 30, mode + " bots bounded runtime");
            Check(engine.State.IsGameOver, mode + " bots finish within 160 turns");
            Console.WriteLine(mode + " bots: " + watch.ElapsedMilliseconds + " ms, ended=" + engine.State.IsGameOver);
        }
    }
    private static void TestBotJokerReplacement()
    {
        var joker = R(5); joker.IsJoker = true;
        var old = new TraditionalRuleValidator(GameMode.Okey101).NormalizeMeld(M(R(6), joker, R(8))); old.OwnerSeat = Seat.West;
        var replacement = R(7);
        var engine = Controlled(GameMode.Okey101, new[] { R(11), R(12) }); engine.State.Table.Add(old);
        engine.State.Players[0].HasOpened = true; engine.State.HasDrawnThisTurn = false;
        engine.State.DiscardPiles[1].Tiles.Add(replacement);
        var before = engine.State.Players.SelectMany(x => x.Hand).Concat(engine.State.Table.SelectMany(x => x.Tiles))
            .Concat(engine.State.Deck).Concat(engine.State.DiscardPiles.SelectMany(x => x.Tiles)).Select(x => x.Id).OrderBy(x => x).ToList();
        var stockCount = engine.State.Deck.Count; string message;
        Check(new TraditionalBotEngine().TryApplyTurn(engine, Seat.South, out message), "bot can draw matching discard and exchange table joker");
        Check(engine.State.CurrentTurn == Seat.East && engine.State.Deck.Count == stockCount && engine.State.Table[0].Tiles.Any(x => x.Id == replacement.Id), "bot uses discard as joker replacement before ending turn");
        var after = engine.State.Players.SelectMany(x => x.Hand).Concat(engine.State.Table.SelectMany(x => x.Tiles))
            .Concat(engine.State.Deck).Concat(engine.State.DiscardPiles.SelectMany(x => x.Tiles)).Select(x => x.Id).OrderBy(x => x).ToList();
        Check(before.SequenceEqual(after), "bot joker exchange conserves all physical tiles");
    }
}
