using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using NaneOkey.Domain;

namespace NaneOkey.UI
{
    public enum TraditionalTargetKind
    {
        Stock,
        PreviousDiscard,
        OwnDiscard,
        Finish,
        OpenRuns,
        OpenPairs,
        Meld
    }

    public sealed class TraditionalTableTarget
    {
        public TraditionalTableTarget() { MeldIndex = -1; }

        public TraditionalTableTarget(TraditionalTargetKind kind, int meldIndex = -1)
        {
            Kind = kind;
            MeldIndex = meldIndex;
        }

        public TraditionalTargetKind Kind { get; set; }
        public int MeldIndex { get; set; }
    }

    // The optional Nane skin shares the felt and opponent racks while its editable
    // board remains a separate control. Traditional games own their table targets.
    public sealed class TraditionalTableView : Control
    {
        private sealed class MeldCard
        {
            public int Index;
            public Rectangle Bounds;
            public int TileWidth;
            public int TilesPerRow;
        }

        private sealed class RevealedTile
        {
            public Tile Tile;
            public Rectangle Bounds;
        }

        private GameState _state;
        private Bitmap _feltCache;
        private readonly Font _rackFont = new Font("Tahoma", 11F, FontStyle.Bold);
        private Seat _localSeat;
        private IList<Meld> _displayMelds;
        private bool _pending;
        private int _openingValue;
        private int _pairCount;
        private string _statusText = string.Empty;
        private bool _canAct;
        private Rectangle _naneStockBounds;
        private bool _naneCanDraw;
        private IDictionary<Seat, int> _seatScores;
        private bool _layoutDirty = true;
        private readonly Dictionary<TraditionalTargetKind, Rectangle> _targets = new Dictionary<TraditionalTargetKind, Rectangle>();
        private readonly Dictionary<Seat, Rectangle> _discardBounds = new Dictionary<Seat, Rectangle>();
        private readonly List<MeldCard> _cards = new List<MeldCard>();
        private readonly VScrollBar _scrollbar = new VScrollBar();
        private readonly ToolTip _tooltip = new ToolTip();
        private Rectangle _meldViewport;
        private Rectangle _runsLane;
        private Rectangle _pairsLane;
        private int _contentHeight;
        private int _topHeight;
        private int _bottomTop;
        private int _sideWidth;
        private TraditionalTableTarget _hoveredTarget;
        private string _tooltipText = string.Empty;
        private readonly Font _smallFont;
        private readonly Font _titleFont;
        private readonly Font _tileFont;
        private readonly Font _labelFont;

        public TraditionalTableView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(24, 91, 61);
            _labelFont = new Font("Tahoma", 9F, FontStyle.Regular);
            Font = _labelFont;
            _smallFont = new Font("Tahoma", 8.25F, FontStyle.Regular);
            _titleFont = new Font("Tahoma", 9.5F, FontStyle.Bold);
            _tileFont = new Font("Tahoma", 12F, FontStyle.Bold);
            TabStop = false;
            _scrollbar.Visible = false;
            _scrollbar.TabStop = false;
            _scrollbar.ValueChanged += delegate { Invalidate(); };
            Controls.Add(_scrollbar);
            _tooltip.InitialDelay = 500;
            _tooltip.ReshowDelay = 100;
            _tooltip.AutoPopDelay = 7000;
        }

        public GameState State { get { return _state; } set { _state = value; UpdateTable(); } }
        public Seat LocalSeat { get { return _localSeat; } set { _localSeat = value; UpdateTable(); } }
        public IList<Meld> DisplayMelds { get { return _displayMelds; } set { _displayMelds = value; UpdateTable(); } }
        public bool Pending { get { return _pending; } set { _pending = value; Invalidate(); } }
        public int OpeningValue { get { return _openingValue; } set { _openingValue = value; Invalidate(); } }
        public int PairCount { get { return _pairCount; } set { _pairCount = value; Invalidate(); } }
        public string StatusText { get { return _statusText; } set { _statusText = value ?? string.Empty; Invalidate(); } }
        public bool CanAct { get { return _canAct; } set { _canAct = value; Invalidate(); } }
        public Rectangle NaneStockBounds { get { return _naneStockBounds; } set { _naneStockBounds = value; UpdateTable(); } }
        public bool NaneCanDraw { get { return _naneCanDraw; } set { _naneCanDraw = value; Invalidate(); } }
        public IDictionary<Seat, int> SeatScores { get { return _seatScores; } set { _seatScores = value; Invalidate(); } }
        public event Action<TraditionalTableTarget> TargetClicked;

        private bool Is101 { get { return _state != null && _state.Mode == GameMode.Okey101; } }
        private bool IsNane { get { return _state != null && _state.Mode == GameMode.NaneOkey; } }
        private IList<Meld> VisibleMelds { get { return _displayMelds ?? (_state == null ? (IList<Meld>)new List<Meld>() : _state.Table); } }

        private void UpdateTable()
        {
            _layoutDirty = true;
            Invalidate();
        }

        public TraditionalTableTarget HitTest(Point clientPoint)
        {
            EnsureTableLayout();
            if (_state == null) return null;
            foreach (var target in _targets)
                if (target.Value.Contains(clientPoint)) return new TraditionalTableTarget(target.Key);
            if (Is101 && _meldViewport.Contains(clientPoint))
            {
                var contentPoint = new Point(clientPoint.X, clientPoint.Y + _scrollbar.Value);
                foreach (var card in _cards)
                    if (card.Bounds.Contains(contentPoint)) return new TraditionalTableTarget(TraditionalTargetKind.Meld, card.Index);
            }
            return null;
        }

        public Rectangle GetTargetBounds(TraditionalTableTarget target)
        {
            EnsureTableLayout();
            if (target == null) return Rectangle.Empty;
            if (target.Kind == TraditionalTargetKind.Meld)
            {
                var card = _cards.FirstOrDefault(x => x.Index == target.MeldIndex);
                if (card == null) return Rectangle.Empty;
                var bounds = card.Bounds;
                bounds.Offset(0, -_scrollbar.Value);
                return Rectangle.Intersect(bounds, _meldViewport);
            }
            Rectangle result;
            return _targets.TryGetValue(target.Kind, out result) ? result : Rectangle.Empty;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _layoutDirty = true;
        }

