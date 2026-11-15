// ForeverPad — tray icons drawn at runtime (no image files to ship).
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ForeverPad
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(28, 30, 36);
        public static readonly Color Panel = Color.FromArgb(40, 43, 51);
        public static readonly Color Text = Color.FromArgb(236, 238, 242);
        public static readonly Color Dim = Color.FromArgb(150, 156, 168);
        public static readonly Color NormalAccent = Color.FromArgb(76, 141, 255);
        public static readonly Color PadAccent = Color.FromArgb(46, 196, 120);
        public static readonly Color Warn = Color.FromArgb(240, 170, 60);

        // Normal is blue and Controller green; every other profile gets a steady
        // colour of its own, so a glance at the tray says you're not on Normal.
        static readonly Color[] Extra = {
            Color.FromArgb(168, 110, 255), Color.FromArgb(240, 140, 60), Color.FromArgb(232, 90, 150),
            Color.FromArgb(40, 190, 200), Color.FromArgb(200, 180, 50), Color.FromArgb(120, 140, 255) };

        public static Color AccentFor(string profile)
        {
            if (profile == Profiles.Normal) return NormalAccent;
            if (profile == Profiles.Controller) return PadAccent;
            int h = 0;
            foreach (char c in (profile ?? "").ToLowerInvariant()) h = h * 31 + c;
            return Extra[(h & 0x7fffffff) % Extra.Length];
        }

        public static GraphicsPath Rounded(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    static class Icons
    {
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

        public static Icon For(string profile, bool pad)
        {
            var accent = Theme.AccentFor(profile);
            const int S = 32;
            using (var bmp = new Bitmap(S, S))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var bg = new SolidBrush(accent))
                    using (var path = Theme.Rounded(new RectangleF(1, 1, S - 2, S - 2), 7))
                        g.FillPath(bg, path);
                    if (pad) DrawPad(g, Color.White, accent);
                    else DrawKeys(g, Color.White, accent);
                }
                IntPtr h = bmp.GetHicon();
                var icon = (Icon)Icon.FromHandle(h).Clone();
                DestroyIcon(h);
                return icon;
            }
        }

        // A simple gamepad silhouette with a d-pad and two buttons cut out.
        static void DrawPad(Graphics g, Color c, Color accent)
        {
            using (var b = new SolidBrush(c))
            using (var cut = new SolidBrush(accent))
            {
                using (var body = Theme.Rounded(new RectangleF(4, 9, 24, 13), 6.5f)) g.FillPath(b, body);
                g.FillEllipse(b, 4, 15, 9, 11);
                g.FillEllipse(b, 19, 15, 9, 11);
                g.FillRectangle(cut, 8, 14, 2, 6);
                g.FillRectangle(cut, 6, 16, 6, 2);
                g.FillEllipse(cut, 20, 13, 3, 3);
                g.FillEllipse(cut, 23, 16, 3, 3);
            }
        }

        // A keyboard: outline with rows of keys.
        static void DrawKeys(Graphics g, Color c, Color accent)
        {
            using (var b = new SolidBrush(c))
            {
                using (var body = Theme.Rounded(new RectangleF(4, 9, 24, 15), 3)) g.FillPath(b, body);
                using (var k = new SolidBrush(accent))
                {
                    for (int i = 0; i < 5; i++) g.FillRectangle(k, 6.5f + i * 4, 11.5f, 2.5f, 2.5f);
                    for (int i = 0; i < 5; i++) g.FillRectangle(k, 6.5f + i * 4, 15.5f, 2.5f, 2.5f);
                    g.FillRectangle(k, 10.5f, 19.5f, 11, 2.5f);
                }
            }
        }
    }
}
