using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Media;
using System.Windows.Forms;
using NaneOkey.Domain;
using NaneOkey.Engine;
using NaneOkey.Network;

namespace NaneOkey.UI
{
    internal enum TileSourceKind
    {
        Hand,
        Board,
        Deck
    }

    internal sealed class TileDragData
    {
        public TileDragData(Tile tile, TileSourceKind sourceKind, int indexA, int indexB)
        {
            Tile = tile;
            SourceKind = sourceKind;
            IndexA = indexA;
            IndexB = indexB;
        }

        public Tile Tile { get; private set; }

        public TileSourceKind SourceKind { get; private set; }

        public int IndexA { get; private set; }

        public int IndexB { get; private set; }

        public bool IsGroupDrag { get; set; }

        public int GroupStartColumn { get; set; }

        public int GroupClickedOffset { get; set; }

        public List<Tile> GroupTiles { get; set; }

        public List<int> GroupVisualModes { get; set; }

        public int OriginalVisualMode { get; set; }

        public bool SourceRemoved { get; set; }
    }

    internal sealed class RotatedSeatLabel : Label
    {
        public float Angle { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var backBrush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(backBrush, ClientRectangle);
            }

            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, SystemColors.ControlDark, ButtonBorderStyle.Solid);
            e.Graphics.TranslateTransform(Width / 2F, Height / 2F);
            e.Graphics.RotateTransform(Angle);

            var rect = Angle == 0F || Angle == 180F
                ? new RectangleF(-Width / 2F, -Height / 2F, Width, Height)
                : new RectangleF(-Height / 2F, -Width / 2F, Height, Width);

            using (var textBrush = new SolidBrush(ForeColor))
            using (var format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisWord;
                format.FormatFlags = StringFormatFlags.LineLimit;
                e.Graphics.DrawString(Text, Font, textBrush, rect, format);
            }