        private Seat RelativeSeat(int relative)
        {
            return (Seat)(((int)_localSeat + relative) % 4);
        }

        private Seat PreviousActiveSeat()
        {
            for (var offset = 1; offset <= 4; offset++)
            {
                var seat = RelativeSeat(offset);
                if (_state != null && _state.Players.Any(x => x.Seat == seat && x.IsActive)) return seat;
            }
            return RelativeSeat(1);
        }

        private void EnsureTableLayout()
        {
            if (!_layoutDirty) return;
            _layoutDirty = false;
            _targets.Clear();
            _discardBounds.Clear();
            _cards.Clear();
            var w = ClientSize.Width;
            var h = ClientSize.Height;
            if (w < 80 || h < 80) { _scrollbar.Visible = false; return; }
            if (IsNane)
            {
                _sideWidth = 84;
                _topHeight = Math.Min(84, h / 2);
                _bottomTop = h - 10;
                _meldViewport = Rectangle.Empty;
                _scrollbar.Visible = false;
                var stock = Rectangle.Intersect(_naneStockBounds, ClientRectangle);
                if (stock.Width > 0 && stock.Height > 0) _targets[TraditionalTargetKind.Stock] = stock;
                return;
            }
            _sideWidth = Math.Max(46, Math.Min(82, w / 9));
            _topHeight = Math.Max(29, Math.Min(62, h / 7));
            var bottomHeight = Math.Max(Is101 ? 70 : 60, Math.Min(87, h / 4));
            _bottomTop = h - bottomHeight;
            var roomy = w >= 480 && h >= 300;
            var pileWidth = Math.Max(64, Math.Min(roomy ? 116 : 96, (w - 24) / 5));
            var pileHeight = Math.Min(roomy ? 72 : 59, bottomHeight - 17);
            var upperPileHeight = roomy ? 72 : 51;
            var upperPileTop = _topHeight + 2;
            var lowerPileTop = _bottomTop + 3;
            _discardBounds[RelativeSeat(0)] = new Rectangle(w - pileWidth - 12, lowerPileTop, pileWidth, pileHeight);
            _discardBounds[RelativeSeat(1)] = new Rectangle(12, lowerPileTop, pileWidth, pileHeight);
            _discardBounds[RelativeSeat(2)] = new Rectangle(12, upperPileTop, pileWidth, upperPileHeight);
            _discardBounds[RelativeSeat(3)] = new Rectangle(w - pileWidth - 12, upperPileTop, pileWidth, upperPileHeight);
            _targets[TraditionalTargetKind.OwnDiscard] = _discardBounds[RelativeSeat(0)];
            _targets[TraditionalTargetKind.PreviousDiscard] = _discardBounds[PreviousActiveSeat()];
            var centralLeft = pileWidth + 20;
            var centralWidth = Math.Max(48, w - 2 * centralLeft);
            if (!Is101)
            {
                var stockWidth = Math.Min(300, Math.Max(100, w - 2 * (_sideWidth + 14)));
                var stockHeight = Math.Min(142, Math.Max(24, _bottomTop - _topHeight - 16));
                var stockTop = _topHeight + Math.Max(5, (_bottomTop - _topHeight - stockHeight) / 2);
                if (stockTop < upperPileTop + upperPileHeight) stockWidth = Math.Min(stockWidth, centralWidth);
                _targets[TraditionalTargetKind.Stock] = new Rectangle((w - stockWidth) / 2, stockTop, stockWidth, stockHeight);
                var finishWidth = Math.Min(132, centralWidth);
                _targets[TraditionalTargetKind.Finish] = new Rectangle((w - finishWidth) / 2, _bottomTop + 5, finishWidth, Math.Min(43, bottomHeight - 25));
                _scrollbar.Visible = false;
                _meldViewport = Rectangle.Empty;
                return;
            }

            var stock101Height = roomy ? 81 : h < 230 ? 47 : 59;
            var stock101Width = Math.Min(roomy ? 236 : 188, Math.Max(96, w - 24));
            _targets[TraditionalTargetKind.Stock] = new Rectangle((w - stock101Width) / 2, _topHeight + 1, stock101Width, stock101Height);
            var buttonGap = 6;
            var buttonWidth = Math.Max(24, (centralWidth - buttonGap) / 2);
            _targets[TraditionalTargetKind.OpenRuns] = new Rectangle(centralLeft, _bottomTop + 7, buttonWidth, 31);
            _targets[TraditionalTargetKind.OpenPairs] = new Rectangle(centralLeft + buttonWidth + buttonGap, _bottomTop + 7, buttonWidth, 31);
            var viewportTop = _topHeight + stock101Height + 7;
            _meldViewport = new Rectangle(_sideWidth, viewportTop, Math.Max(50, w - 2 * _sideWidth), Math.Max(0, _bottomTop - viewportTop - 5));
            if (_meldViewport.Height == 0) { _scrollbar.Visible = false; return; }
            BuildMeldCards();
            var needsScrollbar = _contentHeight > _meldViewport.Height;
            if (needsScrollbar)
            {
                _meldViewport.Width = Math.Max(32, _meldViewport.Width - SystemInformation.VerticalScrollBarWidth - 3);
                BuildMeldCards();
            }
            _scrollbar.Visible = needsScrollbar;
            if (needsScrollbar)
            {
                _scrollbar.Bounds = new Rectangle(_meldViewport.Right + 3, _meldViewport.Top, SystemInformation.VerticalScrollBarWidth, _meldViewport.Height);
                _scrollbar.LargeChange = Math.Max(1, _meldViewport.Height);
                _scrollbar.SmallChange = 30;
                _scrollbar.Maximum = Math.Max(0, _contentHeight - 1);
                _scrollbar.Value = Math.Min(_scrollbar.Value, Math.Max(0, _contentHeight - _meldViewport.Height));
            }
            else _scrollbar.Value = 0;
        }

        private void BuildMeldCards()
        {
            _cards.Clear();
            var pairWidth = Math.Max(72, _meldViewport.Width / 4);
            _runsLane = new Rectangle(_meldViewport.Left, _meldViewport.Top, Math.Max(35, _meldViewport.Width - pairWidth - 7), _meldViewport.Height);
            _pairsLane = new Rectangle(_runsLane.Right + 7, _meldViewport.Top, pairWidth, _meldViewport.Height);
            var runBottom = ArrangeLane(false, _runsLane);
            var pairBottom = ArrangeLane(true, _pairsLane);
            _contentHeight = Math.Max(_meldViewport.Height, Math.Max(runBottom, pairBottom) - _meldViewport.Top + 5);
        }

