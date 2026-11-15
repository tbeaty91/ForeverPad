// ForeverPad — the toggle that pops up above the tray icon.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ForeverPad
{
    // Two-segment pill switch: Normal | Controller.
    class ModeSwitch : Control
    {
        public string Value = Profiles.Normal;
        public bool Locked;
        public event Action<string> Picked;

        public ModeSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI Semibold", 10.5f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (var p = Theme.Rounded(r, Height / 2f - 1))
            using (var b = new SolidBrush(Theme.Panel))
                g.FillPath(b, p);

            float half = Width / 2f;
            bool pad = Value == Profiles.Controller;
            var knob = new RectangleF(pad ? half + 2 : 3, 3, half - 5, Height - 6);
            using (var p = Theme.Rounded(knob, knob.Height / 2f))
            using (var b = new SolidBrush(Locked ? Color.FromArgb(90, 94, 104) : Theme.AccentFor(Value)))
                g.FillPath(b, p);

            var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var on = new SolidBrush(Color.White))
            using (var off = new SolidBrush(Theme.Dim))
            {
                g.DrawString("Normal", Font, pad ? off : on, new RectangleF(0, 0, half, Height), fmt);
                g.DrawString("Controller", Font, pad ? on : off, new RectangleF(half, 0, half, Height), fmt);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (Locked) return;
            string v = e.X < Width / 2 ? Profiles.Normal : Profiles.Controller;
            if (v != Value && Picked != null) Picked(v);
        }
    }

    class Popup : Form
    {
        readonly TrayApp app;
        readonly ModeSwitch sw = new ModeSwitch();
        readonly Label status = new Label();
        readonly Label title = new Label();
        public DateTime HiddenAt = DateTime.MinValue;

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
            ClientSize = new Size(340, 196);
            Padding = new Padding(1);

            title.Text = "WoW Forever setup";
            title.Font = new Font("Segoe UI Semibold", 11f);
            title.ForeColor = Theme.Text;
            title.SetBounds(16, 12, 300, 22);
            Controls.Add(title);

            sw.SetBounds(16, 42, 308, 40);
            sw.Picked += v => app.SwitchTo(v);
            Controls.Add(sw);

            status.ForeColor = Theme.Dim;
            status.SetBounds(16, 90, 308, 38);
            Controls.Add(status);

            int x = 16;
            x = AddLink("Steam setup…", x, () => { Hide(); app.ShowSteamSetup(); });
            x = AddLink("Settings…", x, () => { Hide(); app.ShowSettings(); });
            x = AddLink("Open folders", x, () => { Hide(); app.OpenFolders(); });
            AddLink("Quit", x, () => app.Quit());

            Deactivate += (s, e) => { HiddenAt = DateTime.Now; Hide(); };
        }

        int AddLink(string text, int x, Action click)
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
                Padding = new Padding(4, 1, 4, 1),
            };
            b.FlatAppearance.BorderSize = 0;
            b.Location = new Point(x, 144);
            b.Click += (s, e) => click();
            Controls.Add(b);
            return x + b.PreferredSize.Width + 6;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.AccentFor(sw.Value), 2))
                e.Graphics.DrawRectangle(p, 1, 1, Width - 2, Height - 2);
        }

        public void SetState(string active, string note, bool locked)
        {
            sw.Value = active;
            sw.Locked = locked;
            sw.Invalidate();
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
