// ForeverPad — the tray icon and its menu.
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace ForeverPad
{
    class TrayApp : ApplicationContext
    {
        Settings s = Settings.Load();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Popup popup;
        readonly Timer poll = new Timer { Interval = 2000 };
        readonly ToolStripMenuItem miNormal, miPad, miBoot;
        string shownActive;
        bool shownRunning;

        public TrayApp(bool boot)
        {
            // Boot with the PC by default: on first run, turn it on. On later runs,
            // re-point the Run entry at this exe in case it was moved.
            try
            {
                string seen = Path.Combine(Settings.Dir, "boot-default-applied");
                if (!File.Exists(seen))
                {
                    Settings.StartWithWindows = true;
                    Directory.CreateDirectory(Settings.Dir);
                    File.WriteAllText(seen, "1");
                }
                else if (Settings.StartWithWindows) Settings.StartWithWindows = true;
            }
            catch { }

            popup = new Popup(this);
            var popupHandle = popup.Handle; // create the window now so BeginInvoke works

            var menu = new ContextMenuStrip();
            miNormal = new ToolStripMenuItem("Normal", null, (o, e) => SwitchTo(Profiles.Normal));
            miPad = new ToolStripMenuItem("Controller", null, (o, e) => SwitchTo(Profiles.Controller));
            miBoot = new ToolStripMenuItem("Start with Windows", null, (o, e) =>
            {
                try { Settings.StartWithWindows = !Settings.StartWithWindows; } catch { }
                miBoot.Checked = Settings.StartWithWindows;
            });
            menu.Items.Add(miNormal);
            menu.Items.Add(miPad);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Steam setup…", null, (o, e) => ShowSteamSetup());
            menu.Items.Add("Settings…", null, (o, e) => ShowSettings());
            menu.Items.Add("Open folders", null, (o, e) => OpenFolders());
            menu.Items.Add(miBoot);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit", null, (o, e) => Quit());
            menu.Opening += (o, e) => { miBoot.Checked = Settings.StartWithWindows; Sync(true); };
            tray.ContextMenuStrip = menu;

            tray.MouseClick += (o, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                if (popup.Visible) { popup.Hide(); return; }
                // Clicking the icon deactivates (and hides) the popup first; don't reopen it.
                if ((DateTime.Now - popup.HiddenAt).TotalMilliseconds < 400) return;
                Sync(true);
                popup.ShowNearTray();
            };

            poll.Tick += (o, e) => Sync(false);
            poll.Start();
            Sync(true);
            tray.Visible = true;

            if (boot && s.IsReady) AskAtBoot();
            else if (!s.IsReady)
            {
                tray.ShowBalloonTip(4000, "ForeverPad", "Couldn't find WoW Forever. Pick its folder in Settings.", ToolTipIcon.Info);
                ShowSettings();
            }
        }

        // Re-read state (a Steam launch may have switched it) and update the icon.
        void Sync(bool force)
        {
            string active = s.IsReady ? Profiles.Active(s.WowDir) : Profiles.Normal;
            bool running = s.IsReady && Profiles.GameRunning(s);
            if (!force && active == shownActive && running == shownRunning) return;
            shownActive = active;
            shownRunning = running;

            var old = tray.Icon;
            tray.Icon = Icons.For(active, false);
            if (old != null) old.Dispose();
            string tip = "WoW Forever: " + active + (running ? " (game running)" : "");
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;

            miNormal.Checked = active == Profiles.Normal;
            miPad.Checked = active == Profiles.Controller;
            bool locked = running || !s.IsReady;
            miNormal.Enabled = miPad.Enabled = !locked;

            string note = !s.IsReady ? "WoW Forever folder not set. Open Settings."
                : running ? "WoW Forever is running. Close it to switch profiles."
                : Profiles.Describe(active);
            popup.SetState(active, note, locked);
        }

        public void SwitchTo(string target)
        {
            try
            {
                Profiles.Switch(s, target);
                tray.ShowBalloonTip(2500, "WoW Forever", "Switched to " + target + ".", ToolTipIcon.None);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ForeverPad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            Sync(true);
        }

        // Started with Windows: ask which setup to use for this session.
        void AskAtBoot()
        {
            if (Profiles.GameRunning(s)) return;
            string pick;
            using (var f = new BootAsk(Profiles.Active(s.WowDir)))
            {
                f.ShowDialog();
                pick = f.Choice;
            }
            if (pick != null && pick != Profiles.Active(s.WowDir)) SwitchTo(pick);
        }

        public void ShowSettings()
        {
            using (var f = new SettingsForm(s))
                if (f.ShowDialog() == DialogResult.OK) s = Settings.Load();
            Sync(true);
        }

        public void ShowSteamSetup()
        {
            using (var f = new SteamForm(s)) f.ShowDialog();
        }

        public void OpenFolders()
        {
            if (!s.IsReady) { ShowSettings(); return; }
            Process.Start("explorer.exe", "\"" + s.WowDir + "\"");
            var store = Profiles.StoreRoot(s.WowDir);
            if (Directory.Exists(store)) Process.Start("explorer.exe", "\"" + store + "\"");
        }

        public void Quit()
        {
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }
}