        private int ArrangeLane(bool pairs, Rectangle lane)
        {
            var melds = VisibleMelds;
            var x = lane.Left + 5;
            var y = lane.Top + 27;
            var rowHeight = 0;
            var tileWidth = Math.Max(22, Math.Min(44, _meldViewport.Width / 13));
            var availableWidth = Math.Max(25, lane.Width - 10);
            for (var index = 0; index < melds.Count; index++)
            {
                var meld = melds[index];
                if (meld == null || meld.IsPair != pairs || meld.Tiles.Count == 0) continue;
                var perRow = Math.Max(1, (availableWidth - 8) / (tileWidth + 2));
                var columns = Math.Min(meld.Tiles.Count, perRow);
                var cardWidth = Math.Min(availableWidth, columns * (tileWidth + 2) + 8);
                var tileHeight = tileWidth * 4 / 3;
                var rows = (meld.Tiles.Count + perRow - 1) / perRow;
                var cardHeight = 19 + rows * (tileHeight + 3) + 5;
                if (x + cardWidth > lane.Right - 5 && x > lane.Left + 5)
                {
                    x = lane.Left + 5;
                    y += rowHeight + 5;
                    rowHeight = 0;
                }
                _cards.Add(new MeldCard { Index = index, Bounds = new Rectangle(x, y, cardWidth, cardHeight), TileWidth = tileWidth, TilesPerRow = perRow });
                x += cardWidth + 5;
                rowHeight = Math.Max(rowHeight, cardHeight);
            }
            return y + rowHeight;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            EnsureTableLayout();
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawFelt(g);
            if (_state == null)
            {
                DrawText(g, "Okey masası", _titleFont, Color.Wheat, ClientRectangle, ContentAlignment.MiddleCenter);
                return;
            }
            if (ClientSize.Width < 80 || ClientSize.Height < 80) return;
            DrawOpponentRacks(g);
            if (IsNane)
            {
                Rectangle naneStock;
                if (_targets.TryGetValue(TraditionalTargetKind.Stock, out naneStock))
                {
                    DrawNaneStock(g, naneStock);
                    var footerTop = naneStock.Bottom + 2;
                    if (footerTop + 16 <= ClientSize.Height)
                        DrawText(g, PlayerNameWithScore(_localSeat) + " · Nane Okey", _smallFont, Color.FromArgb(246, 237, 215), new Rectangle(60, footerTop, Math.Max(0, ClientSize.Width - 120), 16), ContentAlignment.MiddleCenter);
                }
                return;
            }
            if (Is101) DrawMeldLanes(g);
            Rectangle stockBounds;
            if (!Is101 && _state.IsGameOver && _state.Table.Count > 0) DrawClassicReveal(g);
            else if (_targets.TryGetValue(TraditionalTargetKind.Stock, out stockBounds)) DrawStock(g, stockBounds);
            foreach (var pile in _discardBounds) DrawDiscard(g, pile.Key, pile.Value);
            if (Is101)
            {
                DrawAction(g, TraditionalTargetKind.OpenRuns, "Per Aç", false);
                DrawAction(g, TraditionalTargetKind.OpenPairs, "Çift Aç", false);
                var summary = _pending ? "Hazırlanan: " : "Açılış: ";
                summary += _openingValue + " / 101  ·  " + _pairCount + " / 5 çift";
                var left = _targets[TraditionalTargetKind.OpenRuns].Left;
                var right = _targets[TraditionalTargetKind.OpenPairs].Right;
                DrawText(g, summary, _smallFont, _pending ? Color.FromArgb(255, 224, 135) : Color.FromArgb(218, 231, 204), new Rectangle(left, _bottomTop + 38, right - left, 17), ContentAlignment.MiddleCenter);
            }
            else DrawAction(g, TraditionalTargetKind.Finish, "Bitir", true);
            var statusLeft = _discardBounds[RelativeSeat(1)].Right + 7;
            var statusRight = _discardBounds[RelativeSeat(0)].Left - 7;
            var localStatus = PlayerNameWithScore(_localSeat) + (string.IsNullOrEmpty(_statusText) ? string.Empty : " · " + _statusText);
            DrawText(g, localStatus, _smallFont, Color.FromArgb(246, 237, 215), new Rectangle(statusLeft, ClientSize.Height - 19, Math.Max(10, statusRight - statusLeft), 16), ContentAlignment.MiddleCenter);
        }

        private void DrawFelt(Graphics g)
        {
            var area = ClientRectangle;
            if (area.Width <= 0 || area.Height <= 0) return;
            if (_feltCache == null || _feltCache.Size != area.Size)
            {
                if (_feltCache != null) _feltCache.Dispose();
                _feltCache = new Bitmap(area.Width, area.Height);
                using (var surface = Graphics.FromImage(_feltCache)) DrawFeltSurface(surface, area);
            }
            g.DrawImageUnscaled(_feltCache, Point.Empty);
        }

        private static void DrawFeltSurface(Graphics g, Rectangle area)
        {
            using (var felt = new LinearGradientBrush(area, Color.FromArgb(32, 107, 72), Color.FromArgb(18, 78, 51), 90F)) g.FillRectangle(felt, area);
            using (var weave = new Pen(Color.FromArgb(11, 213, 231, 171)))
            {
                for (var y = 10; y < area.Height; y += 8) g.DrawLine(weave, 8, y, area.Width - 8, y);
            }
            using (var rim = new Pen(Color.FromArgb(91, 57, 30), 5F)) g.DrawRectangle(rim, 2, 2, Math.Max(1, area.Width - 5), Math.Max(1, area.Height - 5));
            using (var innerRim = new Pen(Color.FromArgb(168, 122, 64))) g.DrawRectangle(innerRim, 5, 5, Math.Max(1, area.Width - 11), Math.Max(1, area.Height - 11));
        }

        private PlayerState PlayerAt(Seat seat)
        {
            return _state == null ? null : _state.Players.FirstOrDefault(x => x.Seat == seat && x.IsActive);
        }

        private static string SeatName(Seat seat)
        {
            switch (seat)
            {
                case Seat.West: return "Garp";
                case Seat.North: return "Şimal";
                case Seat.East: return "Şark";
                default: return "Cenup";
            }
        }

