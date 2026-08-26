using System.Drawing;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NaneOkey.Domain;

namespace NaneOkey.UI
{
    public sealed class TileView : Control
    {
        private const int BaseWidth = 36;
        private const int BaseHeight = 48;

        public TileView(Tile tile)
        {
            Tile = tile;
            Width = BaseWidth;
            Height = BaseHeight;
            DoubleBuffered = true;
        }

        public Tile Tile { get; private set; }

        public int RotationQuarterTurns { get; set; }

        public bool FaceDown { get; set; }

        public void SetTile(Tile tile)
        {
            Tile = tile;
            Invalidate();
        }

        public Cursor CreateDragCursor()
        {
            var bitmap = new Bitmap(Width, Height);
            DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
            var iconHandle = bitmap.GetHicon();
            var cursor = new Cursor(iconHandle);
            DestroyIcon(iconHandle);
            bitmap.Dispose();
            return cursor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var rect = ClientRectangle;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var scaleX = rect.Width / (float)BaseWidth;
            var scaleY = rect.Height / (float)BaseHeight;
            e.Graphics.TranslateTransform(rect.Width / 2F, rect.Height / 2F);
            e.Graphics.RotateTransform(RotationQuarterTurns * 90F);
            e.Graphics.TranslateTransform(-rect.Width / 2F, -rect.Height / 2F);
            e.Graphics.ScaleTransform(scaleX, scaleY);

            using (var shadow = new SolidBrush(Color.FromArgb(60, 40, 20, 20)))
            {
                e.Graphics.FillRectangle(shadow, 2, 3, BaseWidth - 4, BaseHeight - 4);
            }

            using (var face = new SolidBrush(Color.FromArgb(255, 252, 244)))
            {
                e.Graphics.FillRectangle(face, 0, 0, BaseWidth - 2, BaseHeight - 3);
            }

            using (var border = new Pen(Color.FromArgb(153, 129, 95), 1))
            {
                e.Graphics.DrawRectangle(border, 0, 0, BaseWidth - 2, BaseHeight - 3);
            }

            if (FaceDown)
            {
                using (var stripeBrush = new SolidBrush(Color.FromArgb(235, 235, 235)))
                {
                    for (var y = 6; y < BaseHeight - 8; y += 6)
                    {
                        e.Graphics.FillRectangle(stripeBrush, 5, y, BaseWidth - 12, 2);
                    }
                }

                e.Graphics.ResetTransform();
                return;
            }

            var color = ResolveColor(Tile.Color);
            using (var brush = new SolidBrush(color))
            using (var font = new Font("Tahoma", 11F, FontStyle.Bold))
            {
                e.Graphics.DrawString(Tile.Number.ToString(), font, brush, 5, 5);
            }

            using (var brush = new SolidBrush(color))
            {
                e.Graphics.FillEllipse(brush, 12, 30, 8, 8);
            }

            e.Graphics.ResetTransform();
        }

        private static Color ResolveColor(TileColor color)
        {
            switch (color)
            {
                case TileColor.Red:
                    return Color.Firebrick;
                case TileColor.Blue:
                    return Color.RoyalBlue;
                case TileColor.Yellow:
                    return Color.OliveDrab;
                default:
                    return Color.Black;
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
