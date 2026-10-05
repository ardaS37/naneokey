using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NaneOkey.Domain;
using NaneOkey.Engine;
using NaneOkey.Network;
using NaneOkey.UI;

internal static class UiWorkflowTests
{
    private static int _checks;
    private static int _tileId = 500;
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }

    private static T Field<T>(object instance, string name)
    {
        return (T)instance.GetType().GetField(name, PrivateInstance).GetValue(instance);
    }

    private static void Set(object instance, string name, object value)
    {
        instance.GetType().GetField(name, PrivateInstance).SetValue(instance, value);
    }

    private static object Call(object instance, string name, params object[] arguments)
    {
        var method = instance.GetType().GetMethods(PrivateInstance)
            .SingleOrDefault(x => x.Name == name && x.GetParameters().Length == arguments.Length);
        if (method == null) throw new Exception("Test reflection method missing: " + name + " / " + arguments.Length);
        try { return method.Invoke(instance, arguments); }
        catch (TargetInvocationException error) { throw error.InnerException; }
    }

    private static GameSettings Settings(GameMode mode)
    {
        var settings = new GameSettings
        {
            Mode = mode,
            StartingHandSize = mode == GameMode.ClassicOkey ? 14 : mode == GameMode.Okey101 ? 21 : 15,
            ActivePlayerCount = 4,
            TargetScore = mode == GameMode.ClassicOkey ? 20 : 1000
        };
        foreach (Seat seat in Enum.GetValues(typeof(Seat)))
            settings.Players.Add(new PlayerSetup { Name = seat.ToString(), Type = PlayerType.Human, IsActive = true });
        return settings;
    }

    private static Tile T(TileColor color, int number)
    {
        return new Tile(_tileId++, color, number);
    }

    private static GameState Controlled(GameMode mode, Seat currentSeat, IEnumerable<Tile> hand, bool drawn)
    {
        var state = new GameState { Mode = mode, CurrentTurn = currentSeat, HasDrawnThisTurn = drawn, Indicator = mode == GameMode.NaneOkey ? null : T(TileColor.Yellow, 4) };
        foreach (Seat seat in Enum.GetValues(typeof(Seat)))
        {
            state.Players.Add(new PlayerState(seat, seat.ToString(), seat == Seat.South ? PlayerType.Human : PlayerType.Remote, BotDifficulty.Easy));
            if (mode != GameMode.NaneOkey) state.DiscardPiles.Add(new DiscardPile(seat));
        }
        state.Players[(int)currentSeat].Hand.AddRange(hand);
        state.Deck.AddRange(new[] { T(TileColor.Blue, 12), T(TileColor.Red, 9), T(TileColor.Black, 3) });
        return state;
    }

    private static void Load(MainForm form, GameState state)
    {
        Field<GameEngine>(form, "_engine").Restore(state);
        Set(form, "_currentSettings", Settings(state.Mode));
        Field<GameSettings>(form, "_currentSettings").UseNewAppearance = state.UseNewAppearance;
        Set(form, "_oyunBasladi", true);
        Set(form, "_agIstemcisiModu", false);
        Set(form, "_yerelKoltuk", Seat.South);
        Set(form, "_agKoltuguAtandi", true);
        Set(form, "_roundScoreShownForCurrentGame", true);
        Call(form, "LoadBoardFromMelds", state.Table);
        Call(form, "ClearHandSlots");
        Call(form, "SyncHandSlots", state.Players[0].Hand);
        Call(form, "RefreshUi");
    }

    private static void StopTimers(MainForm form)
    {
        foreach (var name in new[] { "_botZamani", "_animasyonZamani", "_turnTimer", "_kutlamaZamani", "_cayAnimasyonZamani", "_cayUcusZamani" })
            Field<Timer>(form, name).Stop();
    }

    private static void ModeSelectionAndSettings()
    {
        foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
        {
            using (var selection = new GameModeForm(GameMode.NaneOkey))
            {
                var button = selection.Controls.OfType<Button>().Single(x => x.Text == GameModeForm.ModeName(mode));
                typeof(Control).GetMethod("OnClick", PrivateInstance).Invoke(button, new object[] { EventArgs.Empty });
                Check(selection.SelectedMode == mode && selection.DialogResult == DialogResult.OK, "Oyun seçimi düğmesi yanlış moda yöneldi.");
                var previous = Settings(selection.SelectedMode);
                previous.TargetScore = 1000;
                previous.ActivePlayerCount = 2;
                previous.EnableLivePreview = true;
                previous.EnableTurnTimer = true;
                previous.TurnSeconds = 45;
                previous.BotThinkSeconds = 19;
                using (var dialog = new NewGameForm(previous))
                {
                    var settings = dialog.CreateSettings();
                    Check(settings.Mode == mode && dialog.Text.StartsWith(GameModeForm.ModeName(mode)), "Ayar ekranı modu kaybetti.");
                    Check(settings.StartingHandSize == (mode == GameMode.ClassicOkey ? 14 : mode == GameMode.Okey101 ? 21 : 15), "Modun taş sayısı yanlış.");
                    Check(settings.TargetScore == (mode == GameMode.ClassicOkey ? 20 : 1000), "Varsayılan puan sınırı yanlış.");
                    Check(settings.EnableLivePreview == (mode == GameMode.NaneOkey), "Gizli el modunda canlı önizleme açık.");
                    Check(settings.ActivePlayerCount == (mode == GameMode.NaneOkey ? 2 : 4), "Mod oyuncu sayısını yanlış hazırladı.");
                    Check(settings.EnableTurnTimer && settings.TurnSeconds == 45 && settings.BotThinkSeconds == 19, "Süre ayarları kayboldu.");
                    Check(Field<CheckBox>(dialog, "_livePreviewBox").Enabled == (mode == GameMode.NaneOkey), "Canlı önizleme kontrolü yanlış etkinleşti.");
                }
                previous.TargetScore = 543;
                using (var dialog = new NewGameForm(previous))
                    Check(dialog.CreateSettings().TargetScore == 543, "Özel hedef puan ayarlarda kayboldu.");
                if (mode == GameMode.ClassicOkey)
                {
                    previous.TargetScore = 20;
                    using (var dialog = new NewGameForm(previous))
                        Check(dialog.CreateSettings().TargetScore == 20, "Klasik 20 puan ayarı Nane minimumuna yuvarlandı.");
                }
            }
        }
    }

    private static void MenuAndDebugUnlock(MainForm form)
    {
        var help = form.MainMenuStrip.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "Yardım");
        Check(help.DropDownItems.OfType<ToolStripMenuItem>().Any(item => item.Text == "Günlük / Sohbet"), "Günlük / Sohbet Yardım menüsünde bulunamadı.");
        var debug = Field<ToolStripMenuItem>(form, "_botDebugMenuItem");
        Check(!debug.Available && !Field<bool>(form, "_botDebugEnabled"), "Bot Debug varsayılan gizli/kapalı başlamadı.");
        Call(form, "AppendBotDebug", "Gizli debug kaydı");
        Call(form, "ShowBotDebugWindow");
        Check(Field<Form>(form, "_botDebugPenceresi") == null && Field<List<string>>(form, "_botDebugKayitlari").Count == 0,
            "Gizli Bot Debug açıldı veya kayıt tuttu.");
        using (var about = (Form)Call(form, "CreateAboutDialog"))
        {
            Check(about.Text == "Hakkında" && about.Controls.OfType<Label>().Any(label => label.Text.Contains("Copyright © Arda Saplıoğlu 2026")),
                "Hakkında yayıncı bilgilerini korumadı.");
            for (var index = 0; index < 2; index++)
            {
                Call(about, "ProcessCmdKey", Message.Create(IntPtr.Zero, 0x100, (IntPtr)0x70, IntPtr.Zero), Keys.F1);
                Call(about, "OnKeyUp", new KeyEventArgs(Keys.F1));
            }
            Check(!debug.Available, "İki F1 basışı Bot Debug'ı erken açtı.");
        }
        using (var about = (Form)Call(form, "CreateAboutDialog"))
        {
            // Reopening starts a new sequence; holding one key must not count as three presses.
            for (var index = 0; index < 4; index++)
                Call(about, "ProcessCmdKey", Message.Create(IntPtr.Zero, 0x100, (IntPtr)0x70, IntPtr.Zero), Keys.F1);
            Check(!debug.Available, "Hakkında yeniden açılınca sayacı sıfırlamadı veya basılı F1 tekrarlarını ayrı basış saydı.");
            Call(about, "OnKeyUp", new KeyEventArgs(Keys.F1));
            Call(about, "ProcessCmdKey", Message.Create(IntPtr.Zero, 0x100, (IntPtr)0x70, IntPtr.Zero), Keys.F1);
            Call(about, "OnKeyUp", new KeyEventArgs(Keys.F1));
            Check(!debug.Available, "Yeni Hakkında penceresinde ikinci F1 erken etkinleştirdi.");
            Call(about, "ProcessCmdKey", Message.Create(IntPtr.Zero, 0x100, (IntPtr)0x70, IntPtr.Zero), Keys.F1);
            Check(debug.Available && Field<bool>(form, "_botDebugEnabled"), "Hakkında üçüncü F1'de Bot Debug'ı açmadı.");
        }
    }

    private static void StartAndClassicActions(MainForm form)
    {
        foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
        {
            Set(form, "_lastPlayedGameStartToken", "|Yeni oyun basladi.");
            Call(form, "StartLocalGame", Settings(mode), true);
            StopTimers(form);
            var state = Field<GameEngine>(form, "_engine").State;
            Check(state.Mode == mode && form.Text == GameModeForm.ModeName(mode) + " - v4.0.0.0", "Yerel başlangıç mod/sürüm başlığı yanlış.");
            Check(state.Players[0].Hand.Count == (mode == GameMode.ClassicOkey ? 15 : mode == GameMode.Okey101 ? 22 : 15), "Başlayan oyuncunun eli yanlış.");
            Check(state.Players[1].Hand.Count == (mode == GameMode.ClassicOkey ? 14 : mode == GameMode.Okey101 ? 21 : 15), "Diğer oyuncunun eli yanlış.");
            Check(Field<Tile[,]>(form, "_elSlotlari").Cast<Tile>().Count(x => x != null) == state.Players[0].Hand.Count, "Istaka başlangıç elini eksik gösterdi.");
        }

        var hand = Enumerable.Range(1, 14).Select(x => T(TileColor.Red, (x - 1) % 13 + 1)).ToList();
        Load(form, Controlled(GameMode.ClassicOkey, Seat.South, hand, false));
        var engine = Field<GameEngine>(form, "_engine");
        Call(form, "AutoArrangeHand");
        Check(Field<Tile[,]>(form, "_elSlotlari").Cast<Tile>().Where(x => x != null).Select(x => x.Id).OrderBy(x => x)
            .SequenceEqual(hand.Select(x => x.Id).OrderBy(x => x)), "Klasik otomatik dizme eldeki fiziksel taşları değiştirdi.");
        var drawId = engine.State.Deck[0].Id;
        Call(form, "DrawTile");
        StopTimers(form);
        Check(engine.State.HasDrawnThisTurn && engine.State.CurrentTurn == Seat.South && engine.State.Players[0].Hand.Count == 15, "Klasik insan taş çekme sırasını erken bitirdi.");
        Check(Field<Label>(form, "_durumEtiketi").Text == "Taş atmalısın", "Çekme sonrasında atma yönlendirmesi eksik.");
        CheckNativeTable(form);
        Check(DragTraditional(form, engine.State.Players[0].Hand.Single(x => x.Id == drawId), "OwnDiscard"), "Klasik taş yana atılamadı.");
        Check(engine.State.Players[0].Hand.Count == 14 && engine.State.CurrentTurn == Seat.East && !engine.State.HasDrawnThisTurn, "Klasik UI atma turu sağdaki oyuncuya geçmedi.");
        Check(engine.State.DiscardPiles[0].Tiles.Last().Id == drawId && engine.State.Table.Count == 0, "Klasik atık/per durumu yanlış.");
    }

    private static void Place(MainForm form, IEnumerable<Tile> tiles, int row, int column)
    {
        var board = Field<Tile[,]>(form, "_masaSlotlari");
        foreach (var tile in tiles) board[row, column++] = tile.Clone();
    }

    private static void DragToBoard(MainForm form, Tile tile, int boardRow, int boardColumn)
    {
        var hand = Field<Tile[,]>(form, "_elSlotlari");
        var before = hand.Cast<Tile>().Concat(Field<Tile[,]>(form, "_masaSlotlari").Cast<Tile>())
            .Where(x => x != null).Select(x => x.Id).OrderBy(x => x).ToList();
        var row = -1;
        var column = -1;
        for (var r = 0; r < hand.GetLength(0); r++)
            for (var c = 0; c < hand.GetLength(1); c++)
                if (hand[r, c] != null && hand[r, c].Id == tile.Id) { row = r; column = c; }
        if (row < 0) throw new Exception("Sürükleme için taş ıstakada bulunamadı.");
        var assembly = typeof(MainForm).Assembly;
        var kind = Enum.Parse(assembly.GetType("NaneOkey.UI.TileSourceKind"), "Hand");
        Check((bool)Call(form, "CanStartDrag", kind), "Tek taş sürüklemesi başlayamadı.");
        var data = Activator.CreateInstance(assembly.GetType("NaneOkey.UI.TileDragData"), new object[] { tile.Clone(), kind, row, column });
        Call(form, "BeginCustomDrag", data, 0, Point.Empty);
        var moved = (bool)Call(form, "MoveDraggedTileToBoard", boardRow, boardColumn);
        Call(form, "EndCustomDrag", !moved);
        Check(moved, "Tek taş tahtaya bırakılamadı.");
        var after = hand.Cast<Tile>().Concat(Field<Tile[,]>(form, "_masaSlotlari").Cast<Tile>())
            .Where(x => x != null).Select(x => x.Id).OrderBy(x => x).ToList();
        Check(before.SequenceEqual(after) && after.Distinct().Count() == after.Count, "Sürüklemede el/masa taşı kopyalandı veya kayboldu.");
        Check(Field<GameEngine>(form, "_engine").State.TurnInProgress, "Sürükleme sırasında düzenlenebilir tur açılmadı.");
    }

    private static object TableTarget(string kind, int meldIndex)
    {
        var assembly = typeof(MainForm).Assembly;
        return Activator.CreateInstance(assembly.GetType("NaneOkey.UI.TraditionalTableTarget"),
            new[] { Enum.Parse(assembly.GetType("NaneOkey.UI.TraditionalTargetKind"), kind), (object)meldIndex });
    }

    private static void CheckNativeTable(MainForm form)
    {
        Check(!Field<Panel>(form, "_matrisPaneli").Visible, "Geleneksel modda Nane kare tahtası kaldı.");
        var table = Field<Control>(form, "_traditionalTable");
        Check(table.Visible && Field<Panel>(form, "_masaPaneli").ClientRectangle.Contains(table.Bounds), "Geleneksel masa görünmüyor veya sınır dışına taşıyor.");
        Check(Field<Tile[,]>(form, "_masaSlotlari").Cast<Tile>().All(x => x == null), "Geleneksel masa Nane slotlarını oyun verisi olarak kullanıyor.");
    }

    private static bool DragTraditional(MainForm form, Tile tile, string targetKind, int meldIndex = -1)
    {
        var hand = Field<Tile[,]>(form, "_elSlotlari");
        var row = -1;
        var column = -1;
        for (var r = 0; r < hand.GetLength(0); r++)
            for (var c = 0; c < hand.GetLength(1); c++)
                if (hand[r, c] != null && hand[r, c].Id == tile.Id) { row = r; column = c; }
        Check(row >= 0, "Geleneksel sürükleme için taş ıstakada bulunamadı.");
        var assembly = typeof(MainForm).Assembly;
        var kind = Enum.Parse(assembly.GetType("NaneOkey.UI.TileSourceKind"), "Hand");
        Check((bool)Call(form, "CanStartDrag", kind), "Geleneksel elde sürükleme başlayamadı.");
        var data = Activator.CreateInstance(assembly.GetType("NaneOkey.UI.TileDragData"), new object[] { tile.Clone(), kind, row, column });
        var table = Field<Control>(form, "_traditionalTable");
        var bounds = (Rectangle)table.GetType().GetMethod("GetTargetBounds").Invoke(table, new[] { TableTarget(targetKind, meldIndex) });
        Check(!bounds.IsEmpty, "Geleneksel bırakma alanı bulunamadı: " + targetKind);
        Call(form, "BeginCustomDrag", data, 0, Point.Empty);
        var result = (bool)Call(form, "TryTraditionalDrop", data, table.PointToScreen(new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2)));
        if (!result) Call(form, "EndCustomDrag", true);
        Check(Field<object>(form, "_aktifSurukleme") == null, "Geleneksel bırakmadan sonra aktif sürükleme kaldı.");
        return result;
    }

    private static void Select(MainForm form, IEnumerable<Tile> tiles)
    {
        var selection = Field<HashSet<int>>(form, "_selectedHandIds");
        selection.Clear();
        foreach (var tile in tiles) selection.Add(tile.Id);
    }

    private static void SaveGame(MainForm form, string name)
    {
        var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screens");
        Directory.CreateDirectory(directory);
        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            var matrix = Field<Panel>(form, "_boardViewport");
            var native = Field<Control>(form, "_traditionalTable");
            // DrawToBitmap reverses overlapping siblings; restore the editable Nane surface above its felt background.
            if (matrix.Visible && native != null && native.Visible)
            {
                using (var board = new Bitmap(matrix.Width, matrix.Height))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    matrix.DrawToBitmap(board, matrix.ClientRectangle);
                    var position = matrix.PointToScreen(Point.Empty);
                    graphics.DrawImageUnscaled(board, position.X - form.Left, position.Y - form.Top);
                }
            }
            using (var menu = new Bitmap(form.MainMenuStrip.Width, form.MainMenuStrip.Height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                form.MainMenuStrip.DrawToBitmap(menu, form.MainMenuStrip.ClientRectangle);
                var clientOrigin = form.PointToScreen(Point.Empty);
                graphics.DrawImageUnscaled(menu, clientOrigin.X - form.Left, clientOrigin.Y - form.Top);
            }
            bitmap.Save(Path.Combine(directory, name), ImageFormat.Png);
        }
    }

    private static void NativeLayoutScreens(MainForm form)
    {
        var game = Field<Panel>(form, "_oyunPaneli");
        foreach (var mode in new[] { GameMode.ClassicOkey, GameMode.Okey101 })
        {
            var hand = Enumerable.Range(1, mode == GameMode.ClassicOkey ? 15 : 22).Select(number => T(TileColor.Red, (number - 1) % 13 + 1)).ToList();
            Load(form, Controlled(mode, Seat.South, hand, true));
            foreach (var screen in new[] { new Size(800, 600), new Size(1024, 768), new Size(1366, 768) })
            foreach (var dpi in new[] { 96, 120, 144 })
            {
                var client = new Size(screen.Width * 96 / dpi - 16, (screen.Height - 40) * 96 / dpi - 39);
                form.ClientSize = client;
                Call(form, "LayoutGameScreen");
                CheckNativeTable(form);
                var rack = Field<Panel>(form, "_elDisPaneli");
                var board = Field<Panel>(form, "_masaPaneli");
                Check(game.ClientRectangle.Contains(rack.Bounds) && game.ClientRectangle.Contains(board.Bounds) && board.Bottom < rack.Top,
                    "Geleneksel masayla ıstaka ekrana sığmadı: " + mode + " / " + client);
                var rackSlots = Field<Panel[,]>(form, "_elSlotPanelleri");
                var handPanel = Field<Panel>(form, "_elPaneli");
                foreach (var slot in rackSlots) Check(handPanel.ClientRectangle.Contains(slot.Bounds), "Geleneksel ıstaka taşı sınır dışına çıktı.");
                var table = Field<Control>(form, "_traditionalTable");
                foreach (var kind in mode == GameMode.ClassicOkey ? new[] { "Stock", "OwnDiscard", "PreviousDiscard", "Finish" } : new[] { "Stock", "OwnDiscard", "PreviousDiscard", "OpenRuns", "OpenPairs" })
                {
                    var bounds = (Rectangle)table.GetType().GetMethod("GetTargetBounds").Invoke(table, new[] { TableTarget(kind, -1) });
                    Check(!bounds.IsEmpty && table.ClientRectangle.Contains(bounds), "Geleneksel eylem alanı sığmadı: " + kind + " / " + client);
                }
                if (dpi == 96 && screen.Width <= 1024 || dpi == 144 && screen.Width == 800)
                    SaveGame(form, (mode == GameMode.ClassicOkey ? "classic-native-" : "101-native-") + screen.Width + "x" + screen.Height + "-dpi" + dpi + ".png");
                Console.WriteLine("{0}, client {1}x{2}: table {3}x{4}, tile {5}x{6}.", mode, client.Width, client.Height, table.Width, table.Height, rackSlots[0, 0].Width, rackSlots[0, 0].Height);
            }
        }
        form.ClientSize = new Size(1008, 689);
        Call(form, "LayoutGameScreen");
    }

    private static void NativeOpeningActions(MainForm form)
    {
        var opening = new List<Tile>();
        foreach (TileColor color in Enum.GetValues(typeof(TileColor))) opening.Add(T(color, 13));
        foreach (TileColor color in Enum.GetValues(typeof(TileColor))) opening.Add(T(color, 7));
        opening.AddRange(new[] { T(TileColor.Blue, 6), T(TileColor.Blue, 7), T(TileColor.Blue, 8) });
        var reserve = T(TileColor.Black, 1);
        var discard = T(TileColor.Red, 2);
        Load(form, Controlled(GameMode.Okey101, Seat.South, opening.Concat(new[] { reserve, discard }), true));
        CheckNativeTable(form);
        Select(form, opening);
        Check((bool)Call(form, "SubmitSelectedTraditionalMelds", false), "101 seçilmiş 101 puanlık perleri açamadı.");
        var melds = (List<Meld>)Call(form, "BuildMeldsFromBoard");
        var rules = new TraditionalRuleValidator(GameMode.Okey101);
        Check(melds.Count == 3 && melds.All(rules.IsValidMeld) && melds.Sum(rules.MeldValue) == 101, "101 yerel masanın per normalizasyonu yanlış.");
        Check(Field<GameEngine>(form, "_engine").State.Table.Count == 0 && !Field<GameEngine>(form, "_engine").State.Players[0].HasOpened, "101 açılış taş atılmadan kalıcı oldu.");
        Check(Field<Tile[,]>(form, "_elSlotlari").Cast<Tile>().Count(x => x != null) == 2, "Açılan taşlar ıstakadan ayrılmadı.");
        Call(form, "UndoTurn");
        var pendingState = Field<GameEngine>(form, "_engine").State;
        Check(!pendingState.TurnInProgress && pendingState.Table.Count == 0 && Field<Tile[,]>(form, "_elSlotlari").Cast<Tile>().Count(x => x != null) == 13,
            "101 Geri Al açılan perleri fiziksel taşlarıyla ıstakaya geri getirmedi.");
        Select(form, opening);
        Check((bool)Call(form, "SubmitSelectedTraditionalMelds", false), "Geri alınmış 101 perleri yeniden açılamadı.");
        SaveGame(form, "101-native-pending.png");
        Check(DragTraditional(form, discard, "OwnDiscard"), "101 UI 101 puan açılışı yan atışla onaylamadı.");
        var state = Field<GameEngine>(form, "_engine").State;
        Check(state.Players[0].HasOpened && !state.Players[0].OpenedWithPairs && state.Players[0].Hand.Single().Id == reserve.Id, "101 açılış el/açılış durumu yanlış.");
        Check(state.Table.Count == 3 && state.Table.All(x => x.OwnerSeat == Seat.South) && state.CurrentTurn == Seat.East, "101 masa/per sahibi/tur yanlış.");
        CheckNativeTable(form);

        var pairs = new List<Tile>();
        for (var number = 1; number <= 5; number++) pairs.AddRange(new[] { T(TileColor.Red, number), T(TileColor.Red, number) });
        reserve = T(TileColor.Black, 10);
        discard = T(TileColor.Blue, 11);
        Load(form, Controlled(GameMode.Okey101, Seat.South, pairs.Concat(new[] { reserve, discard }), true));
        Select(form, pairs);
        Check((bool)Call(form, "SubmitSelectedTraditionalMelds", true), "101 seçilmiş 5 çifti çift alanına açamadı.");
        Check(DragTraditional(form, discard, "OwnDiscard"), "101 çift açılışı yan atışla onaylanmadı.");
        state = Field<GameEngine>(form, "_engine").State;
        Check(state.Players[0].OpenedWithPairs && state.Table.Count == 5 && state.Table.All(x => x.IsPair && x.Tiles.Count == 2), "101 çift alanı per açılışı gibi uygulandı.");

        var oldMeld = new Meld(new[] { T(TileColor.Red, 4), T(TileColor.Red, 5), T(TileColor.Red, 6) }) { OwnerSeat = Seat.West };
        var extension = T(TileColor.Red, 7);
        reserve = T(TileColor.Black, 10);
        discard = T(TileColor.Blue, 11);
        state = Controlled(GameMode.Okey101, Seat.South, new[] { extension, reserve, discard }, true);
        state.Table.Add(oldMeld);
        state.Players[0].HasOpened = true;
        Load(form, state);
        Check(DragTraditional(form, extension, "Meld", 0), "101 eldeki taş masadaki açık pere işlenemedi.");
        Check(DragTraditional(form, discard, "OwnDiscard"), "101 pere işleme yan atışla onaylanmadı.");
        state = Field<GameEngine>(form, "_engine").State;
        Check(state.Table.Single().Tiles.Count == 4 && state.Table.Single().OwnerSeat == Seat.West && state.Players[0].Hand.Single().Id == reserve.Id, "101 işlenen per sahipliği veya eldeki fiziksel taşlar değişti.");

        var joker = T(TileColor.Red, 5);
        joker.IsJoker = true;
        var jokerMeld = new TraditionalRuleValidator(GameMode.Okey101).NormalizeMeld(new Meld(new[] { T(TileColor.Red, 6), joker, T(TileColor.Red, 8) }) { OwnerSeat = Seat.West });
        var replacement = T(TileColor.Red, 7);
        reserve = T(TileColor.Blue, 10);
        discard = T(TileColor.Black, 11);
        state = Controlled(GameMode.Okey101, Seat.South, new[] { replacement, reserve, discard }, true);
        state.Table.Add(jokerMeld);
        state.Players[0].HasOpened = true;
        Load(form, state);
        Check(DragTraditional(form, replacement, "Meld", 0), "101 okeyin temsil ettiği gerçek taş perdeki okeyle değiştirilemedi.");
        state = Field<GameEngine>(form, "_engine").State;
        Check(state.TurnTable.Single().Tiles.Any(x => x.Id == replacement.Id) && state.TurnTable.Single().Tiles.All(x => x.Id != joker.Id)
            && Field<Tile[,]>(form, "_elSlotlari").Cast<Tile>().Any(x => x != null && x.Id == joker.Id), "101 okey değiştirme per ile ıstaka arasındaki fiziksel taşları kaybetti.");
        Check(DragTraditional(form, discard, "OwnDiscard"), "101 okey değiştirme yan atışla onaylanmadı.");
        state = Field<GameEngine>(form, "_engine").State;
        Check(state.Table.Single().OwnerSeat == Seat.West && state.Players[0].Hand.Single(x => x.Id == joker.Id).JokerNumber == 0, "101 alınan okey eski perdeki temsil değerinde kilitli kaldı.");

        var finishHand = new List<Tile>();
        for (var number = 1; number <= 7; number++) finishHand.AddRange(new[] { T(TileColor.Red, number), T(TileColor.Red, number) });
        discard = T(TileColor.Blue, 13);
        Load(form, Controlled(GameMode.ClassicOkey, Seat.South, finishHand.Concat(new[] { discard }), true));
        SaveGame(form, "classic-native.png");
        Check(DragTraditional(form, discard, "Finish"), "Klasik bitiş taşı gösterge yanına bırakılamadı.");
        state = Field<GameEngine>(form, "_engine").State;
        Check(state.IsGameOver && state.Table.Count == 7 && state.Players[0].Hand.Count == 0, "Klasik bitiş masanın per karelerine bağlı kaldı.");
        SaveGame(form, "classic-native-finished.png");
    }

    private static void NativeDrawAndGuardActions(MainForm form)
    {
        var hand = new[] { T(TileColor.Red, 6), T(TileColor.Red, 7), T(TileColor.Red, 8), T(TileColor.Blue, 11) };
        var state = Controlled(GameMode.Okey101, Seat.South, hand, false);
        var discarded = T(TileColor.Black, 12);
        state.DiscardPiles[(int)Seat.West].Tiles.Add(discarded);
        Load(form, state);
        state = Field<GameEngine>(form, "_engine").State;
        Select(form, hand.Take(3));
        Check(!(bool)Call(form, "SubmitSelectedTraditionalMelds", false), "101 taş çekmeden per açtı.");
        Check(!state.TurnInProgress && state.Table.Count == 0 && state.Players[0].Hand.Count == 4, "Çekmeden açış engeli eli veya masayı değiştirdi.");
        Call(form, "TraditionalTargetClicked", TableTarget("PreviousDiscard", -1));
        state = Field<GameEngine>(form, "_engine").State;
        Check(state.HasDrawnThisTurn && state.DrawnDiscardTileId == discarded.Id && state.DiscardPiles[(int)Seat.West].Tiles.Count == 0,
            "101 önceki oyuncunun görsel atık yığınından taş almadı.");
        var deckId = state.Deck[0].Id;
        Call(form, "TraditionalTargetClicked", TableTarget("Stock", -1));
        state = Field<GameEngine>(form, "_engine").State;
        Check(state.HasDrawnThisTurn && state.DrawnDiscardTileId == 0 && state.Players[0].Hand.Any(x => x.Id == deckId) && !state.Players[0].Hand.Any(x => x.Id == discarded.Id),
            "101 açılmaya uymayan atık taş yerine ortadan taş çekemedi.");
        Check(state.DiscardPiles[(int)Seat.West].Tiles.Single().Id == discarded.Id, "101 geri bırakılan atık taşı önceki oyuncunun yığınına dönmedi.");
        Select(form, new[] { hand[0], hand[1], hand[3] });
        Check(!(bool)Call(form, "SubmitSelectedTraditionalMelds", false), "101 seçilen geçersiz taşları per diye açtı.");
        Check(state.TurnTable.Count == 0 && Field<Tile[,]>(form, "_elSlotlari").Cast<Tile>().Where(x => x != null).Select(x => x.Id).OrderBy(x => x)
            .SequenceEqual(state.Players[0].Hand.Select(x => x.Id).OrderBy(x => x)), "Geçersiz açış fiziksel taşları değiştirdi.");

        var opening = new[] { T(TileColor.Red, 6), T(TileColor.Red, 7), T(TileColor.Red, 8) };
        var extension = T(TileColor.Red, 9);
        var reserve = T(TileColor.Black, 11);
        var discard = T(TileColor.Blue, 12);
        Load(form, Controlled(GameMode.Okey101, Seat.South, opening.Concat(new[] { extension, reserve, discard }), true));
        Select(form, opening);
        Check((bool)Call(form, "SubmitSelectedTraditionalMelds", false), "101 hazırlanan ilk per masaya konamadı.");
        Check(DragTraditional(form, extension, "Meld", 0), "101 henüz baraja ulaşmamış kendi hazırlanan perine taş işleyemedi.");
        state = Field<GameEngine>(form, "_engine").State;
        Check(state.TurnTable.Single().Tiles.Count == 4 && state.Table.Count == 0 && !state.Players[0].HasOpened,
            "101 hazırlanmış perin genişlemesi açılışı erken onayladı.");
        Call(form, "UndoTurn");
        Check(!state.TurnInProgress && Field<Tile[,]>(form, "_elSlotlari").Cast<Tile>().Count(x => x != null) == 6,
            "101 hazırlanmış pere eklenen taş Geri Al ile ıstakaya dönmedi.");

        var closedExtension = T(TileColor.Red, 7);
        state = Controlled(GameMode.Okey101, Seat.South, new[] { closedExtension, reserve, discard }, true);
        state.Table.Add(new Meld(new[] { T(TileColor.Red, 4), T(TileColor.Red, 5), T(TileColor.Red, 6) }) { OwnerSeat = Seat.West });
        Load(form, state);
        state = Field<GameEngine>(form, "_engine").State;
        Check(DragTraditional(form, closedExtension, "Meld", 0), "101 kapalı oyuncunun bıraktığı taş sürükleme akışında tüketilemedi.");
        Check(state.TurnTable.Single().Tiles.Count == 3 && Field<Tile[,]>(form, "_elSlotlari").Cast<Tile>().Any(x => x != null && x.Id == closedExtension.Id),
            "101 açılmamış oyuncu başkasının kalıcı perine taş işledi veya taşı kaybetti.");
    }

    private static void DetachedChatActions(MainForm form)
    {
        Load(form, Controlled(GameMode.ClassicOkey, Seat.South, Enumerable.Range(1, 15).Select(number => T(TileColor.Blue, (number - 1) % 13 + 1)), true));
        var previousHost = Field<LanHost>(form, "_host");
        using (var host = new LanHost())
        using (var chatWindow = new Form { ClientSize = new Size(480, 320), StartPosition = FormStartPosition.Manual, Location = new Point(-16000, -16000), ShowInTaskbar = false, Opacity = 0 })
        {
            var log = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true };
            chatWindow.Controls.Add(log);
            Set(form, "_host", host);
            Set(form, "_logPenceresi", chatWindow);
            Set(form, "_logPencereKutusu", log);
            try
            {
                Call(form, "EnsureDetachedChatInput");
                Call(form, "EnsureDetachedChatInput");
                var panel = chatWindow.Controls.OfType<Panel>().Single(x => x.Name == "traditionalChat");
                Check(chatWindow.Controls.OfType<Panel>().Count(x => x.Name == "traditionalChat") == 1, "Günlük tekrar açılınca sohbet girişini çoğalttı.");
                var input = panel.Controls.OfType<TextBox>().Single();
                var send = panel.Controls.OfType<Button>().Single();
                foreach (var size in new[] { new Size(480, 320), new Size(300, 220) })
                {
                    chatWindow.ClientSize = size;
                    chatWindow.PerformLayout();
                    panel.PerformLayout();
                    Check(chatWindow.ClientRectangle.Contains(panel.Bounds) && chatWindow.ClientRectangle.Contains(log.Bounds) && log.Bottom <= panel.Top,
                        "Ayrı sohbet penceresinde günlük ve gönderme satırı çakışıyor.");
                    Check(panel.ClientRectangle.Contains(input.Bounds) && panel.ClientRectangle.Contains(send.Bounds) && input.Right <= send.Left,
                        "Ayrı sohbet penceresinde yazma alanı ve Gönder düğmesi çakışıyor.");
                }
                input.Text = "Sohbet denemesi";
                typeof(Control).GetMethod("OnClick", PrivateInstance).Invoke(send, new object[] { EventArgs.Empty });
                Check(input.Text.Length == 0 && Field<TextBox>(form, "_sohbetGirdiKutusu").TextLength == 0 && log.Text.Contains("[South] Sohbet denemesi"),
                    "Ayrı sohbetin Gönder düğmesi mevcut gönderme akışını çağırmadı.");
                input.Text = "Enter denemesi";
                var key = new KeyEventArgs(Keys.Enter);
                typeof(Control).GetMethod("OnKeyDown", PrivateInstance).Invoke(input, new object[] { key });
                Check(input.Text.Length == 0 && key.SuppressKeyPress && log.Text.Contains("[South] Enter denemesi"), "Ayrı sohbette Enter mesajı göndermedi.");
                var before = log.Text;
                input.Text = "   ";
                typeof(Control).GetMethod("OnClick", PrivateInstance).Invoke(send, new object[] { EventArgs.Empty });
                Check(before == log.Text, "Ayrı sohbet boş mesajı gönderdi.");
                Set(form, "_host", null);
                input.Text = "Korunan taslak";
                typeof(Control).GetMethod("OnClick", PrivateInstance).Invoke(send, new object[] { EventArgs.Empty });
                Check(input.Text == "Korunan taslak" && log.Text.Contains("Sohbet için önce ağ odası kur"), "Bağlantısız sohbet taslağı kaybetti veya bağlantı bilgisini göstermedi.");
            }
            finally
            {
                Set(form, "_host", previousHost);
                Set(form, "_logPenceresi", null);
                Set(form, "_logPencereKutusu", null);
            }
        }
    }

    private static void CaptureRealTraditionalMatches(MainForm form)
    {
        foreach (var mode in new[] { GameMode.ClassicOkey, GameMode.Okey101 })
        {
            Field<ListBox>(form, "_gunlukKutusu").Items.Clear();
            Field<TextBox>(form, "_sohbetGirdiKutusu").Clear();
            var settings = Settings(mode);
            var names = new[] { "Cenup", "Garp", "Şimal", "Şark" };
            for (var seat = 0; seat < 4; seat++) settings.Players[seat].Name = names[seat];
            Call(form, "StartLocalGame", settings, true);
            StopTimers(form);
            var engine = Field<GameEngine>(form, "_engine");
            for (var turn = 0; turn < 4; turn++)
            {
                var seat = engine.State.CurrentTurn;
                string message;
                if (!engine.State.HasDrawnThisTurn) Check(engine.DrawTile(seat, out message), "Gerçek dağıtılmış önizleme sırasında oyuncu taş alamadı.");
                var discard = engine.State.Players.First(x => x.Seat == seat).Hand.Last();
                Check(engine.DiscardTile(seat, discard.Id, out message), "Gerçek dağıtılmış önizleme sırasında oyuncu taş atamadı.");
                Call(form, "Log", message);
            }
            var state = engine.State;
            Check(state.DiscardPiles.Count == 4 && state.DiscardPiles.All(x => x.Tiles.Count == 1) && state.Players.All(x => x.Hand.Count == (mode == GameMode.ClassicOkey ? 14 : 21)),
                "Gerçek maç önizlemesinde rakip elleri veya dört atık yığını eksik.");
            var tiles = state.Players.SelectMany(x => x.Hand).Concat(state.Deck).Concat(state.DiscardPiles.SelectMany(x => x.Tiles)).Concat(new[] { state.Indicator }).ToList();
            Check(tiles.Count == 106 && tiles.Select(x => x.Id).Distinct().Count() == 106, "Gerçek maç önizlemesi fiziksel taşları değiştirdi.");
            Call(form, "SyncHandSlots", state.Players[0].Hand);
            Call(form, "RefreshUi");
            Call(form, "AutoArrangeHand");
            form.ClientSize = new Size(1008, 689);
            Call(form, "LayoutGameScreen");
            SaveGame(form, (mode == GameMode.ClassicOkey ? "classic" : "101") + "-real-match-1024x768.png");
            form.ClientSize = new Size(517, 334);
            Call(form, "LayoutGameScreen");
            SaveGame(form, (mode == GameMode.ClassicOkey ? "classic" : "101") + "-real-match-800x600-dpi144.png");
            StopTimers(form);
        }
        form.ClientSize = new Size(1008, 689);
        Call(form, "LayoutGameScreen");
    }

    private static void NativeFaceSnapshots(MainForm form)
    {
        var original = T(TileColor.Red, 5);
        Load(form, Controlled(GameMode.ClassicOkey, Seat.South, new[] { original }, true));
        var view = Field<TileView[,]>(form, "_elTasGorunumleri")[0, 0];
        Check(view != null && view.Tile.Id == original.Id && view.Tile.Number == 5 && view.Tile.Color == TileColor.Red && !view.Tile.IsJoker && !view.Tile.IsFalseJoker,
            "Aynı kimlikli snapshot testi başlangıç yüzünü hazırlayamadı.");
        var joker = new Tile(original.Id, TileColor.Blue, 9) { IsJoker = true, JokerNumber = 11, JokerColor = TileColor.Yellow };
        var falseJoker = joker.Clone();
        falseJoker.IsJoker = false; falseJoker.IsFalseJoker = true; falseJoker.JokerNumber = 0; falseJoker.JokerColor = TileColor.Black;
        var plain = new Tile(original.Id, TileColor.Yellow, 2);
        foreach (var face in new[] { joker, falseJoker, plain })
        {
            var snapshot = LanGameStateDto.FromDomain(Controlled(GameMode.ClassicOkey, Seat.South, new[] { face }, true)).ToDomain();
            Field<GameEngine>(form, "_engine").Restore(snapshot);
            Call(form, "SyncHandSlots", snapshot.Players[0].Hand);
            Call(form, "RefreshUi");
            var refreshed = Field<TileView[,]>(form, "_elTasGorunumleri")[0, 0];
            Check(ReferenceEquals(view, refreshed) && refreshed.Tile.Id == original.Id, "Aynı fiziksel kimlikli taş snapshot'ta slotunu veya görünüm nesnesini değiştirdi.");
            Check(refreshed.Tile.Number == face.Number && refreshed.Tile.Color == face.Color && refreshed.Tile.IsJoker == face.IsJoker &&
                refreshed.Tile.IsFalseJoker == face.IsFalseJoker && refreshed.Tile.JokerNumber == face.JokerNumber && refreshed.Tile.JokerColor == face.JokerColor,
                "Aynı fiziksel kimlikli taşın yeni sayı, renk veya okey yüzü görüntüde güncellenmedi.");
        }
    }

    private static void NaneBoardOpeningActions(MainForm form)
    {
        foreach (var useNewAppearance in new[] { false, true })
        {
        var naneOpening = new[] { T(TileColor.Red, 12), T(TileColor.Red, 13), T(TileColor.Red, 1), T(TileColor.Red, 5), T(TileColor.Blue, 5), T(TileColor.Yellow, 5) };
        var naneReserve = T(TileColor.Black, 3);
        var naneState = Controlled(GameMode.NaneOkey, Seat.South, naneOpening.Concat(new[] { naneReserve }), false);
        naneState.UseNewAppearance = useNewAppearance;
        Load(form, naneState);
        Check(Field<Panel>(form, "_matrisPaneli").Visible && Field<Control>(form, "_traditionalTable").Visible == useNewAppearance,
            "Nane serbest düzenleme masası veya seçilen masa görünümü mod dönüşünde kayboldu.");
        Check((bool)Call(form, "EnsureEditableTurn", false), "Nane taş çekmeden düzenlemeyi kaybetti.");
        var hand = Field<Tile[,]>(form, "_elSlotlari");
        Array.Clear(hand, 0, hand.Length);
        for (var index = 0; index < 3; index++) hand[0, index] = naneOpening[index].Clone();
        for (var index = 3; index < 6; index++) hand[1, index - 3] = naneOpening[index].Clone();
        hand[1, 6] = naneReserve.Clone();
        var kind = Enum.Parse(typeof(MainForm).Assembly.GetType("NaneOkey.UI.TileSourceKind"), "Hand");
        Call(form, "BeginGroupDrag", kind, 0, 1, Point.Empty);
        Check((bool)Call(form, "MoveDraggedTileToBoard", 0, 1), "Nane sağ tuşla per bloğunu masaya koyamadı.");
        Call(form, "EndCustomDrag", false);
        kind = Enum.Parse(typeof(MainForm).Assembly.GetType("NaneOkey.UI.TileSourceKind"), "Board");
        Call(form, "BeginGroupDrag", kind, 0, 1, Point.Empty);
        Check((bool)Call(form, "MoveDraggedTileToHand", 0, 7), "Nane aynı tur yerleştirdiği per bloğunu geri alamadı.");
        Call(form, "EndCustomDrag", false);
        kind = Enum.Parse(typeof(MainForm).Assembly.GetType("NaneOkey.UI.TileSourceKind"), "Hand");
        Call(form, "BeginGroupDrag", kind, 0, 7, Point.Empty);
        Check((bool)Call(form, "MoveDraggedTileToBoard", 2, 4), "Nane per bloğunu masada farklı yere taşıyamadı.");
        Call(form, "EndCustomDrag", false);
        for (var index = 3; index < 6; index++) DragToBoard(form, naneOpening[index], 0, index + 3);
        Call(form, "CommitTurn");
        var state = Field<GameEngine>(form, "_engine").State;
        Check(state.Mode == GameMode.NaneOkey && state.Players[0].HasOpened && state.Table.Count == 2, "Nane iki per/12-13-1 açılışı değişti.");
        Check(state.Players[0].Hand.Single().Id == naneReserve.Id && state.CurrentTurn == Seat.West && state.DiscardPiles.Count == 0, "Nane taş atmasız hamle akışı değişti.");
        Check(state.UseNewAppearance == useNewAppearance && state.Table.SelectMany(x => x.Tiles).Concat(state.Players[0].Hand).Select(x => x.Id).OrderBy(x => x)
            .SequenceEqual(naneOpening.Concat(new[] { naneReserve }).Select(x => x.Id).OrderBy(x => x)), "Nane görünümü açılışta taşları veya görünüm seçimini değiştirdi.");
        foreach (var player in state.Players.Skip(1))
            player.Hand.AddRange(Enumerable.Range(1, 15).Select(number => T(TileColor.Blue, (number - 1) % 13 + 1)));
        Call(form, "RefreshUi");
        form.ClientSize = new Size(1008, 689);
        Call(form, "LayoutGameScreen");
        SaveGame(form, useNewAppearance ? "nane-new-appearance.png" : "nane-original-appearance.png");
        }
    }

    private static void AppearanceSettingsAndPersistence(MainForm form)
    {
        foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
        foreach (var requested in new[] { false, true })
        {
            var previous = Settings(mode);
            previous.UseNewAppearance = requested;
            using (var dialog = new NewGameForm(previous))
            {
                dialog.StartPosition = FormStartPosition.Manual;
                dialog.Location = new Point(-16000, -16000);
                dialog.ShowInTaskbar = false;
                dialog.Opacity = 0;
                dialog.Show();
                var box = Field<CheckBox>(dialog, "_newAppearanceBox");
                Check(box.Text == "Yeni görünümü kullan" && box.Visible == (mode == GameMode.NaneOkey), "Yeni görünüm seçimi yanlış modda gösterildi.");
                Check(box.Checked == (mode == GameMode.NaneOkey && requested), "Yeni görünüm seçimi ayar ekranında unutuldu.");
                Check(dialog.CreateSettings().UseNewAppearance == (mode == GameMode.NaneOkey && requested), "Yeni görünüm seçimi ayarlara geçmedi.");
                box.Checked = true;
                Check(dialog.CreateSettings().UseNewAppearance == (mode == GameMode.NaneOkey), "Klasik/101 gizli Nane görünüm seçimini taşıdı.");
                if (mode == GameMode.NaneOkey)
                {
                    Check(dialog.ClientRectangle.Contains(box.Bounds), "Yeni görünüm kutusu ayar ekranının dışına çıktı.");
                    foreach (var other in dialog.Controls.OfType<CheckBox>().Where(x => !ReferenceEquals(x, box)))
                        Check(Rectangle.Intersect(box.Bounds, other.Bounds).IsEmpty, "Yeni görünüm kutusu başka seçim kutusuyla çakıştı.");
                }
            }
            var clone = (GameSettings)typeof(MainForm).GetMethod("CloneSettings", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { previous });
            Check(clone.UseNewAppearance == (mode == GameMode.NaneOkey && requested), "Ayar kopyasında Nane görünümü kayboldu veya diğer moda taşındı.");
            var state = Controlled(mode, Seat.South, new[] { T(TileColor.Red, 2) }, false);
            state.UseNewAppearance = requested;
            Check(state.Clone().UseNewAppearance == requested, "Oyun durumu kopyası görünüm seçimini kaybetti.");
            var dto = LanGameStateDto.FromDomain(state);
            var roundTrip = LanJson.Deserialize<LanGameStateDto>(LanJson.Serialize(dto)).ToDomain();
            Check(dto.UseNewAppearance == (mode == GameMode.NaneOkey && requested) && roundTrip.UseNewAppearance == dto.UseNewAppearance,
                "Ağ snapshot'ı görünüm seçimini kaybetti veya başka moda taşıdı.");
            var legacy = LanJson.Deserialize<LanGameStateDto>("{\"Mode\":0}").ToDomain();
            Check(!legacy.UseNewAppearance, "Görünüm alanı olmayan eski snapshot yeni görünümü kendiliğinden açtı.");
        }
        var announcement = new LanRoomAnnouncement { Mode = GameMode.NaneOkey, UseNewAppearance = true, RoomName = "Nane masa" };
        Check(LanJson.Deserialize<LanRoomAnnouncement>(LanJson.Serialize(announcement)).UseNewAppearance, "LAN oda ilanı yeni görünüm seçimini kaybetti.");
        var lobby = new LanLobbySnapshot { Mode = GameMode.NaneOkey, UseNewAppearance = true };
        Check(LanJson.Deserialize<LanLobbySnapshot>(LanJson.Serialize(lobby)).UseNewAppearance, "LAN lobi seçimi görünümü kaybetti.");
        using (var online = new OnlineClient())
        {
            online.UpdateRoomSettings(GameMode.NaneOkey, 1000, true);
            Check(online.UseNewAppearance && online.CurrentRoomUseNewAppearance, "Online Nane oda ayarı görünümü kaybetti.");
            online.UpdateRoomSettings(GameMode.ClassicOkey, 20, true);
            Check(!online.UseNewAppearance && !online.CurrentRoomUseNewAppearance, "Online klasik oda Nane görünüm tercihini taşıdı.");
        }
        foreach (var useNewAppearance in new[] { true, false })
        {
            var settings = Settings(GameMode.NaneOkey);
            settings.UseNewAppearance = useNewAppearance;
            Call(form, "StartLocalGame", settings, true);
            StopTimers(form);
            var state = Field<GameEngine>(form, "_engine").State;
            Check(state.Mode == GameMode.NaneOkey && state.UseNewAppearance == useNewAppearance && state.Players.All(x => x.Hand.Count == 15),
                "Nane görünüm tercihi başlangıçta unutuldu veya taş dağılımını değiştirdi.");
            if (useNewAppearance)
            {
                var table = Field<Control>(form, "_traditionalTable");
                var stock = (Rectangle)table.GetType().GetMethod("GetTargetBounds").Invoke(table, new[] { TableTarget("Stock", -1) });
                Check(!stock.IsEmpty && table.ClientRectangle.Contains(stock), "Yeni Nane destesinin çekme alanı kayboldu.");
                var deckCount = state.Deck.Count;
                var handIds = state.Players[0].Hand.Select(x => x.Id).ToList();
                var allIds = NanePhysicalIds(state);
                var uiIds = Field<Tile[,]>(form, "_elSlotlari").Cast<Tile>().Concat(Field<Tile[,]>(form, "_masaSlotlari").Cast<Tile>())
                    .Where(x => x != null).Select(x => x.Id).OrderBy(x => x).ToArray();
                table.GetType().GetMethod("OnMouseDown", PrivateInstance).Invoke(table,
                    new object[] { new MouseEventArgs(MouseButtons.Left, 1, stock.Left + stock.Width / 2, stock.Top + stock.Height / 2, 0) });
                Check(Field<object>(form, "_aktifSurukleme") != null, "Yeni Nane destesinden sürükleme başlayamadı.");
                Check(!(bool)Call(form, "MoveDraggedTileToBoard", 0, 0), "Yeni Nane deste önizlemesindeki sahte taş masaya kabul edildi.");
                Call(form, "EndCustomDrag", true);
                Check(state.Deck.Count == deckCount && NanePhysicalIds(state).SequenceEqual(allIds), "Masaya reddedilen stok sürüklemesi fiziksel taşları değiştirdi.");
                Check(Field<Tile[,]>(form, "_elSlotlari").Cast<Tile>().Concat(Field<Tile[,]>(form, "_masaSlotlari").Cast<Tile>())
                    .Where(x => x != null).Select(x => x.Id).OrderBy(x => x).SequenceEqual(uiIds), "Masaya reddedilen stok sürüklemesi UI'da eksi kimlikli veya kayıp taş yarattı.");
                table.GetType().GetMethod("OnMouseDown", PrivateInstance).Invoke(table,
                    new object[] { new MouseEventArgs(MouseButtons.Left, 1, stock.Left + stock.Width / 2, stock.Top + stock.Height / 2, 0) });
                Check((bool)Call(form, "MoveDraggedTileToHand", 1, 20), "Yeni Nane destesinden boş ıstaka konumuna taş çekilemedi.");
                Call(form, "EndCustomDrag", false);
                StopTimers(form);
                var newTile = state.Players[0].Hand.Single(x => !handIds.Contains(x.Id));
                Check(state.CurrentTurn == Seat.West && state.Deck.Count == deckCount - 1 && state.Players[0].Hand.Count == 16 && state.DiscardPiles.Count == 0,
                    "Yeni Nane görünümü taş çekme turunu klasik atma kuralına dönüştürdü.");
                Check(Field<Tile[,]>(form, "_elSlotlari")[1, 20].Id == newTile.Id && state.UseNewAppearance,
                    "Yeni Nane desteden çekilen taşı seçilen ıstaka konumuna koymadı.");
                Check(NanePhysicalIds(state).SequenceEqual(allIds) && allIds.Distinct().Count() == allIds.Length,
                    "Yeni Nane stoktan ıstakaya sürükleme fiziksel taşları kaybetti veya kopyaladı.");
                Call(form, "StartLocalGame", settings, true);
                StopTimers(form);
                state = Field<GameEngine>(form, "_engine").State;
                allIds = NanePhysicalIds(state);
                deckCount = state.Deck.Count;
                stock = (Rectangle)table.GetType().GetMethod("GetTargetBounds").Invoke(table, new[] { TableTarget("Stock", -1) });
                table.GetType().GetMethod("OnMouseClick", PrivateInstance).Invoke(table,
                    new object[] { new MouseEventArgs(MouseButtons.Left, 1, stock.Left + stock.Width / 2, stock.Top + stock.Height / 2, 0) });
                StopTimers(form);
                Check(state.CurrentTurn == Seat.West && state.Players[0].Hand.Count == 16 && state.Deck.Count == deckCount - 1 && state.DiscardPiles.Count == 0,
                    "Yeni Nane desteye tıklayınca tek taş çekip sırayı ilerletmedi.");
                Check(NanePhysicalIds(state).SequenceEqual(allIds), "Yeni Nane stok tıklaması fiziksel taşları değiştirdi.");
            }
        }
    }

    private static int[] NanePhysicalIds(GameState state)
    {
        return state.Players.SelectMany(x => x.Hand).Concat(state.Table.SelectMany(x => x.Tiles)).Concat(state.Deck)
            .Select(x => x.Id).OrderBy(x => x).ToArray();
    }

    private static void StockDragActions(MainForm form)
    {
        form.ClientSize = new Size(1008, 689);
        foreach (GameMode mode in Enum.GetValues(typeof(GameMode)))
        foreach (var newAppearance in mode == GameMode.NaneOkey ? new[] { false, true } : new[] { false })
        {
            var hand = Enumerable.Range(1, mode == GameMode.ClassicOkey ? 14 : mode == GameMode.Okey101 ? 21 : 15)
                .Select(number => T(TileColor.Red, (number - 1) % 13 + 1)).ToList();
            var initial = Controlled(mode, Seat.South, hand, false);
            initial.UseNewAppearance = newAppearance;
            Load(form, initial);
            var state = Field<GameEngine>(form, "_engine").State;
            var deckCount = state.Deck.Count;
            var drawId = state.Deck[0].Id;
            var allIds = NanePhysicalIds(state);
            var viewport = Field<Panel>(form, "_handViewport");
            viewport.AutoScrollPosition = Point.Empty;
            Action start = delegate
            {
                if (mode == GameMode.NaneOkey && !newAppearance)
                    Call(form, "DeckLabelMouseDown", Field<Label>(form, "_desteEtiketi"), new MouseEventArgs(MouseButtons.Left, 1, 100, 22, 0));
                else
                {
                    var table = Field<Control>(form, "_traditionalTable");
                    var stock = (Rectangle)table.GetType().GetMethod("GetTargetBounds").Invoke(table, new[] { TableTarget("Stock", -1) });
                    table.GetType().GetMethod("OnMouseDown", PrivateInstance).Invoke(table,
                        new object[] { new MouseEventArgs(MouseButtons.Left, 1, stock.Left + stock.Width / 2, stock.Top + stock.Height / 2, 0) });
                }
            };
            start();
            Check(Field<object>(form, "_aktifSurukleme") != null, mode + " stok sürüklemesi başlayamadı.");
            var preview = Field<Control>(form, "_suruklemeOnizleme") as TileView;
            Check(preview != null && preview.FaceDown && preview.Tile.Id == -1, "Stok sürüklemesi çekilmemiş gizli taşı gösterdi.");
            Check(state.Deck.Count == deckCount && state.Players[0].Hand.Count == hand.Count, "Sürükleme başlarken taş erken çekildi.");
            Check(viewport.HorizontalScroll.Visible, "Büyük ıstaka için yatay kaydırma alanı hazırlanmadı.");
            var clippedSlot = Field<Panel[,]>(form, "_elSlotPanelleri")[1, 23];
            Check(Call(form, "FindHandSlotAtScreen", clippedSlot.PointToScreen(new Point(clippedSlot.Width / 2, clippedSlot.Height / 2))) == null,
                "Görünmeyen ıstaka yuvası ekran dışından bırakma kabul etti.");
            var edge = viewport.PointToScreen(new Point(viewport.ClientSize.Width - 8, viewport.ClientSize.Height / 2));
            Check((bool)Call(form, "ScrollDragViewportAtEdge", viewport, edge) && viewport.AutoScrollPosition.X < 0,
                "Taş sürüklenirken ıstaka kenarında otomatik kaydırma çalışmadı.");
            if (mode != GameMode.NaneOkey || newAppearance)
                Call(form, "TraditionalTargetClicked", TableTarget("Stock", -1));
            Check(state.Deck.Count == deckCount, "Sürükleme sırasında tıklama ikinci çekiş başlattı.");
            Call(form, "CompleteCustomDrag", form.PointToScreen(new Point(-20, -20)));
            Check(Field<object>(form, "_aktifSurukleme") == null && state.Deck.Count == deckCount && NanePhysicalIds(state).SequenceEqual(allIds),
                "İptal edilen stok sürüklemesi taş çekti veya kimlikleri değiştirdi.");

            // An occupied slot must reject the drop without consuming the stock.
            start();
            var rack = Field<Panel[,]>(form, "_elSlotPanelleri");
            var occupied = rack[0, 0];
            Field<Panel>(form, "_handViewport").ScrollControlIntoView(occupied);
            Call(form, "CompleteCustomDrag", occupied.PointToScreen(new Point(occupied.Width / 2, occupied.Height / 2)));
            Check(state.Deck.Count == deckCount && NanePhysicalIds(state).SequenceEqual(allIds), "Dolu ıstaka üzerine bırakma desteyi tüketti.");

            var slot = rack[1, 14];
            Check(Field<Tile[,]>(form, "_elSlotlari")[1, 14] == null, "Stok testi hedef yuvası boş değil.");
            Field<Panel>(form, "_handViewport").ScrollControlIntoView(slot);
            start();
            Call(form, "CompleteCustomDrag", slot.PointToScreen(new Point(slot.Width / 2, slot.Height / 2)));
            StopTimers(form);
            Check(Field<object>(form, "_aktifSurukleme") == null && state.Deck.Count == deckCount - 1 && state.Players[0].Hand.Count == hand.Count + 1,
                mode + " stoktan ıstakaya bırakma tam bir taş çekmedi.");
            Check(Field<Tile[,]>(form, "_elSlotlari")[1, 14] != null && Field<Tile[,]>(form, "_elSlotlari")[1, 14].Id == drawId,
                "Çekilen taş seçilen ıstaka yuvasına yerleşmedi.");
            Check(NanePhysicalIds(state).SequenceEqual(allIds), "Stok bırakması fiziksel taşı kaybetti veya kopyaladı.");
            if (mode == GameMode.NaneOkey)
                Check(state.CurrentTurn == Seat.West && state.DiscardPiles.Count == 0, "Nane stok sürüklemesi tur kurallarını değiştirdi.");
            else
            {
                Check(state.HasDrawnThisTurn && state.CurrentTurn == Seat.South, "Geleneksel stok sürüklemesi atma aşamasını atladı.");
                start();
                Check(Field<object>(form, "_aktifSurukleme") == null && state.Deck.Count == deckCount - 1, "Aynı turda ikinci stok sürüklemesi başladı.");
                Check(DragTraditional(form, state.Players[0].Hand.Single(tile => tile.Id == drawId), "OwnDiscard"), "Çekilen taş sağa atılamadı.");
                Check(state.CurrentTurn == Seat.East && state.DiscardPiles[0].Tiles.Last().Id == drawId,
                    "Stoktan sürüklenen taş sağdaki oyuncuya aktarılmadı.");
            }
        }
    }

    private static void SnapshotAndRemoteProtection(MainForm form)
    {
        var totals = Field<Dictionary<string, int>>(form, "_totalScores");
        var history = Field<List<RoundScoreRecord>>(form, "_roundHistory");
        Set(form, "_matchId", "test-current-match");
        totals["South"] = 150;
        history.Add(new RoundScoreRecord { RoundNumber = 1, Scores = new Dictionary<string, int> { { "South", 150 } } });
        Call(form, "ApplyNetworkMatchId", "test-current-match");
        Check(totals["South"] == 150 && history.Count == 1, "Aynı maç snapshot'ı biriken puanları sıfırladı.");
        Call(form, "ApplyNetworkMatchId", "test-new-match");
        Check(totals.Count == 0 && history.Count == 0 && Field<string>(form, "_matchId") == "test-new-match", "Yeni ağ maçı eski puan/geçmişi taşımaya devam etti.");
        using (var client = new LanClient())
        {
            var handle = form.Handle;
            Set(form, "_currentSettings", null);
            Set(form, "_yerelKoltuk", Seat.West);
            Set(form, "_agKoltuguAtandi", true);
            Call(form, "ConfigureLanClient", client);
            var state = Controlled(GameMode.Okey101, Seat.West, new[] { T(TileColor.Blue, 2), T(TileColor.Black, 8) }, false);
            var snapshot = new LanGameSnapshot { MatchId = "test-new-match", State = LanGameStateDto.FromDomain(state), TargetScore = 777, TurnSeconds = 55, BotThinkSeconds = 13, EnableLivePreview = false, EnableTurnTimer = true };
            var callback = (Action<GameState, LanGameSnapshot>)typeof(LanClient).GetField("GameStateReceived", PrivateInstance).GetValue(client);
            callback(state, snapshot);
            Application.DoEvents();
            StopTimers(form);
            var settings = Field<GameSettings>(form, "_currentSettings");
            Check(settings.Mode == GameMode.Okey101 && settings.TargetScore == 777 && !settings.EnableLivePreview, "LAN snapshot mod/puan/önizleme UI ayarlarına geçmedi.");
            Check(settings.EnableTurnTimer && settings.TurnSeconds == 55 && settings.BotThinkSeconds == 13, "LAN snapshot süre ayarları kayboldu.");
            Check(Field<GameEngine>(form, "_engine").State.Mode == GameMode.Okey101 && Field<Tile[,]>(form, "_elSlotlari").Cast<Tile>().Count(x => x != null) == 2, "Snapshot yerel ağ koltuğunun elini yüklemedi.");
            CheckNativeTable(form);
            var table = Field<Control>(form, "_traditionalTable");
            Check((Seat)table.GetType().GetProperty("LocalSeat").GetValue(table, null) == Seat.West, "LAN istemcisinin masa yönü kendi koltuğuna dönmedi.");
            foreach (var useNewAppearance in new[] { true, false })
            {
                var nane = Controlled(GameMode.NaneOkey, Seat.West, new[] { T(TileColor.Red, 4), T(TileColor.Blue, 7) }, false);
                nane.UseNewAppearance = useNewAppearance;
                var naneSnapshot = new LanGameSnapshot { MatchId = "test-new-match", State = LanGameStateDto.FromDomain(nane), TargetScore = 1000 };
                callback(nane, naneSnapshot);
                Application.DoEvents();
                StopTimers(form);
                Check(Field<GameEngine>(form, "_engine").State.UseNewAppearance == useNewAppearance && Field<GameSettings>(form, "_currentSettings").UseNewAppearance == useNewAppearance,
                    "LAN snapshot Nane görünüm seçimini istemci ayarına veya duruma taşımadı.");
                Check(Field<Panel>(form, "_matrisPaneli").Visible && Field<Control>(form, "_traditionalTable").Visible == useNewAppearance,
                    "LAN Nane serbest düzenleme masası seçilen görünümü göstermedi.");
                Check(Field<Tile[,]>(form, "_elSlotlari").Cast<Tile>().Count(x => x != null) == 2, "LAN Nane görünüm değişimi yerel ıstakayı kaybetti.");
            }
        }

        var hand = Enumerable.Range(1, 15).Select(x => T(TileColor.Blue, (x - 1) % 13 + 1)).ToList();
        var remoteState = Controlled(GameMode.ClassicOkey, Seat.West, hand, true);
        Load(form, remoteState);
        var engine = Field<GameEngine>(form, "_engine");
        engine.BeginTurn(Seat.West);
        var before = LanJson.Serialize(LanGameStateDto.FromDomain(engine.State));
        var missing = hand.Take(14).Select(x => x.Id).ToList();
        Check(!(bool)Call(form, "ApplyTraditionalDiscard", Seat.West, hand[0].Id, false, new List<Meld>(), missing), "Remote layout taş kaybını kabul etti.");
        Check(before == LanJson.Serialize(LanGameStateDto.FromDomain(engine.State)), "Red edilen remote layout state'i değiştirdi.");
        Call(form, "HandleRemoteDiscardRequest", Seat.North, hand[0].Id, false, new List<Meld>(), hand.Select(x => x.Id).ToList());
        Check(before == LanJson.Serialize(LanGameStateDto.FromDomain(engine.State)), "Sırası olmayan remote oyuncu hamle yaptı.");
        var forgedMeld = new Meld(new[] { T(TileColor.Red, 13), T(TileColor.Red, 12), T(TileColor.Red, 11) });
        Check(!(bool)Call(form, "ApplyTraditionalDiscard", Seat.West, hand[0].Id, false, new List<Meld> { forgedMeld }, hand.Select(x => x.Id).ToList()), "Remote sahte fiziksel taşları kabul etti.");
        Check(before == LanJson.Serialize(LanGameStateDto.FromDomain(engine.State)), "Sahte taşlı remote layout state'i değiştirdi.");

        totals["West"] = 82;
        totals["South"] = 125;
        history.Add(new RoundScoreRecord { RoundNumber = 1, Scores = new Dictionary<string, int> { { "West", 82 }, { "South", 125 } } });
        Call(form, "ApplyRemotePlayerName", Seat.West, "South");
        var renamed = engine.State.Players[1].Name;
        Check(renamed != "South" && engine.State.Players.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 4, "Remote ad çakışması benzersizleştirilmedi.");
        Check(totals[renamed] == 82 && totals["South"] == 125 && !totals.ContainsKey("West"), "Remote ad değişimi toplam puanları birleştirdi veya kaybetti.");
        Check(history.Last().Scores[renamed] == 82 && history.Last().Scores["South"] == 125 && !history.Last().Scores.ContainsKey("West"), "Remote ad değişimi tur geçmişini kaybetti.");

        using (var online = new OnlineClient())
        {
            online.UpdateRoomSettings(GameMode.ClassicOkey, 20);
            Check(online.Mode == GameMode.ClassicOkey && online.TargetScore == 20 && online.CurrentRoomMode == GameMode.ClassicOkey, "Online yeni oyun modu oda ayarları API'sinde kayboldu.");
        }
    }

    private static Assembly ResolvePhoton(object sender, ResolveEventArgs args)
    {
        if (new AssemblyName(args.Name).Name != "Photon-DotNet") return null;
        using (var source = typeof(MainForm).Assembly.GetManifestResourceStream("Photon-DotNet.dll"))
        using (var bytes = new MemoryStream())
        {
            source.CopyTo(bytes);
            return Assembly.Load(bytes.ToArray());
        }
    }

    [STAThread]
    public static int Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolvePhoton;
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ModeSelectionAndSettings();
            using (var form = new MainForm())
            {
                form.MinimumSize = Size.Empty;
                form.ClientSize = new Size(1008, 689);
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-16000, -16000);
                form.ShowInTaskbar = false;
                form.Opacity = 0;
                Set(form, "_kesifBaslatildi", true);
                form.Show();
                MenuAndDebugUnlock(form);
                StartAndClassicActions(form);
                NativeLayoutScreens(form);
                NativeOpeningActions(form);
                NativeDrawAndGuardActions(form);
                DetachedChatActions(form);
                NativeFaceSnapshots(form);
                CaptureRealTraditionalMatches(form);
                NaneBoardOpeningActions(form);
                AppearanceSettingsAndPersistence(form);
                StockDragActions(form);
                SnapshotAndRemoteProtection(form);
                StopTimers(form);
            }
            Console.WriteLine("UiWorkflowTests: " + _checks + " kontrol geçti.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