        private string PlayerName(Seat seat)
        {
            var player = PlayerAt(seat);
            return player == null ? SeatName(seat) : player.Name;
        }

        private string PlayerNameWithScore(Seat seat)
        {
            int score;
            return PlayerName(seat) + (_seatScores != null && _seatScores.TryGetValue(seat, out score) ? " [" + score + "]" : string.Empty);
        }

        public Rectangle GetOpponentRackBounds(Seat seat)
        {
            EnsureTableLayout();
            var relative = ((int)seat - (int)_localSeat + 4) % 4;
            var w = ClientSize.Width;
            var h = ClientSize.Height;
            if (relative == 0 || w < 80 || h < 80) return Rectangle.Empty;
            if (relative == 2)
            {
                var width = Math.Min(w - 24, Math.Max(72, Math.Min(360, w / 3)));
                var top = _topHeight < 40 ? 21 : 25;
                return new Rectangle((w - width) / 2, top, width, Math.Max(7, Math.Min(IsNane ? 50 : 37, _topHeight - top - 1)));
            }
            var upper = IsNane ? 16 : _topHeight + (w >= 480 && h >= 300 ? 84 : 64);
            var lower = IsNane ? h - 16 : _bottomTop - 5;
            var height = Math.Max(0, Math.Min(IsNane ? 280 : 240, lower - upper));
            var thickness = Math.Min(w >= 600 ? 46 : 29, _sideWidth - 17);
            return new Rectangle(relative == 1 ? 11 : w - thickness - 11,
                upper + (lower - upper - height) / 2, thickness, height);
        }

        private void DrawOpponentRacks(Graphics g)
        {
            var w = ClientSize.Width;
            var opposite = PlayerAt(RelativeSeat(2));
            if (opposite != null)
            {
                var rect = GetOpponentRackBounds(RelativeSeat(2));
                DrawPlayerName(g, opposite, new Rectangle(rect.Left - 20, 5, rect.Width + 40, 20), false);
                DrawClosedRack(g, rect, opposite.Hand.Count, false);
            }
            for (var relative = 1; relative <= 3; relative += 2)
            {
                var player = PlayerAt(RelativeSeat(relative));
                if (player == null) continue;
                var rect = GetOpponentRackBounds(RelativeSeat(relative));
                if (rect.Height < 40) continue;
                DrawClosedRack(g, rect, player.Hand.Count, true);
                var nameBounds = new Rectangle(relative == 1 ? rect.Right + 2 : rect.Left - 22, rect.Top - 2, 20, rect.Height + 4);
                DrawPlayerName(g, player, nameBounds, true);
            }
        }

        private void DrawPlayerName(Graphics g, PlayerState player, Rectangle bounds, bool vertical)
        {
            var active = _state.CurrentTurn == player.Seat && !_state.IsGameOver;
            var text = PlayerNameWithScore(player.Seat) + " · " + player.Hand.Count;
            if (Is101 && player.HasOpened) text += player.OpenedWithPairs ? " · çift" : " · açık";
            if (IsNane) text += player.HasOpened ? " · açık" : " · kapalı";
            if (active) text = "› " + text;
            var nameFont = ClientSize.Width >= 600 && ClientSize.Height >= 300 ? _rackFont : active ? _titleFont : Font;
            if (!vertical) DrawText(g, text, nameFont, active ? Color.FromArgb(255, 224, 135) : Color.FromArgb(235, 226, 200), bounds, ContentAlignment.MiddleCenter);
            else
            {
                var saved = g.Save();
                g.TranslateTransform(bounds.Left, bounds.Bottom);
                g.RotateTransform(-90F);
                DrawText(g, text, nameFont, active ? Color.FromArgb(255, 224, 135) : Color.FromArgb(235, 226, 200), new Rectangle(0, 0, bounds.Height, bounds.Width), ContentAlignment.MiddleCenter);
                g.Restore(saved);
            }
        }

        private void DrawClosedRack(Graphics g, Rectangle bounds, int count, bool vertical)
        {
            if (bounds.Width < 2 || bounds.Height < 2) return;
            var saved = g.Save();
            if (vertical)
            {
                g.TranslateTransform(bounds.Left, bounds.Bottom);
                g.RotateTransform(-90F);
                bounds = new Rectangle(0, 0, bounds.Height, bounds.Width);
            }
            // Viewed from behind, the wooden back hides the faces on both shelves.
            // Only the narrow top edges of a few tiles rise above it.
            using (var shadow = new SolidBrush(Color.FromArgb(82, 10, 24, 13)))
                g.FillRectangle(shadow, bounds.Left + 2, bounds.Top + 3, bounds.Width, bounds.Height);
            var back = new Rectangle(bounds.Left, bounds.Top + 3, bounds.Width, Math.Max(2, bounds.Height - 5));
            using (var wood = new LinearGradientBrush(back, Color.FromArgb(168, 113, 58), Color.FromArgb(106, 65, 33), 90F)) g.FillRectangle(wood, back);
            using (var grain = new Pen(Color.FromArgb(35, 69, 37, 13)))
            {
                for (var row = back.Top + 5; row < back.Bottom - 2; row += 5)
                    g.DrawLine(grain, back.Left + 5, row, back.Right - 5, row + 1);
            }
            var visibleEdges = Math.Min(12, Math.Min(Math.Max(0, count), Math.Max(0, (bounds.Width - 10) / 4)));
            if (visibleEdges > 0 && bounds.Width >= 30 && bounds.Height >= 12)
            {
                var edgeWidth = Math.Max(3, Math.Min(13, (bounds.Width - 20) / (visibleEdges + 1)));
                var step = edgeWidth + 1;
                var first = bounds.Left + (bounds.Width - visibleEdges * step) / 2;
                using (var tileEdge = new SolidBrush(Color.FromArgb(198, 183, 144)))
                using (var edgeShade = new Pen(Color.FromArgb(106, 94, 66)))
                    for (var index = 0; index < visibleEdges; index++)
                    {
                        var edge = new Rectangle(first + index * step, bounds.Top + 1, edgeWidth, 2);
                        g.FillRectangle(tileEdge, edge);
                        g.DrawLine(edgeShade, edge.Left, edge.Bottom, edge.Right, edge.Bottom);
                    }
            }
            using (var outline = new Pen(Color.FromArgb(70, 43, 24))) g.DrawRectangle(outline, back.Left, back.Top, Math.Max(1, back.Width - 1), Math.Max(1, back.Height - 1));
            using (var highlight = new Pen(Color.FromArgb(205, 150, 86))) g.DrawLine(highlight, back.Left + 2, back.Top + 1, back.Right - 3, back.Top + 1);
            var middle = back.Top + back.Height / 2;
            using (var inset = new Pen(Color.FromArgb(88, 51, 27)))
            {
                g.DrawLine(inset, back.Left + 4, middle, back.Right - 5, middle);
                g.DrawLine(inset, back.Left + 2, back.Bottom - 3, back.Right - 3, back.Bottom - 3);
            }
            using (var rail = new Pen(Color.FromArgb(181, 124, 65)))
            {
                g.DrawLine(rail, back.Left + 4, middle + 1, back.Right - 5, middle + 1);
                g.DrawLine(rail, back.Left + 2, back.Bottom - 2, back.Right - 3, back.Bottom - 2);
            }
            g.Restore(saved);
        }

