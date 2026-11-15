// ForeverPad — the profile picker that pops up above the tray icon.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ForeverPad
{
    // One clickable row per profile: colour chip, glyph, name, "active" tag.
    class ProfileRow : Control
    {
        public string Profile;
        public bool Pad, IsActive, Locked;
        bool hover;

        public ProfileRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI Semibold", 10f);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            var accent = Theme.AccentFor(Profile);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Color fill = IsActive ? (Locked ? Color.FromArgb(90, 94, 104) : accent)
                       : hover && !Locked ? Color.FromArgb(52, 56, 66) : Theme.Panel;
            using (var p = Theme.Rounded(r, 8))
            using (var b = new SolidBrush(fill))
                g.FillPath(b, p);

            using (var ico = Icons.For(Profile, Pad))
                g.DrawIcon(ico, new Rectangle(8, (Height - 24) / 2, 24, 24));

            var fmt = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            using (var t = new SolidBrush(IsActive ? Color.White : Theme.Text))
                g.DrawString(Profile, Font, t, new RectangleF(40, 0, Width - 120, Height), fmt);

            string tag = IsActive ? "active" : Pad ? "gamepad" : "";
            if (tag != "")
            {
                using (var f = new Font("Segoe UI", 8.5f))
                using (var t = new SolidBrush(IsActive ? Color.FromArgb(230, 255, 255, 255) : Theme.Dim))
                {
                    var right = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    g.DrawString(tag, f, t, new RectangleF(0, 0, Width - 12, Height), right);
                }
            }
        }
    }

    class Popup : Form
    {
        readonly TrayApp app;
        readonly Label status = new Label();
        readonly Label title = new Label();
        readonly List<Control> rows = new List<Control>();
        readonly List<Control> footer = new List<Control>();
        public DateTime HiddenAt = DateTime.MinValue;
        string activeShown = Profiles.Normal;
        string layoutKey = "";

        const int W = 340, Gutter = 16, RowH = 38, RowGap = 6;

        public Popup(TrayApp app)
        {
            this.app = app;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(W, 200);

            title.Text = "WoW Forever profile";
            title.Font = new Font("Segoe UI Semibold", 11f);
            title.ForeColor = Theme.Text;
            title.SetBounds(Gutter, 12, W - 2 * Gutter, 22);
            Controls.Add(title);

            status.ForeColor = Theme.Dim;
            Controls.Add(status);

            footer.Add(MakeLink("+ New profile…", () => { Hide(); app.ShowNewProfile(null); }));
            footer.Add(MakeLink("Manage…", () => { Hide(); app.ShowManageProfiles(); }));
            footer.Add(MakeLink("Steam setup…", () => { Hide(); app.ShowSteamSetup(); }));
            footer.Add(MakeLink("Settings…", () => { Hide(); app.ShowSettings(); }));

            Deactivate += (s, e) => { HiddenAt = DateTime.Now; Hide(); };
        }

        Button MakeLink(string text, Action click)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Panel,
                ForeColor = Theme.Text,
                Cursor = Cursors.Hand,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(3, 1, 3, 1),
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => click();
            Controls.Add(b);
            return b;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.AccentFor(activeShown), 2))
                e.Graphics.DrawRectangle(p, 1, 1, Width - 2, Height - 2);
        }

        // pads[i] = whether profiles[i] turns gamepad mode on.
        public void SetState(List<string> profiles, List<bool> pads, string active, string note, bool locked)
        {
            activeShown = active;

            // Rebuild the rows only when something about them changed.
            var key = string.Join("|", profiles.ToArray()) + "#" + active + "#" + locked + "#" +
                      string.Join(",", pads.ConvertAll(p => p ? "1" : "0").ToArray());
            if (key != layoutKey)
            {
                layoutKey = key;
                foreach (var r in rows) { Controls.Remove(r); r.Dispose(); }
                rows.Clear();
                int y = 42;
                for (int i = 0; i < profiles.Count; i++)
                {
                    string target = profiles[i];
                    var row = new ProfileRow { Profile = target, Pad = pads[i], IsActive = target == active, Locked = locked };
                    row.SetBounds(Gutter, y, W - 2 * Gutter, RowH);
                    // Switch after the click finishes: switching rebuilds (disposes) these rows.
                    row.Click += (s, e) => { if (!locked && target != activeShown) BeginInvoke(new Action(() => app.SwitchTo(target))); };
                    Controls.Add(row);
                    rows.Add(row);
                    y += RowH + RowGap;
                }

                status.SetBounds(Gutter, y + 2, W - 2 * Gutter, 36);
                y += 44;

                int x = Gutter;
                foreach (var b in footer)
                {
                    int w = b.PreferredSize.Width;
                    if (x + w > W - Gutter) { x = Gutter; y += 32; }
                    b.Location = new Point(x, y);
                    x += w + 6;
                }
                ClientSize = new Size(W, y + 30 + 12);
            }

            status.Text = note;
            status.ForeColor = locked ? Theme.Warn : Theme.Dim;
            Invalidate();
        }

        public void ShowNearTray()
        {
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            int x = Math.Min(Math.Max(Cursor.Position.X - Width / 2, wa.Left + 8), wa.Right - Width - 8);
            int y = Cursor.Position.Y > wa.Bottom - 40 ? wa.Bottom - Height - 8 : wa.Top + 8; // taskbar at bottom or top
            Location = new Point(x, y);
            Show();
            Activate();
        }
    }
}
