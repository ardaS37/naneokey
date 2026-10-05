using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NaneOkey.Domain;
using NaneOkey.Engine;
using NaneOkey.UI;

internal static class ScreenLayoutTests
{
    private static int _checks;

    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new MainForm())
            {
                form.MinimumSize = Size.Empty;
                var game = Field<Panel>(form, "_oyunPaneli");
                var home = Field<Panel>(form, "_anasayfaPaneli");
                game.Dock = DockStyle.None;
                home.Dock = DockStyle.None;
                var screens = new[] { new Size(800, 600), new Size(1024, 768), new Size(1366, 768) };
                foreach (var settings in new[] {
                    new GameSettings { Mode = GameMode.NaneOkey },
                    new GameSettings { Mode = GameMode.NaneOkey, UseNewAppearance = true },
                    new GameSettings { Mode = GameMode.ClassicOkey },
                    new GameSettings { Mode = GameMode.Okey101 }
                })
                {
                Field<GameEngine>(form, "_engine").State.Mode = settings.Mode;
                Field<GameEngine>(form, "_engine").State.UseNewAppearance = settings.UseNewAppearance;
                typeof(MainForm).GetField("_currentSettings", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(form, settings);
                foreach (var screen in screens)
                {
                    foreach (var dpi in new[] { 96, 120, 144 })
                    {
                        // Screen.WorkingArea is logical when Windows scales an unaware Win7 application.
                        // Reserve the taskbar and window chrome before testing that client area.
                        var client = new Size(screen.Width * 96 / dpi - 16, (screen.Height - 40) * 96 / dpi - 39);
                        form.ClientSize = client;
                        game.Size = client;
                        home.Size = client;
                        Invoke(form, "LayoutGameScreen");
                        CheckGame(form, game);
                        if (settings.Mode != GameMode.NaneOkey) CheckTraditionalPacking(form, settings.Mode);
                        CheckHome(home);
                        var tiles = Field<Panel[,]>(form, "_elSlotPanelleri");
                        Check(tiles[0, 0].Width >= 30 && tiles[0, 0].Height >= 40, "Taşlar okunabilir taban boyutundan küçük.");
                        if (client.Width >= 1000) Check(tiles[0, 0].Width >= 36, "Normal ekranda taşlar gereğinden küçük.");
                        Console.WriteLine("{7}, {0}x{1}, DPI {2}: client {3}x{4}, tile {5}x{6}, shelves fit.",
                            screen.Width, screen.Height, dpi, client.Width, client.Height, tiles[0, 0].Width, tiles[0, 0].Height,
                            settings.Mode + (settings.UseNewAppearance ? " new" : ""));
                    }
                }
                }
            }
            Console.WriteLine("Screen layout: " + _checks + " bounds checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CheckGame(MainForm form, Panel game)
    {
        var state = Field<GameEngine>(form, "_engine").State;
        var newAppearance = state.Mode != GameMode.NaneOkey || state.UseNewAppearance;
        var compactItems = Field<HashSet<Control>>(form, "_compactSideMenuItems");
        foreach (var name in new[] { "_solMenusuPaneli", "_masaPaneli", "_elDisPaneli", "_gunlukPaneli" })
        {
            var panel = Field<Panel>(form, name);
            if (name == "_gunlukPaneli" && game.ClientSize.Width < 1250) continue;
            Fits(game, panel, name);
            foreach (Control control in panel.Controls)
            {
                if (name == "_solMenusuPaneli" && compactItems.Contains(control)) continue;
                if (name == "_masaPaneli" && newAppearance && control is Label) continue;
                if (name == "_masaPaneli" && state.Mode != GameMode.NaneOkey && control == Field<Panel>(form, "_boardViewport")) continue;
                Fits(panel, control, name + "/" + control.GetType().Name);
            }
        }
        var matrix = Field<Panel>(form, "_matrisPaneli");
        var hand = Field<Panel>(form, "_elPaneli");
        foreach (var slot in Field<Panel[,]>(form, "_masaSlotPanelleri")) Fits(matrix, slot, "board slot");
        foreach (var slot in Field<Panel[,]>(form, "_elSlotPanelleri")) Fits(hand, slot, "hand slot");
        if (state.Mode == GameMode.NaneOkey)
            CheckScrollContent(Field<Panel>(form, "_boardViewport"), matrix, "masa kaydırma alanı");
        var handViewport = Field<Panel>(form, "_handViewport");
        CheckScrollContent(handViewport, hand, "ıstaka kaydırma alanı");
        Check(hand.Height <= handViewport.ClientSize.Height, "İki raf aynı anda görünmüyor.");
        var board = Field<Panel>(form, "_masaPaneli");
        var rack = Field<Panel>(form, "_elDisPaneli");
        if (board.Bottom >= rack.Top) throw new Exception("Board and shelves overlap.");
        _checks++;
    }

    private static void CheckHome(Panel home)
    {
        foreach (var card in home.Controls.OfType<Panel>())
        {
            Fits(home, card, "home card");
            foreach (Control control in card.Controls) Fits(card, control, "home card/" + control.GetType().Name);
        }
    }

    private static void CheckTraditionalPacking(MainForm form, GameMode mode)
    {
        Invoke(form, "ClearHandSlots");
        var count = mode == GameMode.Okey101 ? 22 : 15;
        var tiles = Enumerable.Range(1, count).Select(x => new Tile(100000 + x, TileColor.Red, (x - 1) % 13 + 1)).ToList();
        typeof(MainForm).GetMethod("SyncHandSlots", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, new object[] { tiles });
        var hand = Field<Tile[,]>(form, "_elSlotlari");
        var panels = Field<Panel[,]>(form, "_elSlotPanelleri");
        var viewport = Field<Panel>(form, "_handViewport");
        Check(hand.Cast<Tile>().Where(x => x != null).Select(x => x.Id).OrderBy(x => x).SequenceEqual(tiles.Select(x => x.Id).OrderBy(x => x)),
            "İki raflı başlangıç dizimi taş kaybetti.");
        Check(Enumerable.Range(0, hand.GetLength(1)).Any(x => hand[1, x] != null), "Başlangıç eli iki rafa dağılmadı.");
        for (var row = 0; row < hand.GetLength(0); row++)
            for (var column = 0; column < hand.GetLength(1); column++)
                if (hand[row, column] != null)
                {
                    var point = viewport.PointToClient(panels[row, column].PointToScreen(Point.Empty));
                    Check(viewport.ClientRectangle.Contains(new Rectangle(point, panels[row, column].Size)),
                        "Başlangıçtaki taşın görünmesi için ıstakayı kaydırmak gerekiyor.");
                }
        Invoke(form, "ClearHandSlots");
    }

    private static void Fits(Control parent, Control child, string name)
    {
        if (child.Left < 0 || child.Top < 0 || child.Right > parent.ClientSize.Width || child.Bottom > parent.ClientSize.Height)
        {
            throw new Exception(name + " exceeds " + parent.ClientSize + ": " + child.Bounds);
        }
        _checks++;
    }

    private static void CheckScrollContent(Panel viewport, Control content, string name)
    {
        Check(content.Parent == viewport && viewport.AutoScroll, name + " tam içerik panelini taşımıyor.");
        var virtualBounds = content.Bounds;
        virtualBounds.Offset(-viewport.AutoScrollPosition.X, -viewport.AutoScrollPosition.Y);
        var virtualWidth = Math.Max(viewport.DisplayRectangle.Width, viewport.AutoScrollMinSize.Width);
        var virtualHeight = Math.Max(viewport.DisplayRectangle.Height, viewport.AutoScrollMinSize.Height);
        Check(virtualBounds.Left >= 0 && virtualBounds.Top >= 0 && virtualBounds.Right <= virtualWidth && virtualBounds.Bottom <= virtualHeight,
            name + " kaydırılarak ulaşılamayan içerik barındırıyor.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++;
    }

    private static T Field<T>(MainForm form, string name)
    {
        return (T)typeof(MainForm).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
    }

    private static void Invoke(MainForm form, string name)
    {
        typeof(MainForm).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, null);
    }
}