        private void DrawNaneStock(Graphics g, Rectangle bounds)
        {
            var active = TargetEnabled(TraditionalTargetKind.Stock);
            DrawTargetOutline(g, bounds, active, IsHovered(TraditionalTargetKind.Stock), false);
            var tileWidth = Math.Min(32, Math.Min((bounds.Width - 12) / 7, (bounds.Height - 8) * 3 / 4));
            var stacks = Math.Min(3, (_state.Deck.Count + 4) / 5);
            var stackAreaWidth = Math.Max(0, stacks * (tileWidth + 3) + 5);
            if (tileWidth >= 6 && _state.Deck.Count > 0)
            {
                var left = bounds.Left + 5;
                for (var stack = 0; stack < stacks; stack++)
                {
                    var layers = Math.Min(3, Math.Max(0, _state.Deck.Count - stack * 5));
                    for (var layer = layers - 1; layer >= 0; layer--)
                        DrawTile(g, null, new Rectangle(left + stack * (tileWidth + 3) + layer, bounds.Top + 3 + layer, tileWidth, tileWidth * 4 / 3), true);
                }
            }
            DrawText(g, "Ortadan Çek\nKalan: " + _state.Deck.Count, Font, active ? Color.FromArgb(255, 232, 156) : Color.FromArgb(231, 226, 191), new Rectangle(bounds.Left + stackAreaWidth + 6, bounds.Top + 3, Math.Max(0, bounds.Width - stackAreaWidth - 10), Math.Max(0, bounds.Height - 6)), ContentAlignment.MiddleCenter);
        }

        private void DrawStock(Graphics g, Rectangle bounds)
        {
            var active = TargetEnabled(TraditionalTargetKind.Stock);
            DrawTargetOutline(g, bounds, active, IsHovered(TraditionalTargetKind.Stock), false);
            if (!Is101 && bounds.Height < 90)
            {
                DrawCompactClassicStock(g, bounds, active);
                return;
            }
            var tileWidth = Math.Max(10, Math.Min(Is101 ? 32 : 36, (bounds.Width - 32) / 6 - 2));
            if (Is101 && bounds.Height < 55) tileWidth = Math.Min(tileWidth, 17);
            else if (Is101 && bounds.Height < 75) tileWidth = Math.Min(tileWidth, 21);
            var tileHeight = tileWidth * 4 / 3;
            var stacks = Math.Min(5, (_state.Deck.Count + 4) / 5);
            var stackLeft = bounds.Left + tileWidth + 15;
            var stackTop = bounds.Top + (Is101 ? 4 : bounds.Height < 90 ? 20 : 23);
            for (var stack = 0; stack < stacks; stack++)
            {
                var remaining = Math.Min(Is101 && bounds.Height < 75 ? 3 : 5, Math.Max(0, _state.Deck.Count - stack * 5));
                for (var layer = remaining - 1; layer >= 0; layer--)
                    DrawTile(g, null, new Rectangle(stackLeft + stack * (tileWidth + 3) + layer, stackTop + layer * 2, tileWidth, tileHeight), true);
            }
            if (_state.Indicator != null)
            {
                var indicatorBounds = new Rectangle(bounds.Left + 8, stackTop, tileWidth, tileHeight);
                DrawTile(g, _state.Indicator, indicatorBounds, false);
                if (!Is101 && bounds.Height >= 90) DrawText(g, "Gösterge", _smallFont, Color.FromArgb(231, 226, 191), new Rectangle(bounds.Left + 2, indicatorBounds.Bottom + 4, 58, 14), ContentAlignment.MiddleLeft);
            }
            var captionTop = Is101 ? bounds.Bottom - 16 : bounds.Bottom - 22;
            DrawText(g, "Ortadan Çek · " + _state.Deck.Count, Is101 ? _smallFont : Font, active ? Color.FromArgb(255, 232, 156) : Color.FromArgb(231, 226, 191), new Rectangle(bounds.Left + 47, captionTop, bounds.Width - 52, 17), ContentAlignment.MiddleCenter);
            if (!Is101)
                DrawText(g, "KLASİK OKEY", _titleFont, Color.FromArgb(191, 210, 171), new Rectangle(bounds.Left, bounds.Top + 3, bounds.Width, 17), ContentAlignment.MiddleCenter);
        }

        private void DrawCompactClassicStock(Graphics g, Rectangle bounds, bool active)
        {
            var stackLimit = bounds.Width >= 240 ? 3 : 1;
            var captionWidth = Math.Min(90, bounds.Width / 2);
            var tileWidth = Math.Max(6, Math.Min(36, Math.Min((bounds.Height - 8) * 3 / 4, (bounds.Width - captionWidth - 26) / (stackLimit + 1))));
            var tileHeight = tileWidth * 4 / 3;
            var top = bounds.Top + 3;
            if (_state.Indicator != null)
                DrawTile(g, _state.Indicator, new Rectangle(bounds.Left + 8, top, tileWidth, tileHeight), false);
            var stacks = Math.Min(stackLimit, (_state.Deck.Count + 4) / 5);
            var left = bounds.Left + tileWidth + 17;
            for (var stack = 0; stack < stacks; stack++)
            {
                var layers = Math.Min(3, Math.Max(0, _state.Deck.Count - stack * 5));
                for (var layer = layers - 1; layer >= 0; layer--)
                    DrawTile(g, null, new Rectangle(left + stack * (tileWidth + 3) + layer, top + layer * 2, tileWidth, tileHeight), true);
            }
            var captionLeft = left + stacks * (tileWidth + 3) + 6;
            DrawText(g, bounds.Height < 36 ? "Çek · " + _state.Deck.Count : "Ortadan Çek\nKalan: " + _state.Deck.Count, Font,
                active ? Color.FromArgb(255, 232, 156) : Color.FromArgb(231, 226, 191),
                new Rectangle(captionLeft, bounds.Top + 2, Math.Max(0, bounds.Right - captionLeft - 4), Math.Max(0, bounds.Height - 4)), ContentAlignment.MiddleCenter);
        }

