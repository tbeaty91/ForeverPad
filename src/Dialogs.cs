// ForeverPad — Settings and Steam setup windows.
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ForeverPad
{
    class DarkForm : Form
    {
        public DarkForm(string title, int w, int h)
        {
            Text = title;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(w, h);
            Icon = Icons.For(Profiles.Normal, false);
        }

        protected Label AddLabel(string text, int x, int y, int w, int h, bool dim)
        {
            var l = new Label { Text = text, ForeColor = dim ? Theme.Dim : Theme.Text };
            l.SetBounds(x, y, w, h);
            Controls.Add(l);
            return l;
        }

        protected TextBox AddBox(string text, int x, int y, int w)
        {
            var t = new TextBox { Text = text, BackColor = Theme.Panel, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            t.SetBounds(x, y, w, 24);
            Controls.Add(t);
            return t;
        }

        protected Button AddButton(string text, int x, int y, int w, Color back, EventHandler click)
        {
            var b = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = Color.White, Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
            b.SetBounds(x, y, w, 30);
            b.Click += click;
            Controls.Add(b);
            return b;
        }

        protected CheckBox AddCheck(string text, bool on, int x, int y, int w)
        {
            var c = new CheckBox { Text = text, Checked = on, ForeColor = Theme.Text };
            c.SetBounds(x, y, w, 24);
            Controls.Add(c);
            return c;
        }
    }

    class SettingsForm : DarkForm
    {
        public SettingsForm(Settings s) : base("ForeverPad settings", 560, 380)
        {
            AddLabel("WoW Forever folder (the one named _forever_)", 16, 14, 520, 20, false);
            var dir = AddBox(s.WowDir, 16, 36, 430);
            AddButton("Browse…", 454, 34, 90, Theme.Panel, (o, e) =>
            {
                using (var f = new FolderBrowserDialog { Description = "Pick the _forever_ folder inside World of Warcraft", SelectedPath = dir.Text })
                    if (f.ShowDialog(this) == DialogResult.OK) dir.Text = f.SelectedPath;
            });

            AddLabel("Game exe (found automatically, change it only if it's wrong)", 16, 72, 520, 20, false);
            var exe = AddBox(s.GameExe, 16, 94, 430);
            AddButton("Browse…", 454, 92, 90, Theme.Panel, (o, e) =>
            {
                using (var f = new OpenFileDialog { Filter = "Programs (*.exe)|*.exe", InitialDirectory = Directory.Exists(dir.Text) ? dir.Text : "" })
                    if (f.ShowDialog(this) == DialogResult.OK) exe.Text = f.FileName;
            });

            AddLabel("Extra launch arguments (usually blank)", 16, 130, 520, 20, false);
            var args = AddBox(s.LaunchArgs, 16, 152, 430);

            var restore = AddCheck("After a Steam controller session ends, switch back to Normal", s.RestoreAfterPlay, 16, 190, 528);
            var copy = AddCheck("New Controller profile copies Normal's Config.wtf (login, graphics, sound)", s.CopyConfigToController, 16, 216, 528);
            var boot = AddCheck("Start ForeverPad with Windows", Settings.StartWithWindows, 16, 242, 528);

            AddLabel("The Controller profile starts with no addons, keybinds or cache and keeps whatever you add to it.",
                16, 276, 528, 36, true);

            AddButton("Save", 360, 330, 90, Theme.NormalAccent, (o, e) =>
            {
                if (!Settings.IsWowDir(dir.Text))
                {
                    MessageBox.Show(this, "That folder doesn't contain a WoW game exe.\nPick World of Warcraft\\_forever_.", "ForeverPad");
                    return;
                }
                s.WowDir = dir.Text.Trim();
                s.GameExe = File.Exists(exe.Text) ? exe.Text.Trim() : (Settings.DetectGameExe(s.WowDir) ?? "");
                s.LaunchArgs = args.Text.Trim();
                s.RestoreAfterPlay = restore.Checked;
                s.CopyConfigToController = copy.Checked;
                s.Save();
                try { Settings.StartWithWindows = boot.Checked; } catch { }
                DialogResult = DialogResult.OK;
                Close();
            });
            AddButton("Cancel", 456, 330, 90, Theme.Panel, (o, e) => Close());

            if (s.WowDir != "" && s.GameExe == "") exe.Text = Settings.DetectGameExe(s.WowDir) ?? "";
            dir.TextChanged += (o, e) => { var d = Settings.DetectGameExe(dir.Text); if (d != null) exe.Text = d; };
        }
    }

    class SteamForm : DarkForm
    {
        readonly Settings s;
        readonly Label result;

        public SteamForm(Settings s) : base("Steam setup — WoW Forever (Controller)", 600, 470)
        {
            this.s = s;
            string steam = Steam.InstallDir;
            AddLabel("Adds a non-Steam game called \"" + Steam.ShortcutName + "\" to your Steam library. Launching it:",
                16, 14, 568, 22, false);
            AddLabel("  1.  switches WoW Forever to the Controller profile (gamepad on)\n" +
                     "  2.  starts WoW Forever under Steam, so Steam Input reads your controller\n" +
                     "  3.  when you quit WoW, switches back to Normal (you can turn that off in Settings)",
                16, 40, 568, 58, true);

            AddLabel(steam == null ? "Steam: not found. Install Steam and sign in first." : "Steam: " + steam,
                16, 106, 568, 20, steam == null);

            var add = AddButton("Add to Steam", 16, 132, 160, Theme.PadAccent, (o, e) => DoAdd());
            add.Enabled = steam != null && s.IsReady;
            if (!s.IsReady) AddLabel("Set your WoW Forever folder in Settings first.", 186, 138, 380, 20, true);

            result = AddLabel("", 16, 172, 568, 44, false);

            AddLabel("Playing from Apple TV:", 16, 222, 568, 20, false);
            AddLabel(
                "•  Install Steam Link on the Apple TV and pair your Bluetooth controller in tvOS Settings.\n" +
                "•  Open Steam Link, connect to this PC, and launch \"" + Steam.ShortcutName + "\".\n" +
                "•  In Steam, open the game's Properties ▸ Controller and choose \"Enable Steam Input\".\n" +
                "     Start with the \"Gamepad\" layout and pick another one later if you want.\n" +
                "•  The first time in game, look at Options ▸ Controls ▸ Gamepad to check it's on.\n" +
                "     ConsolePort is a good first addon for the Controller profile.\n" +
                "•  If WoW asks you to log in, that's because Battle.net isn't running. Start Battle.net\n" +
                "     once and tick \"keep me logged in\", or log in with the controller's on-screen keyboard.",
                16, 246, 568, 170, true);

            AddButton("Close", 494, 426, 90, Theme.Panel, (o, e) => Close());
        }

        void DoAdd()
        {
            bool wasRunning = Steam.IsRunning;
            if (wasRunning)
            {
                var ok = MessageBox.Show(this,
                    "Steam has to be closed while the shortcut is added. Steam rewrites its library file when it exits.\n\n" +
                    "Close Steam now? It will start again afterwards.", "ForeverPad", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (ok != DialogResult.OK) return;
            }
            UseWaitCursor = true;
            result.ForeColor = Theme.Dim;
            result.Text = wasRunning ? "Closing Steam…" : "Writing shortcut…";
            var t = new Thread(() =>
            {
                string msg; bool good = false;
                try
                {
                    if (wasRunning && !Steam.Shutdown(45000))
                        throw new InvalidOperationException("Steam didn't close. Quit it from its own tray icon and try again.");
                    int n = Steam.AddShortcut(s);
                    good = true;
                    msg = "Added \"" + Steam.ShortcutName + "\" to " + (n == 1 ? "your Steam library." : n + " Steam accounts.") +
                          " A backup of the old library file is next to it (shortcuts.vdf.foreverpad-backup).";
                    if (wasRunning) Steam.Start();
                }
                catch (Exception ex) { msg = ex.Message; }
                BeginInvoke(new Action(() =>
                {
                    UseWaitCursor = false;
                    result.ForeColor = good ? Theme.PadAccent : Theme.Warn;
                    result.Text = msg;
                }));
            });
            t.IsBackground = true;
            t.Start();
        }
    }
}
