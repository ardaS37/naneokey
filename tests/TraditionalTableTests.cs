using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NaneOkey.Domain;
using NaneOkey.UI;

internal static class TraditionalTableTests
{
    private static int _checks;
    private static int _tileId = 20000;
    private static readonly BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly Assembly App = typeof(MainForm).Assembly;
    private static readonly Type ViewType = App.GetType("NaneOkey.UI.TraditionalTableView", true);
    private static readonly Type TargetType = App.GetType("NaneOkey.UI.TraditionalTableTarget", true);
    private static readonly Type KindType = App.GetType("NaneOkey.UI.TraditionalTargetKind", true);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }

    private static void Set(object instance, string name, object value)
    {
        instance.GetType().GetProperty(name, Instance).SetValue(instance, value, null);
    }

    private static object Get(object instance, string name)
    {
        return instance.GetType().GetProperty(name, Instance).GetValue(instance, null);
    }

    private static object Call(object instance, string name, params object[] arguments)
    {
        try { return instance.GetType().GetMethod(name, Instance).Invoke(instance, arguments); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }

    private static object Target(string kind, int meldIndex = -1)
    {
        return Activator.CreateInstance(TargetType, new[] { Enum.Parse(KindType, kind), (object)meldIndex });
    }

    private static Rectangle Bounds(Control view, string kind, int meldIndex = -1)
    {
        return (Rectangle)Call(view, "GetTargetBounds", Target(kind, meldIndex));
    }

    private static Tile Tile(TileColor color, int number)
    {
        return new Tile(_tileId++, color, number);
    }

    private static GameState State(GameMode mode, Seat localSeat)
    {
        var state = new GameState { Mode = mode, CurrentTurn = localSeat, HasDrawnThisTurn = true, Indicator = Tile(TileColor.Red, 4) };
        foreach (Seat seat in Enum.GetValues(typeof(Seat)))
        {
            state.Players.Add(new PlayerState(seat, seat.ToString(), PlayerType.Human, BotDifficulty.Easy));
            state.DiscardPiles.Add(new DiscardPile(seat));
            state.DiscardPiles.Last().Tiles.Add(Tile((TileColor)((int)seat % 4), (int)seat + 1));
            state.Players.Last().Hand.AddRange(Enumerable.Range(1, mode == GameMode.ClassicOkey ? 14 : 21).Select(number => Tile(TileColor.Blue, (number - 1) % 13 + 1)));
        }
        state.Deck.AddRange(Enumerable.Range(1, 20).Select(number => Tile(TileColor.Black, (number - 1) % 13 + 1)));
        return state;
    }

    private static Control Create(GameState state, Seat localSeat, Size size)
    {
        var view = (Control)Activator.CreateInstance(ViewType);
        view.Size = size;
        Set(view, "State", state);
        Set(view, "LocalSeat", localSeat);
        Set(view, "DisplayMelds", state.Table);
        Set(view, "CanAct", true);
        Set(view, "StatusText", "Taş seçerek masadaki alana bırak.");
        return view;
    }

    private static void CheckTarget(Control view, string kind, int meldIndex = -1)
    {
        var bounds = Bounds(view, kind, meldIndex);
        Check(bounds.Width > 0 && bounds.Height > 0, kind + " alanı yok: " + view.Size);
        Check(view.ClientRectangle.Contains(bounds), kind + " alanı kontrol dışına taşıyor: " + bounds + " / " + view.Size);
        var hit = Call(view, "HitTest", new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2));
        Check(hit != null && Get(hit, "Kind").ToString() == kind, kind + " merkezinde yanlış hedef: " + view.Size);
        if (kind == "Meld") Check((int)Get(hit, "MeldIndex") == meldIndex, "Per merkezi yanlış per sırasını işaretliyor.");
    }

    private static void Save(Control view, string name)
    {
        var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screens");
        Directory.CreateDirectory(directory);
        using (var bitmap = new Bitmap(view.Width, view.Height))
        {
            view.DrawToBitmap(bitmap, view.ClientRectangle);
            bitmap.Save(Path.Combine(directory, name), ImageFormat.Png);
        }
    }

    private static void TestModeGeometry()
    {
        foreach (var mode in new[] { GameMode.ClassicOkey, GameMode.Okey101 })
        foreach (var size in new[] { new Size(1000, 500), new Size(640, 380), new Size(360, 240), new Size(215, 180) })
        foreach (Seat localSeat in Enum.GetValues(typeof(Seat)))
        {
            var state = State(mode, localSeat);
            var ids = state.Players.SelectMany(x => x.Hand).Concat(state.Deck).Concat(state.DiscardPiles.SelectMany(x => x.Tiles)).Select(x => x.Id).ToArray();
            using (var view = Create(state, localSeat, size))
            {
                CheckTarget(view, "Stock");
                CheckTarget(view, "PreviousDiscard");
                CheckTarget(view, "OwnDiscard");
                if (mode == GameMode.ClassicOkey)
                {
                    CheckTarget(view, "Finish");
                    Check(Bounds(view, "OpenRuns").IsEmpty && Bounds(view, "OpenPairs").IsEmpty, "Klasik masada 101 açış alanları kaldı.");
                }
                else
                {
                    CheckTarget(view, "OpenRuns");
                    CheckTarget(view, "OpenPairs");
                }
                var kinds = mode == GameMode.ClassicOkey ? new[] { "Stock", "PreviousDiscard", "OwnDiscard", "Finish" }
                    : new[] { "Stock", "PreviousDiscard", "OwnDiscard", "OpenRuns", "OpenPairs" };
                for (var first = 0; first < kinds.Length; first++)
                for (var second = first + 1; second < kinds.Length; second++)
                    Check(Rectangle.Intersect(Bounds(view, kinds[first]), Bounds(view, kinds[second])).IsEmpty,
                        "Eylem alanları çakışıyor: " + kinds[first] + " / " + kinds[second] + " / " + size);
                if (mode == GameMode.ClassicOkey)
                {
                    var piles = (IDictionary<Seat, Rectangle>)view.GetType().GetField("_discardBounds", Instance).GetValue(view);
                    Check(piles.All(pile => Rectangle.Intersect(Bounds(view, "Stock"), pile.Value).IsEmpty), "Klasik deste üst atık alanının taşını veya adını örttü: " + size);
                }
                Check(Call(view, "HitTest", new Point(-1, -1)) == null, "Kontrol dışı nokta hedef kabul edildi.");
                if (localSeat == Seat.South)
                    Save(view, (mode == GameMode.ClassicOkey ? "classic-table-" : "101-table-") + size.Width + "x" + size.Height + ".png");
                Check(ids.SequenceEqual(state.Players.SelectMany(x => x.Hand).Concat(state.Deck).Concat(state.DiscardPiles.SelectMany(x => x.Tiles)).Select(x => x.Id)), "Masa çizimi oyun taşlarını değiştirdi.");
            }
        }
    }

    private static void TestOpenedTable()
    {
        var state = State(GameMode.Okey101, Seat.South);
        var run = new Meld(new[] { Tile(TileColor.Red, 10), Tile(TileColor.Red, 11), Tile(TileColor.Red, 12) }) { OwnerSeat = Seat.West };
        var pair = new Meld(new[] { Tile(TileColor.Blue, 8), Tile(TileColor.Blue, 8) }) { OwnerSeat = Seat.North, IsPair = true };
        state.Table.AddRange(new[] { run, pair });
        using (var view = Create(state, Seat.South, new Size(1000, 500)))
        {
            CheckTarget(view, "Meld", 0);
            CheckTarget(view, "Meld", 1);
            Check(view.Font.Size >= 9F && Bounds(view, "Stock").Width >= 236 && Bounds(view, "Stock").Height >= 81,
                "Normal boyuttaki 101 masasının yazı ve deste alanı küçük kaldı.");
            var cards = (System.Collections.IList)view.GetType().GetField("_cards", Instance).GetValue(view);
            var perWidth = (int)cards[0].GetType().GetField("TileWidth", Instance).GetValue(cards[0]);
            Check(perWidth >= 40, "Normal boyuttaki 101 masasındaki per taşları küçük kaldı.");
            state.DrawnDiscardTileId = 999;
            Check((bool)Call(view, "TargetEnabled", Enum.Parse(KindType, "Stock")), "101'de alınan atığı geri bırakmak için desteden çekme kapalı kaldı.");
            state.DrawnDiscardTileId = 0;
            Check(!(bool)Call(view, "TargetEnabled", Enum.Parse(KindType, "Stock")), "101'de normal çekişten sonra ikinci çekiş etkin kaldı.");
            Save(view, "101-opened-table.png");
            view.Size = new Size(360, 240);
            CheckTarget(view, "Meld", 0);
            CheckTarget(view, "Meld", 1);
            Set(view, "Pending", true);
            Set(view, "OpeningValue", 101);
            Set(view, "PairCount", 0);
            Save(view, "101-opened-table-small.png");
            var owner = run.OwnerSeat;
            Check(run.OwnerSeat == owner && run.Tiles.Count == 3 && pair.Tiles.Count == 2, "Per çizimi sahipliği veya taşları değiştirdi.");
        }
    }

    private static void TestCompactStockRendering()
    {
        foreach (var size in new[] { new Size(400, 170), new Size(360, 130), new Size(215, 130) })
        {
            var state = State(GameMode.ClassicOkey, Seat.South);
            using (var view = Create(state, Seat.South, size))
            using (var before = new Bitmap(size.Width, size.Height))
            using (var after = new Bitmap(size.Width, size.Height))
            {
                var stock = Bounds(view, "Stock");
                view.DrawToBitmap(before, view.ClientRectangle);
                state.Indicator = new Tile(state.Indicator.Id, TileColor.Blue, state.Indicator.Number);
                Set(view, "State", state);
                view.DrawToBitmap(after, view.ClientRectangle);
                var changedInside = false;
                var changedOutside = false;
                for (var y = 0; y < size.Height; y++)
                for (var x = 0; x < size.Width; x++)
                {
                    if (before.GetPixel(x, y) == after.GetPixel(x, y)) continue;
                    if (stock.Contains(x, y)) changedInside = true;
                    else changedOutside = true;
                }
                Check(changedInside && !changedOutside, "Kısa klasik deste alanında gösterge alan dışına taştı: " + size);
            }
        }
    }

    private static void TestRightwardDiscardGeometry()
    {
        foreach (var mode in new[] { GameMode.ClassicOkey, GameMode.Okey101 })
        foreach (Seat seat in Enum.GetValues(typeof(Seat)))
        {
            var state = State(mode, seat);
            state.HasDrawnThisTurn = false;
            using (var view = Create(state, seat, new Size(1000, 500)))
            {
                var own = Bounds(view, "OwnDiscard");
                var previous = Bounds(view, "PreviousDiscard");
                Check(own.Left > view.Width / 2 && own.Top > view.Height / 2, mode + " atılacak taş sağda değil: " + seat);
                Check(previous.Right < view.Width / 2 && previous.Top > view.Height / 2, mode + " alınacak önceki taş solda değil: " + seat);
                var piles = (IDictionary<Seat, Rectangle>)view.GetType().GetField("_discardBounds", Instance).GetValue(view);
                var previousSeat = (Seat)(((int)seat + 1) % 4);
                var rightSeat = (Seat)(((int)seat + 3) % 4);
                Check(piles[seat] == own && piles[previousSeat] == previous, mode + " atık yönü koltuk sahipliğini değiştirdi: " + seat);
                Check(piles[rightSeat].Left > view.Width / 2 && piles[rightSeat].Top < view.Height / 2,
                    mode + " sağ oyuncunun atık yığını yanlış köşede: " + seat);
                Check((bool)Call(view, "TargetEnabled", Enum.Parse(KindType, "PreviousDiscard")), mode + " sol atık alınamıyor");
                state.DiscardPiles[(int)previousSeat].Tiles.Clear();
                Set(view, "State", state);
                Check(!(bool)Call(view, "TargetEnabled", Enum.Parse(KindType, "PreviousDiscard")), mode + " başka yığın sol atık yerine etkinleşti");
                state.Players[(int)previousSeat].IsActive = false;
                Set(view, "State", state);
                previous = Bounds(view, "PreviousDiscard");
                Check(previous == piles[(Seat)(((int)seat + 2) % 4)], mode + " kapalı koltuğun ardından doğru önceki oyuncuya dönmedi");
                Check((bool)Call(view, "TargetEnabled", Enum.Parse(KindType, "PreviousDiscard")), mode + " boş koltuk önceki aktif oyuncunun taşını engelledi");
            }
        }
    }

    private static void TestCrowdedTable()
    {
        var state = State(GameMode.Okey101, Seat.East);
        for (var index = 0; index < 40; index++)
        {
            var color = (TileColor)(index % 4);
            state.Table.Add(new Meld(new[] { Tile(color, 4), Tile(color, 5), Tile(color, 6) }) { OwnerSeat = (Seat)(index % 4) });
        }
        using (var view = Create(state, Seat.East, new Size(640, 380)))
        {
            CheckTarget(view, "Stock");
            CheckTarget(view, "OwnDiscard");
            CheckTarget(view, "OpenRuns");
            var scroll = view.Controls.OfType<VScrollBar>().SingleOrDefault();
            Check(scroll != null && scroll.Visible && scroll.Maximum >= scroll.LargeChange, "Kalabalık masada per kaydırma eksik.");
            var first = Bounds(view, "Meld", 0);
            CheckTarget(view, "Meld", 0);
            scroll.Value = Math.Max(scroll.Minimum, scroll.Maximum - scroll.LargeChange + 1);
            Call(scroll, "OnValueChanged", EventArgs.Empty);
            Check(Bounds(view, "Meld", 0).IsEmpty || Bounds(view, "Meld", 0) != first, "Per kaydırma görünümü güncellemedi.");
            CheckTarget(view, "Meld", 39);
            Save(view, "101-crowded-scrolled-table.png");
            Check(state.Table.Count == 40 && state.Table.All(x => x.Tiles.Count == 3), "Kaydırma masadaki perleri değiştirdi.");
        }
    }

    private static byte[] Render(Control view)
    {
        using (var bitmap = new Bitmap(view.Width, view.Height))
        using (var bytes = new MemoryStream())
        {
            view.DrawToBitmap(bitmap, view.ClientRectangle);
            bitmap.Save(bytes, ImageFormat.Png);
            return bytes.ToArray();
        }
    }

    private static void TestOpponentRackPrivacy()
    {
        foreach (var mode in new[] { GameMode.ClassicOkey, GameMode.Okey101, GameMode.NaneOkey })
        {
            var state = State(mode, Seat.South);
            state.UseNewAppearance = mode == GameMode.NaneOkey;
            using (var view = Create(state, Seat.South, new Size(1000, 500)))
            {
                if (mode == GameMode.NaneOkey) Set(view, "NaneStockBounds", new Rectangle(390, 425, 220, 55));
                var original = Render(view);
                foreach (var player in state.Players.Where(x => x.Seat != Seat.South))
                {
                    var changed = player.Hand.Select(tile => new Tile(tile.Id, (TileColor)(((int)tile.Color + 1) % 4), tile.Number == 13 ? 1 : tile.Number + 1)
                        { IsJoker = !tile.IsJoker, IsFalseJoker = !tile.IsFalseJoker }).ToList();
                    player.Hand.Clear();
                    player.Hand.AddRange(changed);
                }
                Set(view, "State", state);
                Check(original.SequenceEqual(Render(view)), "Rakip ıstakasının arkası gizli taşların yüzünü gösterdi: " + mode);
                Save(view, (mode == GameMode.ClassicOkey ? "classic" : mode == GameMode.Okey101 ? "101" : "nane-new") + "-opponent-racks.png");
            }
        }
    }

    private static void TestNaneAppearanceGeometry()
    {
        foreach (var size in new[] { new Size(1000, 500), new Size(640, 380), new Size(360, 240), new Size(215, 180) })
        foreach (Seat seat in Enum.GetValues(typeof(Seat)))
        {
            var state = State(GameMode.NaneOkey, seat);
            state.UseNewAppearance = true;
            state.Indicator = null;
            state.DiscardPiles.Clear();
            using (var view = Create(state, seat, size))
            {
                var stock = new Rectangle((size.Width - Math.Min(200, size.Width - 120)) / 2, size.Height - 57, Math.Min(200, size.Width - 120), 45);
                Set(view, "NaneStockBounds", stock);
                Set(view, "NaneCanDraw", true);
                CheckTarget(view, "Stock");
                foreach (var kind in new[] { "PreviousDiscard", "OwnDiscard", "Finish", "OpenRuns", "OpenPairs", "Meld" })
                    Check(Bounds(view, kind).IsEmpty, "Yeni Nane görünümü geleneksel oyunun eylem alanını açtı: " + kind);
                Check(!view.Controls.OfType<VScrollBar>().Single().Visible, "Yeni Nane görünümü 101 per kaydırmasını gösterdi.");
                Check(Call(view, "HitTest", new Point(size.Width / 2, size.Height / 2)) == null, "Yeni Nane görünümü serbest düzenleme masasında native per hedefi yarattı.");
                if (seat == Seat.South) Save(view, "nane-background-" + size.Width + "x" + size.Height + ".png");
            }
        }
    }

    [STAThread]
    public static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            TestModeGeometry();
            TestRightwardDiscardGeometry();
            TestCompactStockRendering();
            TestOpenedTable();
            TestCrowdedTable();
            TestOpponentRackPrivacy();
            TestNaneAppearanceGeometry();
            Console.WriteLine("TraditionalTableTests: " + _checks + " kontrol geçti.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