        private void DrawClassicReveal(Graphics g)
        {
            var top = _topHeight + 55;
            if (_bottomTop - top < 70) top = _topHeight + 22;
            var area = new Rectangle(_sideWidth + 7, top, Math.Max(40, ClientSize.Width - 2 * (_sideWidth + 7)), Math.Max(20, _bottomTop - top - 7));
            using (var fill = new SolidBrush(Color.FromArgb(30, 77, 47))) g.FillRectangle(fill, area);
            using (var border = new Pen(Color.FromArgb(171, 174, 107))) g.DrawRectangle(border, area.Left, area.Top, area.Width - 1, area.Height - 1);
            var title = (string.IsNullOrEmpty(_state.WinnerName) ? "Bitiren oyuncu" : _state.WinnerName) + " · Biten el";
            DrawText(g, title, _titleFont, Color.FromArgb(255, 230, 157), new Rectangle(area.Left + 4, area.Top + 3, area.Width - 8, 18), ContentAlignment.MiddleCenter);
            List<RevealedTile> tiles = null;
            for (var width = 36; width >= 12; width--)
            {
                var candidate = ArrangeClassicReveal(area, width);
                tiles = candidate;
                if (candidate.Count > 0 && candidate.Max(x => x.Bounds.Bottom) <= area.Bottom - 3) break;
            }
            if (tiles == null) return;
            var saved = g.Save();
            g.SetClip(area, CombineMode.Intersect);
            foreach (var tile in tiles) DrawTile(g, tile.Tile, tile.Bounds, false);
            g.Restore(saved);
        }

        private List<RevealedTile> ArrangeClassicReveal(Rectangle area, int tileWidth)
        {
            var tiles = new List<RevealedTile>();
            var x = area.Left + 6;
            var y = area.Top + 25;
            var tileHeight = tileWidth * 4 / 3;
            foreach (var meld in _state.Table)
            {
                if (meld == null || meld.Tiles.Count == 0) continue;
                var groupWidth = meld.Tiles.Count * (tileWidth + 2);
                if (groupWidth <= area.Width - 12 && x > area.Left + 6 && x + groupWidth > area.Right - 6)
                {
                    x = area.Left + 6;
                    y += tileHeight + 6;
                }
                foreach (var tile in meld.Tiles)
                {
                    if (x + tileWidth > area.Right - 6)
                    {
                        x = area.Left + 6;
                        y += tileHeight + 6;
                    }
                    tiles.Add(new RevealedTile { Tile = tile, Bounds = new Rectangle(x, y, tileWidth, tileHeight) });
                    x += tileWidth + 2;
                }
                x += 7;
            }
            return tiles;
        }

        private void DrawDiscard(Graphics g, Seat seat, Rectangle bounds)
        {
            var own = seat == _localSeat;
            var previous = seat == PreviousActiveSeat();
            var kind = own ? TraditionalTargetKind.OwnDiscard : TraditionalTargetKind.PreviousDiscard;
            var actionable = (own || previous) && TargetEnabled(kind);
            if (own || previous) DrawTargetOutline(g, bounds, actionable, IsHovered(kind), false);
            var pile = _state.DiscardPiles.FirstOrDefault(x => x.Seat == seat);
            var count = pile == null ? 0 : pile.Tiles.Count;
            var tileWidth = Math.Max(18, Math.Min(40, (bounds.Height - 23) * 3 / 4));
            var tileHeight = tileWidth * 4 / 3;
            var tileBounds = new Rectangle(bounds.Left + (bounds.Width - tileWidth) / 2, bounds.Top + 2, tileWidth, tileHeight);
            if (count > 0)
            {
                var layers = Math.Min(3, count - 1);
                for (var layer = layers; layer > 0; layer--)
                {
                    var under = tileBounds;
                    under.Offset(layer * 2, -layer);
                    DrawTile(g, null, under, true);
                }
                DrawTile(g, pile.Tiles[count - 1], tileBounds, false);
            }
            else
            {
                // A small pile marker is not a tile-sized slot or a table grid.
                using (var marker = new Pen(Color.FromArgb(90, 198, 213, 174)))
                    g.DrawEllipse(marker, bounds.Left + bounds.Width / 2 - 10, bounds.Top + 13, 20, 13);
            }
            var caption = own ? "Taş At" : previous ? "Önceki Taş" : PlayerName(seat);
            if (!own && !previous && count > 0) caption += " · " + count;
            DrawText(g, caption, own && actionable ? _titleFont : _smallFont, actionable ? Color.FromArgb(255, 232, 156) : Color.FromArgb(231, 226, 202), new Rectangle(bounds.Left + 2, bounds.Bottom - 19, bounds.Width - 4, 18), ContentAlignment.MiddleCenter);
        }

        private void DrawAction(Graphics g, TraditionalTargetKind kind, string label, bool finish)
        {
            Rectangle bounds;
            if (!_targets.TryGetValue(kind, out bounds)) return;
            var active = TargetEnabled(kind);
            DrawTargetOutline(g, bounds, active, IsHovered(kind), true);
            DrawText(g, label, _titleFont, active ? Color.FromArgb(255, 235, 173) : Color.FromArgb(207, 215, 196), finish ? new Rectangle(bounds.Left, bounds.Top + 3, bounds.Width, 17) : bounds, ContentAlignment.MiddleCenter);
            if (finish) DrawText(g, "Son taşı bırak", _smallFont, Color.FromArgb(218, 222, 195), new Rectangle(bounds.Left, bounds.Top + 21, bounds.Width, 17), ContentAlignment.MiddleCenter);
        }

