// ForeverPad — Settings and Steam setup windows.
using System;
using System.Collections.Generic;
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

            AddLabel("Battle.net launch code (blank = start the exe directly, which asks for a login)", 16, 130, 528, 20, false);
            var code = AddBox(s.BnetCode, 16, 152, 120);
            AddLabel(Settings.BattleNetExe() == null ? "Battle.net not found: the game exe will be started directly."
                                                     : "WoWF = WoW Forever. Battle.net logs you in.",
                146, 155, 400, 20, true);

            var restore = AddCheck("After a Steam controller session ends, switch back to Normal", s.RestoreAfterPlay, 16, 190, 528);
            var copy = AddCheck("Blank profiles copy the current Config.wtf (login, graphics, sound)", s.CopyConfigToController, 16, 216, 528);
            var boot = AddCheck("Start with Windows and ask Normal or Controller", Settings.StartWithWindows, 16, 242, 528);

            AddLabel("A blank profile starts with no addons, keybinds or cache and keeps whatever you add to it. Make more from the tray: + New profile.",
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
                s.BnetCode = code.Text.Trim();
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
                     "  2.  restarts Battle.net under Steam and has it launch WoW Forever (no login,\n" +
                     "       and Steam Input still reads your controller)\n" +
                     "  3.  when you quit WoW, switches back to Normal (you can turn that off in Settings)",
                16, 40, 568, 64, true);

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
                "•  Battle.net must be signed in with \"keep me logged in\" ticked, and set to the WoW\n" +
                "     Forever version. Any Battle.net window that's already open is closed and reopened.",
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

    // Shown at Windows start-up: one big button per profile, current one highlighted.
    class BootAsk : DarkForm
    {
        public string Choice; // null = keep current

        public BootAsk(string wowDir, List<string> profiles, string current)
            : base("WoW Forever", 420, 120 + profiles.Count * 50)
        {
            TopMost = true;
            AddLabel("Which WoW Forever profile for this session?", 20, 16, 380, 22, false);
            int y = 48;
            foreach (var name in profiles)
            {
                string pick = name;
                var b = AddButton(name + (Profiles.IsPad(wowDir, name) ? "   (gamepad)" : ""), 20, y, 380, name == current ? Theme.AccentFor(name) : Theme.Panel,
                    (o, e) => { Choice = pick; Close(); });
                b.Height = 42;
                b.Font = new Font("Segoe UI Semibold", 11f);
                if (name == current) AcceptButton = b;
                y += 50;
            }
            AddLabel("Change it any time from the tray icon. Steam launches switch on their own.", 20, y + 6, 380, 36, true);
        }
    }

    // A one-line text prompt.
    class AskText : DarkForm
    {
        readonly TextBox box;
        public string Value { get { return box.Text.Trim(); } }

        public AskText(string title, string prompt, string initial) : base(title, 400, 130)
        {
            AddLabel(prompt, 16, 14, 368, 20, false);
            box = AddBox(initial, 16, 38, 368);
            var ok = AddButton("OK", 198, 84, 90, Theme.NormalAccent, (o, e) => { DialogResult = DialogResult.OK; Close(); });
            AddButton("Cancel", 294, 84, 90, Theme.Panel, (o, e) => Close());
            AcceptButton = ok;
        }
    }

    class NewProfileForm : DarkForm
    {
        public string Created;

        public NewProfileForm(Settings s, string copyFrom) : base("New WoW Forever profile", 460, 300)
        {
            var list = Profiles.List(s.WowDir);
            AddLabel("Name", 16, 14, 420, 20, false);
            var name = AddBox(copyFrom == null ? "" : copyFrom + " test", 16, 36, 428);

            AddLabel("Start from", 16, 72, 420, 20, false);
            var from = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.Panel, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
            from.SetBounds(16, 94, 428, 26);
            from.Items.Add("Blank: no addons, no keybinds, no saved settings");
            foreach (var p in list) from.Items.Add("Copy of " + p + ": its addons, keybinds and settings");
            from.SelectedIndex = copyFrom == null ? 0 : Math.Max(0, list.IndexOf(copyFrom) + 1);
            Controls.Add(from);

            var pad = AddCheck("Gamepad mode on when this profile is active", copyFrom != null && Profiles.IsPad(s.WowDir, copyFrom), 16, 132, 428);
            from.SelectedIndexChanged += (o, e) =>
            {
                if (from.SelectedIndex > 0) pad.Checked = Profiles.IsPad(s.WowDir, list[from.SelectedIndex - 1]);
            };
            var go = AddCheck("Switch to it now", true, 16, 158, 428);

            AddLabel("Copies don't touch the original: try new addons in the copy and your main setup stays exactly as it was. " +
                     "The cache isn't copied; WoW rebuilds it.", 16, 190, 428, 50, true);
            var status = AddLabel("", 16, 244, 240, 40, false);

            Button create = null;
            create = AddButton("Create", 258, 256, 90, Theme.NormalAccent, (o, e) =>
            {
                string n = name.Text.Trim();
                string src = from.SelectedIndex > 0 ? list[from.SelectedIndex - 1] : null;
                bool padOn = pad.Checked, switchNow = go.Checked;
                var err = Profiles.NameError(n);
                if (err != null) { status.ForeColor = Theme.Warn; status.Text = err; return; }

                create.Enabled = false;
                UseWaitCursor = true;
                status.ForeColor = Theme.Dim;
                status.Text = "Creating…";
                var t = new System.Threading.Thread(() =>
                {
                    string fail = null;
                    try
                    {
                        Profiles.Create(s, n, src, padOn, msg => BeginInvoke(new Action(() => status.Text = msg)));
                        if (switchNow && !Profiles.GameRunning(s)) Profiles.Switch(s, n);
                    }
                    catch (Exception ex) { fail = ex.Message; }
                    BeginInvoke(new Action(() =>
                    {
                        UseWaitCursor = false;
                        create.Enabled = true;
                        if (fail != null) { status.ForeColor = Theme.Warn; status.Text = fail; return; }
                        Created = n;
                        DialogResult = DialogResult.OK;
                        Close();
                    }));
                });
                t.IsBackground = true;
                t.Start();
            });
            AddButton("Cancel", 354, 256, 90, Theme.Panel, (o, e) => Close());
            AcceptButton = create;
        }
    }

    class ManageProfilesForm : DarkForm
    {
        readonly Settings s;
        readonly ListBox box = new ListBox();
        readonly CheckBox pad;
        readonly Label info;
        readonly Button bRename, bDelete, bDup, bSwitch;
        public bool Changed;

        public ManageProfilesForm(Settings s) : base("WoW Forever profiles", 520, 340)
        {
            this.s = s;
            box.BackColor = Theme.Panel;
            box.ForeColor = Theme.Text;
            box.BorderStyle = BorderStyle.None;
            box.Font = new Font("Segoe UI", 10.5f);
            box.ItemHeight = 22;
            box.SetBounds(16, 16, 250, 270);
            box.SelectedIndexChanged += (o, e) => UpdateButtons();
            Controls.Add(box);

            bSwitch = AddButton("Switch to it", 282, 16, 222, Theme.NormalAccent, (o, e) => Do(() => Profiles.Switch(s, Sel)));
            AddButton("New…", 282, 54, 108, Theme.Panel, (o, e) => NewFrom(null));
            bDup = AddButton("Duplicate…", 396, 54, 108, Theme.Panel, (o, e) => NewFrom(Sel));
            bRename = AddButton("Rename…", 282, 92, 108, Theme.Panel, (o, e) =>
            {
                using (var f = new AskText("Rename profile", "New name for " + Sel, Sel))
                    if (f.ShowDialog(this) == DialogResult.OK) { string to = f.Value; renamedTo = to; Do(() => Profiles.Rename(s, Sel, to)); }
            });
            bDelete = AddButton("Delete…", 396, 92, 108, Theme.Panel, (o, e) =>
            {
                var r = MessageBox.Show(this, "Delete the " + Sel + " profile? Its addons, keybinds and settings go to the Recycle Bin.",
                    "ForeverPad", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (r == DialogResult.OK) Do(() => Profiles.Delete(s, Sel));
            });
            pad = AddCheck("Gamepad mode on for this profile", false, 282, 136, 222);
            pad.CheckedChanged += (o, e) =>
            {
                if (Sel == null || pad.Checked == Profiles.IsPad(s.WowDir, Sel)) return;
                Do(() => Profiles.SetGamepad(s.WowDir, Sel, pad.Checked));
            };
            info = AddLabel("", 282, 166, 222, 100, true);
            AddButton("Close", 414, 296, 90, Theme.Panel, (o, e) => Close());
            Reload(Profiles.Active(s.WowDir));
        }

        string Sel { get { var i = box.SelectedIndex; return i < 0 ? null : names[i]; } }
        List<string> names = new List<string>();
        string renamedTo;

        void Reload(string select)
        {
            names = Profiles.List(s.WowDir);
            string active = Profiles.Active(s.WowDir);
            box.Items.Clear();
            foreach (var n in names) box.Items.Add(n + (n == active ? "   (active)" : "") + (Profiles.IsPad(s.WowDir, n) ? "   (gamepad)" : ""));
            int i = names.IndexOf(select ?? active);
            box.SelectedIndex = i >= 0 ? i : 0;
        }

        void UpdateButtons()
        {
            string n = Sel;
            if (n == null) return;
            bool active = n == Profiles.Active(s.WowDir);
            bool running = Profiles.GameRunning(s);
            bSwitch.Enabled = !active && !running;
            bRename.Enabled = bDelete.Enabled = !active && !Profiles.IsBuiltIn(n);
            bDup.Enabled = true;
            pad.Checked = Profiles.IsPad(s.WowDir, n);
            info.Text = Profiles.IsBuiltIn(n)
                ? (n == Profiles.Normal ? "Your main setup. Steam sessions return here." : "Used by the Steam shortcut.") + " Built in, so it can't be renamed or deleted."
                : active ? "Active now. Switch to another profile to rename or delete it." : "";
            if (running) info.Text = "WoW Forever is running: close it to switch.";
        }

        void NewFrom(string src)
        {
            using (var f = new NewProfileForm(s, src))
                if (f.ShowDialog(this) == DialogResult.OK) { Changed = true; Reload(f.Created); }
        }

        void Do(Action a)
        {
            string keep = Sel;
            try { a(); Changed = true; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "ForeverPad", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            Reload(Profiles.List(s.WowDir).Contains(keep) ? keep : renamedTo);
            renamedTo = null;
        }
    }
}