            e.Graphics.ResetTransform();
        }
    }

    internal sealed class ConfettiParticle
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float SpeedX { get; set; }
        public float SpeedY { get; set; }
        public float Size { get; set; }
        public Color Color { get; set; }
    }

    internal sealed class BotTurnResult
    {
        public Seat Seat { get; set; }
        public IList<int> BeforeHandIds { get; set; }
        public IList<Meld> BeforeTable { get; set; }
        public GameState State { get; set; }
        public string Message { get; set; }
        public string DebugInfo { get; set; }
        public bool Success { get; set; }
        public int TurnId { get; set; }
    }

    internal sealed class LogEntry
    {
        public string TimeText { get; set; }
        public string Text { get; set; }
        public string EmoteCode { get; set; }
        public string RenderText { get; set; }
    }

    public sealed partial class MainForm : Form
    {
        private const int BoardRows = 10;
        private const int BoardCols = 22;
        private const int HandRows = 2;
        private const int HandCols = 24;
        private const int TileWidth = 36;
        private const int TileHeight = 48;
        private const int BoardGapX = 1;
        private const int BoardGapY = 1;
        private const int HandGapX = 1;
        private const int HandGapY = 1;
        private const int AutoMeldGapCols = 2;
        // Configure your own Photon Realtime App ID locally; never commit credentials.
        private const string PhotonRealtimeAppId = "00000000-0000-0000-0000-000000000000";
        private Color _tahtaAcikRenk = Color.FromArgb(37, 111, 167);
        private Color _tahtaKoyuRenk = Color.FromArgb(13, 62, 118);
        private Color _matrisArkaRenk = Color.FromArgb(18, 67, 112);
        private Color _slotArkaRenk = Color.FromArgb(24, 76, 121);
        private Color _slotCizgiRenk = Color.FromArgb(65, 115, 156);
        private readonly RuleValidator _tahtaDogrulayici = new RuleValidator();
        private readonly Random _rastgeleMasaYerlesimi = new Random();
        private readonly GameEngine _engine = new GameEngine();
        private readonly Dictionary<Seat, Label> _oyuncuEtiketleri = new Dictionary<Seat, Label>();
        private readonly ListBox _gunlukKutusu = new ListBox();
        private readonly TextBox _sohbetGirdiKutusu = new TextBox();
        private readonly Button _sohbetGonderButonu = new Button();
        private readonly Button _ifadeButonu = new Button();
        private readonly Button _cayButonu = new Button();
        private readonly ToolStripDropDown _ifadeMenusu = new ToolStripDropDown();
        private readonly ListBox _odaListesiKutusu = new ListBox();
        private readonly Label _odaDurumEtiketi = new Label();
        private readonly Label _desteEtiketi = new Label();
        private readonly Label _durumEtiketi = new Label();
        private readonly Label _botDusunmeEtiketi = new Label();
        private readonly Label _hamleSuresiEtiketi = new Label();
        private readonly Panel _elPaneli = new Panel();
        private readonly Panel _anasayfaPaneli = new Panel();
        private readonly Panel _oyunPaneli = new Panel();
        private readonly Panel _masaPaneli = new Panel();
        private readonly Panel _matrisPaneli = new Panel();
        private readonly Panel _boardViewport = new Panel();
        private readonly Panel _handViewport = new Panel();
        private readonly Panel _solMenusuPaneli = new Panel();
        private readonly Panel _elDisPaneli = new Panel();
        private readonly Panel _gunlukPaneli = new Panel();
        private readonly Panel _kutlamaPaneli = new Panel();
        private readonly Timer _botZamani = new Timer();
        private readonly Timer _animasyonZamani = new Timer();
        private readonly Timer _kutlamaZamani = new Timer();
        private readonly Timer _turnTimer = new Timer();
        private readonly Timer _cayAnimasyonZamani = new Timer();
        private readonly Timer _cayUcusZamani = new Timer();
        private readonly Timer _dragScrollTimer = new Timer();
        private readonly Tile[,] _masaSlotlari = new Tile[BoardRows, BoardCols];
        private readonly int[,] _masaYonleri = new int[BoardRows, BoardCols];
        private readonly Panel[,] _masaSlotPanelleri = new Panel[BoardRows, BoardCols];
        private readonly TileView[,] _masaTasGorunumleri = new TileView[BoardRows, BoardCols];
        private readonly Tile[,] _elSlotlari = new Tile[HandRows, HandCols];
        private readonly int[,] _elGorunumleri = new int[HandRows, HandCols];
        private readonly Panel[,] _elSlotPanelleri = new Panel[HandRows, HandCols];
        private readonly TileView[,] _elTasGorunumleri = new TileView[HandRows, HandCols];
        private float _uiScale = 1F;
        private float _boardUiScale = 1F;
        private bool _handNeedsHorizontalScroll;
        private readonly Dictionary<Control, int> _sideMenuOrder = new Dictionary<Control, int>();
        private int _cayAnimasyonAdimi;
        private Image _cayEmoteImage;
        private PictureBox _cayUcusGorunumu;
        private PointF _cayUcusBaslangic;
        private PointF _cayUcusBitis;
        private int _cayUcusAdimi;
        private int _cayUcusToplamAdim;
        private bool _oyunBasladi;
        private readonly LanDiscoveryService _discovery = new LanDiscoveryService();
        private LanHost _host;
        private LanClient _client;
        private OnlineClient _onlineClient;
        private bool _onlineHostModu;
        private readonly Dictionary<int, Seat> _onlineActorSeats = new Dictionary<int, Seat>();
        private readonly List<Seat> _onlinePendingBotSeats = new List<Seat>();
        private bool _agIstemcisiModu;
        private Seat _yerelKoltuk = Seat.South;
        private bool _agKoltuguAtandi;
        private GameState _bekleyenAgDurumu;
        private TileDragData _aktifSurukleme;
        private Control _suruklemeOnizleme;
        private Point _suruklemeOfseti;
        private TileView _animasyonGorunumu;
        private PointF _animasyonBaslangic;
        private PointF _animasyonBitis;
        private int _animasyonAdimi;
        private int _animasyonToplamAdim;
        private Action _animasyonSonrasi;
        private GameSettings _currentSettings;
        private string _matchId;
        private readonly List<RoundScoreRecord> _roundHistory = new List<RoundScoreRecord>();
        private readonly Dictionary<string, int> _totalScores = new Dictionary<string, int>();
        private readonly List<ConfettiParticle> _konfetiler = new List<ConfettiParticle>();
        private Form _logPenceresi;
        private TextBox _logPencereKutusu;
        private Form _botDebugPenceresi;
        private ToolStripMenuItem _botDebugMenuItem;
        private bool _botDebugEnabled;
        private TextBox _botDebugKutusu;
        private readonly List<string> _botDebugKayitlari = new List<string>();
        private bool _showingRoundScore;
        private bool _roundScoreShownForCurrentGame;
        private int _kutlamaKareSayisi;
        private byte[] _embeddedMoveSoundBytes;
        private byte[] _embeddedRoundWinSoundBytes;
        private byte[] _embeddedLoseSoundBytes;
        private byte[] _embeddedFullWinSoundBytes;
        private byte[] _embeddedTurnStartSoundBytes;
        private Image _emoteSprite;
        private ProgressBar _botDusunmeCubugu;
        private ProgressBar _hamleSuresiCubugu;
        private bool _botDusunuyor;
        private int _botTurnId;
        private Point? _bekleyenAgCekmeSlotu;
        private bool _kesifBaslatildi;
        private string _lastPlayedGameStartToken;
        private MouseButtons _aktifSuruklemeTus;
        private DateTime _turnDeadline;
        private Seat? _turnTimerSeat;

        public MainForm()
        {
            Text = "Nane Okey";
            AutoScaleMode = AutoScaleMode.None;
            var workingArea = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(1420, workingArea.Width), Math.Min(840, workingArea.Height));
            MinimumSize = new Size(Math.Min(800, workingArea.Width), Math.Min(560, workingArea.Height));
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(20, 56, 38);
            Font = new Font("Tahoma", 9F, FontStyle.Regular, GraphicsUnit.Point, 162);
            DoubleBuffered = true;
            KeyPreview = true;
            try
            {
                var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (exeIcon != null)
                {
                    Icon = exeIcon;
                }
            }
            catch
            {
            }
            MouseMove += MainFormMouseMove;
            MouseUp += MainFormMouseUp;
            SetDoubleBuffered(_oyunPaneli);
            SetDoubleBuffered(_masaPaneli);
            SetDoubleBuffered(_matrisPaneli);
            SetDoubleBuffered(_boardViewport);
            SetDoubleBuffered(_handViewport);
            SetDoubleBuffered(_elPaneli);
            SetDoubleBuffered(_elDisPaneli);
            SetDoubleBuffered(_solMenusuPaneli);
            SetDoubleBuffered(_gunlukPaneli);
            SetDoubleBuffered(_anasayfaPaneli);
            SetDoubleBuffered(_kutlamaPaneli);

            BuildMenu();
            BuildHomeScreen();
            BuildGameScreen();
            Resize += (_, __) => LayoutGameScreen();
            Shown += (_, __) => TryStartDiscovery();

            _botZamani.Interval = 850;
            _botZamani.Tick += BotTimerOnTick;
            _animasyonZamani.Interval = 15;
            _animasyonZamani.Tick += AnimationTimerOnTick;
            _kutlamaZamani.Interval = 30;
            _kutlamaZamani.Tick += CelebrationTimerOnTick;
            _turnTimer.Interval = 500;
            _turnTimer.Tick += TurnTimerOnTick;
            _cayAnimasyonZamani.Interval = 80;
            _cayAnimasyonZamani.Tick += CayAnimasyonTimerOnTick;
            _cayUcusZamani.Interval = 18;
            _cayUcusZamani.Tick += CayUcusTimerOnTick;
            _dragScrollTimer.Interval = 70;
            _dragScrollTimer.Tick += delegate
            {
                if (_aktifSurukleme == null) return;
                var location = Cursor.Position;
                if (ScrollDragViewportAtEdge(_handViewport, location) || ScrollDragViewportAtEdge(_boardViewport, location))
                    UpdateDragPreviewPosition();
            };
            _discovery.RoomsUpdated += rooms => InvokeOnUi(() => RefreshRoomList(rooms));
            _discovery.QueryReceived += endpoint => InvokeOnUi(() => HandleDiscoveryQuery(endpoint));

            ShowHomeScreen();
            ClearBoardSlots();
            ClearHandSlots();
        }

        private void InvokeOnUi(Action action)
        {
            if (action == null || IsDisposed || Disposing)
            {
                return;
            }

            if (!IsHandleCreated)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        private void BuildMenu()
        {
            var menuStrip = new MenuStrip
            {
                BackColor = Color.FromArgb(240, 230, 210),
                Dock = DockStyle.Top
            };

            var oyunMenusu = new ToolStripMenuItem("Oyun");
            oyunMenusu.DropDownItems.Add("Ana Menü", null, (_, __) => ShowHomeScreen());
            oyunMenusu.DropDownItems.Add("Yeni Oyun", null, (_, __) => ShowNewGameDialog());
            oyunMenusu.DropDownItems.Add("Soldan Taş Al", null, (_, __) => DrawDiscardTile());
            oyunMenusu.DropDownItems.Add("Taş At / Bitir", null, (_, __) => ShowTraditionalDiscardDialog());
            oyunMenusu.DropDownItems.Add("Çıkış", null, (_, __) => Close());

            var agMenusu = new ToolStripMenuItem("Ağ");
            agMenusu.DropDownItems.Add("Oda Kur", null, (_, __) => StartLanHost());
            agMenusu.DropDownItems.Add("Odaya Katıl", null, (_, __) => JoinLanHost());

            var onlineMenusu = new ToolStripMenuItem("Online");
            onlineMenusu.DropDownItems.Add("Online Oda Kur", null, (_, __) => StartOnlineRoom());
            onlineMenusu.DropDownItems.Add("Online Odaya Katıl", null, (_, __) => JoinOnlineRoomPrompt());
            onlineMenusu.DropDownItems.Add("Online Odaları Bul", null, (_, __) => RefreshOnlineRooms());

            var yardimMenusu = new ToolStripMenuItem("Yardım");
            yardimMenusu.DropDownItems.Add("Nasıl Oynanır", null, (_, __) => ShowHowToPlay());
            yardimMenusu.DropDownItems.Add("Günlük / Sohbet", null, (_, __) => ShowLogWindow());
            _botDebugMenuItem = new ToolStripMenuItem("Bot Debug", null, (_, __) => ShowBotDebugWindow()) { Visible = false };
            yardimMenusu.DropDownItems.Add(_botDebugMenuItem);
            yardimMenusu.DropDownItems.Add("Hakkında", null, (_, __) => ShowAboutDialog());

            menuStrip.Items.Add(oyunMenusu);
            menuStrip.Items.Add(agMenusu);
            menuStrip.Items.Add(onlineMenusu);
            menuStrip.Items.Add(yardimMenusu);
            MainMenuStrip = menuStrip;
            Controls.Add(menuStrip);
        }

        private void ShowHowToPlay()
        {
            using (var dialog = new HowToPlayForm(_engine.State.Mode))
            {
                dialog.ShowDialog(this);
            }
        }

        private void BuildHomeScreen()
        {
            _anasayfaPaneli.Dock = DockStyle.Fill;
            _anasayfaPaneli.BackColor = Color.FromArgb(23, 74, 46);
            _anasayfaPaneli.Padding = new Padding(24, 48, 24, 24);

            var baslik = new Label
            {
                Text = "Nane Okey",
                Font = new Font("Georgia", 28F, FontStyle.Bold),
                ForeColor = Color.FromArgb(250, 235, 205),
                AutoSize = false,
                Height = 72,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter
            };

            var altBaslik = new Label
            {
                Text = "Lütfen oyun başlatın.",
                Font = new Font("Tahoma", 11F, FontStyle.Regular),
                ForeColor = Color.FromArgb(230, 220, 190),
                AutoSize = false,
                Height = 40,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.TopCenter
            };

            var kart = new Panel
            {
                Width = 430,
                Height = 460,
                BackColor = Color.FromArgb(85, 53, 30),
                BorderStyle = BorderStyle.FixedSingle
            };
            kart.Paint += DrawWoodPanel;

            var odaKart = new Panel
            {
                Width = 340,
                Height = 460,
                BackColor = Color.FromArgb(85, 53, 30),
                BorderStyle = BorderStyle.FixedSingle
            };
            odaKart.Paint += DrawWoodPanel;

            var odaBaslik = new Label
            {
                Text = "Açık Odalar",
                Left = 20,
                Top = 24,
                Width = 300,
                Height = 32,
                Font = new Font("Georgia", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(250, 235, 205),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var odaBilgi = new Label
            {
                Text = "Yerel ağdaki odaları görmek için burada bekleyin.\nÇift tıklayarak katılabilirsiniz.",
                Left = 18,
                Top = 68,
                Width = 304,
                Height = 42,
                Font = new Font("Tahoma", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(236, 223, 195),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            _odaListesiKutusu.Left = 20;
            _odaListesiKutusu.Top = 126;
            _odaListesiKutusu.Width = 300;
            _odaListesiKutusu.Height = 188;
            _odaListesiKutusu.DoubleClick += (_, __) => JoinSelectedRoom();

            var yenileButonu = new Button
            {
                Text = "Açık Odaları Yenile",
                Left = 20,
                Top = 322,
                Width = 300,
                Height = 34,
                Font = new Font("Tahoma", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 221, 181),
                FlatStyle = FlatStyle.Flat
            };
            yenileButonu.FlatAppearance.BorderColor = Color.FromArgb(110, 68, 33);
            yenileButonu.FlatAppearance.BorderSize = 1;
            yenileButonu.Click += (_, __) => RetryDiscovery();

            var onlineYenileButonu = new Button
            {
                Text = "Online Odaları Bul",
                Left = 20,
                Top = 356,
                Width = 300,
                Height = 34,
                Font = new Font("Tahoma", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 221, 181),
                FlatStyle = FlatStyle.Flat
            };
            onlineYenileButonu.FlatAppearance.BorderColor = Color.FromArgb(110, 68, 33);
            onlineYenileButonu.FlatAppearance.BorderSize = 1;
            onlineYenileButonu.Click += (_, __) => RefreshOnlineRooms();

            _odaDurumEtiketi.Left = 20;
            _odaDurumEtiketi.Top = 394;
            _odaDurumEtiketi.Width = 300;
            _odaDurumEtiketi.Height = 22;
            _odaDurumEtiketi.ForeColor = Color.FromArgb(236, 223, 195);
            _odaDurumEtiketi.TextAlign = ContentAlignment.MiddleCenter;
            _odaDurumEtiketi.Text = "Açık odalar taranıyor.";

            odaKart.Controls.Add(odaBaslik);
            odaKart.Controls.Add(odaBilgi);
            odaKart.Controls.Add(_odaListesiKutusu);
            odaKart.Controls.Add(yenileButonu);
            odaKart.Controls.Add(onlineYenileButonu);
            odaKart.Controls.Add(_odaDurumEtiketi);

            var tekOyun = CreateHomeButton("Tek Oyunculu Başla", (_, __) => ShowNewGameDialog());

            var yeniOyun = CreateHomeButton("Oyuncuları Ayarla", (_, __) => ShowNewGameDialog());

            var agKur = CreateHomeButton("Ağ Odası Kur", (_, __) =>
            {
                StartLanHost();
            });

            var agKat = CreateHomeButton("Ağ Oyununa Katıl", (_, __) =>
            {
                ShowGameScreen();
                JoinLanHost();
            });

            var onlineKur = CreateHomeButton("Online Oda Kur", (_, __) => StartOnlineRoom());

            var onlineKat = CreateHomeButton("Online Odaya Katıl", (_, __) => JoinOnlineRoomPrompt());

            kart.Controls.Add(tekOyun);
            kart.Controls.Add(yeniOyun);
            kart.Controls.Add(agKur);
            kart.Controls.Add(agKat);
            kart.Controls.Add(onlineKur);
            kart.Controls.Add(onlineKat);
            CenterHomeButtons(kart, new[] { tekOyun, yeniOyun, agKur, agKat, onlineKur, onlineKat });

            _anasayfaPaneli.Controls.Add(kart);
            _anasayfaPaneli.Controls.Add(odaKart);
            _anasayfaPaneli.Controls.Add(altBaslik);
            _anasayfaPaneli.Controls.Add(baslik);
            Controls.Add(_anasayfaPaneli);

            var homeControlBounds = kart.Controls.Cast<Control>().Concat(odaKart.Controls.Cast<Control>())
                .ToDictionary(control => control, control => control.Bounds);
            Action placeHomeCards = () =>
            {
                if (MainMenuStrip != null) MainMenuStrip.BringToFront();
                var compact = _anasayfaPaneli.ClientSize.Height < 650;
                _anasayfaPaneli.Padding = new Padding(16, 32, 16, 12);
                baslik.Height = compact ? 48 : 72;
                altBaslik.Height = compact ? 28 : 40;
                var cardTop = 32 + baslik.Height + altBaslik.Height + 18;
                var availableWidth = Math.Max(1, _anasayfaPaneli.ClientSize.Width - 32);
                var availableHeight = Math.Max(1, _anasayfaPaneli.ClientSize.Height - cardTop - 12);
                var scale = Math.Min(1D, Math.Min(availableWidth / 798D, availableHeight / 460D));
                kart.Size = new Size((int)Math.Round(430 * scale), (int)Math.Round(460 * scale));
                odaKart.Size = new Size((int)Math.Round(340 * scale), kart.Height);
                foreach (var entry in homeControlBounds)
                {
                    var bounds = entry.Value;
                    entry.Key.SetBounds((int)Math.Round(bounds.Left * scale), (int)Math.Round(bounds.Top * scale),
                        (int)Math.Round(bounds.Width * scale), (int)Math.Round(bounds.Height * scale));
                }
                var totalWidth = kart.Width + (int)Math.Round(28 * scale) + odaKart.Width;
                var startLeft = (_anasayfaPaneli.ClientSize.Width - totalWidth) / 2;
                kart.Left = startLeft;
                kart.Top = cardTop;
                odaKart.Left = kart.Right + (int)Math.Round(28 * scale);
                odaKart.Top = cardTop;
            };

            _anasayfaPaneli.Resize += (_, __) => placeHomeCards();
            placeHomeCards();
        }

        private void BuildGameScreen()
        {
            _oyunPaneli.Dock = DockStyle.Fill;
            _oyunPaneli.BackColor = Color.FromArgb(24, 61, 42);
            _oyunPaneli.Visible = false;
            _oyunPaneli.Resize += (_, __) => LayoutGameScreen();

            BuildLeftMenu();
            BuildBoard();
            BuildHandArea();
            BuildLogArea();

            _kutlamaPaneli.Dock = DockStyle.Fill;
            _kutlamaPaneli.BackColor = Color.Transparent;
            _kutlamaPaneli.Visible = false;
            _kutlamaPaneli.Enabled = false;
            _kutlamaPaneli.Paint += DrawCelebrationOverlay;

            Controls.Add(_oyunPaneli);
            Controls.Add(_kutlamaPaneli);
            _kutlamaPaneli.BringToFront();
        }

        private void BuildLeftMenu()
        {
            _solMenusuPaneli.SetBounds(10, 40, 140, 740);
            _solMenusuPaneli.BackColor = Color.FromArgb(206, 229, 244);
            _solMenusuPaneli.BorderStyle = BorderStyle.FixedSingle;

            AddSideButton("Ana Menü", 18, (_, __) => ShowHomeScreen());
            AddSideButton("Yeni Oyun", 76, (_, __) => ShowNewGameDialog());
            AddMenuDivider(132);
            AddSideButton("Hamleyi Oyna", 146, (_, __) => PlayOrCommitTurn());
            AddSideButton("Ortadan Taş Çek", 204, (_, __) => DrawTile());
            AddSideButton("Geri Al", 262, (_, __) => UndoTurn());
            AddSideButton("Oto Diz", 320, (_, __) => AutoArrangeHand());
            AddMenuDivider(376);
            AddSideButton("Tahta Rengi", 390, (_, __) => { if (UsesNewTableAppearance || !_gunlukPaneli.Visible) ShowLogWindow(); else ChooseBoardColor(); });
            AddSideButton("Oda Kur", 448, (_, __) => StartLanHost());
            AddSideButton("Katıl", 506, (_, __) => JoinLanHost());
            AddSideButton("Soldan Taş Al", 556, (_, __) => DrawDiscardTile());

            _botDusunmeEtiketi.SetBounds(12, 582, 114, 18);
            _botDusunmeEtiketi.TextAlign = ContentAlignment.MiddleCenter;
            _botDusunmeEtiketi.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            _botDusunmeEtiketi.Visible = false;
            _solMenusuPaneli.Controls.Add(_botDusunmeEtiketi);

            _botDusunmeCubugu = new ProgressBar();
            _botDusunmeCubugu.SetBounds(12, 600, 114, 10);
            _botDusunmeCubugu.Style = ProgressBarStyle.Marquee;
            _botDusunmeCubugu.MarqueeAnimationSpeed = 30;
            _botDusunmeCubugu.Visible = false;
            _solMenusuPaneli.Controls.Add(_botDusunmeCubugu);

            _hamleSuresiEtiketi.SetBounds(12, 618, 114, 18);
            _hamleSuresiEtiketi.Text = "Hamle Süresi";
            _hamleSuresiEtiketi.TextAlign = ContentAlignment.MiddleCenter;
            _hamleSuresiEtiketi.Font = new Font("Tahoma", 8F, FontStyle.Bold);
            _hamleSuresiEtiketi.Visible = false;
            _solMenusuPaneli.Controls.Add(_hamleSuresiEtiketi);

            _hamleSuresiCubugu = new ProgressBar();
            _hamleSuresiCubugu.SetBounds(12, 636, 114, 14);
            _hamleSuresiCubugu.Minimum = 0;
            _hamleSuresiCubugu.Maximum = 100;
            _hamleSuresiCubugu.Step = 1;
            _hamleSuresiCubugu.Visible = false;
            _solMenusuPaneli.Controls.Add(_hamleSuresiCubugu);

            _durumEtiketi.SetBounds(12, 660, 114, 46);
            _durumEtiketi.BackColor = Color.FromArgb(243, 237, 227);
            _durumEtiketi.BorderStyle = BorderStyle.FixedSingle;
            _durumEtiketi.TextAlign = ContentAlignment.MiddleCenter;
            _durumEtiketi.Font = new Font("Tahoma", 9.5F, FontStyle.Bold);
            _durumEtiketi.Text = "Hazır";
            _solMenusuPaneli.Controls.Add(_durumEtiketi);

            _oyunPaneli.Controls.Add(_solMenusuPaneli);
        }

        private void BuildBoard()
        {
            _masaPaneli.BackColor = _tahtaKoyuRenk;
            _masaPaneli.BorderStyle = BorderStyle.FixedSingle;
            _masaPaneli.Paint += DrawBoardSurface;

            _oyuncuEtiketleri[Seat.North] = CreateSeatLabel(0F);
            _oyuncuEtiketleri[Seat.West] = CreateSeatLabel(270F);
            _oyuncuEtiketleri[Seat.East] = CreateSeatLabel(90F);
            _oyuncuEtiketleri[Seat.South] = CreateSeatLabel(0F);

            foreach (var etiket in _oyuncuEtiketleri.Values)
            {
                _masaPaneli.Controls.Add(etiket);
            }

            _matrisPaneli.BackColor = _matrisArkaRenk;
            _matrisPaneli.BorderStyle = BorderStyle.FixedSingle;
            _matrisPaneli.Paint += PaintNewNaneBoardBackground;
            _boardViewport.AutoScroll = true;
            _boardViewport.BackColor = _matrisArkaRenk;
            _boardViewport.Paint += PaintNewNaneBoardBackground;
            _boardViewport.Controls.Add(_matrisPaneli);
            _masaPaneli.Controls.Add(_boardViewport);

            for (var row = 0; row < BoardRows; row++)
            {
                for (var col = 0; col < BoardCols; col++)
                {
                    var slot = new Panel
                    {
                        Width = TileWidth,
                        Height = TileHeight,
                        BackColor = _slotArkaRenk,
                        Tag = row + "," + col
                    };
                    slot.Paint += DrawBoardSlot;
                    _masaSlotPanelleri[row, col] = slot;
                    _matrisPaneli.Controls.Add(slot);
                }
            }

            _desteEtiketi.BackColor = Color.FromArgb(255, 246, 222);
            _desteEtiketi.BorderStyle = BorderStyle.FixedSingle;
            _desteEtiketi.Font = new Font("Tahoma", 9.25F, FontStyle.Bold);
            _desteEtiketi.TextAlign = ContentAlignment.MiddleCenter;
            _desteEtiketi.Cursor = Cursors.Hand;
            _desteEtiketi.MouseDown += DeckLabelMouseDown;
            _masaPaneli.Controls.Add(_desteEtiketi);

            _oyunPaneli.Controls.Add(_masaPaneli);
        }

        private void BuildHandArea()
        {
            _elDisPaneli.BackColor = Color.FromArgb(117, 74, 37);
            _elDisPaneli.BorderStyle = BorderStyle.FixedSingle;
            _elDisPaneli.Paint += DrawWoodPanel;

            _elPaneli.Left = 12;
            _elPaneli.Top = 8;
            _elPaneli.BackColor = Color.FromArgb(231, 212, 181);
            _elPaneli.BorderStyle = BorderStyle.FixedSingle;
            _elPaneli.Paint += DrawHandSlots;

            for (var row = 0; row < HandRows; row++)
            {
                for (var col = 0; col < HandCols; col++)
                {
                    var slot = new Panel
                    {
                        Width = TileWidth,
                        Height = TileHeight,
                        BackColor = Color.Transparent,
                        Tag = row + "," + col
                    };
                    _elSlotPanelleri[row, col] = slot;
                    _elPaneli.Controls.Add(slot);
                }
            }

            _handViewport.AutoScroll = true;
            _handViewport.BackColor = _elPaneli.BackColor;
            _handViewport.Controls.Add(_elPaneli);
            _elDisPaneli.Controls.Add(_handViewport);
            _oyunPaneli.Controls.Add(_elDisPaneli);
        }

        private void BuildLogArea()
        {
            _gunlukPaneli.BackColor = Color.FromArgb(238, 229, 212);
            _gunlukPaneli.BorderStyle = BorderStyle.FixedSingle;

            var baslik = new Label
            {
                Text = "Oyun Günlüğü",
                Left = 10,
                Top = 10,
                Width = 200,
                Height = 22,
                Font = new Font("Tahoma", 10F, FontStyle.Bold)
            };

            _gunlukKutusu.SetBounds(10, 38, 262, 620);
            _gunlukKutusu.BackColor = Color.White;
            _gunlukKutusu.BorderStyle = BorderStyle.FixedSingle;
            _gunlukKutusu.DrawMode = DrawMode.OwnerDrawVariable;
            _gunlukKutusu.ItemHeight = 24;
            _gunlukKutusu.IntegralHeight = false;
            _gunlukKutusu.DrawItem += DrawLogItem;
            _gunlukKutusu.MeasureItem += MeasureLogItem;

            _sohbetGirdiKutusu.SetBounds(10, 665, 135, 26);
            _sohbetGirdiKutusu.KeyDown += SohbetKutusuKeyDown;

            _ifadeButonu.Text = "İfade";
            _ifadeButonu.SetBounds(150, 665, 54, 26);
            _ifadeButonu.Click += (_, __) => _ifadeMenusu.Show(_ifadeButonu, 0, _ifadeButonu.Height);

            _cayButonu.Text = "Çay";
            _cayButonu.SetBounds(209, 665, 42, 26);
            _cayButonu.Click += (_, __) => SendTeaButtonMessage();

            BuildChatEmoteMenu();

            _sohbetGonderButonu.Text = "Gönder";
            _sohbetGonderButonu.SetBounds(257, 665, 75, 26);
            _sohbetGonderButonu.Click += (_, __) => SendChatMessage();

            _gunlukPaneli.Controls.Add(baslik);
            _gunlukPaneli.Controls.Add(_gunlukKutusu);
            _gunlukPaneli.Controls.Add(_sohbetGirdiKutusu);
            _gunlukPaneli.Controls.Add(_ifadeButonu);
            _gunlukPaneli.Controls.Add(_cayButonu);
            _gunlukPaneli.Controls.Add(_sohbetGonderButonu);
            _oyunPaneli.Controls.Add(_gunlukPaneli);
            LayoutLogSurface();
        }

        private Label CreateSeatLabel(float angle)
        {
            return new RotatedSeatLabel
            {
                BackColor = Color.FromArgb(233, 205, 152),
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Tahoma", 9F, FontStyle.Bold),
                Angle = angle
            };
        }

        private Button CreateHomeButton(string text, EventHandler handler)
        {
            var button = new Button
            {
                Width = 300,
                Height = 52,
                Left = 64,
                Text = text,
                Font = new Font("Tahoma", 10F, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 221, 181),
                FlatStyle = FlatStyle.Flat
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(110, 68, 33);
            button.FlatAppearance.BorderSize = 1;
            button.Click += handler;
            return button;
        }

        private static void CenterHomeButtons(Control parent, IList<Button> buttons)
        {
            const int gap = 14;
            var totalHeight = buttons.Sum(x => x.Height) + gap * (buttons.Count - 1);
            var top = Math.Max(18, (parent.ClientSize.Height - totalHeight) / 2);

            foreach (var button in buttons)
            {
                button.Top = top;
                top += button.Height + gap;
            }
        }

        private void AddSideButton(string text, int top, EventHandler handler)
        {
            var button = new Button
            {
                Text = text,
                Left = 12,
                Top = top,
                Width = 114,
                Height = 54,
                Font = new Font("Tahoma", 9F, FontStyle.Bold),
                BackColor = Color.FromArgb(243, 237, 227),
                FlatStyle = FlatStyle.Flat
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(120, 94, 56);
            button.Click += handler;
            _solMenusuPaneli.Controls.Add(button);
        }

        private void AddMenuDivider(int top)
        {
            var divider = new Panel
            {
                Left = 16,
                Top = top,
                Width = 106,
                Height = 2,
                BackColor = Color.FromArgb(126, 158, 184)
            };
            _solMenusuPaneli.Controls.Add(divider);
        }

        private void LayoutGameScreen()
        {
            if (MainMenuStrip != null) MainMenuStrip.BringToFront();
            if (_oyunPaneli.ClientSize.Width <= 0 || _oyunPaneli.ClientSize.Height <= 0)
            {
                return;
            }

            var width = _oyunPaneli.ClientSize.Width;
            var outerGap = width < 1100 ? 6 : 10;
            var topGap = MainMenuStrip != null && _oyunPaneli.Top < MainMenuStrip.Bottom
                ? MainMenuStrip.Height + 8 : 8;
            var compactTraditional = width < 1250;
            var sideWidth = width < 650 ? 92 : Math.Min(145, Math.Max(110, (int)Math.Round(width * 0.095)));
            var logWidth = compactTraditional ? 0 : Math.Min(330, Math.Max(170, (int)Math.Round(width * 0.21)));
            var panelGap = width < 1100 ? 8 : 15;
            var usableHeight = Math.Max(1, _oyunPaneli.ClientSize.Height - topGap - outerGap);
            var boardWidth = Math.Max(1, width - sideWidth - logWidth - outerGap * 2 - panelGap * (compactTraditional ? 1 : 2));
            _tableLayoutMode = _engine.State.Mode;
            _tableLayoutAppearance = UsesNewTableAppearance;

            // The two shelves and the board share one budget; never grow it past the client area.
            _uiScale = IsTraditionalGame
                ? CalculateTraditionalUiScale(boardWidth, usableHeight - panelGap)
                : CalculateUiScale(boardWidth, usableHeight - panelGap);
            _handNeedsHorizontalScroll = GetHandContentWidth() > boardWidth - 18;
            var handHeight = Math.Min(GetHandOuterHeight(), usableHeight - panelGap);
            var boardHeight = Math.Max(1, usableHeight - handHeight - panelGap);

            _solMenusuPaneli.SetBounds(outerGap, topGap, sideWidth, usableHeight);
            _masaPaneli.SetBounds(_solMenusuPaneli.Right + panelGap, topGap, boardWidth, boardHeight);
            _elDisPaneli.SetBounds(_masaPaneli.Left, _masaPaneli.Bottom + panelGap, boardWidth, handHeight);
            _gunlukPaneli.SetBounds(_masaPaneli.Right + panelGap, topGap, logWidth, usableHeight);
            _gunlukPaneli.Visible = !compactTraditional;

            LayoutSideMenuSurface();
            LayoutBoardSurface();
            LayoutHandSurface();
            LayoutLogSurface();
            if (_oyunBasladi)
            {
                RenderTiles();
            }
            _oyunPaneli.Invalidate(true);
            _masaPaneli.Invalidate(true);
            _matrisPaneli.Invalidate(true);
            _elDisPaneli.Invalidate(true);
        }

        private void LayoutSideMenuSurface()
        {
            ConfigureTraditionalSideMenu();
            var items = _solMenusuPaneli.Controls.Cast<Control>()
                .Where(control => (control is Button || control is Panel) && !_compactSideMenuItems.Contains(control)).ToList();
            foreach (var item in items)
            {
                if (!_sideMenuOrder.ContainsKey(item))
                {
                    _sideMenuOrder[item] = item.Top;
                }
            }
            items = items.OrderBy(control => _sideMenuOrder[control]).ToList();
            var buttonCount = items.Count(control => control is Button);
            var dividerCount = items.Count - buttonCount;
            var footerScale = Math.Min(1D, _solMenusuPaneli.ClientSize.Height / 440D);
            var padding = Math.Max(2, (int)Math.Round(10 * footerScale));
            var buttonGap = Math.Max(1, (int)Math.Round(4 * footerScale));
            var dividerStep = Math.Max(2, (int)Math.Round(10 * footerScale));
            var footerHeight = (int)Math.Round(110 * footerScale);
            var buttonHeight = Math.Min(54, Math.Max(1,
                (_solMenusuPaneli.ClientSize.Height - padding * 2 - footerHeight - dividerCount * dividerStep - Math.Max(0, buttonCount - 1) * buttonGap)
                / Math.Max(1, buttonCount)));
            var top = padding;
            var contentWidth = Math.Max(1, _solMenusuPaneli.ClientSize.Width - 16);
            foreach (var item in items)
            {
                if (item is Button)
                {
                    item.SetBounds(8, top, contentWidth, buttonHeight);
                    FitSideButtonFont(item, contentWidth - 8, buttonHeight - 6);
                    top += buttonHeight + buttonGap;
                }
                else
                {
                    item.SetBounds(12, top + Math.Max(1, dividerStep / 3), Math.Max(1, contentWidth - 8), 2);
                    top += dividerStep;
                }
            }
            top += buttonGap;
            _botDusunmeEtiketi.SetBounds(8, top, contentWidth, Math.Max(1, (int)Math.Round(16 * footerScale)));
            _botDusunmeCubugu.SetBounds(8, top + (int)Math.Round(16 * footerScale), contentWidth, Math.Max(1, (int)Math.Round(8 * footerScale)));
            _hamleSuresiEtiketi.SetBounds(8, top + (int)Math.Round(28 * footerScale), contentWidth, Math.Max(1, (int)Math.Round(18 * footerScale)));
            _hamleSuresiCubugu.SetBounds(8, top + (int)Math.Round(46 * footerScale), contentWidth, Math.Max(1, (int)Math.Round(12 * footerScale)));
            var statusTop = top + (int)Math.Round(66 * footerScale);
            _durumEtiketi.SetBounds(8, statusTop, contentWidth,
                Math.Max(1, Math.Min((int)Math.Round(46 * footerScale), _solMenusuPaneli.ClientSize.Height - statusTop - padding)));
        }

        private static void FitSideButtonFont(Control button, int availableWidth, int availableHeight)
        {
            var size = 9.5F;
            while (size > 8F)
            {
                using (var candidate = new Font("Tahoma", size, FontStyle.Bold))
                {
                    var measured = TextRenderer.MeasureText(button.Text, candidate, new Size(Math.Max(1, availableWidth), int.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                    if (measured.Width <= availableWidth && measured.Height <= availableHeight) break;
                }
                size -= 0.25F;
            }
            if (Math.Abs(button.Font.SizeInPoints - size) < 0.01F) return;
            var oldFont = button.Font;
            button.Font = new Font("Tahoma", size, FontStyle.Bold);
            oldFont.Dispose();
        }

        private void LayoutBoardSurface()
        {
            if (IsTraditionalGame)
            {
                LayoutTraditionalTable();
                return;
            }
            SetTraditionalTableVisibility(IsNewNaneAppearance);
            var margin = IsNewNaneAppearance ? 84 : 36;
            var header = IsNewNaneAppearance ? 84 : 44;
            var footer = IsNewNaneAppearance ? 76 : 84;
            _boardUiScale = CalculateBoardUiScale(Math.Max(1, _masaPaneli.ClientSize.Width - margin * 2),
                Math.Max(1, _masaPaneli.ClientSize.Height - header - footer));
            var tileWidth = GetBoardTileWidth();
            var tileHeight = GetBoardTileHeight();
            var boardGapX = GetBoardGapX();
            var boardGapY = GetBoardGapY();
            var matrixWidth = (BoardCols * tileWidth) + ((BoardCols + 1) * boardGapX) + 2;
            var matrixHeight = (BoardRows * tileHeight) + ((BoardRows + 1) * boardGapY) + 2;
            var scroll = _boardViewport.AutoScrollPosition;
            _boardViewport.SuspendLayout();
            _boardViewport.AutoScrollPosition = Point.Empty;
            _boardViewport.AutoScrollMinSize = Size.Empty;
            _boardViewport.SetBounds(margin, header, Math.Max(1, _masaPaneli.ClientSize.Width - margin * 2),
                Math.Max(1, _masaPaneli.ClientSize.Height - header - footer));
            _boardViewport.BackColor = IsNewNaneAppearance ? Color.FromArgb(24, 91, 61) : _matrisArkaRenk;
            var matrixLeft = Math.Max(0, (_boardViewport.Width - matrixWidth) / 2);
            var matrixTop = Math.Max(0, (_boardViewport.Height - matrixHeight) / 2);
            _matrisPaneli.SetBounds(matrixLeft, matrixTop, matrixWidth, matrixHeight);
            _boardViewport.AutoScrollMinSize = new Size(matrixWidth + matrixLeft, matrixHeight + matrixTop);
            _boardViewport.ResumeLayout(true);
            _boardViewport.AutoScrollPosition = matrixWidth <= _boardViewport.ClientSize.Width && matrixHeight <= _boardViewport.ClientSize.Height
                ? Point.Empty : new Point(-scroll.X, -scroll.Y);
            if (!_boardViewport.HorizontalScroll.Visible && !_boardViewport.VerticalScroll.Visible)
                _matrisPaneli.Location = new Point(matrixLeft, matrixTop);

            for (var row = 0; row < BoardRows; row++)
            {
                for (var col = 0; col < BoardCols; col++)
                {
                    _masaSlotPanelleri[row, col].SetBounds(
                        boardGapX + col * (tileWidth + boardGapX),
                        boardGapY + row * (tileHeight + boardGapY),
                        tileWidth,
                        tileHeight);
                }
            }

            var matrixMidY = _boardViewport.Top + (_boardViewport.Height / 2);
            var deckWidth = Math.Min(210, Math.Max(120, _masaPaneli.ClientSize.Width - 80));
            _desteEtiketi.SetBounds((_masaPaneli.ClientSize.Width - deckWidth) / 2, _boardViewport.Bottom + 5, deckWidth, 48);
            ApplySeatLabelLayout(matrixMidY);
            if (IsNewNaneAppearance) RefreshTraditionalTable();
        }

        private void ApplySeatLabelLayout(int matrixMidY)
        {
            foreach (var actualSeat in _oyuncuEtiketleri.Keys.ToList())
            {
                var label = _oyuncuEtiketleri[actualSeat] as RotatedSeatLabel;
                if (label == null)
                {
                    continue;
                }

                var displaySeat = GetDisplaySeat(actualSeat);
                switch (displaySeat)
                {
                    case Seat.North:
                        label.Angle = 0F;
                        var northWidth = Math.Min(300, Math.Max(1, _masaPaneli.ClientSize.Width - 80));
                        label.SetBounds((_masaPaneli.ClientSize.Width - northWidth) / 2, Math.Max(8, _boardViewport.Top - 34), northWidth, 26);
                        break;
                    case Seat.West:
                        label.Angle = 270F;
                        var westHeight = Math.Min(164, Math.Max(1, _masaPaneli.ClientSize.Height - 16));
                        label.SetBounds(10, Math.Max(8, Math.Min(matrixMidY - westHeight / 2, _masaPaneli.ClientSize.Height - westHeight - 8)), 28, westHeight);
                        break;
                    case Seat.East:
                        label.Angle = 90F;
                        var eastHeight = Math.Min(164, Math.Max(1, _masaPaneli.ClientSize.Height - 16));
                        label.SetBounds(_masaPaneli.ClientSize.Width - 38, Math.Max(8, Math.Min(matrixMidY - eastHeight / 2, _masaPaneli.ClientSize.Height - eastHeight - 8)), 28, eastHeight);
                        break;
                    default:
                        label.Angle = 0F;
                        var southWidth = Math.Min(340, Math.Max(1, _masaPaneli.ClientSize.Width - 80));
                        label.SetBounds((_masaPaneli.ClientSize.Width - southWidth) / 2, _desteEtiketi.Bottom + 3, southWidth, 24);
                        break;
                }

                label.Invalidate();
            }
        }

        private Seat GetDisplaySeat(Seat actualSeat)
        {
            var displayIndex = (((int)actualSeat - (int)_yerelKoltuk) + 4) % 4;
            return (Seat)displayIndex;
        }

        private void LayoutHandSurface()
        {
            var tileWidth = GetTileWidth();
            var tileHeight = GetTileHeight();
            var handGapX = GetHandGapX();
            var handGapY = GetHandGapY();
            var panelHeight = (tileHeight * HandRows) + (handGapY * (HandRows - 1)) + 14;
            var gridWidth = HandCols * tileWidth + (HandCols - 1) * handGapX;
            var scroll = _handViewport.AutoScrollPosition;
            _handViewport.SuspendLayout();
            _handViewport.AutoScrollPosition = Point.Empty;
            _handViewport.AutoScrollMinSize = Size.Empty;
            _handViewport.SetBounds(8, 4, Math.Max(1, _elDisPaneli.ClientSize.Width - 16), Math.Max(1, _elDisPaneli.ClientSize.Height - 8));
            _elPaneli.SetBounds(0, 0, Math.Max(_handViewport.Width, gridWidth + 18), panelHeight);
            _handViewport.AutoScrollMinSize = _elPaneli.Size;
            _handViewport.ResumeLayout(true);
            _handViewport.AutoScrollPosition = new Point(-scroll.X, 0);
            var startX = GetHandGridStartX();

            for (var row = 0; row < HandRows; row++)
            {
                for (var col = 0; col < HandCols; col++)
                {
                    _elSlotPanelleri[row, col].SetBounds(
                        startX + col * (tileWidth + handGapX),
                        6 + row * (tileHeight + handGapY),
                        tileWidth,
                        tileHeight);
                }
            }
        }

        private void LayoutLogSurface()
        {
            var contentWidth = Math.Max(1, _gunlukPaneli.ClientSize.Width - 20);
            var compact = _gunlukPaneli.ClientSize.Width < 310;
            var inputTop = Math.Max(38, _gunlukPaneli.ClientSize.Height - (compact ? 66 : 36));
            _gunlukKutusu.SetBounds(10, 38, contentWidth, Math.Max(1, inputTop - 48));
            _sohbetGirdiKutusu.SetBounds(10, inputTop, compact ? contentWidth : Math.Max(1, contentWidth - 182), 26);
            var buttonTop = compact ? inputTop + 30 : inputTop;
            var firstLeft = compact ? 10 : _sohbetGirdiKutusu.Right + 5;
            _ifadeButonu.SetBounds(firstLeft, buttonTop, compact ? 45 : 54, 26);
            _cayButonu.SetBounds(_ifadeButonu.Right + 4, buttonTop, compact ? 36 : 42, 26);
            _sohbetGonderButonu.SetBounds(_cayButonu.Right + 4, buttonTop, compact ? Math.Max(1, contentWidth - 89) : 75, 26);
            foreach (var label in _gunlukPaneli.Controls.OfType<Label>())
            {
                label.Width = contentWidth;
            }
        }

        private void ShowHomeScreen()
        {
            _anasayfaPaneli.Visible = true;
            _oyunPaneli.Visible = false;
            _durumEtiketi.Text = "Ana Menü";
        }

        private void ShowGameScreen()
        {
            _anasayfaPaneli.Visible = false;
            _oyunPaneli.Visible = true;
            LayoutGameScreen();
        }

        private AboutForm CreateAboutDialog()
        {
            var dialog = new AboutForm();
            dialog.BotDebugUnlocked += delegate
            {
                _botDebugEnabled = true;
                _botDebugMenuItem.Visible = true;
            };
            return dialog;
        }

        private void ShowAboutDialog()
        {
            using (var dialog = CreateAboutDialog()) dialog.ShowDialog(this);
        }

        private void FocusLogPanel()
        {
            ShowLogWindow();
        }

        private void ShowLogWindow()
        {
            if (_logPenceresi == null || _logPenceresi.IsDisposed)
            {
                _logPencereKutusu = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point, 162),
                    BackColor = Color.White
                };

                _logPenceresi = new Form
                {
                    Text = "Oyun Günlüğü",
                    Width = Math.Min(520, Screen.FromControl(this).WorkingArea.Width - 24),
                    Height = Math.Min(720, Screen.FromControl(this).WorkingArea.Height - 24),
                    StartPosition = FormStartPosition.CenterParent,
                    MinimizeBox = true,
                    MaximizeBox = false
                };
                _logPenceresi.Controls.Add(_logPencereKutusu);
                _logPenceresi.FormClosed += (_, __) =>
                {
                    _logPenceresi = null;
                    _logPencereKutusu = null;
                };
                SyncDetachedLogWindow();
            }

            if (_logPenceresi == null || _logPenceresi.IsDisposed)
            {
                return;
            }

            EnsureDetachedChatInput();
            _logPenceresi.Show(this);
            _logPenceresi.BringToFront();
            _logPenceresi.Activate();
            if (_logPencereKutusu != null)
            {
                _logPencereKutusu.SelectionStart = _logPencereKutusu.TextLength;
                _logPencereKutusu.ScrollToCaret();
            }
        }

        private void SyncDetachedLogWindow()
        {
            if (_logPencereKutusu == null)
            {
                return;
            }

            var lines = new List<string>();
            foreach (var item in _gunlukKutusu.Items)
            {
                var entry = item as LogEntry;
                if (entry == null)
                {
                    continue;
                }

                lines.Add(entry.TimeText + "  " + entry.Text);
            }

            _logPencereKutusu.Lines = lines.ToArray();
            _logPencereKutusu.SelectionStart = _logPencereKutusu.TextLength;
            _logPencereKutusu.ScrollToCaret();
        }

        private void ShowBotDebugWindow()
        {
            if (!_botDebugEnabled) return;
            if (IsLanModeActive())
            {
                MessageBox.Show(this, "Bot debug ekranı ağ/oda modu açıkken kullanılamaz.", "Bot Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_botDebugPenceresi == null || _botDebugPenceresi.IsDisposed)
            {
                _botDebugKutusu = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point, 162),
                    BackColor = Color.White
                };

                _botDebugPenceresi = new Form
                {
                    Text = "Bot Debug",
                    Width = 620,
                    Height = 720,
                    StartPosition = FormStartPosition.CenterParent,
                    MinimizeBox = true,
                    MaximizeBox = false
                };
                _botDebugPenceresi.Controls.Add(_botDebugKutusu);
                _botDebugPenceresi.FormClosed += (_, __) =>
                {
                    _botDebugPenceresi = null;
                    _botDebugKutusu = null;
                };

                if (_botDebugKayitlari.Count > 0)
                {
                    _botDebugKutusu.Lines = _botDebugKayitlari.ToArray();
                    _botDebugKutusu.SelectionStart = _botDebugKutusu.TextLength;
                    _botDebugKutusu.ScrollToCaret();
                }
            }

            _botDebugPenceresi.Show(this);
            _botDebugPenceresi.BringToFront();
            _botDebugPenceresi.Activate();
        }

        private void AppendBotDebug(string text)
        {
            if (!_botDebugEnabled || string.IsNullOrWhiteSpace(text) || IsLanModeActive())
            {
                return;
            }

            var entry = DateTime.Now.ToString("HH:mm:ss") + Environment.NewLine + text + Environment.NewLine;
            _botDebugKayitlari.Add(entry);
            if (_botDebugKutusu == null || _botDebugKutusu.IsDisposed)
            {
                return;
            }

            _botDebugKutusu.AppendText(entry + Environment.NewLine);
            _botDebugKutusu.SelectionStart = _botDebugKutusu.TextLength;
            _botDebugKutusu.ScrollToCaret();
        }

        private bool IsLanModeActive()
        {
            return _host != null || _agIstemcisiModu || (_client != null && _client.IsConnected) || (_onlineClient != null && _onlineClient.IsConnected);
        }

        private void CloseBotDebugWindow()
        {
            if (_botDebugPenceresi != null && !_botDebugPenceresi.IsDisposed)
            {
                _botDebugPenceresi.Close();
            }
        }

        private void ShowNewGameDialog()
        {
            if (_agIstemcisiModu)
            {
                Log("Yeni oyunu oda yöneticisi başlatabilir.");
                return;
            }
            var initialSettings = _oyunBasladi && _engine.State != null
                ? BuildSettingsFromCurrentPlayers()
                : (_currentSettings != null ? CloneSettings(_currentSettings) : CreateDefaultSettings());

            var settings = SelectNewGameSettings(initialSettings);
            if (settings != null) StartLocalGame(settings, true);
        }

        private void StartLocalGame(GameSettings settings)
        {
            StartLocalGame(settings, true);
        }

        private void StartLocalGame(GameSettings settings, bool resetScores)
        {
            CancelActiveDragState();
            ClearLivePreview();
            StopTurnTimer();
            _botTurnId++;
            _botDusunuyor = false;
            _currentSettings = CloneSettings(settings);
            _engine.MaxBotThinkMilliseconds = System.Math.Max(1000, _currentSettings.BotThinkSeconds * 1000);
            if (resetScores)
            {
                _matchId = Guid.NewGuid().ToString("N");
                _roundHistory.Clear();
                _totalScores.Clear();
            }

            _engine.StartNewGame(settings);
            _selectedHandIds.Clear();
            _oyunBasladi = true;
            _agIstemcisiModu = false;
            _yerelKoltuk = Seat.South;
            _agKoltuguAtandi = true;
            _bekleyenAgDurumu = null;
            _roundScoreShownForCurrentGame = false;
            if (_host != null)
            {
                _host.EnableLivePreview = _currentSettings.EnableLivePreview;
                _host.EnableTurnTimer = _currentSettings.EnableTurnTimer;
                _host.TurnSeconds = _currentSettings.TurnSeconds;
                _host.BotThinkSeconds = _currentSettings.BotThinkSeconds;
                _host.Mode = _currentSettings.Mode;
                _host.UseNewAppearance = _engine.State.UseNewAppearance;
                _host.TargetScore = _currentSettings.TargetScore;
                _host.MatchId = _matchId;
            }
            if (_onlineClient != null && _onlineClient.InRoom && _onlineClient.IsMasterClient)
                _onlineClient.UpdateRoomSettings(settings.Mode, settings.TargetScore, _engine.State.UseNewAppearance);
            LoadBoardFromMelds(_engine.State.Table);
            ClearHandSlots();
            LayoutGameScreen();
            var localPlayer = GetLocalPlayerOrFallback(_engine.State);
            if (localPlayer != null)
            {
                SyncHandSlots(localPlayer.Hand);
            }
            _host?.BroadcastLobby(_engine.State.Players);
            BroadcastGameStateToNetworks();
            Log("Yeni oyun kuruldu.");
            ShowGameScreen();
            RefreshUi();
            TryPlayGameStartSound();
            _botZamani.Start();
        }

        private void CancelActiveDragState()
        {
            _dragScrollTimer.Stop();
            Capture = false;
            if (_suruklemeOnizleme != null)
            {
                Controls.Remove(_suruklemeOnizleme);
                _suruklemeOnizleme.Dispose();
                _suruklemeOnizleme = null;
            }

            _aktifSurukleme = null;
            _aktifSuruklemeTus = MouseButtons.None;
        }

        private GameSettings CreateDefaultSettings()
        {
            var settings = new GameSettings
            {
                StartingHandSize = 15,
                LanPort = 51234,
                ActivePlayerCount = 4,
                TargetScore = 1000,
                EnableLivePreview = false,
                EnableTurnTimer = false,
                TurnSeconds = 30,
                BotThinkSeconds = 30
            };
            settings.Players.Add(new PlayerSetup { Name = "Nurhan", Type = PlayerType.Human, Difficulty = BotDifficulty.SmartHard, IsActive = true });
            settings.Players.Add(new PlayerSetup { Name = "Kemal", Type = PlayerType.Bot, Difficulty = BotDifficulty.SmartHard, IsActive = true });
            settings.Players.Add(new PlayerSetup { Name = "Sarp", Type = PlayerType.Bot, Difficulty = BotDifficulty.SmartHard, IsActive = true });
            settings.Players.Add(new PlayerSetup { Name = "Şükrü", Type = PlayerType.Bot, Difficulty = BotDifficulty.SmartHard, IsActive = true });
            return settings;
        }

        private GameSettings CreateDefaultLanSettings()
        {
            var settings = new GameSettings
            {
                StartingHandSize = 15,
                LanPort = 51234,
                ActivePlayerCount = 4,
                TargetScore = 1000,
                EnableLivePreview = false,
                EnableTurnTimer = false,
                TurnSeconds = 30,
                BotThinkSeconds = 30
            };
            settings.Players.Add(new PlayerSetup { Name = "Nurhan", Type = PlayerType.Human, Difficulty = BotDifficulty.SmartHard, IsActive = true });
            settings.Players.Add(new PlayerSetup { Name = "Kemal", Type = PlayerType.Bot, Difficulty = BotDifficulty.SmartHard, IsActive = true });
            settings.Players.Add(new PlayerSetup { Name = "Sarp", Type = PlayerType.Bot, Difficulty = BotDifficulty.SmartHard, IsActive = true });
            settings.Players.Add(new PlayerSetup { Name = "Şükrü", Type = PlayerType.Remote, Difficulty = BotDifficulty.SmartHard, IsActive = true });
            return settings;
        }

        private static GameSettings CloneSettings(GameSettings settings)
        {
            var clone = new GameSettings
            {
                Mode = settings.Mode,
                UseNewAppearance = settings.Mode == GameMode.NaneOkey && settings.UseNewAppearance,
                StartingHandSize = settings.StartingHandSize,
                LanPort = settings.LanPort,
                ActivePlayerCount = settings.ActivePlayerCount,
                TargetScore = settings.TargetScore,
                EnableLivePreview = settings.EnableLivePreview,
                EnableTurnTimer = settings.EnableTurnTimer,
                TurnSeconds = settings.TurnSeconds,
                BotThinkSeconds = settings.BotThinkSeconds
            };

            foreach (var player in settings.Players)
            {
                clone.Players.Add(new PlayerSetup
                {
                    Name = player.Name,
                    Type = player.Type,
                    Difficulty = player.Difficulty,
                    IsActive = player.IsActive
                });
            }

            return clone;
        }

        private GameSettings BuildSettingsFromCurrentPlayers()
        {
            var baseSettings = _currentSettings ?? CreateDefaultLanSettings();
            var settings = CloneSettings(baseSettings);
            settings.Players.Clear();

            foreach (var player in _engine.State.Players)
            {
                settings.Players.Add(new PlayerSetup
                {
                    Name = player.Name,
                    Type = player.Type,
                    Difficulty = player.Difficulty,
                    IsActive = player.IsActive
                });
            }

            settings.ActivePlayerCount = settings.Players.Count(x => x.IsActive);
            return settings;
        }

        private void BeginTurn()
        {
            if (EnsureEditableTurn(true))
            {
                Log("Taşları elinden masaya koyabilir, masadakileri istediğin slotlara taşıyabilirsin.");
                RefreshUi();
            }
        }

        private void PlayOrCommitTurn()
        {
            if (IsTraditionalGame)
            {
                ShowTraditionalDiscardDialog();
                return;
            }
            if (!_engine.State.TurnInProgress)
            {
                if (_engine.State.Deck.Count == 0)
                {
                    PassTurn();
                    return;
                }

                BeginTurn();
                return;
            }

            CommitTurn();
        }

        private void DeckLabelMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (!CanHumanAct(false) || _engine.State.Deck.Count == 0)
            {
                return;
            }

            if (IsTraditionalGame && _engine.State.HasDrawnThisTurn &&
                !(_engine.State.Mode == GameMode.Okey101 && _engine.State.DrawnDiscardTileId > 0)) return;

            var previewTile = new Tile(-1, TileColor.Black, 0);
            BeginCustomDrag(new TileDragData(previewTile, TileSourceKind.Deck, -1, -1), 2,
                new Point(GetTileWidth() / 2, GetTileHeight() / 2));
        }

        private void ChooseBoardColor()
        {
            using (var dialog = new ColorDialog())
            {
                dialog.FullOpen = true;
                dialog.Color = _tahtaAcikRenk;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                _tahtaAcikRenk = dialog.Color;
                _tahtaKoyuRenk = DarkenColor(dialog.Color, 70);
                _matrisArkaRenk = DarkenColor(dialog.Color, 55);
                _slotArkaRenk = DarkenColor(dialog.Color, 45);
                _slotCizgiRenk = LightenColor(dialog.Color, 18);
                ApplyBoardColors();
            }
        }

        private void TryStartDiscovery()
        {
            if (_kesifBaslatildi || !IsHandleCreated)
            {
                return;
            }

            try
            {
                _discovery.StartListening();
                _kesifBaslatildi = true;
                _odaDurumEtiketi.Text = "Açık odalar taranıyor.";
                _discovery.QueryRooms();
            }
            catch (SocketException ex)
            {
                _odaDurumEtiketi.Text = "Açık oda taraması kapalı.";
                Log("Açık odalar servisi başlatılamadı: " + ex.Message);
                MessageBox.Show(
                    this,
                    "Açık odalar taraması başlatılamadı.\nYine de IP ve oda adıyla manuel bağlanabilirsiniz.\n\nDetay: " + ex.Message,
                    "LAN Keşif Uyarısı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                _odaDurumEtiketi.Text = "Açık oda taraması kapalı.";
                Log("Açık odalar servisi kapalı: " + ex.Message);
                MessageBox.Show(
                    this,
                    "Açık odalar taraması başlatılamadı.\nYine de IP ve oda adıyla manuel bağlanabilirsiniz.\n\nDetay: " + ex.Message,
                    "LAN Keşif Uyarısı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void RetryDiscovery()
        {
            _odaListesiKutusu.Items.Clear();
            _odaDurumEtiketi.Text = "Açık odalar yeniden taranıyor.";

            if (_discovery.IsListening)
            {
                _discovery.QueryRooms();
                _odaDurumEtiketi.Text = "Açık odalar yeniden taranıyor.";
                return;
            }

            _kesifBaslatildi = false;
            TryStartDiscovery();
        }

        private void HandleDiscoveryQuery(IPEndPoint endpoint)
        {
            if (_host == null || endpoint == null)
            {
                return;
            }

            try
            {
                _host.RespondToDiscovery(endpoint);
            }
            catch (SocketException ex)
            {
                Log("Keşif yanıtı gönderilemedi: " + ex.Message);
            }
        }

        private void ApplyBoardColors()
        {
            _masaPaneli.BackColor = _tahtaKoyuRenk;
            _matrisPaneli.BackColor = _matrisArkaRenk;

            for (var row = 0; row < BoardRows; row++)
            {
                for (var col = 0; col < BoardCols; col++)
                {
                    _masaSlotPanelleri[row, col].BackColor = _slotArkaRenk;
                    _masaSlotPanelleri[row, col].Invalidate();
                }
            }

            _matrisPaneli.Invalidate();
            _masaPaneli.Invalidate();
        }

        private void CommitTurn()
        {
            if (IsTraditionalGame)
            {
                ShowTraditionalDiscardDialog();
                return;
            }
            if (_agIstemcisiModu)
            {
                if (!_engine.State.TurnInProgress)
                {
                    if (_engine.State.Deck.Count == 0)
                    {
                        SendNetworkPassRequest();
                        Log("Pas geçme isteği hosta gönderildi.");
                    }
                    else
                    {
                        Log("Onaylanacak açık tur yok.");
                    }
                    return;
                }

                var remoteMelds = BuildMeldsFromBoard();
                var remoteHandIds = _elSlotlari.Cast<Tile>().Where(x => x != null).Select(x => x.Id).ToList();

                string localError;
                if (!_engine.ReplaceTurnLayout(_yerelKoltuk, remoteMelds, remoteHandIds, out localError))
                {
                    Log(localError);
                    return;
                }

                var normalizedRemoteMelds = _engine.State.TurnTable.Select(x => x.Clone()).ToList();
                var normalizedRemoteHandIds = _engine.State.TurnHand.Select(x => x.Id).ToList();
                SendNetworkCommitRequest(normalizedRemoteMelds, normalizedRemoteHandIds);
                Log("Hamle hosta gönderildi.");
                return;
            }

            if (!_engine.State.TurnInProgress)
            {
                Log("Onaylanacak açık tur yok.");
                return;
            }

            var melds = BuildMeldsFromBoard();
            var handIds = _elSlotlari.Cast<Tile>().Where(x => x != null).Select(x => x.Id).ToList();

            string error;
            if (!_engine.ReplaceTurnLayout(_engine.State.CurrentTurn, melds, handIds, out error))
            {
                Log(error);
                return;
            }

            if (_engine.CommitTurn(_engine.State.CurrentTurn, out error))
            {
                LoadBoardFromMelds(_engine.State.Table);
                var localPlayer = GetLocalPlayerOrFallback(_engine.State);
                if (localPlayer != null)
                {
                    SyncHandSlots(localPlayer.Hand);
                }
                Log(_engine.State.LastAction);
                BroadcastTextToNetworks(_engine.State.LastAction);
                BroadcastGameStateToNetworks();
                RefreshUi();
                CheckForWinner();
            }
            else if (_engine.State.Deck.Count == 0 && error == "Hamleyi onaylamak için elinden en az bir taş koymalısın.")
            {
                PassTurn();
            }
            else
            {
                Log(error);
            }
        }

        private void PassTurn()
        {
            if (!CanHumanAct(true))
            {
                return;
            }

            if (_agIstemcisiModu)
            {
                SendNetworkPassRequest();
                Log("Pas geçme isteği hosta gönderildi.");
                return;
            }

            string message;
            if (_engine.PassTurn(_engine.State.CurrentTurn, out message))
            {
                LoadBoardFromMelds(_engine.State.Table);
                var localPlayer = GetLocalPlayerOrFallback(_engine.State);
                if (localPlayer != null)
                {
                    SyncHandSlots(localPlayer.Hand);
                }
                Log(message);
                BroadcastTextToNetworks(message);
                BroadcastGameStateToNetworks();
                RefreshUi();
            }
            else
            {
                Log(message);
            }
        }

        private List<Meld> BuildMeldsFromBoard()
        {
            if (IsTraditionalGame)
                return (_engine.State.TurnInProgress ? _engine.State.TurnTable : _engine.State.Table)
                    .Select(x => x.Clone()).ToList();
            var result = new List<Meld>();
            for (var row = 0; row < BoardRows; row++)
            {
                var current = new List<Tile>();
                var startCol = -1;
                for (var col = 0; col < BoardCols; col++)
                {
                    var tile = _masaSlotlari[row, col];
                    if (tile == null)
                    {
                        if (current.Count > 0)
                        {
                            var meld = _engine.NormalizeMeld(new Meld(current)
                            {
                                BoardRow = row,
                                StartColumn = startCol
                            });
                            result.Add(meld);
                            current = new List<Tile>();
                            startCol = -1;
                        }
                    }
                    else
                    {
                        if (current.Count == 0)
                        {
                            startCol = col;
                        }
                        current.Add(tile);
                    }
                }

                if (current.Count > 0)
                {
                    var meld = _engine.NormalizeMeld(new Meld(current)
                    {
                        BoardRow = row,
                        StartColumn = startCol
                    });
                    result.Add(meld);
                }
            }
            return result;
        }

        private void UndoTurn()
        {
            if (_engine.State.TurnInProgress)
            {
                _engine.UndoTurn(_engine.State.CurrentTurn);
                LoadBoardFromMelds(_engine.State.Table);
                var localPlayer = GetLocalPlayerOrFallback(_engine.State);
                if (localPlayer != null)
                {
                    SyncHandSlots(localPlayer.Hand);
                }
                Log("Tur düzeni geri alındı.");
                RefreshUi();
            }
        }

        private void DrawTile()
        {
            if (!CanHumanAct(true))
            {
                return;
            }

            if (_agIstemcisiModu)
            {
                SendNetworkDrawRequest(-1, -1);
                Log("Taş çekme isteği hosta gönderildi.");
                return;
            }

            var actingSeat = _engine.State.CurrentTurn;
            var beforeHandIds = _engine.State.Players.First(x => x.Seat == actingSeat).Hand.Select(x => x.Id).ToList();
            string message;
            if (_engine.DrawTile(actingSeat, out message))
            {
                LoadBoardFromMelds(_engine.State.Table);
                var localPlayer = GetLocalPlayerOrFallback(_engine.State);
                if (localPlayer != null)
                {
                    SyncHandSlots(localPlayer.Hand);
                }
                Log(message);
                BroadcastTextToNetworks(message);
                BroadcastGameStateToNetworks();
                RefreshUi();
                TryAnimateDraw(actingSeat, beforeHandIds);
            }
            else Log(message);
            if (IsTraditionalGame) CheckForWinner();
        }

        private void AutoDrawForSeat(Seat seat, string reason)
        {
            var beforeHandIds = _engine.State.Players.First(x => x.Seat == seat).Hand.Select(x => x.Id).ToList();
            string message;
            if (_engine.DrawTile(seat, out message))
            {
                LoadBoardFromMelds(_engine.State.Table);
                var localPlayer = GetLocalPlayerOrFallback(_engine.State);
                if (localPlayer != null && seat == _yerelKoltuk)
                {
                    SyncHandSlots(localPlayer.Hand);
                }

                if (!string.IsNullOrWhiteSpace(reason))
                {
                    Log(reason);
                    BroadcastTextToNetworks(reason);
                }

                Log(message);
                BroadcastTextToNetworks(message);
                BroadcastGameStateToNetworks();
                RefreshUi();
                TryAnimateDraw(seat, beforeHandIds);
            }
            else
            {
                Log(message);
                _host?.SendTextToSeat(seat, message);
                if (_onlineClient != null && _onlineClient.InRoom && _onlineClient.IsMasterClient)
                    _onlineClient.SendEnvelope("text", message, false);
            }
            if (IsTraditionalGame) CheckForWinner();
        }

        private void CheckForWinner()
        {
            if (!_engine.State.IsGameOver || _showingRoundScore || _roundScoreShownForCurrentGame)
            {
                return;
            }

            _showingRoundScore = true;
            _roundScoreShownForCurrentGame = true;
            try
            {
                ShowRoundScoreDialog();
            }
            finally
            {
                _showingRoundScore = false;
            }
        }

        private void ShowRoundScoreDialog()
        {
            if (IsTraditionalGame)
            {
                ShowTraditionalRoundScoreDialog();
                return;
            }
            var players = _engine.State.Players.Where(x => x.IsActive).ToList();
            var winner = players.FirstOrDefault(x => string.Equals(x.Name, _engine.State.WinnerName, StringComparison.OrdinalIgnoreCase))
                ?? players.FirstOrDefault(x => x.Hand.Count == 0)
                ?? players.First();

            var roundScores = new Dictionary<string, int>();
            var winnerGain = 100;
            foreach (var player in players)
            {
                if (player.Seat == winner.Seat)
                {
                    continue;
                }

                var score = player.Hand.Sum(x => x.Number);
                if (!player.HasOpened)
                {
                    score += 100;
                }

                roundScores[player.Name] = -score;
                winnerGain += score;
            }

            roundScores[winner.Name] = winnerGain;

            foreach (var player in players)
            {
                if (!_totalScores.ContainsKey(player.Name))
                {
                    _totalScores[player.Name] = 0;
                }

                _totalScores[player.Name] += roundScores[player.Name];
            }

            _roundHistory.Add(new RoundScoreRecord
            {
                RoundNumber = _roundHistory.Count + 1,
                Scores = new Dictionary<string, int>(roundScores)
            });

            var totals = players.Select(x => new TotalScoreRecord
            {
                PlayerName = x.Name,
                TotalScore = _totalScores.ContainsKey(x.Name) ? _totalScores[x.Name] : 0
            }).ToList();

            var targetScore = _currentSettings != null ? _currentSettings.TargetScore : 1000;
            var overallWinner = totals
                .Where(x => x.TotalScore >= targetScore)
                .OrderByDescending(x => x.TotalScore)
                .FirstOrDefault();
            var canStartNextRound = !_agIstemcisiModu && overallWinner == null;
            var winnerText = overallWinner != null
                ? overallWinner.PlayerName + " oyunu kazandı. Toplam puanı " + overallWinner.TotalScore + "."
                : winner.Name + " turu kazandı. Hedef puan " + targetScore + ", en yüksek toplam puan önde.";

            if (overallWinner != null)
            {
                StartCelebration(overallWinner.PlayerName + " genel kazanan oldu.");
            }
            PlayRoundEndSound(winner, overallWinner);

            using (var dialog = new RoundScoreForm(
                players.Select(x => x.Name).ToList(),
                _roundHistory.ToList(),
                totals,
                canStartNextRound,
                winnerText))
            {
                var result = dialog.ShowDialog(this);
                if (overallWinner != null)
                {
                    return;
                }

                if (result == DialogResult.OK && canStartNextRound && _currentSettings != null)
                {
                    var nextSettings = _host != null ? BuildSettingsFromCurrentPlayers() : CloneSettings(_currentSettings);
                    StartLocalGame(nextSettings, false);
                }
            }
        }

        private void RefreshUi()
        {
            if (!_oyunBasladi)
            {
                ClearBoardSlots();
                ClearHandSlots();
                RenderTiles();
                _desteEtiketi.Text = "Oyun başlamadı";
                foreach (var seat in _oyuncuEtiketleri.Keys.ToList())
                {
                    _oyuncuEtiketleri[seat].Text = string.Empty;
                }
                _durumEtiketi.Text = "Hazır";
                StopTurnTimer();
                return;
            }

            var state = _engine.State;
            var localPlayer = GetLocalPlayerOrFallback(state);
            if (localPlayer == null)
            {
                RenderTiles();
                _desteEtiketi.Text = "Oyun bekleniyor";
                _durumEtiketi.Text = "Bağlanıyor";
                StopTurnTimer();
                return;
            }

            if (!state.TurnInProgress)
            {
                LoadBoardFromMelds(state.Table);
                SyncHandSlots(localPlayer.Hand);
            }

            RenderTiles();
            _desteEtiketi.Text = "Ortadaki Taş" + Environment.NewLine + state.Deck.Count;

            foreach (var seat in _oyuncuEtiketleri.Keys.ToList())
            {
                var oyuncu = state.Players[(int)seat];
                if (!oyuncu.IsActive)
                {
                    _oyuncuEtiketleri[seat].Text = string.Empty;
                    _oyuncuEtiketleri[seat].BackColor = Color.FromArgb(233, 205, 152);
                    _oyuncuEtiketleri[seat].ForeColor = Color.Black;
                    continue;
                }

                var sira = state.CurrentTurn == seat ? "  < Sıra" : string.Empty;
                var acik = oyuncu.HasOpened ? "Açık" : "Kapalı";
                var toplamPuan = _totalScores.ContainsKey(oyuncu.Name) ? _totalScores[oyuncu.Name] : 0;
                _oyuncuEtiketleri[seat].Text = string.Format("{0} [{1}] {2}{3}", oyuncu.Name, toplamPuan, acik, sira);
                _oyuncuEtiketleri[seat].BackColor = state.CurrentTurn == seat
                    ? Color.FromArgb(196, 32, 32)
                    : Color.FromArgb(233, 205, 152);
                _oyuncuEtiketleri[seat].ForeColor = state.CurrentTurn == seat
                    ? Color.White
                    : Color.Black;
            }

            _durumEtiketi.Text = state.TurnInProgress ? "Tur Açık" : "Bekleme";
            RefreshTraditionalUi();
            UpdateTurnTimer();
        }

        private void UpdateTurnTimer()
        {
            if (_currentSettings == null ||
                !_currentSettings.EnableTurnTimer ||
                !_oyunBasladi ||
                _engine.State.IsGameOver)
            {
                StopTurnTimer();
                return;
            }

            _hamleSuresiEtiketi.Visible = true;
            _hamleSuresiCubugu.Visible = true;

            var seat = _engine.State.CurrentTurn;
            if (!_turnTimerSeat.HasValue || _turnTimerSeat.Value != seat)
            {
                _turnTimerSeat = seat;
                var seconds = System.Math.Max(5, _currentSettings.TurnSeconds);
                _turnDeadline = DateTime.Now.AddSeconds(seconds);
                _turnTimer.Start();
            }

            UpdateTurnTimerProgress();
        }

        private void StopTurnTimer()
        {
            _turnTimer.Stop();
            _turnTimerSeat = null;
            if (_hamleSuresiEtiketi != null)
            {
                _hamleSuresiEtiketi.Visible = false;
            }

            if (_hamleSuresiCubugu != null)
            {
                _hamleSuresiCubugu.Visible = false;
                _hamleSuresiCubugu.Value = 0;
            }
        }

        private void TurnTimerOnTick(object sender, EventArgs e)
        {
            if (!_turnTimerSeat.HasValue)
            {
                return;
            }

            if (DateTime.Now < _turnDeadline)
            {
                UpdateTurnTimerProgress();
                return;
            }

            var seat = _turnTimerSeat.Value;
            _turnTimerSeat = null;
            _turnTimer.Stop();
            _botTurnId++;
            _botDusunuyor = false;
            _botDusunmeEtiketi.Visible = false;
            _botDusunmeCubugu.Visible = false;
            HandleTurnTimeout(seat);
        }

        private void HandleTurnTimeout(Seat seat)
        {
            if (_agIstemcisiModu || !_oyunBasladi || _engine.State.IsGameOver)
            {
                return;
            }

            if (_engine.State.CurrentTurn != seat)
            {
                UpdateTurnTimer();
                return;
            }

            if (IsTraditionalGame) CompleteTraditionalTimeout(seat);
            else AutoDrawForSeat(seat, "Tur süresi doldu, taş çekildi.");
        }

        private void UpdateTurnTimerProgress()
        {
            if (_hamleSuresiCubugu == null || _currentSettings == null || _turnTimerSeat == null)
            {
                return;
            }

            var totalSeconds = System.Math.Max(5, _currentSettings.TurnSeconds);
            var remaining = _turnDeadline - DateTime.Now;
            var ratio = remaining.TotalMilliseconds / (totalSeconds * 1000D);
            ratio = System.Math.Max(0D, System.Math.Min(1D, ratio));
            _hamleSuresiCubugu.Value = (int)System.Math.Round(ratio * 100D);

            var seat = _turnTimerSeat.Value;
            var currentPlayer = _engine.State.Players.FirstOrDefault(x => x.Seat == seat);
            var name = currentPlayer != null ? currentPlayer.Name : SeatName(seat);
            var secondsLeft = System.Math.Max(0, (int)System.Math.Ceiling(remaining.TotalSeconds));
            _hamleSuresiEtiketi.Text = name + " " + secondsLeft + " sn";
        }

        private void ClearLivePreview()
        {
        }

        private void BroadcastGameStateToNetworks()
        {
            var snapshot = _engine.Snapshot();
            _host?.BroadcastGameState(snapshot);
            if (_onlineClient != null && _onlineClient.InRoom && _onlineClient.IsMasterClient)
            {
                var onlineSnapshot = new LanGameSnapshot
                {
                    MatchId = _matchId,
                    State = LanGameStateDto.FromDomain(snapshot),
                    EnableLivePreview = _currentSettings != null && _currentSettings.EnableLivePreview,
                    EnableTurnTimer = _currentSettings != null && _currentSettings.EnableTurnTimer,
                    TurnSeconds = _currentSettings != null ? _currentSettings.TurnSeconds : 30,
                    BotThinkSeconds = _currentSettings != null ? _currentSettings.BotThinkSeconds : 30,
                    TargetScore = _currentSettings != null ? _currentSettings.TargetScore : 1000
                };
                _onlineClient.SendEnvelope("game", LanJson.Serialize(onlineSnapshot), false);
            }
        }

        private void BroadcastTextToNetworks(string text)
        {
            _host?.BroadcastText(text);
            if (_onlineClient != null && _onlineClient.InRoom && _onlineClient.IsMasterClient)
            {
                _onlineClient.SendEnvelope("text", text, false);
            }
        }

        private void SendNetworkDrawRequest(int row, int col)
        {
            if (_client != null)
            {
                _client.SendDrawRequest(row, col);
                return;
            }

            if (_onlineClient != null && _onlineClient.InRoom)
            {
                _onlineClient.SendEnvelope("draw", LanJson.Serialize(new LanDrawRequest { TargetRow = row, TargetColumn = col }), false);
            }
        }

        private void SendNetworkPassRequest()
        {
            if (_client != null)
            {
                _client.SendPassRequest();
                return;
            }

            if (_onlineClient != null && _onlineClient.InRoom)
            {
                _onlineClient.SendEnvelope("pass", string.Empty, false);
            }
        }

        private void SendNetworkCommitRequest(IList<Meld> melds, IList<int> handTileIds)
        {
            if (_client != null)
            {
                _client.SendCommitRequest(_yerelKoltuk, melds, handTileIds);
                return;
            }

            if (_onlineClient != null && _onlineClient.InRoom)
            {
                var layout = new LanTurnLayout
                {
                    Seat = _yerelKoltuk.ToString(),
                    Melds = melds == null ? new List<LanMeldDto>() : melds.Select(LanMeldDto.FromDomain).ToList(),
                    HandTileIds = handTileIds == null ? new List<int>() : new List<int>(handTileIds)
                };
                _onlineClient.SendEnvelope("commit", LanJson.Serialize(layout), false);
            }
        }

        private void ApplyLivePreview(Seat seat, IList<Meld> melds)
        {
            if (_currentSettings == null || !_currentSettings.EnableLivePreview)
            {
                return;
            }

            if (!_oyunBasladi || _engine.State.IsGameOver)
            {
                return;
            }

            if (_engine.State.CurrentTurn != seat)
            {
                return;
            }

            if (seat == _yerelKoltuk)
            {
                return;
            }

            LoadBoardFromMelds(melds ?? new List<Meld>());
            RenderBoard();
        }

        private void SendLivePreviewIfNeeded()
        {
            if (_currentSettings == null || !_currentSettings.EnableLivePreview)
            {
                return;
            }

            if (!_oyunBasladi || _engine.State.IsGameOver)
            {
                return;
            }

            if (!_engine.State.TurnInProgress || _engine.State.CurrentTurn != _yerelKoltuk)
            {
                return;
            }

            var melds = BuildMeldsFromBoard();
            var handIds = _elSlotlari.Cast<Tile>().Where(x => x != null).Select(x => x.Id).ToList();
            if (_agIstemcisiModu)
            {
                if (_client != null)
                {
                    _client.SendPreviewRequest(_yerelKoltuk, melds, handIds);
                }
                else if (_onlineClient != null && _onlineClient.InRoom)
                {
                    var preview = new LanTurnPreview
                    {
                        Seat = _yerelKoltuk.ToString(),
                        Melds = melds.Select(LanMeldDto.FromDomain).ToList(),
                        HandTileIds = handIds
                    };
                    _onlineClient.SendEnvelope("preview", LanJson.Serialize(preview), false);
                }
            }
            else if (_host != null)
            {
                _host.BroadcastPreview(_yerelKoltuk, melds, handIds);
            }
            else if (_onlineClient != null && _onlineClient.InRoom && _onlineClient.IsMasterClient)
            {
                var preview = new LanTurnPreview
                {
                    Seat = _yerelKoltuk.ToString(),
                    Melds = melds.Select(LanMeldDto.FromDomain).ToList(),
                    HandTileIds = handIds
                };
                _onlineClient.SendEnvelope("preview", LanJson.Serialize(preview), false);
            }
        }

        private void RenderHand()
        {
            for (var row = 0; row < HandRows; row++)
            {
                for (var col = 0; col < HandCols; col++)
                {
                    RenderTileSlot(
                        _elSlotPanelleri[row, col],
                        _elSlotlari[row, col],
                        TileSourceKind.Hand,
                        row,
                        col,
                        _elGorunumleri[row, col],
                        ref _elTasGorunumleri[row, col]);
                }
            }
        }

        // El ve masa slotlarındaki değişiklikleri tek bir yerleşim güncellemesinde işler.
        private void RenderTiles()
        {
            _matrisPaneli.SuspendLayout();
            _elPaneli.SuspendLayout();
            try
            {
                RenderHand();
                UpdateTraditionalSelectionViews();
                if (!IsTraditionalGame) RenderBoard();
                if (UsesNewTableAppearance) RefreshTraditionalTable();
            }
            finally
            {
                _elPaneli.ResumeLayout(false);
                _matrisPaneli.ResumeLayout(false);
            }
        }

        private void AutoArrangeHand()
        {
            var localPlayer = GetLocalPlayerOrFallback(_engine.State);
            if (localPlayer == null)
            {
                return;
            }

            var handTiles = _elSlotlari.Cast<Tile>().Where(x => x != null).Select(x => x.Clone()).ToList();
            if (handTiles.Count == 0)
            {
                return;
            }

            var arrangedGroups = IsTraditionalGame ? BuildTraditionalHandGroups(handTiles) : BuildAutoArrangedHandGroups(handTiles);
            ClearHandSlots();
            if (IsTraditionalGame) PlaceTraditionalHandGroups(arrangedGroups);
            else PlaceAutoArrangedHandGroups(arrangedGroups);

            _handViewport.AutoScrollPosition = Point.Empty;

            RefreshUi();
        }

        private List<List<Tile>> BuildAutoArrangedHandGroups(List<Tile> handTiles)
        {
            var candidates = GenerateHandCandidates(handTiles);
            var usedIds = new HashSet<int>();
            var groups = new List<List<Tile>>();

            foreach (var meld in candidates)
            {
                if (meld.Tiles.Any(x => usedIds.Contains(x.Id)))
                {
                    continue;
                }

                foreach (var tile in meld.Tiles)
                {
                    usedIds.Add(tile.Id);
                }

                groups.Add(meld.Tiles.Select(x => x.Clone()).ToList());
            }

            var remaining = handTiles
                .Where(x => !usedIds.Contains(x.Id))
                .ToList();

            groups.AddRange(BuildPotentialHandGroups(remaining));

            return groups;
        }

        private List<List<Tile>> BuildPotentialHandGroups(IList<Tile> tiles)
        {
            var result = new List<List<Tile>>();
            var usedIds = new HashSet<int>();

            // Aynı sayının farklı renkleri, ileride gruba dönüşebilecek şekilde birlikte kalır.
            foreach (var numberGroup in tiles.GroupBy(x => x.Number).OrderBy(x => x.Key))
            {
                var group = numberGroup
                    .OrderBy(x => (int)x.Color)
                    .ToList();
                if (group.Count < 2)
                {
                    continue;
                }

                result.Add(group.Select(x => x.Clone()).ToList());
                foreach (var tile in group)
                {
                    usedIds.Add(tile.Id);
                }
            }

            // Aynı renkte, aralarında en fazla bir eksik sayı olan taşları da yan yana tutar.
            foreach (var colorGroup in tiles.Where(x => !usedIds.Contains(x.Id)).GroupBy(x => x.Color))
            {
                var run = new List<Tile>();
                var previousOrder = -1;
                foreach (var tile in colorGroup.OrderBy(x => _tahtaDogrulayici.NormalizeRunOrder(x.Number)))
                {
                    var currentOrder = _tahtaDogrulayici.NormalizeRunOrder(tile.Number);
                    if (run.Count > 0 && currentOrder > previousOrder + 2)
                    {
                        if (run.Count >= 2)
                        {
                            result.Add(run.Select(x => x.Clone()).ToList());
                            foreach (var runTile in run)
                            {
                                usedIds.Add(runTile.Id);
                            }
                        }
                        run.Clear();
                    }

                    run.Add(tile);
                    previousOrder = currentOrder;
                }

                if (run.Count >= 2)
                {
                    result.Add(run.Select(x => x.Clone()).ToList());
                    foreach (var runTile in run)
                    {
                        usedIds.Add(runTile.Id);
                    }
                }
            }

            foreach (var tile in tiles
                .Where(x => !usedIds.Contains(x.Id))
                .OrderBy(x => x.Number == 1 ? 14 : x.Number)
                .ThenBy(x => (int)x.Color))
            {
                result.Add(new List<Tile> { tile.Clone() });
            }

            return result;
        }

        private void PlaceAutoArrangedHandGroups(IList<List<Tile>> groups)
        {
            const int groupGap = 2;
            var row = 0;
            var nextLeftCol = 0;

            foreach (var group in groups)
            {
                if (group == null || group.Count == 0)
                {
                    continue;
                }

                if (group.Count > HandCols)
                {
                    PlaceAutoArrangedOverflow(group, ref row, ref nextLeftCol);
                    continue;
                }

                if (nextLeftCol + group.Count > HandCols)
                {
                    row++;
                    nextLeftCol = 0;
                }

                if (row >= HandRows)
                {
                    return;
                }

                for (var index = 0; index < group.Count; index++)
                {
                    _elSlotlari[row, nextLeftCol + index] = group[index].Clone();
                    _elGorunumleri[row, nextLeftCol + index] = 0;
                }

                nextLeftCol += group.Count + groupGap;
            }
        }

        private int GetVisibleHandColumnCount()
        {
            return Math.Max(1, Math.Min(HandCols, (_handViewport.ClientSize.Width - 18) / Math.Max(1, GetTileWidth() + GetHandGapX())));
        }

        private void PlaceTraditionalHandGroups(IList<List<Tile>> groups)
        {
            var preferredColumns = Math.Min(14, GetVisibleHandColumnCount());
            if (TryPlaceTraditionalHandGroups(groups, preferredColumns, 1) ||
                TryPlaceTraditionalHandGroups(groups, preferredColumns, 0) ||
                TryPlaceTraditionalHandGroups(groups, HandCols, 1) ||
                TryPlaceTraditionalHandGroups(groups, HandCols, 0)) return;
            ClearHandSlots();
            foreach (var tile in groups.Where(x => x != null).SelectMany(x => x))
                PlaceInLeftmostTopEmptyHandSlot(tile.Clone());
        }

        private bool TryPlaceTraditionalHandGroups(IList<List<Tile>> groups, int columns, int gap)
        {
            ClearHandSlots();
            var row = 0;
            var column = 0;
            foreach (var group in groups.Where(x => x != null && x.Count > 0))
            {
                if (group.Count > columns) return false;
                if (column + group.Count > columns) { row++; column = 0; }
                if (row >= HandRows) return false;
                foreach (var tile in group)
                {
                    _elSlotlari[row, column] = tile.Clone();
                    _elGorunumleri[row, column++] = 0;
                }
                column += gap;
            }
            return true;
        }

        private void PlaceAutoArrangedOverflow(IList<Tile> group, ref int row, ref int nextLeftCol)
        {
            foreach (var tile in group)
            {
                if (nextLeftCol >= HandCols)
                {
                    row++;
                    nextLeftCol = 0;
                }

                if (row >= HandRows)
                {
                    return;
                }

                _elSlotlari[row, nextLeftCol] = tile.Clone();
                _elGorunumleri[row, nextLeftCol] = 0;
                nextLeftCol++;
            }

            nextLeftCol += 2;
        }

        private List<Meld> GenerateHandCandidates(List<Tile> handTiles)
        {
            var result = new List<Meld>();

            foreach (var group in handTiles.GroupBy(x => x.Number))
            {
                var distinctColors = group
                    .GroupBy(x => x.Color)
                    .Select(x => x.First())
                    .OrderBy(x => (int)x.Color)
                    .ToList();
                if (distinctColors.Count >= 3)
                {
                    result.Add(new Meld(distinctColors.Take(3)));
                    if (distinctColors.Count >= 4)
                    {
                        result.Add(new Meld(distinctColors.Take(4)));
                    }
                }
            }

            foreach (var colorGroup in handTiles.GroupBy(x => x.Color))
            {
                var sorted = colorGroup.OrderBy(x => _tahtaDogrulayici.NormalizeRunOrder(x.Number)).ToList();
                for (var start = 0; start < sorted.Count; start++)
                {
                    var run = new List<Tile> { sorted[start] };
                    for (var index = start + 1; index < sorted.Count; index++)
                    {
                        var previous = _tahtaDogrulayici.NormalizeRunOrder(run.Last().Number);
                        var current = _tahtaDogrulayici.NormalizeRunOrder(sorted[index].Number);
                        if (current == previous)
                        {
                            continue;
                        }

                        if (current != previous + 1)
                        {
                            break;
                        }

                        run.Add(sorted[index]);
                        if (run.Count >= 3)
                        {
                            result.Add(new Meld(run));
                        }
                    }
                }
            }

            return result
                .Where(x => _tahtaDogrulayici.IsValidMeld(x))
                .OrderByDescending(x => x.Tiles.Count)
                .ThenBy(x => _tahtaDogrulayici.IsValidSet(x.Tiles) ? 0 : 1)
                .ThenBy(x => x.Tiles.Min(t => _tahtaDogrulayici.NormalizeRunOrder(t.Number)))
                .ToList();
        }

        private void RenderBoard()
        {
            for (var row = 0; row < BoardRows; row++)
            {
                for (var col = 0; col < BoardCols; col++)
                {
                    RenderTileSlot(
                        _masaSlotPanelleri[row, col],
                        _masaSlotlari[row, col],
                        TileSourceKind.Board,
                        row,
                        col,
                        _masaYonleri[row, col],
                        ref _masaTasGorunumleri[row, col]);
                }
            }
        }

        private void RenderTileSlot(
            Panel panel,
            Tile tile,
            TileSourceKind sourceKind,
            int row,
            int col,
            int visualMode,
            ref TileView tileView)
        {
            // Empty felt positions do not need 220 transparent child-window repaints.
            if (sourceKind == TileSourceKind.Board)
                panel.Visible = !IsNewNaneAppearance || tile != null;
            if (tile == null)
            {
                if (tileView != null && tileView.Visible)
                {
                    tileView.Visible = false;
                }

                return;
            }

            if (tileView == null)
            {
                tileView = CreateTileView(tile, sourceKind, row, col, visualMode);
                tileView.Left = 0;
                tileView.Top = 0;
                panel.Controls.Add(tileView);
                return;
            }

            var rotation = visualMode == 1 ? 2 : 0;
            var faceDown = visualMode == 2;
            var sameFace = tileView.Tile != null && tileView.Tile.Id == tile.Id &&
                tileView.Tile.Number == tile.Number && tileView.Tile.Color == tile.Color &&
                tileView.Tile.IsJoker == tile.IsJoker && tileView.Tile.IsFalseJoker == tile.IsFalseJoker &&
                tileView.Tile.JokerNumber == tile.JokerNumber && tileView.Tile.JokerColor == tile.JokerColor;
            var changed = !tileView.Visible ||
                          !sameFace ||
                          tileView.RotationQuarterTurns != rotation ||
                          tileView.FaceDown != faceDown;

            if (!sameFace)
            {
                tileView.SetTile(tile);
            }

            if (tileView.RotationQuarterTurns != rotation)
            {
                tileView.RotationQuarterTurns = rotation;
            }

            if (tileView.FaceDown != faceDown)
            {
                tileView.FaceDown = faceDown;
            }

            if (tileView.Width != (sourceKind == TileSourceKind.Board ? GetBoardTileWidth() : GetTileWidth()) ||
                tileView.Height != (sourceKind == TileSourceKind.Board ? GetBoardTileHeight() : GetTileHeight()))
            {
                ApplyTileViewSize(tileView, sourceKind == TileSourceKind.Board);
                changed = true;
            }

            if (!tileView.Visible)
            {
                tileView.Visible = true;
            }

            if (changed)
            {
                tileView.Invalidate();
            }
        }

        private TileView CreateTileView(Tile tile, TileSourceKind sourceKind, int indexA, int indexB, int visualMode)
        {
            var tileView = new TileView(tile)
            {
                Cursor = Cursors.Hand,
                RotationQuarterTurns = visualMode == 1 ? 2 : 0,
                FaceDown = visualMode == 2
            };
            ApplyTileViewSize(tileView, sourceKind == TileSourceKind.Board);

            tileView.MouseDown += (_, args) =>
            {
                if (IsTraditionalGame && sourceKind == TileSourceKind.Hand && args.Button == MouseButtons.Left &&
                    (ModifierKeys & Keys.Control) == Keys.Control)
                {
                    ToggleTraditionalSelection(tileView.Tile.Id);
                    return;
                }
                if (args.Button != MouseButtons.Left && args.Button != MouseButtons.Right)
                {
                    return;
                }

                if (!CanStartDrag(sourceKind))
                {
                    return;
                }

                if (args.Button == MouseButtons.Right && (sourceKind == TileSourceKind.Board || sourceKind == TileSourceKind.Hand))
                {
                    BeginGroupDrag(sourceKind, indexA, indexB, args.Location);
                    return;
                }

                if (args.Button != MouseButtons.Left)
                {
                    return;
                }

                BeginCustomDrag(new TileDragData(tileView.Tile, sourceKind, indexA, indexB), visualMode, args.Location);
            };

            tileView.MouseUp += (_, args) =>
            {
                if (args.Button == MouseButtons.Middle && EnsureEditableTurn(false))
                {
                    if (sourceKind == TileSourceKind.Board)
                    {
                        _masaYonleri[indexA, indexB] = NextVisualMode(_masaYonleri[indexA, indexB]);
                    }
                    else if (sourceKind == TileSourceKind.Hand)
                    {
                        _elGorunumleri[indexA, indexB] = NextVisualMode(_elGorunumleri[indexA, indexB]);
                    }
                    RefreshUi();
                }
            };
            tileView.DoubleClick += (_, __) =>
            {
                if (IsTraditionalGame && sourceKind == TileSourceKind.Hand)
                {
                    EndCustomDrag(true);
                    ToggleTraditionalSelection(tileView.Tile.Id);
                }
            };

            return tileView;
        }

        private void BeginCustomDrag(TileDragData data, int visualMode, Point mouseOffset)
        {
            EndCustomDrag(true);
            StopAnimation();

            _aktifSurukleme = data;
            _aktifSurukleme.OriginalVisualMode = visualMode;
            _suruklemeOfseti = mouseOffset;
            _aktifSuruklemeTus = MouseButtons.Left;
            if (data.SourceKind != TileSourceKind.Deck)
            {
                RemoveFromSource(data);
                RemoveResidualUiCopies(new[] { data.Tile.Id });
                data.SourceRemoved = true;
            }
            _suruklemeOnizleme = BuildDragPreviewControl(data, visualMode);
            _suruklemeOnizleme.Parent = _oyunPaneli;
            _suruklemeOnizleme.BringToFront();
            UpdateDragPreviewPosition();
            Capture = true;
            _dragScrollTimer.Start();
            RenderTiles();
        }

        private void BeginGroupDrag(TileSourceKind sourceKind, int row, int col, Point mouseOffset)
        {
            var allowHandReorder = sourceKind == TileSourceKind.Hand && CanReorderLocalHandWhileWaiting();

            if (!allowHandReorder && !EnsureEditableTurn(true))
            {
                return;
            }

            var meldStart = col;
            while (meldStart > 0 && GetTileAt(sourceKind, row, meldStart - 1) != null)
            {
                meldStart--;
            }

            var meldTiles = new List<Tile>();
            var meldVisualModes = new List<int>();
            var cursor = meldStart;
            var limit = sourceKind == TileSourceKind.Hand ? HandCols : BoardCols;
            while (cursor < limit && GetTileAt(sourceKind, row, cursor) != null)
            {
                meldTiles.Add(GetTileAt(sourceKind, row, cursor).Clone());
                meldVisualModes.Add(GetVisualModeAt(sourceKind, row, cursor));
                cursor++;
            }

            if (meldTiles.Count == 0)
            {
                return;
            }

            var dragData = new TileDragData(meldTiles[0].Clone(), sourceKind, row, col)
            {
                IsGroupDrag = true,
                GroupStartColumn = meldStart,
                GroupClickedOffset = col - meldStart,
                GroupTiles = meldTiles,
                GroupVisualModes = meldVisualModes,
                OriginalVisualMode = GetVisualModeAt(sourceKind, row, col)
            };

            EndCustomDrag(true);
            StopAnimation();
            _aktifSurukleme = dragData;
            _suruklemeOfseti = new Point(
                dragData.GroupClickedOffset * (sourceKind == TileSourceKind.Hand ? GetTileWidth() + GetHandGapX() : GetBoardTileWidth() + GetBoardGapX()) + mouseOffset.X,
                mouseOffset.Y);
            _aktifSuruklemeTus = MouseButtons.Right;
            RemoveFromSource(dragData);
            RemoveResidualUiCopies(dragData.GroupTiles.Select(x => x.Id));
            dragData.SourceRemoved = true;
            _suruklemeOnizleme = BuildDragPreviewControl(dragData, dragData.OriginalVisualMode);
            _suruklemeOnizleme.Parent = _oyunPaneli;
            _suruklemeOnizleme.BringToFront();
            UpdateDragPreviewPosition();
            Capture = true;
            _dragScrollTimer.Start();
            RenderTiles();
        }

        private bool CanStartDrag(TileSourceKind sourceKind)
        {
            if (IsTraditionalGame && sourceKind == TileSourceKind.Hand)
            {
                if (!_oyunBasladi || _engine.State.IsGameOver || (_agIstemcisiModu && !_agKoltuguAtandi)) return false;
                if (_engine.State.CurrentTurn == _yerelKoltuk && _engine.State.HasDrawnThisTurn)
                    return EnsureEditableTurn(false);
                return true;
            }
            if (IsTraditionalGame && sourceKind == TileSourceKind.Board)
                return _engine.State.Mode == GameMode.Okey101 && EnsureEditableTurn(false);
            if (sourceKind == TileSourceKind.Hand)
            {
                if (CanReorderLocalHandWhileWaiting())
                {
                    return true;
                }

                return EnsureEditableTurn(false);
            }

            if (sourceKind == TileSourceKind.Deck)
            {
                return CanHumanAct(false);
            }

            return EnsureEditableTurn(false);
        }

        private bool CanReorderLocalHandWhileWaiting()
        {
            if (!_oyunBasladi || _engine.State.IsGameOver)
            {
                return false;
            }

            if (_agIstemcisiModu && !_agKoltuguAtandi)
            {
                return false;
            }

            return _engine.State.CurrentTurn != _yerelKoltuk;
        }

        private void MainFormMouseMove(object sender, MouseEventArgs e)
        {
            if (_aktifSurukleme != null)
            {
                UpdateDragPreviewPosition();
            }
        }

        private void MainFormMouseUp(object sender, MouseEventArgs e)
        {
            if (_aktifSurukleme == null || e.Button != _aktifSuruklemeTus)
            {
                return;
            }

            FinishCustomDrag();
        }

        private void FinishCustomDrag()
        {
            CompleteCustomDrag(Cursor.Position);
        }

        private void CompleteCustomDrag(Point screenLocation)
        {
            if (_aktifSurukleme == null) return;
            if (IsTraditionalGame && TryTraditionalDrop(_aktifSurukleme, screenLocation)) return;
            if (_aktifSurukleme.SourceKind == TileSourceKind.Deck &&
                (UsesNewTableAppearance
                    ? _traditionalTable.GetTargetBounds(new TraditionalTableTarget(TraditionalTargetKind.Stock)).Contains(_traditionalTable.PointToClient(screenLocation))
                    : _desteEtiketi.RectangleToScreen(_desteEtiketi.ClientRectangle).Contains(screenLocation)))
            {
                EndCustomDrag(false);
                DrawTile();
                return;
            }
            var handTarget = FindHandSlotAtScreen(screenLocation);
            if (handTarget.HasValue)
            {
                var moved = MoveDraggedTileToHand(handTarget.Value.X, handTarget.Value.Y);
                EndCustomDrag(!moved);
                if (moved)
                {
                    SendLivePreviewIfNeeded();
                }
                RefreshUi();
                return;
            }

            var boardTarget = FindBoardSlotAtScreen(screenLocation);
            if (boardTarget.HasValue)
            {
                var moved = MoveDraggedTileToBoard(boardTarget.Value.X, boardTarget.Value.Y);
                EndCustomDrag(!moved);
                if (moved)
                {
                    SendLivePreviewIfNeeded();
                }
                RefreshUi();
                return;
            }

            EndCustomDrag(true);
            RefreshUi();
        }

        private bool MoveDraggedTileToBoard(int row, int col)
        {
            var data = _aktifSurukleme;
            if (data.SourceKind == TileSourceKind.Deck)
            {
                Log("Desteden çekilen taşı önce ıstakana bırakmalısın.");
                return false;
            }
            if (_engine.State.Mode == GameMode.ClassicOkey)
            {
                Log("Klasik Okey'de perler elde tamamlanır. Bitirmek için Hamleyi Oyna düğmesini kullan.");
                return false;
            }
            if (!EnsureEditableTurn(true))
            {
                return false;
            }

            if (!_engine.State.TurnInProgress)
            {
                Log("Masaya taş koymak için sıra sende olmalı.");
                return false;
            }

            var existing = _masaSlotlari[row, col];
            if (existing != null)
            {
                Log("Dolu masa slotunun üstüne bırakamazsın.");
                return false;
            }

            if (data.IsGroupDrag)
            {
                return MoveDraggedGroupToBoard(row, col);
            }

            var movingVisualMode = GetSourceVisualMode(data);
            _masaSlotlari[row, col] = data.Tile;
            _masaYonleri[row, col] = movingVisualMode;
            PlayEmbeddedMoveSound();
            return true;
        }

        private bool MoveDraggedGroupToBoard(int row, int col)
        {
            var data = _aktifSurukleme;
            if (data == null || !data.IsGroupDrag || data.GroupTiles == null || data.GroupTiles.Count == 0)
            {
                return false;
            }

            var targetStart = col - data.GroupClickedOffset;
            if (targetStart < 0 || targetStart + data.GroupTiles.Count > BoardCols)
            {
                Log("Dizi oraya sığmıyor.");
                return false;
            }

            for (var index = 0; index < data.GroupTiles.Count; index++)
            {
                var targetCol = targetStart + index;
                if (_masaSlotlari[row, targetCol] != null)
                {
                    Log("Diziyi dolu slotların üstüne bırakamazsın.");
                    return false;
                }
            }

            for (var index = 0; index < data.GroupTiles.Count; index++)
            {
                _masaSlotlari[row, targetStart + index] = data.GroupTiles[index].Clone();
                _masaYonleri[row, targetStart + index] = data.GroupVisualModes[index];
            }

            PlayEmbeddedMoveSound();
            return true;
        }

        private bool MoveDraggedTileToHand(int row, int col)
        {
            var data = _aktifSurukleme;
            if (data.IsGroupDrag)
            {
                return MoveDraggedGroupToHand(row, col);
            }

            if (data.SourceKind == TileSourceKind.Deck)
            {
                return DrawTileToSpecificHandSlot(row, col);
            }

            if (data.SourceKind == TileSourceKind.Board && !_engine.State.OriginalHandIds.Contains(data.Tile.Id))
            {
                Log("Masadaki eski taşı eline alamazsın. Sadece bu tur elinden koyduğun taşı geri alabilirsin.");
                return false;
            }

            var existing = _elSlotlari[row, col];
            var existingVisualMode = _elGorunumleri[row, col];
            var movingVisualMode = GetSourceVisualMode(data);
            _elSlotlari[row, col] = data.Tile;
            _elGorunumleri[row, col] = movingVisualMode;
            if (existing != null)
            {
                ReturnTileToOrigin(data, existing, existingVisualMode);
            }
            PlayEmbeddedMoveSound();
            return true;
        }

        private bool MoveDraggedGroupToHand(int row, int col)
        {
            var data = _aktifSurukleme;
            if (data == null || !data.IsGroupDrag || data.GroupTiles == null || data.GroupTiles.Count == 0)
            {
                return false;
            }

            if (data.SourceKind == TileSourceKind.Board && data.GroupTiles.Any(x => !_engine.State.OriginalHandIds.Contains(x.Id)))
            {
                Log("Masadaki eski taşları blok halinde eline alamazsın.");
                return false;
            }

            var targetStart = col - data.GroupClickedOffset;
            if (targetStart < 0 || targetStart + data.GroupTiles.Count > HandCols)
            {
                Log("Dizi elde oraya sığmıyor.");
                return false;
            }

            for (var index = 0; index < data.GroupTiles.Count; index++)
            {
                if (_elSlotlari[row, targetStart + index] != null)
                {
                    Log("Diziyi dolu el slotlarının üstüne bırakamazsın.");
                    return false;
                }
            }

            for (var index = 0; index < data.GroupTiles.Count; index++)
            {
                _elSlotlari[row, targetStart + index] = data.GroupTiles[index].Clone();
                _elGorunumleri[row, targetStart + index] = data.GroupVisualModes[index];
            }

            PlayEmbeddedMoveSound();
            return true;
        }

        private bool DrawTileToSpecificHandSlot(int row, int col)
        {
            if (_elSlotlari[row, col] != null)
            {
                Log("Ortadan taşı dolu el slotuna bırakamazsın.");
                return false;
            }

            if (!CanHumanAct(true))
            {
                return false;
            }

            if (_agIstemcisiModu)
            {
                _bekleyenAgCekmeSlotu = new Point(row, col);
                SendNetworkDrawRequest(row, col);
                Log("Taş çekme isteği hosta gönderildi.");
                return true;
            }

            var actingSeat = _engine.State.CurrentTurn;
            var beforeHandIds = _engine.State.Players.First(x => x.Seat == actingSeat).Hand.Select(x => x.Id).ToList();
            string message;
            if (!_engine.DrawTile(actingSeat, out message))
            {
                Log(message);
                return false;
            }

            var localPlayer = GetLocalPlayerOrFallback(_engine.State);
            if (localPlayer == null)
            {
                return false;
            }

            SyncHandSlots(localPlayer.Hand);
            var newTile = localPlayer.Hand.FirstOrDefault(x => !beforeHandIds.Contains(x.Id));
            if (newTile != null)
            {
                MoveHandTileToPreferredSlot(newTile.Id, row, col);
                _handViewport.ScrollControlIntoView(_elSlotPanelleri[row, col]);
            }

            Log(message);
            BroadcastTextToNetworks(message);
            BroadcastGameStateToNetworks();
            RefreshUi();
            TryAnimateDraw(actingSeat, beforeHandIds);
            if (IsTraditionalGame) CheckForWinner();
            return true;
        }

        private void MoveHandTileToPreferredSlot(int tileId, int targetRow, int targetCol)
        {
            if (_elSlotlari[targetRow, targetCol] != null)
            {
                return;
            }

            for (var row = 0; row < HandRows; row++)
            {
                for (var col = 0; col < HandCols; col++)
                {
                    if (_elSlotlari[row, col] == null || _elSlotlari[row, col].Id != tileId)
                    {
                        continue;
                    }

                    var tile = _elSlotlari[row, col];
                    var visual = _elGorunumleri[row, col];
                    _elSlotlari[row, col] = null;
                    _elGorunumleri[row, col] = 0;
                    _elSlotlari[targetRow, targetCol] = tile;
                    _elGorunumleri[targetRow, targetCol] = visual;
                    return;
                }
            }
        }

        private Point? FindHandSlotAtScreen(Point screenPoint)
        {
            if (!_handViewport.Visible || !_handViewport.RectangleToScreen(_handViewport.ClientRectangle).Contains(screenPoint)) return null;
            for (var row = 0; row < HandRows; row++)
            {
                for (var col = 0; col < HandCols; col++)
                {
                    var rect = _elSlotPanelleri[row, col].RectangleToScreen(_elSlotPanelleri[row, col].ClientRectangle);
                    if (rect.Contains(screenPoint))
                    {
                        return new Point(row, col);
                    }
                }
            }

            return null;
        }

        private Point? FindBoardSlotAtScreen(Point screenPoint)
        {
            if (IsTraditionalGame) return null;
            if (!_boardViewport.Visible || !_boardViewport.RectangleToScreen(_boardViewport.ClientRectangle).Contains(screenPoint)) return null;
            if (IsNewNaneAppearance)
            {
                var point = _matrisPaneli.PointToClient(screenPoint);
                if (!_matrisPaneli.ClientRectangle.Contains(point)) return null;
                var row = Math.Max(0, Math.Min(BoardRows - 1, (point.Y - GetBoardGapY()) / (GetBoardTileHeight() + GetBoardGapY())));
                var col = Math.Max(0, Math.Min(BoardCols - 1, (point.X - GetBoardGapX()) / (GetBoardTileWidth() + GetBoardGapX())));
                return new Point(row, col);
            }
            for (var row = 0; row < BoardRows; row++)
            {
                for (var col = 0; col < BoardCols; col++)
                {
                    var rect = _masaSlotPanelleri[row, col].RectangleToScreen(_masaSlotPanelleri[row, col].ClientRectangle);
                    if (rect.Contains(screenPoint))
                    {
                        return new Point(row, col);
                    }
                }
            }

            return null;
        }

        private bool ScrollDragViewportAtEdge(Panel viewport, Point screenLocation)
        {
            if (_aktifSurukleme == null || !viewport.Visible) return false;
            var location = viewport.PointToClient(screenLocation);
            if (!viewport.ClientRectangle.Contains(location)) return false;
            var position = viewport.AutoScrollPosition;
            var x = -position.X;
            var y = -position.Y;
            var step = Math.Max(10, GetTileWidth() / 2);
            if (viewport.HorizontalScroll.Visible)
            {
                if (location.X < 24) x = Math.Max(0, x - step);
                else if (location.X >= viewport.ClientSize.Width - 24) x += step;
            }
            if (viewport.VerticalScroll.Visible)
            {
                if (location.Y < 24) y = Math.Max(0, y - step);
                else if (location.Y >= viewport.ClientSize.Height - 24) y += step;
            }
            if (x == -position.X && y == -position.Y) return false;
            viewport.AutoScrollPosition = new Point(x, y);
            return viewport.AutoScrollPosition != position;
        }

        private void UpdateDragPreviewPosition()
        {
            if (_suruklemeOnizleme == null)
            {
                return;
            }

            var previousBounds = _suruklemeOnizleme.Bounds;
            var location = _oyunPaneli.PointToClient(Cursor.Position);
            _suruklemeOnizleme.Left = location.X - _suruklemeOfseti.X;
            _suruklemeOnizleme.Top = location.Y - _suruklemeOfseti.Y;
            _suruklemeOnizleme.BringToFront();
            _oyunPaneli.Invalidate(previousBounds);
            _oyunPaneli.Invalidate(_suruklemeOnizleme.Bounds);
        }

        private void EndCustomDrag(bool restoreOnly)
        {
            _dragScrollTimer.Stop();
            Capture = false;
            if (_suruklemeOnizleme != null)
            {
                var previewBounds = _suruklemeOnizleme.Bounds;
                var parent = _suruklemeOnizleme.Parent;
                if (parent != null)
                {
                    parent.Controls.Remove(_suruklemeOnizleme);
                    parent.Invalidate(previewBounds);
                }
                _suruklemeOnizleme.Dispose();
                _suruklemeOnizleme = null;
            }

            if (restoreOnly)
            {
                RestoreDragSource(_aktifSurukleme);
                _aktifSurukleme = null;
                _aktifSuruklemeTus = MouseButtons.None;
                return;
            }

            _aktifSurukleme = null;
            _aktifSuruklemeTus = MouseButtons.None;
        }

        private void RemoveFromSource(TileDragData data)
        {
            if (data == null)
            {
                return;
            }

            if (data.IsGroupDrag)
            {
                for (var index = 0; index < data.GroupTiles.Count; index++)
                {
                    SetTileAt(data.SourceKind, data.IndexA, data.GroupStartColumn + index, null, 0);
                }
                return;
            }

            if (data.SourceKind == TileSourceKind.Board)
            {
                _masaSlotlari[data.IndexA, data.IndexB] = null;
                _masaYonleri[data.IndexA, data.IndexB] = 0;
            }
            else if (data.SourceKind == TileSourceKind.Hand)
            {
                _elSlotlari[data.IndexA, data.IndexB] = null;
                _elGorunumleri[data.IndexA, data.IndexB] = 0;
            }
        }

        private void RemoveResidualUiCopies(IEnumerable<int> tileIds)
        {
            if (tileIds == null)
            {
                return;
            }

            var ids = new HashSet<int>(tileIds);
            if (ids.Count == 0)
            {
                return;
            }

            for (var row = 0; row < HandRows; row++)
            {
                for (var col = 0; col < HandCols; col++)
                {
                    if (_elSlotlari[row, col] != null && ids.Contains(_elSlotlari[row, col].Id))
                    {
                        _elSlotlari[row, col] = null;
                        _elGorunumleri[row, col] = 0;
                    }
                }
            }

            for (var row = 0; row < BoardRows; row++)
            {
                for (var col = 0; col < BoardCols; col++)
                {
                    if (_masaSlotlari[row, col] != null && ids.Contains(_masaSlotlari[row, col].Id))
                    {
                        _masaSlotlari[row, col] = null;
                        _masaYonleri[row, col] = 0;
                    }
                }
            }
        }

        private void ReturnTileToOrigin(TileDragData sourceData, Tile tile, int visualMode)
        {
            if (sourceData.SourceKind == TileSourceKind.Board)
            {
                _masaSlotlari[sourceData.IndexA, sourceData.IndexB] = tile;
                _masaYonleri[sourceData.IndexA, sourceData.IndexB] = visualMode;
                return;
            }

            if (sourceData.SourceKind == TileSourceKind.Hand)
            {
                _elSlotlari[sourceData.IndexA, sourceData.IndexB] = tile;
                _elGorunumleri[sourceData.IndexA, sourceData.IndexB] = visualMode;
                return;
            }

            if (sourceData.SourceKind == TileSourceKind.Deck)
            {
                return;
            }

            TryPlaceIntoFirstEmptyBoard(tile, visualMode);
        }

        private void RestoreDragSource(TileDragData data)
        {
            if (data == null || !data.SourceRemoved)
            {
                return;
            }

            if (data.IsGroupDrag)
            {
                for (var index = 0; index < data.GroupTiles.Count; index++)
                {
                    SetTileAt(data.SourceKind, data.IndexA, data.GroupStartColumn + index, data.GroupTiles[index].Clone(), data.GroupVisualModes[index]);
                }
                return;
            }

            if (data.SourceKind == TileSourceKind.Board)
            {
                _masaSlotlari[data.IndexA, data.IndexB] = data.Tile.Clone();
                _masaYonleri[data.IndexA, data.IndexB] = data.OriginalVisualMode;
                return;
            }

            if (data.SourceKind == TileSourceKind.Hand)
            {
                _elSlotlari[data.IndexA, data.IndexB] = data.Tile.Clone();
                _elGorunumleri[data.IndexA, data.IndexB] = data.OriginalVisualMode;
            }
        }

        private bool TryPlaceIntoFirstEmptyBoard(Tile tile, int visualMode)
        {
            for (var row = 0; row < BoardRows; row++)
            {
                for (var col = 0; col < BoardCols; col++)
                {
                    if (_masaSlotlari[row, col] == null)
                    {
                        _masaSlotlari[row, col] = tile;
                        _masaYonleri[row, col] = visualMode;
                        return true;
                    }
                }
            }
            return false;
        }

        private int GetSourceVisualMode(TileDragData data)
        {
            if (data.SourceKind == TileSourceKind.Deck)
            {
                return 2;
            }

            return data.SourceKind == TileSourceKind.Board
                ? data.SourceRemoved ? data.OriginalVisualMode : _masaYonleri[data.IndexA, data.IndexB]
                : data.SourceRemoved ? data.OriginalVisualMode : _elGorunumleri[data.IndexA, data.IndexB];
        }

        private Control BuildDragPreviewControl(TileDragData data, int visualMode)
        {
            if (data != null && data.IsGroupDrag && data.GroupTiles != null && data.GroupTiles.Count > 0)
            {
                var tileWidth = data.SourceKind == TileSourceKind.Board ? GetBoardTileWidth() : GetTileWidth();
                var tileHeight = data.SourceKind == TileSourceKind.Board ? GetBoardTileHeight() : GetTileHeight();
                var gap = data.SourceKind == TileSourceKind.Hand ? GetHandGapX() : GetBoardGapX();
                var preview = new Panel
                {
                    Width = data.GroupTiles.Count * tileWidth + (data.GroupTiles.Count - 1) * gap,
                    Height = tileHeight,
                    BackColor = Color.Transparent,
                    Enabled = false
                };

                for (var index = 0; index < data.GroupTiles.Count; index++)
                {
                    var tileView = new TileView(data.GroupTiles[index])
                    {
                        RotationQuarterTurns = data.GroupVisualModes[index] == 1 ? 2 : 0,
                        FaceDown = data.GroupVisualModes[index] == 2,
                        Enabled = false,
                        Left = index * (tileWidth + gap),
                        Top = 0
                    };
                    ApplyTileViewSize(tileView, data.SourceKind == TileSourceKind.Board);
                    preview.Controls.Add(tileView);
                }

                return preview;
            }

            var singlePreview = new TileView(data.Tile)
            {
                RotationQuarterTurns = visualMode == 1 ? 2 : 0,
                FaceDown = visualMode == 2,
                Enabled = false
            };
            ApplyTileViewSize(singlePreview, data.SourceKind == TileSourceKind.Board);
            return singlePreview;
        }

        private Tile GetTileAt(TileSourceKind sourceKind, int row, int col)
        {
            return sourceKind == TileSourceKind.Hand ? _elSlotlari[row, col] : _masaSlotlari[row, col];
        }

        private int GetVisualModeAt(TileSourceKind sourceKind, int row, int col)
        {
            return sourceKind == TileSourceKind.Hand ? _elGorunumleri[row, col] : _masaYonleri[row, col];
        }

        private void SetTileAt(TileSourceKind sourceKind, int row, int col, Tile tile, int visualMode)
        {
            if (sourceKind == TileSourceKind.Hand)
            {
                _elSlotlari[row, col] = tile;
                _elGorunumleri[row, col] = visualMode;
                return;
            }

            _masaSlotlari[row, col] = tile;
            _masaYonleri[row, col] = visualMode;
        }

        private void SyncHandSlots(IEnumerable<Tile> hand)
        {
            var sourceTiles = hand.Select(x => x.Clone()).ToList();
            var sourceMap = sourceTiles.ToDictionary(x => x.Id, x => x);
            var preservedIds = new HashSet<int>();

            for (var row = 0; row < HandRows; row++)
            {
                for (var col = 0; col < HandCols; col++)
                {
                    var current = _elSlotlari[row, col];
                    if (current == null)
                    {
                        continue;
                    }

                    if (!sourceMap.ContainsKey(current.Id) || preservedIds.Contains(current.Id))
                    {
                        _elSlotlari[row, col] = null;
                        _elGorunumleri[row, col] = 0;
                        continue;
                    }

                    _elSlotlari[row, col] = sourceMap[current.Id].Clone();
                    preservedIds.Add(current.Id);
                }
            }

            if (preservedIds.Count == 0)
            {
                if (IsTraditionalGame)
                {
                    var columns = Math.Max(Math.Min(12, GetVisibleHandColumnCount()), (sourceTiles.Count + HandRows - 1) / HandRows);
                    columns = Math.Min(HandCols, columns);
                    for (var index = 0; index < Math.Min(sourceTiles.Count, HandRows * columns); index++)
                    {
                        _elSlotlari[index / columns, index % columns] = sourceTiles[index].Clone();
                        _elGorunumleri[index / columns, index % columns] = 0;
                    }
                    return;
                }
                if (sourceTiles.Count <= HandCols)
                {
                    for (var index = 0; index < sourceTiles.Count; index++)
                    {
                        _elSlotlari[0, index] = sourceTiles[index].Clone();
                        _elGorunumleri[0, index] = 0;
                    }
                    return;
                }

                foreach (var tile in sourceTiles)
                {
                    if (!PlaceInLeftmostTopEmptyHandSlot(tile.Clone()))
                    {
                        return;
                    }
                }

                return;
            }

            foreach (var tile in sourceTiles)
            {
                if (preservedIds.Contains(tile.Id))
                {
                    continue;
                }

                if (!PlaceInTopRightEmptyHandSlot(tile.Clone()))
                {
                    return;
                }
            }
        }

        private bool PlaceInTopRightEmptyHandSlot(Tile tile)
        {
            if (IsTraditionalGame)
            {
                var columns = Math.Max(11, Math.Min(12, GetVisibleHandColumnCount()));
                for (var row = 0; row < HandRows; row++)
                    for (var col = columns - 1; col >= 0; col--)
                        if (_elSlotlari[row, col] == null)
                        {
                            _elSlotlari[row, col] = tile;
                            _elGorunumleri[row, col] = 0;
                            return true;
                        }
            }
            for (var row = 0; row < HandRows; row++)
            {
                for (var col = HandCols - 1; col >= 0; col--)
                {
                    if (_elSlotlari[row, col] == null)
                    {
                        _elSlotlari[row, col] = tile;
                        _elGorunumleri[row, col] = 0;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool PlaceInLeftmostTopEmptyHandSlot(Tile tile)
        {
            for (var row = 0; row < HandRows; row++)
            {
                for (var col = 0; col < HandCols; col++)
                {
                    if (_elSlotlari[row, col] == null)
                    {
                        _elSlotlari[row, col] = tile;
                        _elGorunumleri[row, col] = 0;
                        return true;
                    }
                }
            }

            return false;
        }

        private void LoadBoardFromMelds(IList<Meld> melds)
        {
            ClearBoardSlots();
            if (IsTraditionalGame)
            {
                RefreshTraditionalTable();
                return;
            }

            var orderedMelds = melds
                .Where(x => x != null)
                .OrderBy(x => x.BoardRow < 0 ? int.MaxValue : x.BoardRow)
                .ThenBy(x => x.StartColumn < 0 ? int.MaxValue : x.StartColumn)
                .ToList();

            foreach (var meld in orderedMelds)
            {
                if (TryPlaceMeldAtStoredPosition(meld))
                {
                    continue;
                }

                int row;
                int col;
                if (!FindRandomPlacement(meld.Tiles.Count, out row, out col))
                {
                    break;
                }

                PlaceMeld(meld, row, col);
                meld.BoardRow = row;
                meld.StartColumn = col;
            }
        }

        private bool TryPlaceMeldAtStoredPosition(Meld meld)
        {
            if (meld.BoardRow < 0 || meld.BoardRow >= BoardRows || meld.StartColumn < 0)
            {
                return false;
            }

            if (!CanPlaceMeldAt(meld.BoardRow, meld.StartColumn, meld.Tiles.Count, 0))
            {
                return false;
            }

            PlaceMeld(meld, meld.BoardRow, meld.StartColumn);
            return true;
        }

        private bool FindRandomPlacement(int tileCount, out int rowIndex, out int colIndex)
        {
            var candidates = new List<Point>();
            for (var row = 0; row < BoardRows; row++)
            {
                for (var col = AutoMeldGapCols; col <= BoardCols - tileCount - AutoMeldGapCols; col++)
                {
                    if (!CanPlaceMeldAt(row, col, tileCount, AutoMeldGapCols))
                    {
                        continue;
                    }

                    candidates.Add(new Point(col, row));
                }
            }

            if (candidates.Count == 0)
            {
                for (var row = 0; row < BoardRows; row++)
                {
                    for (var col = 0; col <= BoardCols - tileCount; col++)
                    {
                        if (CanPlaceMeldAt(row, col, tileCount, 0))
                        {
                            rowIndex = row;
                            colIndex = col;
                            return true;
                        }
                    }
                }

                rowIndex = -1;
                colIndex = -1;
                return false;
            }

            var candidate = candidates[_rastgeleMasaYerlesimi.Next(candidates.Count)];
            rowIndex = candidate.Y;
            colIndex = candidate.X;
            return true;
        }

        private void PlaceMeld(Meld meld, int row, int col)
        {
            for (var offset = 0; offset < meld.Tiles.Count; offset++)
            {
                _masaSlotlari[row, col + offset] = meld.Tiles[offset].Clone();
            }
        }

        private bool CanPlaceMeldAt(int row, int col, int tileCount, int outerGap)
        {
            if (row < 0 || row >= BoardRows || col < 0 || col + tileCount > BoardCols)
            {
                return false;
            }

            var left = System.Math.Max(0, col - outerGap);
            var right = System.Math.Min(BoardCols, col + tileCount + outerGap);
            for (var checkCol = left; checkCol < right; checkCol++)
            {
                if (_masaSlotlari[row, checkCol] != null)
                {
                    return false;
                }
            }

            return true;
        }

        private void Log(string text)
        {
            var entry = new LogEntry
            {
                TimeText = DateTime.Now.ToString("HH:mm:ss"),
                Text = text ?? string.Empty,
                RenderText = text ?? string.Empty
            };

            Seat teaFrom;
            Seat teaTo;
            string teaSender;
            string teaTarget;
            if (TryParseTeaGift(entry.RenderText, out teaFrom, out teaTo, out teaSender, out teaTarget))
            {
                entry.EmoteCode = ":cay:";
                entry.RenderText = string.Format("{0}, {1}'a çay gönderdi.", teaSender, teaTarget);
                entry.Text = entry.RenderText;
                StartTeaFlightAnimation(teaFrom, teaTo);
            }

            foreach (var code in GetEmoteCodes())
            {
                if (string.IsNullOrWhiteSpace(entry.EmoteCode) && entry.RenderText.Contains(code))
                {
                    entry.EmoteCode = code;
                    entry.RenderText = entry.RenderText.Replace(code, string.Empty).Trim();
                    break;
                }
            }

            _gunlukKutusu.Items.Add(entry);
            _gunlukKutusu.TopIndex = Math.Max(0, _gunlukKutusu.Items.Count - 1);

            if (_logPencereKutusu != null && !_logPencereKutusu.IsDisposed)
            {
                _logPencereKutusu.AppendText(entry.TimeText + "  " + entry.Text + Environment.NewLine);
                _logPencereKutusu.SelectionStart = _logPencereKutusu.TextLength;
                _logPencereKutusu.ScrollToCaret();
            }
        }

        private bool TryParseTeaGift(string text, out Seat fromSeat, out Seat toSeat, out string senderName, out string targetName)
        {
            fromSeat = Seat.South;
            toSeat = Seat.South;
            senderName = string.Empty;
            targetName = string.Empty;

            var marker = ":cayto:";
            var index = (text ?? string.Empty).IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var payload = text.Substring(index + marker.Length).Trim();
            var end = payload.IndexOf(' ');
            if (end >= 0)
            {
                payload = payload.Substring(0, end);
            }

            var parts = payload.Split(new[] { '|' });
            if (parts.Length < 4 || !Enum.TryParse(parts[0], out fromSeat) || !Enum.TryParse(parts[1], out toSeat))
            {
                return false;
            }

            senderName = string.IsNullOrWhiteSpace(parts[2]) ? SeatName(fromSeat) : parts[2];
            targetName = string.IsNullOrWhiteSpace(parts[3]) ? SeatName(toSeat) : parts[3];
            return true;
        }

        private void StartTeaFlightAnimation(Seat fromSeat, Seat toSeat)
        {
            if (!_oyunPaneli.Visible || !_oyuncuEtiketleri.ContainsKey(fromSeat) || !_oyuncuEtiketleri.ContainsKey(toSeat))
            {
                return;
            }

            var image = EnsureTeaEmoteImageLoaded();
            if (image == null)
            {
                return;
            }

            StopTeaFlightAnimation();
            _cayUcusBaslangic = _oyunPaneli.PointToClient(GetControlCenterScreen(_oyuncuEtiketleri[fromSeat]));
            _cayUcusBitis = _oyunPaneli.PointToClient(GetControlCenterScreen(_oyuncuEtiketleri[toSeat]));
            _cayUcusAdimi = 0;
            _cayUcusToplamAdim = 34;
            _cayUcusGorunumu = new PictureBox
            {
                Parent = _oyunPaneli,
                Image = image,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Enabled = false,
                Width = 48,
                Height = 48,
                Left = (int)_cayUcusBaslangic.X,
                Top = (int)_cayUcusBaslangic.Y
            };
            _cayUcusGorunumu.BringToFront();
            _cayUcusZamani.Start();
        }

        private void CayUcusTimerOnTick(object sender, EventArgs e)
        {
            if (_cayUcusGorunumu == null)
            {
                _cayUcusZamani.Stop();
                return;
            }

            _cayUcusAdimi++;
            var progress = System.Math.Min(1F, (float)_cayUcusAdimi / _cayUcusToplamAdim);
            var wave = (float)System.Math.Sin(progress * System.Math.PI * 4D) * 10F;
            var x = _cayUcusBaslangic.X + (_cayUcusBitis.X - _cayUcusBaslangic.X) * progress;
            var y = _cayUcusBaslangic.Y + (_cayUcusBitis.Y - _cayUcusBaslangic.Y) * progress + wave;
            _cayUcusGorunumu.Left = (int)x;
            _cayUcusGorunumu.Top = (int)y;
            _cayUcusGorunumu.BringToFront();

            if (progress >= 1F)
            {
                StopTeaFlightAnimation();
            }
        }

        private void StopTeaFlightAnimation()
        {
            _cayUcusZamani.Stop();
            if (_cayUcusGorunumu == null)
            {
                return;
            }

            var parent = _cayUcusGorunumu.Parent;
            var bounds = _cayUcusGorunumu.Bounds;
            if (parent != null)
            {
                parent.Controls.Remove(_cayUcusGorunumu);
                parent.Invalidate(bounds);
            }
            _cayUcusGorunumu.Dispose();
            _cayUcusGorunumu = null;
        }

        private void TryAnimateDraw(Seat seat, IList<int> beforeHandIds)
        {
            var player = _engine.State.Players.FirstOrDefault(x => x.Seat == seat);
            if (player == null)
            {
                return;
            }

            var newTile = player.Hand.FirstOrDefault(x => beforeHandIds == null || !beforeHandIds.Contains(x.Id));
            if (newTile == null)
            {
                return;
            }

            var start = UsesNewTableAppearance && _traditionalTable != null
                ? GetTraditionalTargetCenter(TraditionalTargetKind.Stock)
                : GetControlCenterScreen(_desteEtiketi);
            var destination = seat == _yerelKoltuk
                ? FindHandTileCenter(newTile.Id) ?? (UsesNewTableAppearance ? GetTraditionalSeatCenter(seat) : GetControlCenterScreen(_oyuncuEtiketleri[seat]))
                : (UsesNewTableAppearance ? GetTraditionalSeatCenter(seat) : GetControlCenterScreen(_oyuncuEtiketleri[seat]));
            StartTileAnimation(newTile, start, destination, seat != _yerelKoltuk);
            PlayEmbeddedMoveSound();
        }

        private void TryAnimatePlacement(Seat seat, IList<Meld> beforeTable, IList<Meld> afterTable)
        {
            var oldIds = new HashSet<int>((beforeTable ?? new List<Meld>()).SelectMany(x => x.Tiles).Select(x => x.Id));
            var newTile = (afterTable ?? new List<Meld>()).SelectMany(x => x.Tiles).FirstOrDefault(x => !oldIds.Contains(x.Id));
            if (newTile == null)
            {
                return;
            }

            var destination = FindBoardTileCenter(newTile.Id);
            if (!destination.HasValue)
            {
                return;
            }

            StartTileAnimation(newTile, GetControlCenterScreen(_oyuncuEtiketleri[seat]), destination.Value, false);
            PlayEmbeddedMoveSound();
        }

        private void StartTileAnimation(Tile tile, Point startScreen, Point endScreen, bool faceDown)
        {
            if (tile == null)
            {
                return;
            }

            StopAnimation();

            _animasyonBaslangic = _oyunPaneli.PointToClient(startScreen);
            _animasyonBitis = _oyunPaneli.PointToClient(endScreen);
            _animasyonAdimi = 0;
            _animasyonToplamAdim = 10;
            _animasyonGorunumu = new TileView(tile)
            {
                FaceDown = faceDown,
                Enabled = false,
                Parent = _oyunPaneli,
                Left = (int)_animasyonBaslangic.X,
                Top = (int)_animasyonBaslangic.Y
            };
            ApplyTileViewSize(_animasyonGorunumu);
            _animasyonGorunumu.BringToFront();
            _animasyonZamani.Start();
        }

        private void AnimationTimerOnTick(object sender, EventArgs e)
        {
            if (_animasyonGorunumu == null)
            {
                _animasyonZamani.Stop();
                return;
            }

            _animasyonAdimi++;
            var progress = System.Math.Min(1F, (float)_animasyonAdimi / _animasyonToplamAdim);
            var x = _animasyonBaslangic.X + (_animasyonBitis.X - _animasyonBaslangic.X) * progress;
            var y = _animasyonBaslangic.Y + (_animasyonBitis.Y - _animasyonBaslangic.Y) * progress;
            _animasyonGorunumu.Left = (int)x;
            _animasyonGorunumu.Top = (int)y;
            _animasyonGorunumu.BringToFront();

            if (progress >= 1F)
            {
                StopAnimation();
            }
        }

        private void StopAnimation()
        {
            _animasyonZamani.Stop();
            if (_animasyonGorunumu != null)
            {
                var animationBounds = _animasyonGorunumu.Bounds;
                var parent = _animasyonGorunumu.Parent;
                if (parent != null)
                {
                    parent.Controls.Remove(_animasyonGorunumu);
                    parent.Invalidate(animationBounds);
                    parent.Refresh();
                }
                _animasyonGorunumu.Dispose();
                _animasyonGorunumu = null;
                _masaPaneli.Invalidate(true);
                _elDisPaneli.Invalidate(true);
            }

            var after = _animasyonSonrasi;
            _animasyonSonrasi = null;
            if (after != null)
            {
                after();
            }
        }

        private void StartCelebration(string text)
        {
            Log(text);
            _konfetiler.Clear();
            var random = new Random();
            var colors = new[]
            {
                Color.Gold,
                Color.OrangeRed,
                Color.DeepSkyBlue,
                Color.LimeGreen,
                Color.HotPink,
                Color.White
            };

            for (var index = 0; index < 140; index++)
            {
                _konfetiler.Add(new ConfettiParticle
                {
                    X = random.Next(0, System.Math.Max(1, _oyunPaneli.Width)),
                    Y = random.Next(-_oyunPaneli.Height, 0),
                    SpeedX = (float)(random.NextDouble() * 6.0 - 3.0),
                    SpeedY = 3F + (float)(random.NextDouble() * 5.0),
                    Size = 6F + (float)(random.NextDouble() * 8.0),
                    Color = colors[random.Next(colors.Length)]
                });
            }

            _kutlamaKareSayisi = 0;
            _kutlamaPaneli.Bounds = _oyunPaneli.Bounds;
            _kutlamaPaneli.Visible = true;
            _kutlamaPaneli.BringToFront();
            _kutlamaZamani.Start();
        }

        private void CelebrationTimerOnTick(object sender, EventArgs e)
        {
            _kutlamaKareSayisi++;
            foreach (var konfeti in _konfetiler)
            {
                konfeti.X += konfeti.SpeedX;
                konfeti.Y += konfeti.SpeedY;
                konfeti.SpeedY += 0.08F;
            }

            _kutlamaPaneli.Invalidate();
            if (_kutlamaKareSayisi >= 130)
            {
                _kutlamaZamani.Stop();
                _kutlamaPaneli.Visible = false;
                _konfetiler.Clear();
                _kutlamaPaneli.Invalidate();
            }
        }

        private Point GetControlCenterScreen(Control control)
        {
            var screenRect = control.RectangleToScreen(control.ClientRectangle);
            return new Point(screenRect.Left + screenRect.Width / 2 - GetTileWidth() / 2, screenRect.Top + screenRect.Height / 2 - GetTileHeight() / 2);
        }

        private Point? FindHandTileCenter(int tileId)
        {
            for (var row = 0; row < HandRows; row++)
            {
                for (var col = 0; col < HandCols; col++)
                {
                    if (_elSlotlari[row, col] != null && _elSlotlari[row, col].Id == tileId)
                    {
                        return GetControlCenterScreen(_elSlotPanelleri[row, col]);
                    }
                }
            }

            return null;
        }

        private Point? FindBoardTileCenter(int tileId)
        {
            for (var row = 0; row < BoardRows; row++)
            {
                for (var col = 0; col < BoardCols; col++)
                {
                    if (_masaSlotlari[row, col] != null && _masaSlotlari[row, col].Id == tileId)
                    {
                        return GetControlCenterScreen(_masaSlotPanelleri[row, col]);
                    }
                }
            }

            return null;
        }

        private void BotTimerOnTick(object sender, EventArgs e)
        {
            if (_engine.State.IsGameOver)
            {
                _botZamani.Stop();
                return;
            }

            if (_botDusunuyor)
            {
                return;
            }

            var currentPlayer = _engine.State.Players.First(x => x.Seat == _engine.State.CurrentTurn);
            if (currentPlayer.Type != PlayerType.Bot)
            {
                return;
            }

            var actingSeat = _engine.State.CurrentTurn;
            var beforeTable = _engine.State.Table.Select(x => x.Clone()).ToList();
            var beforeHandIds = _engine.State.Players.First(x => x.Seat == actingSeat).Hand.Select(x => x.Id).ToList();
            var snapshot = _engine.Snapshot();
            var turnId = ++_botTurnId;
            var maxThinkMilliseconds = ResolveBotWorkerThinkMilliseconds(actingSeat);
            _botDusunuyor = true;
            _botDusunmeEtiketi.Text = currentPlayer.Name + " düşünüyor";
            _botDusunmeEtiketi.Visible = true;
            _botDusunmeCubugu.Visible = true;
            _durumEtiketi.Text = "Bot Düşünüyor";
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                var worker = new GameEngine();
                worker.MaxBotThinkMilliseconds = maxThinkMilliseconds;
                worker.Restore(snapshot);
                string message;
                var success = worker.RunBotTurnIfNeeded(out message);
                var result = new BotTurnResult
                {
                    Seat = actingSeat,
                    BeforeHandIds = beforeHandIds,
                    BeforeTable = beforeTable,
                    State = success ? worker.Snapshot() : snapshot,
                    Message = message,
                    DebugInfo = worker.LastBotDebugInfo,
                    Success = success,
                    TurnId = turnId
                };
                BeginInvoke(new Action(() => FinishBotTurn(result)));
            });
        }

        private int ResolveBotWorkerThinkMilliseconds(Seat actingSeat)
        {
            var configured = _currentSettings != null
                ? System.Math.Max(1000, _currentSettings.BotThinkSeconds * 1000)
                : 30000;

            if (_currentSettings == null || !_currentSettings.EnableTurnTimer || !_turnTimerSeat.HasValue || _turnTimerSeat.Value != actingSeat)
            {
                return configured;
            }

            var safeMilliseconds = (int)System.Math.Floor((_turnDeadline - DateTime.Now).TotalMilliseconds) - 5000;
            return System.Math.Max(1000, System.Math.Min(configured, safeMilliseconds));
        }

        private void FinishBotTurn(BotTurnResult result)
        {
            _botDusunuyor = false;
            _botDusunmeEtiketi.Visible = false;
            _botDusunmeCubugu.Visible = false;

            if (result == null || result.TurnId != _botTurnId || !_oyunBasladi || _engine.State.IsGameOver || _engine.State.CurrentTurn != result.Seat)
            {
                RefreshUi();
                return;
            }

            if (result == null || !result.Success)
            {
                RefreshUi();
                return;
            }

            _engine.Restore(result.State);
            LoadBoardFromMelds(_engine.State.Table);
            Log(result.Message);
            AppendBotDebug(result.DebugInfo);
            BroadcastTextToNetworks(result.Message);
            BroadcastGameStateToNetworks();
            RefreshUi();
            if (!string.IsNullOrWhiteSpace(result.Message) && result.Message.Contains("ortadan tas cekti"))
            {
                TryAnimateDraw(result.Seat, result.BeforeHandIds);
            }
            else
            {
                TryAnimatePlacement(result.Seat, result.BeforeTable, _engine.State.Table);
            }
            CheckForWinner();
        }

        private void StartLanHost()
        {
            CloseBotDebugWindow();
            if (_host != null)
            {
                Log("Ağ odası zaten açık.");
                return;
            }

            var settings = SelectNewGameSettings(CreateDefaultLanSettings());
            if (settings == null) return;

            var roomName = ShowPrompt("Oda adı", "Ağ Odası Kur", "Nane Masa");
            if (string.IsNullOrWhiteSpace(roomName))
            {
                return;
            }

            StartLocalGame(settings);
            EnsureLanRemoteSeat();
            _host = new LanHost();
            _host.EnableLivePreview = _currentSettings != null && _currentSettings.EnableLivePreview;
            _host.EnableTurnTimer = _currentSettings != null && _currentSettings.EnableTurnTimer;
            _host.TurnSeconds = _currentSettings != null ? _currentSettings.TurnSeconds : 30;
            _host.BotThinkSeconds = _currentSettings != null ? _currentSettings.BotThinkSeconds : 30;
            _host.Mode = _engine.State.Mode;
            _host.UseNewAppearance = _engine.State.UseNewAppearance;
            _host.TargetScore = _currentSettings.TargetScore;
            _host.MatchId = _matchId;
            _host.LogReceived += Log;
            _host.LobbyChanged += snapshot => BeginInvoke(new Action(() =>
            {
                Log("Ağ odası güncellendi: " + snapshot.RoomName + " - " + snapshot.HostIp);
                _durumEtiketi.Text = snapshot.RoomName + Environment.NewLine + snapshot.HostIp;
            }));
            _host.RemotePlayerNamed += (seat, playerName) => BeginInvoke(new Action(() => ApplyRemotePlayerName(seat, playerName)));
            _host.RemoteDrawRequested += (seat, row, col) => BeginInvoke(new Action(() => HandleRemoteDrawRequest(seat, row, col)));
            _host.RemotePassRequested += seat => BeginInvoke(new Action(() => HandleRemotePassRequest(seat)));
            _host.RemoteDiscardDrawRequested += seat => BeginInvoke(new Action(() => HandleRemoteDiscardDrawRequest(seat)));
            _host.RemoteDiscardRequested += (seat, tileId, finish, melds, handIds) => BeginInvoke(new Action(() => HandleRemoteDiscardRequest(seat, tileId, finish, melds, handIds)));
            _host.RemoteCommitRequested += (seat, melds, handTileIds) => BeginInvoke(new Action(() => HandleRemoteCommitRequest(seat, melds, handTileIds)));
            _host.RemotePreviewRequested += (seat, melds, handTileIds) => BeginInvoke(new Action(() => HandleRemotePreviewRequest(seat, melds, handTileIds)));
            _host.RemotePlayerDisconnected += (seat, playerName) => BeginInvoke(new Action(() => ConvertDisconnectedSeatToBot(seat, playerName + " bağlantısı koptu")));
            try
            {
                _host.Start(_currentSettings != null ? _currentSettings.LanPort : 51234, roomName, _engine.State.Players);
            }
            catch (SocketException ex)
            {
                Log("Ağ odası açılamadı: " + ex.Message);
                MessageBox.Show(
                    this,
                    "Ağ odası açılamadı.\nBaşka bir program portu kullanıyor olabilir ya da güvenlik duvarı engelliyor olabilir.\n\nDetay: " + ex.Message,
                    "Ağ Odası Hatası",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _host.Dispose();
                _host = null;
                return;
            }
            if (_oyunBasladi)
            {
                _host.BroadcastGameState(_engine.Snapshot());
            }
            _discovery.BroadcastRoom(new LanRoomAnnouncement
            {
                HostName = Environment.MachineName,
                HostIp = _host.LocalIpAddress,
                HostIpCandidates = new System.Collections.Generic.List<string>(_host.LocalIpCandidates),
                RoomName = roomName,
                Port = _host.Port,
                Mode = _engine.State.Mode,
                UseNewAppearance = _engine.State.UseNewAppearance,
                ProtocolVersion = LanProtocol.Version
            });
            Log("Ağ odası açıldı. IP: " + _host.LocalIpAddress + " Oda: " + roomName + " Port: " + _host.Port);
        }

        private void StartOnlineRoom()
        {
            CloseBotDebugWindow();
            var settings = SelectNewGameSettings(CreateDefaultLanSettings());
            if (settings == null) return;
            var roomName = ShowPrompt("Oda adı", "Online Oda Kur", "Nane Masa");
            if (string.IsNullOrWhiteSpace(roomName))
            {
                return;
            }

            var name = ShowPrompt("Oyuncu adı", "Online Oda Kur", "Nurhan");
            name = NormalizeNetworkName(name);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            settings.Players[0].Name = name;
            StartLocalGame(settings);
            EnsureOnlineClient(name);
            _onlineHostModu = true;
            _onlineActorSeats.Clear();
            _onlinePendingBotSeats.Clear();
            _onlineClient.Mode = settings.Mode;
            _onlineClient.UseNewAppearance = settings.Mode == GameMode.NaneOkey && settings.UseNewAppearance;
            _onlineClient.TargetScore = settings.TargetScore;
            _onlineClient.CreateRoom(roomName);
            Log("Online oda kuruluyor: " + roomName);
        }

        private void JoinOnlineRoomPrompt()
        {
            CloseBotDebugWindow();
            var roomName = ShowPrompt("Oda adı", "Online Odaya Katıl", "Nane Masa");
            if (string.IsNullOrWhiteSpace(roomName))
            {
                return;
            }

            var name = ShowPrompt("Oyuncu adı", "Online Odaya Katıl", "Misafir");
            name = NormalizeNetworkName(name);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            EnsureOnlineClient(name);
            _onlineHostModu = false;
            _agIstemcisiModu = true;
            _agKoltuguAtandi = false;
            _bekleyenAgDurumu = null;
            _onlineActorSeats.Clear();
            _onlinePendingBotSeats.Clear();
            _onlineClient.JoinRoom(roomName);
            ShowGameScreen();
            Log("Online odaya katılma isteği gönderildi: " + roomName);
        }

        private void RefreshOnlineRooms()
        {
            var name = NormalizeNetworkName(Environment.UserName);
            EnsureOnlineClient(name);
            _odaDurumEtiketi.Text = "Online odalar bekleniyor.";
        }

        private void EnsureOnlineClient(string playerName)
        {
            if (_onlineClient != null && _onlineClient.IsConnected)
            {
                _onlineClient.SetPlayerName(playerName);
                return;
            }

            if (_onlineClient != null)
            {
                _onlineClient.Dispose();
            }

            _onlineClient = new OnlineClient();
            ConfigureOnlineClient(_onlineClient);
            _onlineClient.Connect(PhotonRealtimeAppId, playerName);
        }

        private void ConfigureOnlineClient(OnlineClient onlineClient)
        {
            onlineClient.LogReceived += text => BeginInvoke(new Action(() => Log(text)));
            onlineClient.RoomsUpdated += rooms => BeginInvoke(new Action(() => RefreshOnlineRoomList(rooms)));
            onlineClient.RoomCreated += () => BeginInvoke(new Action(() =>
            {
                _onlineHostModu = true;
            }));
            onlineClient.RoomJoined += () => BeginInvoke(new Action(() =>
            {
                if (onlineClient.IsMasterClient)
                {
                    _onlineHostModu = true;
                    _agIstemcisiModu = false;
                    _agKoltuguAtandi = true;
                    _yerelKoltuk = Seat.South;
                    _onlineActorSeats[onlineClient.LocalActorNumber] = Seat.South;
                    BroadcastGameStateToNetworks();
                }
                else
                {
                    _onlineHostModu = false;
                    _agIstemcisiModu = true;
                    _agKoltuguAtandi = false;
                    Log("Online koltuk ataması bekleniyor.");
                }
            }));
            onlineClient.PlayerEntered += (actorNumber, playerName) => BeginInvoke(new Action(() => HandleOnlinePlayerEntered(actorNumber, playerName)));
            onlineClient.PlayerLeft += actorNumber => BeginInvoke(new Action(() => HandleOnlinePlayerLeft(actorNumber)));
            onlineClient.MasterClientChanged += actorNumber => BeginInvoke(new Action(() => HandleOnlineMasterClientChanged(actorNumber)));
            onlineClient.EnvelopeReceived += (sender, envelope) => BeginInvoke(new Action(() => HandleOnlineEnvelope(sender, envelope)));
        }

        private void RefreshOnlineRoomList(IList<OnlineRoomInfo> rooms)
        {
            _odaListesiKutusu.BeginUpdate();
            _odaListesiKutusu.Items.Clear();
            foreach (var room in rooms)
            {
                _odaListesiKutusu.Items.Add(room);
            }
            _odaListesiKutusu.EndUpdate();
            _odaDurumEtiketi.Text = rooms.Count == 0 ? "Online oda bulunamadı." : rooms.Count + " online oda bulundu.";
        }

        private void HandleOnlinePlayerEntered(int actorNumber, string playerName)
        {
            if (_onlineClient == null || !_onlineClient.IsMasterClient || actorNumber == _onlineClient.LocalActorNumber)
            {
                return;
            }

            Seat seat;
            if (!TryAssignOnlineSeat(actorNumber, out seat))
            {
                Log("Online odada boş koltuk kalmadı.");
                return;
            }

            var normalizedName = NormalizeNetworkName(playerName);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = NormalizeNetworkName(_onlineClient.GetPlayerName(actorNumber));
            }

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = "Misafir";
            }

            ApplyRemotePlayerName(seat, normalizedName);
            var assignment = new OnlineSeatAssignment { ActorNumber = actorNumber, Seat = seat.ToString() };
            _onlineClient.SendEnvelope("assign", LanJson.Serialize(assignment), false);
            BroadcastGameStateToNetworks();
        }

        private void HandleOnlinePlayerLeft(int actorNumber)
        {
            Seat seat;
            var hadSeat = _onlineActorSeats.TryGetValue(actorNumber, out seat);
            _onlineActorSeats.Remove(actorNumber);

            if (!hadSeat)
            {
                return;
            }

            if (_onlineClient != null && _onlineClient.IsMasterClient)
            {
                ConvertDisconnectedSeatToBot(seat, "Online oyuncu düştü");
            }
            else if (!_onlinePendingBotSeats.Contains(seat))
            {
                _onlinePendingBotSeats.Add(seat);
            }
        }

        private void HandleOnlineMasterClientChanged(int actorNumber)
        {
            if (_onlineClient == null || actorNumber != _onlineClient.LocalActorNumber)
            {
                Log("Online oda yöneticisi değişti.");
                return;
            }

            _onlineHostModu = true;
            _agIstemcisiModu = false;
            _agKoltuguAtandi = true;
            _onlineActorSeats[_onlineClient.LocalActorNumber] = _yerelKoltuk;
            PromoteLocalOnlinePlayerToAdmin();
            ConvertStaleOnlineHumansToBots();
            foreach (var seat in _onlinePendingBotSeats.ToList())
            {
                ConvertDisconnectedSeatToBot(seat, "Online oyuncu düştü");
            }

            _onlinePendingBotSeats.Clear();
            Log("Online oda yöneticisi sen oldun. Oyun devam ediyor.");
            BroadcastGameStateToNetworks();
        }

        private bool TryAssignOnlineSeat(int actorNumber, out Seat seat)
        {
            if (_onlineActorSeats.TryGetValue(actorNumber, out seat))
            {
                return true;
            }

            var usedSeats = new HashSet<Seat>(_onlineActorSeats.Values);
            var candidates = _engine.State.Players
                .Where(x => x.IsActive && x.Seat != Seat.South && (x.Type == PlayerType.Remote || x.Type == PlayerType.Bot))
                .OrderBy(x => x.Type == PlayerType.Remote ? 0 : 1)
                .ThenBy(x => (int)x.Seat)
                .Select(x => x.Seat)
                .ToList();
            foreach (var candidate in candidates)
            {
                if (!usedSeats.Contains(candidate))
                {
                    _onlineActorSeats[actorNumber] = candidate;
                    seat = candidate;
                    return true;
                }
            }

            seat = Seat.South;
            return false;
        }

        private void HandleOnlineEnvelope(int senderActorNumber, LanEnvelope envelope)
        {
            if (envelope == null)
            {
                return;
            }

            if (envelope.Type == "assign")
            {
                if (_onlineClient == null || senderActorNumber != _onlineClient.MasterActorNumber) return;
                var assignment = LanJson.Deserialize<OnlineSeatAssignment>(envelope.Payload);
                Seat assignedSeat;
                if (assignment != null &&
                    Enum.TryParse(assignment.Seat, out assignedSeat))
                {
                    _onlineActorSeats[assignment.ActorNumber] = assignedSeat;
                    if (assignment.ActorNumber == (_onlineClient != null ? _onlineClient.LocalActorNumber : 0))
                    {
                        ApplyAssignedSeat(assignedSeat);
                    }
                }
                return;
            }

            if (envelope.Type == "game")
            {
                if (_onlineClient == null || senderActorNumber != _onlineClient.MasterActorNumber) return;
                var snapshot = LanJson.Deserialize<LanGameSnapshot>(envelope.Payload);
                if (snapshot != null && snapshot.State != null)
                {
                    if (senderActorNumber > 0 && !_onlineActorSeats.ContainsKey(senderActorNumber))
                    {
                        _onlineActorSeats[senderActorNumber] = Seat.South;
                    }

                    if (_currentSettings == null)
                    {
                        _currentSettings = CreateDefaultLanSettings();
                    }

                    _currentSettings.EnableLivePreview = snapshot.EnableLivePreview;
                    ApplyNetworkMatchId(snapshot.MatchId);
                    _currentSettings.Mode = snapshot.State.Mode;
                    _currentSettings.UseNewAppearance = snapshot.State.UseNewAppearance;
                    _currentSettings.TargetScore = snapshot.TargetScore > 0 ? snapshot.TargetScore : 1000;
                    _currentSettings.EnableTurnTimer = snapshot.EnableTurnTimer;
                    _currentSettings.TurnSeconds = snapshot.TurnSeconds > 0 ? snapshot.TurnSeconds : 30;
                    _currentSettings.BotThinkSeconds = snapshot.BotThinkSeconds > 0 ? snapshot.BotThinkSeconds : 30;
                    _engine.MaxBotThinkMilliseconds = System.Math.Max(1000, _currentSettings.BotThinkSeconds * 1000);

                    ApplyRemoteGameState(snapshot.State.ToDomain());
                }
                return;
            }

            if (envelope.Type == "text")
            {
                Log(envelope.Payload);
                return;
            }

            if (_onlineClient == null || !_onlineClient.IsMasterClient)
            {
                return;
            }

            Seat remoteSeat;
            if (!_onlineActorSeats.TryGetValue(senderActorNumber, out remoteSeat))
            {
                return;
            }

            if (envelope.Type == "draw")
            {
                var request = LanJson.Deserialize<LanDrawRequest>(envelope.Payload);
                HandleRemoteDrawRequest(remoteSeat, request != null ? request.TargetRow : -1, request != null ? request.TargetColumn : -1);
            }
            else if (envelope.Type == "pass")
            {
                HandleRemotePassRequest(remoteSeat);
            }
            else if (envelope.Type == "discard_draw")
            {
                HandleRemoteDiscardDrawRequest(remoteSeat);
            }
            else if (envelope.Type == "discard")
            {
                var request = LanJson.Deserialize<LanDiscardRequest>(envelope.Payload);
                if (request != null) HandleRemoteDiscardRequest(remoteSeat, request.TileId, request.FinishClassic,
                    request.Melds != null ? request.Melds.ConvertAll(x => x.ToDomain()) : new List<Meld>(), request.HandTileIds ?? new List<int>());
            }
            else if (envelope.Type == "commit")
            {
                var layout = LanJson.Deserialize<LanTurnLayout>(envelope.Payload);
                HandleRemoteCommitRequest(
                    remoteSeat,
                    layout != null && layout.Melds != null ? layout.Melds.ConvertAll(x => x.ToDomain()) : new List<Meld>(),
                    layout != null ? layout.HandTileIds : new List<int>());
            }
            else if (envelope.Type == "preview")
            {
                var preview = LanJson.Deserialize<LanTurnPreview>(envelope.Payload);
                HandleRemotePreviewRequest(
                    remoteSeat,
                    preview != null && preview.Melds != null ? preview.Melds.ConvertAll(x => x.ToDomain()) : new List<Meld>(),
                    preview != null ? preview.HandTileIds : new List<int>());
            }
        }

        private void ApplyRemotePlayerName(Seat seat, string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return;
            }

            var player = _engine.State.Players.FirstOrDefault(x => x.Seat == seat);
            if (player == null)
            {
                return;
            }

            player.Type = PlayerType.Remote;
            RenamePlayerPreservingScores(player, playerName.Trim());
            RefreshUi();
            _host?.BroadcastLobby(_engine.State.Players);
            BroadcastGameStateToNetworks();
        }

        private void ConvertDisconnectedSeatToBot(Seat seat, string reason)
        {
            if (!_oyunBasladi || _engine.State == null)
            {
                return;
            }

            var player = _engine.State.Players.FirstOrDefault(x => x.Seat == seat);
            if (player == null || !player.IsActive || player.Type == PlayerType.Bot)
            {
                return;
            }

            var oldName = string.IsNullOrWhiteSpace(player.Name) ? SeatName(seat) : player.Name;
            player.Type = PlayerType.Bot;
            player.Difficulty = BotDifficulty.SmartHard;
            var botName = oldName.EndsWith(" Bot", StringComparison.OrdinalIgnoreCase)
                ? oldName
                : oldName + " Bot";
            RenamePlayerPreservingScores(player, botName);

            Log(reason + ". " + SeatName(seat) + " bot olarak devam ediyor.");
            _host?.BroadcastLobby(_engine.State.Players);
            BroadcastGameStateToNetworks();
            RefreshUi();
            _botZamani.Start();
        }

        private void PromoteLocalOnlinePlayerToAdmin()
        {
            if (_engine.State == null)
            {
                return;
            }

            var localPlayer = _engine.State.Players.FirstOrDefault(x => x.Seat == _yerelKoltuk);
            if (localPlayer != null && localPlayer.IsActive)
            {
                localPlayer.Type = PlayerType.Human;
            }
        }

        private void ConvertStaleOnlineHumansToBots()
        {
            if (_engine.State == null)
            {
                return;
            }

            foreach (var player in _engine.State.Players.Where(x => x.IsActive && x.Seat != _yerelKoltuk && x.Type == PlayerType.Human).ToList())
            {
                ConvertDisconnectedSeatToBot(player.Seat, "Online yönetici düştü");
            }
        }

        private void EnsureLanRemoteSeat()
        {
            if (_engine.State.Players.Any(x => x.IsActive && x.Type == PlayerType.Remote))
            {
                return;
            }

            var remotePlayer = _engine.State.Players.FirstOrDefault(x => x.IsActive && x.Seat != Seat.South);
            if (remotePlayer == null)
            {
                return;
            }

            remotePlayer.Type = PlayerType.Remote;
            remotePlayer.Name = string.IsNullOrWhiteSpace(remotePlayer.Name) ? SeatName(remotePlayer.Seat) : remotePlayer.Name;
            Log(remotePlayer.Name + " ağ oyuncusu koltuğu olarak ayarlandı.");
        }

        private void JoinLanHost()
        {
            CloseBotDebugWindow();
            if (_client != null && _client.IsConnected)
            {
                Log("Zaten bir ağa bağlısın.");
                return;
            }

            var host = ShowPrompt("Sunucu IP adresi", "Ağ Oyununa Katıl", "127.0.0.1");
            if (string.IsNullOrWhiteSpace(host))
            {
                return;
            }

            var roomName = ShowPrompt("Oda adı", "Ağ Oyununa Katıl", "Nane Masa");
            if (string.IsNullOrWhiteSpace(roomName))
            {
                return;
            }

            var name = ShowPrompt("Oyuncu adı", "Ağ Oyununa Katıl", "Misafir");
            name = NormalizeNetworkName(name);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            SocketException ex;
            if (!TryConnectToLanRoom(host, 51234, name, roomName, out ex))
            {
                Log("Ağ oyununa bağlanılamadı: " + ex.Message);
                MessageBox.Show(
                    this,
                    "Ağ oyununa bağlanılamadı.\nIP yanlış olabilir, host açık olmayabilir ya da güvenlik duvarı engelliyor olabilir.\n\nDetay: " + ex.Message,
                    "Bağlantı Hatası",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            ShowGameScreen();
        }

        private void RefreshRoomList(List<LanRoomAnnouncement> rooms)
        {
            var selected = _odaListesiKutusu.SelectedItem as LanRoomAnnouncement;
            _odaListesiKutusu.BeginUpdate();
            _odaListesiKutusu.Items.Clear();
            foreach (var room in rooms)
            {
                _odaListesiKutusu.Items.Add(room);
            }
            _odaListesiKutusu.DisplayMember = "RoomName";
            _odaListesiKutusu.DisplayMember = string.Empty;
            _odaListesiKutusu.EndUpdate();

            if (selected != null)
            {
                foreach (var item in _odaListesiKutusu.Items)
                {
                    var room = item as LanRoomAnnouncement;
                    if (room != null && room.RoomName == selected.RoomName && room.HostIp == selected.HostIp)
                    {
                        _odaListesiKutusu.SelectedItem = item;
                        break;
                    }
                }
            }

            _odaDurumEtiketi.Text = rooms.Count == 0 ? "Açık oda bulunamadı." : rooms.Count + " açık oda bulundu.";
        }

        private void ApplyRemoteGameState(GameState state)
        {
            _agIstemcisiModu = true;
            CloseBotDebugWindow();
            _botZamani.Stop();
            if (state == null || state.Players == null || state.Players.Count == 0)
            {
                Log("Ağdan eksik oyun durumu geldi.");
                return;
            }

            if (!_agKoltuguAtandi)
            {
                _bekleyenAgDurumu = state.Clone();
                Log("Ağ koltuğu bekleniyor, oyun durumu sıraya alındı.");
                return;
            }

            var previousState = _oyunBasladi ? _engine.Snapshot() : null;
            _engine.Restore(state);
            if (_currentSettings != null)
            {
                _currentSettings.Mode = state.Mode;
                _currentSettings.UseNewAppearance = state.UseNewAppearance;
            }
            _oyunBasladi = true;
            ClearLivePreview();
            if (!_engine.State.IsGameOver)
            {
                _roundScoreShownForCurrentGame = false;
            }
            LoadBoardFromMelds(_engine.State.Table);
            var localPlayer = GetLocalPlayerOrFallback(_engine.State);
            if (localPlayer != null)
            {
                SyncHandSlots(localPlayer.Hand);
                if (_bekleyenAgCekmeSlotu.HasValue && previousState != null)
                {
                    var previousPlayer = GetLocalPlayerOrFallback(previousState);
                    var previousIds = previousPlayer != null
                        ? new HashSet<int>(previousPlayer.Hand.Select(x => x.Id))
                        : new HashSet<int>();
                    var newTile = localPlayer.Hand.FirstOrDefault(x => !previousIds.Contains(x.Id));
                    if (newTile != null)
                    {
                        MoveHandTileToPreferredSlot(newTile.Id, _bekleyenAgCekmeSlotu.Value.X, _bekleyenAgCekmeSlotu.Value.Y);
                    }
                    _bekleyenAgCekmeSlotu = null;
                }
            }
            else
            {
                ClearHandSlots();
            }
            RefreshUi();

            if (previousState != null && !string.IsNullOrWhiteSpace(_engine.State.LastAction) && _engine.State.LastAction.Contains("ortadan tas cekti"))
            {
                var actingPlayer = _engine.State.Players.FirstOrDefault(x => _engine.State.LastAction.StartsWith(x.Name + " "));
                if (actingPlayer != null)
                {
                    var previousPlayer = previousState.Players.FirstOrDefault(x => x.Seat == actingPlayer.Seat);
                    var beforeHandIds = previousPlayer != null
                        ? previousPlayer.Hand.Select(x => x.Id).ToList()
                        : new List<int>();
                    TryAnimateDraw(actingPlayer.Seat, beforeHandIds);
                }
            }

            CheckForWinner();
        }

        private void ApplyAssignedSeat(Seat seat)
        {
            _yerelKoltuk = seat;
            _agKoltuguAtandi = true;
            Log("Ağ koltuğun: " + SeatName(seat));
            LayoutBoardSurface();

            if (_bekleyenAgDurumu != null)
            {
                var pendingState = _bekleyenAgDurumu;
                _bekleyenAgDurumu = null;
                ApplyRemoteGameState(pendingState);
            }
            else
            {
                RefreshUi();
            }
        }

        private static string SeatName(Seat seat)
        {
            switch (seat)
            {
                case Seat.West:
                    return "Koltuk 2";
                case Seat.North:
                    return "Koltuk 3";
                case Seat.East:
                    return "Koltuk 4";
                default:
                    return "Koltuk 1";
            }
        }

        private PlayerState GetLocalPlayerOrFallback(GameState state)
        {
            if (state == null || state.Players == null || state.Players.Count == 0)
            {
                return null;
            }

            var player = state.Players.FirstOrDefault(x => x.Seat == _yerelKoltuk);
            if (player != null)
            {
                return player;
            }

            player = state.Players.FirstOrDefault(x => x.Seat == Seat.South);
            return player ?? state.Players.FirstOrDefault();
        }

        private void HandleRemoteDrawRequest(Seat seat, int targetRow, int targetCol)
        {
            if (!_oyunBasladi || _engine.State.CurrentTurn != seat)
            {
                return;
            }

            var beforeHandIds = _engine.State.Players.First(x => x.Seat == seat).Hand.Select(x => x.Id).ToList();
            string message;
            if (_engine.DrawTile(seat, out message))
            {
                if (seat == _yerelKoltuk && targetRow >= 0 && targetCol >= 0)
                {
                    var localPlayer = GetLocalPlayerOrFallback(_engine.State);
                    var newTile = localPlayer != null
                        ? localPlayer.Hand.FirstOrDefault(x => !beforeHandIds.Contains(x.Id))
                        : null;
                    if (newTile != null)
                    {
                        SyncHandSlots(localPlayer.Hand);
                        MoveHandTileToPreferredSlot(newTile.Id, targetRow, targetCol);
                    }
                }
                Log(message);
                BroadcastTextToNetworks(message);
                BroadcastGameStateToNetworks();
                RefreshUi();
                TryAnimateDraw(seat, beforeHandIds);
            }
            else
            {
                Log(message);
                _host?.SendTextToSeat(seat, message);
                if (_onlineClient != null && _onlineClient.InRoom && _onlineClient.IsMasterClient)
                    _onlineClient.SendEnvelope("text", message, false);
            }
            if (IsTraditionalGame) CheckForWinner();
        }

        private void HandleRemotePassRequest(Seat seat)
        {
            if (!_oyunBasladi || _engine.State.CurrentTurn != seat)
            {
                return;
            }

            string message;
            if (_engine.PassTurn(seat, out message))
            {
                Log(message);
                BroadcastTextToNetworks(message);
                BroadcastGameStateToNetworks();
                RefreshUi();
            }
            else
            {
                Log(message);
            }
            if (IsTraditionalGame) CheckForWinner();
        }

        private void HandleRemoteCommitRequest(Seat seat, IList<Meld> melds, IList<int> handTileIds)
        {
            if (!_oyunBasladi || _engine.State.CurrentTurn != seat)
            {
                return;
            }

            var beforeTable = _engine.State.Table.Select(x => x.Clone()).ToList();
            if (!_engine.State.TurnInProgress)
            {
                _engine.BeginTurn(seat);
            }

            string error;
            if (!_engine.ReplaceTurnLayout(seat, melds, handTileIds, out error))
            {
                Log(error);
                _host?.SendTextToSeat(seat, error);
                if (_onlineClient != null && _onlineClient.InRoom && _onlineClient.IsMasterClient)
                {
                    _onlineClient.SendEnvelope("text", error, false);
                }
                return;
            }

            if (_engine.CommitTurn(seat, out error))
            {
                ClearLivePreview();
                Log(_engine.State.LastAction);
                BroadcastTextToNetworks(_engine.State.LastAction);
                BroadcastGameStateToNetworks();
                RefreshUi();
                TryAnimatePlacement(seat, beforeTable, _engine.State.Table);
                CheckForWinner();
            }
            else if (_engine.State.Deck.Count == 0 && error == "Hamleyi onaylamak için elinden en az bir taş koymalısın.")
            {
                HandleRemotePassRequest(seat);
            }
            else
            {
                Log(error);
                _host?.SendTextToSeat(seat, error);
                if (_onlineClient != null && _onlineClient.InRoom && _onlineClient.IsMasterClient)
                {
                    _onlineClient.SendEnvelope("text", error, false);
                }
            }
        }

        private void HandleRemotePreviewRequest(Seat seat, IList<Meld> melds, IList<int> handTileIds)
        {
            if (!_oyunBasladi || _engine.State.CurrentTurn != seat)
            {
                return;
            }

            if (_currentSettings == null || !_currentSettings.EnableLivePreview)
            {
                return;
            }

            if (!_engine.State.TurnInProgress)
            {
                return;
            }

            ApplyLivePreview(seat, melds);
            if (_host != null)
            {
                _host.BroadcastPreview(seat, melds, handTileIds);
            }
            else if (_onlineClient != null && _onlineClient.InRoom && _onlineClient.IsMasterClient)
            {
                var preview = new LanTurnPreview
                {
                    Seat = seat.ToString(),
                    Melds = melds == null ? new List<LanMeldDto>() : melds.Select(LanMeldDto.FromDomain).ToList(),
                    HandTileIds = handTileIds == null ? new List<int>() : new List<int>(handTileIds)
                };
                _onlineClient.SendEnvelope("preview", LanJson.Serialize(preview), false);
            }
        }

        private void JoinSelectedRoom()
        {
            var onlineRoom = _odaListesiKutusu.SelectedItem as OnlineRoomInfo;
            if (onlineRoom != null)
            {
                var onlineName = ShowPrompt("Oyuncu adı", "Online Odaya Katıl", "Misafir");
                onlineName = NormalizeNetworkName(onlineName);
                if (string.IsNullOrWhiteSpace(onlineName))
                {
                    return;
                }

                EnsureOnlineClient(onlineName);
                _onlineHostModu = false;
                _agIstemcisiModu = true;
                _agKoltuguAtandi = false;
                _onlineClient.JoinRoom(onlineRoom.Name);
                ShowGameScreen();
                return;
            }

            var room = _odaListesiKutusu.SelectedItem as LanRoomAnnouncement;
            if (room == null)
            {
                return;
            }

            var name = ShowPrompt("Oyuncu adı", "Ağ Oyununa Katıl", "Misafir");
            name = NormalizeNetworkName(name);
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            SocketException ex;
            if (!TryConnectToLanRoom(room.HostIp, room.HostIpCandidates, room.Port, name, room.RoomName, out ex))
            {
                Log("Ağ oyununa bağlanılamadı: " + ex.Message);
                MessageBox.Show(
                    this,
                    "Seçilen odaya bağlanılamadı.\nOda listede görünüyorsa UDP keşfi çalışıyor demektir; bu durumda sorun çoğunlukla TCP portunun engellenmesi, yanlış ağ profili ya da cihaz izolasyonudur.\n\nDetay: " + ex.Message,
                    "Bağlantı Hatası",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            ShowGameScreen();
        }

        private bool TryConnectToLanRoom(string host, int preferredPort, string playerName, string roomName, out SocketException lastError)
        {
            return TryConnectToLanRoom(host, null, preferredPort, playerName, roomName, out lastError);
        }

        private bool TryConnectToLanRoom(string host, IList<string> hostCandidates, int preferredPort, string playerName, string roomName, out SocketException lastError)
        {
            lastError = null;
            _agIstemcisiModu = true;
            _agKoltuguAtandi = false;
            _bekleyenAgDurumu = null;

            var hostsToTry = new List<string>();
            if (!string.IsNullOrWhiteSpace(host))
            {
                hostsToTry.Add(host.Trim());
            }

            if (hostCandidates != null)
            {
                foreach (var candidate in hostCandidates)
                {
                    if (string.IsNullOrWhiteSpace(candidate))
                    {
                        continue;
                    }

                    var normalized = candidate.Trim();
                    if (!hostsToTry.Contains(normalized))
                    {
                        hostsToTry.Add(normalized);
                    }
                }
            }

            Log("Bağlantı hedefleri: " + string.Join(", ", hostsToTry.ToArray()) + " Port: " + preferredPort);

            var portsToTry = new List<int> { preferredPort };
            for (var port = preferredPort; port < preferredPort + 128; port++)
            {
                if (!portsToTry.Contains(port))
                {
                    portsToTry.Add(port);
                }
            }

            var preferredPortOnly = new List<int> { preferredPort };
            if (TryConnectToLanCandidates(hostsToTry, preferredPortOnly, playerName, roomName, 1200, host, preferredPort, out lastError))
            {
                return true;
            }

            if (portsToTry.Count > 1 && TryConnectToLanCandidates(hostsToTry, portsToTry.Skip(1).ToList(), playerName, roomName, 250, host, preferredPort, out lastError))
            {
                return true;
            }

            _client = null;
            _agIstemcisiModu = false;
            return false;
        }

        private bool TryConnectToLanCandidates(IList<string> hostsToTry, IList<int> portsToTry, string playerName, string roomName, int timeoutMilliseconds, string primaryHost, int primaryPort, out SocketException lastError)
        {
            lastError = null;
            foreach (var candidateHost in hostsToTry)
            {
                foreach (var port in portsToTry)
                {
                    var client = new LanClient();
                    ConfigureLanClient(client);
                    try
                    {
                        client.Connect(candidateHost, port, playerName, roomName, timeoutMilliseconds);
                        _client = client;
                        if (candidateHost != primaryHost || port != primaryPort)
                        {
                            Log("Oda alternatif adresten bulundu: " + candidateHost + ":" + port);
                        }

                        return true;
                    }
                    catch (SocketException ex)
                    {
                        lastError = ex;
                        client.Dispose();
                    }
                }
            }

            return false;
        }

        private void ConfigureLanClient(LanClient client)
        {
            client.LogReceived += text => BeginInvoke(new Action(() => Log(text)));
            client.SeatAssigned += seat => BeginInvoke(new Action(() => ApplyAssignedSeat(seat)));
            client.LobbyReceived += snapshot => BeginInvoke(new Action(() =>
            {
                Log("Lobi geldi: " + snapshot.RoomName + " - " + snapshot.HostIp);
                _durumEtiketi.Text = snapshot.RoomName + Environment.NewLine + snapshot.HostIp;
            }));
            client.GameStateReceived += (state, snapshot) => BeginInvoke(new Action(() =>
            {
                if (_currentSettings == null)
                {
                    _currentSettings = CreateDefaultLanSettings();
                }

                if (snapshot != null)
                {
                    ApplyNetworkMatchId(snapshot.MatchId);
                    _currentSettings.Mode = state.Mode;
                    _currentSettings.UseNewAppearance = state.UseNewAppearance;
                    _currentSettings.TargetScore = snapshot.TargetScore > 0 ? snapshot.TargetScore : 1000;
                    _currentSettings.EnableLivePreview = snapshot.EnableLivePreview;
                    _currentSettings.EnableTurnTimer = snapshot.EnableTurnTimer;
                    _currentSettings.TurnSeconds = snapshot.TurnSeconds > 0 ? snapshot.TurnSeconds : 30;
                    _currentSettings.BotThinkSeconds = snapshot.BotThinkSeconds > 0 ? snapshot.BotThinkSeconds : 30;
                    _engine.MaxBotThinkMilliseconds = System.Math.Max(1000, _currentSettings.BotThinkSeconds * 1000);
                }

                ApplyRemoteGameState(state);
            }));
            client.PreviewReceived += (seat, melds, handTileIds) => BeginInvoke(new Action(() => ApplyLivePreview(seat, melds)));
        }

        private void SohbetKutusuKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendChatMessage();
            }
        }

        private void SendChatMessage()
        {
            var message = _sohbetGirdiKutusu.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var teaMessage = TryBuildOutgoingTeaMessage(message);
            if (teaMessage == null)
            {
                return;
            }

            if (SendChatPayload(teaMessage))
            {
                _sohbetGirdiKutusu.Clear();
            }
        }

        private void SendTeaButtonMessage()
        {
            var teaMessage = TryBuildOutgoingTeaMessage(":cay:");
            if (teaMessage != null)
            {
                SendChatPayload(teaMessage);
            }
        }

        private bool SendChatPayload(string message)
        {
            if (_client != null && _client.IsConnected)
            {
                _client.SendText(message);
                return true;
            }

            if (_onlineClient != null && _onlineClient.InRoom)
            {
                var localPlayer = GetLocalPlayerOrFallback(_engine.State);
                var playerName = localPlayer != null ? localPlayer.Name : "Oyuncu";
                var chatText = "[" + playerName + "] " + message;
                Log(chatText);
                _onlineClient.SendEnvelope("text", chatText, false);
                return true;
            }

            if (_host != null)
            {
                var localPlayer = GetLocalPlayerOrFallback(_engine.State);
                var playerName = localPlayer != null ? localPlayer.Name : "Oyuncu";
                var chatText = "[" + playerName + "] " + message;
                Log(chatText);
                _host.BroadcastText(chatText);
                return true;
            }

            Log("Sohbet için önce ağ odası kur ya da bir odaya katıl.");
            return false;
        }

        private string TryBuildOutgoingTeaMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || !message.Contains(":cay:"))
            {
                return message;
            }

            var sender = GetLocalPlayerOrFallback(_engine.State);
            if (sender == null)
            {
                Log("Çay göndermek için önce oyunda olmalısın.");
                return null;
            }

            var targets = _engine.State.Players
                .Where(x => x.IsActive && x.Seat != sender.Seat)
                .ToList();
            if (targets.Count == 0)
            {
                Log("Çay gönderecek oyuncu yok.");
                return null;
            }

            var target = ShowTeaTargetDialog(targets);
            if (target == null)
            {
                return null;
            }

            var senderName = SanitizeTeaField(sender.Name);
            var targetName = SanitizeTeaField(target.Name);
            return string.Format(":cayto:{0}|{1}|{2}|{3}", sender.Seat, target.Seat, senderName, targetName);
        }

        private PlayerState ShowTeaTargetDialog(IList<PlayerState> players)
        {
            using (var form = new Form())
            using (var label = new Label())
            using (var list = new ListBox())
            using (var okButton = new Button())
            using (var cancelButton = new Button())
            {
                form.Text = "Çay gönder";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(280, 220);
                form.BackColor = Color.FromArgb(233, 228, 219);

                label.Text = "Kime çay gönderilsin?";
                label.SetBounds(12, 12, 250, 24);

                list.SetBounds(12, 42, 256, 118);
                list.DisplayMember = "Name";
                foreach (var player in players)
                {
                    list.Items.Add(player);
                }
                if (list.Items.Count > 0)
                {
                    list.SelectedIndex = 0;
                }
                list.DoubleClick += (_, __) => form.DialogResult = DialogResult.OK;

                okButton.Text = "Gönder";
                okButton.SetBounds(104, 174, 76, 28);
                okButton.DialogResult = DialogResult.OK;

                cancelButton.Text = "Vazgeç";
                cancelButton.SetBounds(192, 174, 76, 28);
                cancelButton.DialogResult = DialogResult.Cancel;

                form.Controls.Add(label);
                form.Controls.Add(list);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return null;
                }

                return list.SelectedItem as PlayerState;
            }
        }

        private static string SanitizeTeaField(string value)
        {
            return (value ?? string.Empty).Replace("|", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private void DrawLogItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= _gunlukKutusu.Items.Count)
            {
                return;
            }

            var entry = _gunlukKutusu.Items[e.Index] as LogEntry;
            if (entry == null)
            {
                return;
            }

            var bounds = e.Bounds;
            var textRect = new Rectangle(bounds.Left + 4, bounds.Top + 4, bounds.Width - 8, bounds.Height - 8);
            Image emote = null;
            if (!string.IsNullOrWhiteSpace(entry.EmoteCode))
            {
                emote = CreateEmoteImageForCode(entry.EmoteCode);
                if (emote != null)
                {
                    textRect.Width -= 26;
                }
            }

            using (var brush = new SolidBrush(e.ForeColor))
            using (var format = new StringFormat())
            {
                format.Alignment = StringAlignment.Near;
                format.LineAlignment = StringAlignment.Near;
                format.Trimming = StringTrimming.Word;
                e.Graphics.DrawString(entry.TimeText + "  " + entry.RenderText, e.Font, brush, textRect, format);
            }

            if (emote != null)
            {
                e.Graphics.DrawImage(emote, bounds.Right - 26, bounds.Top + 2, 24, 24);
            }

            e.DrawFocusRectangle();
        }

        private void MeasureLogItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _gunlukKutusu.Items.Count)
            {
                e.ItemHeight = 24;
                return;
            }

            var entry = _gunlukKutusu.Items[e.Index] as LogEntry;
            if (entry == null)
            {
                e.ItemHeight = 24;
                return;
            }

            var width = Math.Max(80, _gunlukKutusu.ClientSize.Width - 12);
            if (!string.IsNullOrWhiteSpace(entry.EmoteCode))
            {
                width -= 26;
            }

            using (var graphics = _gunlukKutusu.CreateGraphics())
            {
                var size = graphics.MeasureString(entry.TimeText + "  " + entry.RenderText, _gunlukKutusu.Font, width);
                e.ItemHeight = Math.Max(24, (int)Math.Ceiling(size.Height) + 8);
            }
        }

        private bool CanHumanAct(bool showMessage)
        {
            if (_engine.State.IsGameOver || !_oyunBasladi)
            {
                return false;
            }

            if (_agIstemcisiModu)
            {
                if (!_agKoltuguAtandi)
                {
                    if (showMessage)
                    {
                        Log("Ağ koltuğu henüz atanmadı.");
                    }

                    return false;
                }

                if (_engine.State.CurrentTurn == _yerelKoltuk)
                {
                    return true;
                }

                if (showMessage)
                {
                    Log("Sıra şu an sende değil.");
                }

                return false;
            }

            if (_engine.State.Players[(int)_engine.State.CurrentTurn].Type == PlayerType.Human)
            {
                return true;
            }

            if (showMessage)
            {
                Log("Şu an insan oyuncunun sırası değil.");
            }

            return false;
        }

        private void BuildChatEmoteMenu()
        {
            _ifadeMenusu.Items.Clear();
            _ifadeMenusu.Padding = Padding.Empty;

            var panel = new FlowLayoutPanel
            {
                Width = 292,
                Height = 286,
                BackColor = Color.FromArgb(251, 248, 240),
                WrapContents = true,
                Margin = Padding.Empty,
                Padding = new Padding(6)
            };

            AddChatEmote(panel, ":gul:", "Gül", ":gul:", 0, 0);
            AddChatEmote(panel, ":kahkaha:", "Kahkaha", ":kahkaha:", 0, 1);
            AddChatEmote(panel, ":gozkirp:", "Göz kırp", ":gozkirp:", 0, 2);
            AddChatEmote(panel, ":sasir:", "Şaşır", ":sasir:", 0, 3);
            AddChatEmote(panel, ":dil:", "Dil", ":dil:", 0, 4);
            AddChatEmote(panel, ":havali:", "Havalı", ":havali:", 0, 5);
            AddChatEmote(panel, ":kizgin:", "Kızgın", ":kizgin:", 0, 6);
            AddChatEmote(panel, ":kararsiz:", "Kararsız", ":kararsiz:", 0, 7);
            AddChatEmote(panel, ":mahcup:", "Mahcup", ":mahcup:", 0, 8);
            AddChatEmote(panel, ":uzgun:", "Üzgün", ":uzgun:", 0, 9);
            AddChatEmote(panel, ":kalp:", "Kalp", ":kalp:", 2, 3);
            AddChatEmote(panel, ":kirik:", "Kırık", ":kirik:", 2, 4);
            AddChatEmote(panel, ":cay:", "Çay", ":cay:", 4, 5);
            AddChatEmote(panel, ":kahve:", "Kahve", ":kahve:", 2, 6);
            AddChatEmote(panel, ":sigara:", "Sigara", ":sigara:", 1, 9);
            AddChatEmote(panel, ":ok:", "Tamam", ":ok:", 3, 2);

            _ifadeMenusu.Items.Add(new ToolStripControlHost(panel)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = panel.Size
            });
        }

        private void AddChatEmote(FlowLayoutPanel panel, string code, string caption, string emoteText, int spriteRow, int spriteCol)
        {
            var button = new Button
            {
                Width = 64,
                Height = 62,
                Margin = new Padding(4),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Tahoma", 7.2F, FontStyle.Bold),
                Text = code + Environment.NewLine + caption,
                TextImageRelation = TextImageRelation.ImageAboveText,
                ImageAlign = ContentAlignment.MiddleCenter,
                TextAlign = ContentAlignment.BottomCenter
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(196, 196, 196);
            button.FlatAppearance.BorderSize = 1;
            button.Image = CreateEmoteImageForCode(code) ?? CreateEmoteImage(spriteRow, spriteCol);
            button.Click += (_, __) =>
            {
                if (_sohbetGirdiKutusu.TextLength > 0 && !_sohbetGirdiKutusu.Text.EndsWith(" "))
                {
                    _sohbetGirdiKutusu.AppendText(" ");
                }

                _sohbetGirdiKutusu.AppendText(emoteText + " ");
                _sohbetGirdiKutusu.Focus();
                _sohbetGirdiKutusu.SelectionStart = _sohbetGirdiKutusu.TextLength;
                _ifadeMenusu.Close();
            };
            panel.Controls.Add(button);
        }

        private void CayAnimasyonTimerOnTick(object sender, EventArgs e)
        {
            _cayAnimasyonAdimi++;
            if (_gunlukKutusu != null && !_gunlukKutusu.IsDisposed)
            {
                _gunlukKutusu.Invalidate();
            }
        }

        private static bool IsTeaEmoteCode(string code)
        {
            var normalized = NormalizeEmoteCode(code);
            return string.Equals(normalized, "(çay)", StringComparison.OrdinalIgnoreCase);
        }

        private Image EnsureTeaEmoteImageLoaded()
        {
            if (_cayEmoteImage != null)
            {
                return _cayEmoteImage;
            }

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("NaneOkey.Assets.cay.png"))
            {
                if (stream == null)
                {
                    return null;
                }

                _cayEmoteImage = Image.FromStream(stream);
                return _cayEmoteImage;
            }
        }

        private Image CreateEmoteImageForCode(string code)
        {
            if (IsTeaEmoteCode(code))
            {
                var tea = EnsureTeaEmoteImageLoaded();
                if (tea != null)
                {
                    return tea;
                }
            }

            Rectangle customSource;
            if (TryGetCustomEmoteSource(code, out customSource))
            {
                return CreateEmoteImage(customSource);
            }

            int row;
            int col;
            if (!TryGetEmoteSpritePosition(code, out row, out col))
            {
                return null;
            }

            return CreateEmoteImage(row, col);
        }

        private bool TryGetCustomEmoteSource(string code, out Rectangle source)
        {
            code = NormalizeEmoteCode(code);
            source = Rectangle.Empty;
            switch (code)
            {
                case ":@":
                    source = new Rectangle(1140, 0, 120, 120);
                    return true;
                case ":S":
                    source = new Rectangle(1300, 0, 120, 120);
                    return true;
                case ":$":
                    source = new Rectangle(1480, 0, 120, 120);
                    return true;
                case ":(":
                    source = new Rectangle(1660, 0, 120, 120);
                    return true;
                case "(U)":
                    source = new Rectangle(820, 320, 120, 120);
                    return true;
                case ":P":
                    source = new Rectangle(835, 0, 120, 120);
                    return true;
                case "(H)":
                    source = new Rectangle(1005, 0, 120, 120);
                    return true;
                default:
                    return false;
            }
        }

        private bool TryGetEmoteSpritePosition(string code, out int row, out int col)
        {
            code = NormalizeEmoteCode(code);
            row = 0;
            col = 0;
            switch (code)
            {
                case ":)":
                    row = 0; col = 0; return true;
                case ":D":
                    row = 0; col = 1; return true;
                case ";)":
                    row = 0; col = 2; return true;
                case ":-O":
                    row = 0; col = 3; return true;
                case ":P":
                    row = 0; col = 4; return true;
                case "(H)":
                    row = 0; col = 5; return true;
                case ":@":
                    row = 0; col = 6; return true;
                case ":S":
                    row = 0; col = 7; return true;
                case ":$":
                    row = 0; col = 8; return true;
                case ":(":
                    row = 0; col = 9; return true;
                case "(L)":
                    row = 2; col = 3; return true;
                case "(U)":
                    row = 2; col = 4; return true;
                case "(çay)":
                    row = 4; col = 5; return true;
                case "(kahve)":
                    row = 2; col = 6; return true;
                case "(sigara)":
                    row = 1; col = 9; return true;
                case "(ok)":
                    row = 3; col = 2; return true;
                default:
                    return false;
            }
        }

        private static string[] GetEmoteCodes()
        {
            return new[]
            {
                ":kahkaha:", ":gozkirp:", ":kararsiz:", ":sigara:", ":kahve:", ":mahcup:",
                ":havali:", ":kizgin:", ":uzgun:", ":sasir:", ":kirik:", ":kalp:",
                ":cay:", ":gul:", ":dil:", ":ok:",
                "(sigara)", "(kahve)", "(çay)", ":-O", ":)", ":D", ";)", ":(", ":P", ":@", ":S", ":$", "(H)", "(L)", "(U)", "(ok)"
            };
        }

        private static string NormalizeEmoteCode(string code)
        {
            switch (code)
            {
                case ":gul:":
                    return ":)";
                case ":kahkaha:":
                    return ":D";
                case ":gozkirp:":
                    return ";)";
                case ":sasir:":
                    return ":-O";
                case ":dil:":
                    return ":P";
                case ":havali:":
                    return "(H)";
                case ":kizgin:":
                    return ":@";
                case ":kararsiz:":
                    return ":S";
                case ":mahcup:":
                    return ":$";
                case ":uzgun:":
                    return ":(";
                case ":kalp:":
                    return "(L)";
                case ":kirik:":
                    return "(U)";
                case ":cay:":
                    return "(çay)";
                case ":kahve:":
                    return "(kahve)";
                case ":sigara:":
                    return "(sigara)";
                case ":ok:":
                    return "(ok)";
                default:
                    return code;
            }
        }

        private Image CreateEmoteImage(int row, int col)
        {
            var sprite = EnsureEmoteSpriteLoaded();
            if (sprite == null)
            {
                return null;
            }

            var xStarts = new[] { 160, 350, 515, 670, 855, 1040, 1225, 1410, 1595, 1775 };
            var yStarts = new[] { 0, 160, 320, 480, 640, 800 };
            if (row < 0 || row >= yStarts.Length || col < 0 || col >= xStarts.Length)
            {
                return null;
            }

            var source = new Rectangle(xStarts[col], yStarts[row], 120, 120);
            return CreateEmoteImage(source);
        }

        private Image CreateEmoteImage(Rectangle source)
        {
            var sprite = EnsureEmoteSpriteLoaded();
            if (sprite == null)
            {
                return null;
            }

            var bitmap = new Bitmap(28, 28);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(sprite, new Rectangle(0, 0, 28, 28), source, GraphicsUnit.Pixel);
            }
            return bitmap;
        }

        private Image EnsureEmoteSpriteLoaded()
        {
            if (_emoteSprite != null)
            {
                return _emoteSprite;
            }

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NaneOkey.Assets.emotes.png"))
            {
                if (stream == null)
                {
                    return null;
                }

                _emoteSprite = Image.FromStream(stream);
            }

            return _emoteSprite;
        }

        private bool EnsureEditableTurn(bool showMessage)
        {
            if (!CanHumanAct(showMessage))
            {
                return false;
            }

            if (IsTraditionalGame && !_engine.State.HasDrawnThisTurn)
            {
                if (showMessage) Log("Önce ortadan veya soldan bir taş çekmelisin.");
                return false;
            }
            if (_engine.State.TurnInProgress)
            {
                return true;
            }

            _engine.BeginTurn(_engine.State.CurrentTurn);
            LoadBoardFromMelds(_engine.State.TurnTable);
            SyncHandSlots(_engine.State.TurnHand);
            return true;
        }

        private void DrawCelebrationOverlay(object sender, PaintEventArgs e)
        {
            if (!_kutlamaPaneli.Visible || _konfetiler.Count == 0)
            {
                return;
            }

            foreach (var konfeti in _konfetiler)
            {
                using (var brush = new SolidBrush(konfeti.Color))
                {
                    e.Graphics.FillEllipse(brush, konfeti.X, konfeti.Y, konfeti.Size, konfeti.Size * 0.7F);
                }
            }
        }

        private void PlayEmbeddedMoveSound()
        {
            try
            {
                PlaySound(EnsureEmbeddedMoveSoundLoaded());
            }
            catch
            {
            }
        }

        private void TryPlayGameStartSound()
        {
            if (!_oyunBasladi || _engine.State == null)
            {
                return;
            }

            if (!string.Equals(_engine.State.LastAction, "Yeni oyun basladi.", StringComparison.Ordinal))
            {
                return;
            }

            var token = (_engine.State.WinnerName ?? string.Empty) + "|" + (_engine.State.LastAction ?? string.Empty);
            if (string.Equals(_lastPlayedGameStartToken, token, StringComparison.Ordinal))
            {
                return;
            }

            _lastPlayedGameStartToken = token;
            PlaySound(EnsureEmbeddedTurnStartSoundLoaded());
        }

        private void PlayRoundEndSound(PlayerState roundWinner, TotalScoreRecord overallWinner)
        {
            var localPlayer = GetLocalPlayerOrFallback(_engine.State);
            if (localPlayer == null)
            {
                return;
            }

            if (overallWinner != null)
            {
                if (string.Equals(overallWinner.PlayerName, localPlayer.Name, StringComparison.OrdinalIgnoreCase))
                {
                    PlaySound(EnsureEmbeddedFullWinSoundLoaded());
                }
                else
                {
                    PlaySound(EnsureEmbeddedLoseSoundLoaded());
                }

                return;
            }

            if (roundWinner != null && roundWinner.Seat == localPlayer.Seat)
            {
                PlaySound(EnsureEmbeddedRoundWinSoundLoaded());
            }
            else
            {
                PlaySound(EnsureEmbeddedLoseSoundLoaded());
            }
        }

        private void PlaySound(byte[] soundBytes)
        {
            if (soundBytes == null || soundBytes.Length == 0)
            {
                return;
            }

            using (var stream = new MemoryStream(soundBytes, false))
            {
                var player = new SoundPlayer(stream);
                player.Play();
            }
        }

        private byte[] EnsureEmbeddedMoveSoundLoaded()
        {
            return EnsureEmbeddedSoundLoaded("NaneOkey.Assets.move.wav", ref _embeddedMoveSoundBytes);
        }

        private byte[] EnsureEmbeddedRoundWinSoundLoaded()
        {
            return EnsureEmbeddedSoundLoaded("NaneOkey.Assets.win.wav", ref _embeddedRoundWinSoundBytes);
        }

        private byte[] EnsureEmbeddedLoseSoundLoaded()
        {
            return EnsureEmbeddedSoundLoaded("NaneOkey.Assets.lose.wav", ref _embeddedLoseSoundBytes);
        }

        private byte[] EnsureEmbeddedFullWinSoundLoaded()
        {
            return EnsureEmbeddedSoundLoaded("NaneOkey.Assets.fullwin.wav", ref _embeddedFullWinSoundBytes);
        }

        private byte[] EnsureEmbeddedTurnStartSoundLoaded()
        {
            return EnsureEmbeddedSoundLoaded("NaneOkey.Assets.basla.wav", ref _embeddedTurnStartSoundBytes);
        }

        private byte[] EnsureEmbeddedSoundLoaded(string resourceName, ref byte[] cache)
        {
            if (cache != null && cache.Length > 0)
            {
                return cache;
            }

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return null;
                }

                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    cache = memory.ToArray();
                }
            }

            return cache;
        }

        private void ClearBoardSlots()
        {
            for (var row = 0; row < BoardRows; row++)
            {
                for (var col = 0; col < BoardCols; col++)
                {
                    _masaSlotlari[row, col] = null;
                    _masaYonleri[row, col] = 0;
                }
            }
        }

        private void ClearHandSlots()
        {
            for (var row = 0; row < HandRows; row++)
            {
                for (var col = 0; col < HandCols; col++)
                {
                    _elSlotlari[row, col] = null;
                    _elGorunumleri[row, col] = 0;
                }
            }
        }

        private static int NextVisualMode(int currentMode)
        {
            return (currentMode + 1) % 3;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _dragScrollTimer.Dispose();
            if (_newNaneFeltCache != null) _newNaneFeltCache.Dispose();
            base.OnFormClosed(e);
            _host?.Dispose();
            _client?.Dispose();
            _onlineClient?.Dispose();
            _discovery.Dispose();
        }

        private static string ShowPrompt(string labelText, string title, string defaultValue)
        {
            using (var form = new Form())
            using (var label = new Label())
            using (var textBox = new TextBox())
            using (var okButton = new Button())
            using (var cancelButton = new Button())
            {
                form.Text = title;
                form.Width = 360;
                form.Height = 150;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;

                label.Left = 12;
                label.Top = 12;
                label.Width = 320;
                label.Text = labelText;

                textBox.Left = 12;
                textBox.Top = 36;
                textBox.Width = 320;
                textBox.Text = defaultValue;

                okButton.Text = "Tamam";
                okButton.Left = 176;
                okButton.Top = 70;
                okButton.Width = 75;
                okButton.DialogResult = DialogResult.OK;

                cancelButton.Text = "Vazgeç";
                cancelButton.Left = 257;
                cancelButton.Top = 70;
                cancelButton.Width = 75;
                cancelButton.DialogResult = DialogResult.Cancel;

                form.Controls.Add(label);
                form.Controls.Add(textBox);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                return form.ShowDialog() == DialogResult.OK ? textBox.Text : string.Empty;
            }
        }

        private static string NormalizeNetworkName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length == 0)
            {
                return string.Empty;
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

        private static void DrawWoodPanel(object sender, PaintEventArgs e)
        {
            var rect = ((Control)sender).ClientRectangle;
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(rect, Color.FromArgb(132, 88, 47), Color.FromArgb(90, 57, 32), 90F))
            {
                e.Graphics.FillRectangle(brush, rect);
            }
            using (var pen = new Pen(Color.FromArgb(77, 46, 24), 2))
            {
                e.Graphics.DrawRectangle(pen, 1, 1, rect.Width - 3, rect.Height - 3);
            }
        }

        private void DrawBoardSurface(object sender, PaintEventArgs e)
        {
            if (IsTraditionalGame) return;
            if (IsNewNaneAppearance) { PaintCachedNewNaneFelt(e.Graphics, Point.Empty); return; }
            var rect = ((Control)sender).ClientRectangle;
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(rect, _tahtaAcikRenk, _tahtaKoyuRenk, 90F))
            {
                e.Graphics.FillRectangle(brush, rect);
            }
            using (var pen = new Pen(Color.FromArgb(230, 207, 145), 4))
            {
                var guideRect = _boardViewport.Bounds;
                var ellipseMarginX = Math.Max(110, guideRect.Width / 4);
                var ellipseMarginY = Math.Max(36, guideRect.Height / 5);
                var ellipseLeft = Math.Max(20, guideRect.Left - ellipseMarginX);
                var ellipseTop = Math.Max(20, guideRect.Top - ellipseMarginY);
                var ellipseWidth = Math.Min(rect.Width - ellipseLeft - 20, guideRect.Width + ellipseMarginX * 2);
                var ellipseHeight = Math.Min(rect.Height - ellipseTop - 28, guideRect.Height + ellipseMarginY * 2 + 70);
                e.Graphics.DrawEllipse(
                    pen,
                    ellipseLeft,
                    ellipseTop,
                    Math.Max(200, ellipseWidth),
                    Math.Max(180, ellipseHeight));
            }
        }

        private void DrawBoardSlot(object sender, PaintEventArgs e)
        {
            if (IsNewNaneAppearance) return;
            var rect = ((Control)sender).ClientRectangle;
            using (var pen = new Pen(_slotCizgiRenk, 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
            }
        }

        private float CalculateUiScale(int boardWidth, int availableHeight)
        {
            // Keep the pieces readable; the board and all 48 shelf positions remain reachable by scrolling.
            return CalculateTraditionalUiScale(boardWidth, availableHeight);
        }

        private float CalculateBoardUiScale(int width, int height)
        {
            // The rack keeps its readable size; fit the ten-row board independently.
            var low = 0.58D;
            var high = Math.Max(low, _uiScale);
            for (var iteration = 0; iteration < 24; iteration++)
            {
                var scale = (low + high) / 2;
                var tileWidth = Math.Max(1, (int)Math.Round(TileWidth * scale));
                var tileHeight = Math.Max(1, (int)Math.Round(TileHeight * scale));
                var gapX = Math.Max(1, (int)Math.Round(BoardGapX * scale));
                var gapY = Math.Max(1, (int)Math.Round(BoardGapY * scale));
                if (BoardCols * tileWidth + (BoardCols + 1) * gapX + 2 <= width &&
                    BoardRows * tileHeight + (BoardRows + 1) * gapY + 2 <= height) low = scale;
                else high = scale;
            }
            return (float)Math.Max(0.58D, low - 0.00001D);
        }

        private int GetBoardTileWidth() { return Math.Max(1, (int)Math.Round(TileWidth * _boardUiScale)); }
        private int GetBoardTileHeight() { return Math.Max(1, (int)Math.Round(TileHeight * _boardUiScale)); }

        private int GetTileWidth()
        {
            return Math.Max(1, (int)Math.Round(TileWidth * _uiScale));
        }

        private int GetTileHeight()
        {
            return Math.Max(1, (int)Math.Round(TileHeight * _uiScale));
        }

        private int GetBoardGapX()
        {
            return Math.Max(1, (int)Math.Round(BoardGapX * _boardUiScale));
        }

        private int GetBoardGapY()
        {
            return Math.Max(1, (int)Math.Round(BoardGapY * _boardUiScale));
        }

        private int GetHandGapX()
        {
            return Math.Max(1, (int)Math.Round(HandGapX * _uiScale));
        }

        private int GetHandGapY()
        {
            return Math.Max(1, (int)Math.Round(HandGapY * _uiScale));
        }

        private int GetHandOuterHeight()
        {
            return (GetTileHeight() * HandRows) + (GetHandGapY() * (HandRows - 1)) + 24 +
                (_handNeedsHorizontalScroll ? SystemInformation.HorizontalScrollBarHeight : 0);
        }

        private int GetHandContentWidth()
        {
            return HandCols * GetTileWidth() + (HandCols - 1) * GetHandGapX() + 18;
        }

        private int GetHandGridStartX()
        {
            var gridWidth = (HandCols * GetTileWidth()) + ((HandCols - 1) * GetHandGapX());
            return Math.Max(8, (_elPaneli.ClientSize.Width - gridWidth) / 2);
        }

        private void ApplyTileViewSize(TileView tileView, bool board = false)
        {
            if (tileView == null)
            {
                return;
            }

            tileView.Width = board ? GetBoardTileWidth() : GetTileWidth();
            tileView.Height = board ? GetBoardTileHeight() : GetTileHeight();
        }

        private static Color DarkenColor(Color color, int amount)
        {
            return Color.FromArgb(
                Math.Max(0, color.R - amount),
                Math.Max(0, color.G - amount),
                Math.Max(0, color.B - amount));
        }

        private static Color LightenColor(Color color, int amount)
        {
            return Color.FromArgb(
                Math.Min(255, color.R + amount),
                Math.Min(255, color.G + amount),
                Math.Min(255, color.B + amount));
        }

        private static void SetDoubleBuffered(Control control)
        {
            if (control == null)
            {
                return;
            }

            var property = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            property?.SetValue(control, true, null);
        }

        private void DrawHandSlots(object sender, PaintEventArgs e)
        {
            // Real okey shelves have wooden rails; the free board-slot guides belong to Nane Okey.
            if (UsesNewTableAppearance)
            {
                var width = ((Control)sender).ClientSize.Width;
                using (var rail = new SolidBrush(Color.FromArgb(128, 81, 38)))
                using (var highlight = new Pen(Color.FromArgb(230, 193, 129), 2))
                    for (var row = 0; row < HandRows; row++)
                    {
                        var y = 6 + (row + 1) * GetTileHeight() + row * GetHandGapY();
                        e.Graphics.FillRectangle(rail, 4, y - 2, Math.Max(1, width - 8), 5);
                        e.Graphics.DrawLine(highlight, 4, y - 2, width - 4, y - 2);
                    }
                return;
            }
            var tileWidth = GetTileWidth();
            var tileHeight = GetTileHeight();
            var handGapX = GetHandGapX();
            var handGapY = GetHandGapY();
            var startX = GetHandGridStartX();
            using (var pen = new Pen(Color.FromArgb(166, 138, 100), 1))
            {
                for (var row = 0; row < HandRows; row++)
                {
                    for (var col = 0; col < HandCols; col++)
                    {
                        e.Graphics.DrawRectangle(
                            pen,
                            startX + col * (tileWidth + handGapX),
                            6 + row * (tileHeight + handGapY),
                            tileWidth,
                            tileHeight);
                    }
                }
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F1)
            {
                ShowHowToPlay();
                return true;
            }

            if (keyData == Keys.F2)
            {
                ShowHomeScreen();
                return true;
            }

            if (keyData == Keys.F3)
            {
                ShowNewGameDialog();
                return true;
            }

            if (keyData == Keys.F4)
            {
                PlayOrCommitTurn();
                return true;
            }

            if (keyData == Keys.F5)
            {
                DrawTile();
                return true;
            }

            if (keyData == Keys.F6)
            {
                UndoTurn();
                return true;
            }

            if (keyData == Keys.F7)
            {
                AutoArrangeHand();
                return true;
            }

            if (keyData == Keys.F8)
            {
                ChooseBoardColor();
                return true;
            }

            if (keyData == Keys.F9)
            {
                StartLanHost();
                return true;
            }

            if (keyData == Keys.F10)
            {
                JoinLanHost();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

    }
}