        private void DrawTargetOutline(Graphics g, Rectangle bounds, bool enabled, bool hovered, bool filled)
        {
            if (bounds.Width < 1 || bounds.Height < 1) return;
            if (filled || hovered)
                using (var fill = new SolidBrush(hovered && enabled ? Color.FromArgb(84, 116, 59) : Color.FromArgb(49, 83, 52))) g.FillRectangle(fill, bounds);
            using (var pen = new Pen(hovered && enabled ? Color.FromArgb(255, 225, 128) : enabled ? Color.FromArgb(199, 181, 120) : Color.FromArgb(91, 136, 99), hovered ? 2F : 1F))
            {
                if (!filled) pen.DashStyle = DashStyle.Dot;
                g.DrawRectangle(pen, bounds.Left, bounds.Top, Math.Max(1, bounds.Width - 1), Math.Max(1, bounds.Height - 1));
            }
        }

        private void DrawMeldLanes(Graphics g)
        {
            if (_meldViewport.Width < 1 || _meldViewport.Height < 1) return;
            var saved = g.Save();
            g.SetClip(_meldViewport, CombineMode.Intersect);
            using (var shade = new SolidBrush(Color.FromArgb(23, 8, 28, 19)))
            {
                g.FillRectangle(shade, _runsLane);
                g.FillRectangle(shade, _pairsLane);
            }
            using (var line = new Pen(Color.FromArgb(96, 131, 101)))
                g.DrawLine(line, _pairsLane.Left - 4, _meldViewport.Top, _pairsLane.Left - 4, _meldViewport.Bottom);
            g.TranslateTransform(0, -_scrollbar.Value);
            DrawLaneHeading(g, _runsLane, "PERLER", false);
            DrawLaneHeading(g, _pairsLane, "ÇİFTLER", true);
            var melds = VisibleMelds;
            foreach (var card in _cards)
            {
                if (card.Index >= melds.Count) continue;
                var bounds = card.Bounds;
                if (bounds.Bottom < _meldViewport.Top + _scrollbar.Value || bounds.Top > _meldViewport.Bottom + _scrollbar.Value) continue;
                var meld = melds[card.Index];
                var pendingCard = _pending && meld.OwnerSeat == _localSeat && (_state.Table.Count <= card.Index || !_state.Table.Any(x => x.Tiles.Select(t => t.Id).SequenceEqual(meld.Tiles.Select(t => t.Id))));
                var hovered = _hoveredTarget != null && _hoveredTarget.Kind == TraditionalTargetKind.Meld && _hoveredTarget.MeldIndex == card.Index;
                using (var brush = new SolidBrush(pendingCard ? Color.FromArgb(70, 99, 47) : Color.FromArgb(28, 81, 48))) g.FillRectangle(brush, bounds);
                using (var pen = new Pen(hovered && _canAct ? Color.FromArgb(255, 225, 128) : pendingCard ? Color.FromArgb(217, 192, 99) : Color.FromArgb(95, 130, 92)))
                    g.DrawRectangle(pen, bounds.Left, bounds.Top, Math.Max(1, bounds.Width - 1), Math.Max(1, bounds.Height - 1));
                var owner = PlayerName(meld.OwnerSeat) + (pendingCard ? " · hazır" : string.Empty);
                DrawText(g, owner, _smallFont, pendingCard ? Color.FromArgb(255, 228, 149) : Color.FromArgb(218, 223, 187), new Rectangle(bounds.Left + 4, bounds.Top + 2, bounds.Width - 8, 14), ContentAlignment.MiddleLeft);
                for (var index = 0; index < meld.Tiles.Count; index++)
                {
                    var tileBounds = new Rectangle(bounds.Left + 4 + (index % card.TilesPerRow) * (card.TileWidth + 2), bounds.Top + 19 + (index / card.TilesPerRow) * (card.TileWidth * 4 / 3 + 3), card.TileWidth, card.TileWidth * 4 / 3);
                    DrawTile(g, meld.Tiles[index], tileBounds, false);
                }
            }
            g.Restore(saved);
        }

        private void DrawLaneHeading(Graphics g, Rectangle lane, string text, bool pairs)
        {
            DrawText(g, text, _titleFont, Color.FromArgb(226, 222, 168), new Rectangle(lane.Left + 5, lane.Top + 4, lane.Width - 10, 17), ContentAlignment.MiddleLeft);
            if (!_cards.Any(x => VisibleMelds[x.Index].IsPair == pairs))
                DrawText(g, pairs ? "Açılan\nçiftler" : "Açılan seriler ve gruplar", _smallFont, Color.FromArgb(162, 189, 155), new Rectangle(lane.Left + 7, lane.Top + 36, Math.Max(15, lane.Width - 14), Math.Max(20, lane.Height - 43)), ContentAlignment.TopCenter);
        }

        private void DrawTile(Graphics g, Tile tile, Rectangle bounds, bool faceDown)
        {
            if (bounds.Width < 2 || bounds.Height < 2) return;
            var saved = g.Save();
            g.TranslateTransform(bounds.Left, bounds.Top);
            g.ScaleTransform(bounds.Width / 36F, bounds.Height / 48F);
            using (var shadow = new SolidBrush(Color.FromArgb(80, 10, 22, 11))) g.FillRectangle(shadow, 2, 3, 34, 45);
            using (var face = new SolidBrush(faceDown ? Color.FromArgb(238, 233, 214) : Color.FromArgb(255, 252, 244))) g.FillRectangle(face, 0, 0, 33, 44);
            using (var edge = new Pen(Color.FromArgb(156, 133, 97))) g.DrawRectangle(edge, 0, 0, 33, 44);
            if (faceDown)
            {
                using (var stripe = new Pen(Color.FromArgb(199, 190, 160), 1.7F))
                    for (var y = 8; y < 39; y += 7) g.DrawLine(stripe, 6, y, 27, y);
            }
            else if (tile != null)
            {
                var color = TileColorBrush(tile.Color);
                using (var brush = new SolidBrush(color))
                {
                    g.DrawString(tile.IsFalseJoker ? "★" : tile.Number.ToString(), _tileFont, brush, 4, 3);
                    g.FillEllipse(brush, 13, 30, 7, 7);
                }
                if (tile.IsJoker)
                {
                    using (var marker = new SolidBrush(Color.Firebrick)) g.FillEllipse(marker, 25, 27, 7, 7);
                    using (var white = new SolidBrush(Color.FromArgb(255, 250, 231))) g.FillEllipse(white, 27, 29, 3, 3);
                    if (tile.JokerNumber > 0)
                        using (var brush = new SolidBrush(TileColorBrush(tile.JokerColor))) g.DrawString(tile.JokerNumber.ToString(), _smallFont, brush, 2, 32);
                }
            }
            g.Restore(saved);
        }

