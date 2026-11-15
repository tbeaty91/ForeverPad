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
        readonly ToolStripMenuItem miNormal, miPad, miBoot, miUpdate;
        readonly Timer updTimer = new Timer { Interval = 15000 };
        UpdateInfo pending;
        string announced;
        string shownActive;
        bool shownRunning;

        public TrayApp(string mode)
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
            miBoot = new ToolStripMenuItem("Start with Windows (asks at boot)", null, (o, e) =>
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
            miUpdate = new ToolStripMenuItem("Check for updates", null, (o, e) =>
            {
                if (pending != null) PromptUpdate(pending); else CheckForUpdates(true);
            });
            menu.Items.Add(miUpdate);
            menu.Items.Add("Uninstall…", null, (o, e) =>
            {
                tray.Visible = false;
                if (Installer.Uninstall() == 0) Quit(); else tray.Visible = true;
            });
            menu.Items.Add("Quit  (v" + Program.Version + ")", null, (o, e) => Quit());
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

            bool boot = mode == "--boot";
            if (mode == "--installed")
            {
                tray.ShowBalloonTip(6000, "ForeverPad is installed",
                    "It lives here in the tray (check the ^ arrow) and starts with Windows. Click the icon to switch setups.", ToolTipIcon.Info);
                if (Steam.ShortcutIsStale())
                {
                    var r = MessageBox.Show("Your Steam shortcut \"" + Steam.ShortcutName + "\" still points at the copy you downloaded.\n\n" +
                        "Open Steam setup and press \"Add to Steam\" to point it at the installed copy?", "ForeverPad",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes) ShowSteamSetup();
                }
            }
            else if (mode == "--updated")
                tray.ShowBalloonTip(4000, "ForeverPad updated", "Now running v" + Program.Version + ".", ToolTipIcon.Info);

            tray.BalloonTipClicked += (o, e) => { if (pending != null) PromptUpdate(pending); };
            updTimer.Tick += (o, e) => { updTimer.Interval = 6 * 60 * 60 * 1000; CheckForUpdates(false); };
            updTimer.Start();

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

        // ---- Updates (GitHub releases) ----
        void CheckForUpdates(bool manual)
        {
            miUpdate.Enabled = false;
            var t = new System.Threading.Thread(() =>
            {
                UpdateInfo u = null; string err = null;
                try { u = Updater.Check(); }
                catch (System.Net.WebException ex)
                {
                    var resp = ex.Response as System.Net.HttpWebResponse;
                    err = resp != null && resp.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "No releases found. Either none has been published yet, or the GitHub repo is private and can't be read without signing in."
                        : "Couldn't reach GitHub: " + ex.Message;
                }
                catch (Exception ex) { err = ex.Message; }
                popup.BeginInvoke(new Action(() => OnChecked(u, err, manual)));
            });
            t.IsBackground = true;
            t.Start();
        }

        void OnChecked(UpdateInfo u, string err, bool manual)
        {
            miUpdate.Enabled = true;
            if (err != null) { if (manual) MessageBox.Show(err, "ForeverPad"); return; }
            pending = u;
            if (u == null)
            {
                miUpdate.Text = "Check for updates";
                if (manual) MessageBox.Show("ForeverPad v" + Program.Version + " is up to date.", "ForeverPad");
                return;
            }
            miUpdate.Text = "Update to " + u.Tag + "…";
            if (manual) { PromptUpdate(u); return; }
            if (announced != u.Tag)
            {
                announced = u.Tag;
                tray.ShowBalloonTip(8000, "ForeverPad update", u.Tag + " is available. Click here to install it.", ToolTipIcon.Info);
            }
        }

        void PromptUpdate(UpdateInfo u)
        {
            string notes = string.IsNullOrEmpty(u.Notes) ? "" : "\n\n" + (u.Notes.Length > 800 ? u.Notes.Substring(0, 800) + "…" : u.Notes);
            var r = MessageBox.Show("Update ForeverPad from v" + Program.Version + " to " + u.Tag + "?" + notes +
                "\n\nIt restarts itself in a few seconds. Your setups and settings aren't touched.",
                "ForeverPad", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            try
            {
                Updater.Apply(u);
                Quit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update failed: " + ex.Message + "\n\nYou can also download it from " + Updater.ReleasesPage, "ForeverPad");
            }
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