        private static Color TileColorBrush(TileColor color)
        {
            switch (color)
            {
                case TileColor.Red: return Color.Firebrick;
                case TileColor.Blue: return Color.RoyalBlue;
                case TileColor.Yellow: return Color.FromArgb(147, 113, 15);
                default: return Color.FromArgb(35, 34, 30);
            }
        }

        private static string TileColorName(TileColor color)
        {
            switch (color)
            {
                case TileColor.Red: return "kırmızı";
                case TileColor.Blue: return "mavi";
                case TileColor.Yellow: return "sarı";
                default: return "siyah";
            }
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle bounds, ContentAlignment alignment)
        {
            if (string.IsNullOrEmpty(text) || bounds.Width < 1 || bounds.Height < 1) return;
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat())
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.LineLimit;
                format.Alignment = alignment == ContentAlignment.MiddleLeft || alignment == ContentAlignment.TopLeft ? StringAlignment.Near : StringAlignment.Center;
                format.LineAlignment = alignment == ContentAlignment.TopCenter || alignment == ContentAlignment.TopLeft ? StringAlignment.Near : StringAlignment.Center;
                g.DrawString(text, font, brush, bounds, format);
            }
        }

        private bool TargetEnabled(TraditionalTargetKind kind)
        {
            if (!_canAct || _state == null || _state.IsGameOver) return false;
            if (IsNane) return kind == TraditionalTargetKind.Stock && _naneCanDraw && _state.Deck.Count > 0;
            if (kind == TraditionalTargetKind.Stock) return (!_state.HasDrawnThisTurn || (Is101 && _state.DrawnDiscardTileId > 0)) && _state.Deck.Count > 0;
            if (kind == TraditionalTargetKind.PreviousDiscard)
            {
                var pile = _state.DiscardPiles.FirstOrDefault(x => x.Seat == PreviousActiveSeat());
                return !_state.HasDrawnThisTurn && pile != null && pile.Tiles.Count > 0;
            }
            return _state.HasDrawnThisTurn;
        }

        private bool IsHovered(TraditionalTargetKind kind)
        {
            return _hoveredTarget != null && _hoveredTarget.Kind == kind;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var target = HitTest(e.Location);
            if (!SameTarget(target, _hoveredTarget)) { _hoveredTarget = target; Invalidate(); }
            Cursor = target != null && TargetEnabled(target.Kind) ? Cursors.Hand : Cursors.Default;
            var tooltip = TargetTooltip(target);
            if (!string.Equals(tooltip, _tooltipText, StringComparison.Ordinal))
            {
                _tooltipText = tooltip;
                _tooltip.SetToolTip(this, tooltip);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredTarget = null;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            var target = HitTest(e.Location);
            if (target != null && TargetEnabled(target.Kind) && TargetClicked != null) TargetClicked(target);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            EnsureTableLayout();
            if (!_scrollbar.Visible) return;
            var maximum = Math.Max(0, _scrollbar.Maximum - _scrollbar.LargeChange + 1);
            _scrollbar.Value = Math.Max(0, Math.Min(maximum, _scrollbar.Value - e.Delta / 120 * 45));
        }

        private static bool SameTarget(TraditionalTableTarget a, TraditionalTableTarget b)
        {
            return a == null ? b == null : b != null && a.Kind == b.Kind && a.MeldIndex == b.MeldIndex;
        }

        private string TargetTooltip(TraditionalTableTarget target)
        {
            if (_state == null) return string.Empty;
            if (target == null) return PlayerNameWithScore(_localSeat) + (string.IsNullOrEmpty(_statusText) ? string.Empty : " · " + _statusText);
            switch (target.Kind)
            {
                case TraditionalTargetKind.Stock:
                    if (IsNane) return "Hamle yapamıyorsan ortadan bir taş çek; sıran sonraki oyuncuya geçer.";
                    var indicator = _state.Indicator;
                    return indicator == null ? "Kapalı desteden bir taş çek." : "Kapalı desteden bir taş çek. Gösterge: " + indicator + ". Okey: " + (indicator.Number == 13 ? 1 : indicator.Number + 1) + indicator.ColorName + ".";
                case TraditionalTargetKind.PreviousDiscard: return "Solundan gelen, senden önceki oyuncunun son attığı taşı al.";
                case TraditionalTargetKind.OwnDiscard: return PlayerNameWithScore(_localSeat) + ". " + (Is101 ? "Atacağın taşı sağa bırak; açılışın ve işlemelerin onaylanır." : "Atacağın taşı sağa bırak.");
                case TraditionalTargetKind.Finish: return "Elin perlerden ya da 7 çiftten oluşuyorsa son taşını buraya bırak.";
                case TraditionalTargetKind.OpenRuns: return "Istakada seçtiğin seri ve grupları aç. İlk açılış en az 101 puan.";
                case TraditionalTargetKind.OpenPairs: return "Istakada seçtiğin çiftleri aç. İlk açılış en az 5 çift.";
                case TraditionalTargetKind.Meld:
                    var melds = VisibleMelds;
                    if (target.MeldIndex < 0 || target.MeldIndex >= melds.Count) return string.Empty;
                    var meld = melds[target.MeldIndex];
                    var representations = meld.Tiles.Where(x => x.IsJoker && x.JokerNumber > 0).Select(x => x.JokerNumber + " " + TileColorName(x.JokerColor)).ToArray();
                    return PlayerName(meld.OwnerSeat) + " · " + (meld.IsPair ? "Çift" : "Per") + (representations.Length == 0 ? string.Empty : ". Okeyin değeri: " + string.Join(", ", representations)) + ". Seçili taşı bu pere işle.";
                default: return string.Empty;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tooltip.Dispose();
                _rackFont.Dispose();
                if (_feltCache != null) _feltCache.Dispose();
                _smallFont.Dispose();
                _titleFont.Dispose();
                _tileFont.Dispose();
                _labelFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
