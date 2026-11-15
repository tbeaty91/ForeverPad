// ForeverPad — profile switching.
//
// The live game folder (_forever_) always holds the ACTIVE profile's
// Interface, WTF and Cache folders as real folders, so WoW, CurseForge and
// Battle.net never see anything unusual. Inactive profiles are parked next to
// it in "_forever__profiles\<Name>\". A switch is a handful of folder renames
// on the same drive: instant, and nothing is ever deleted. If any rename
// fails, the ones already done are undone.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace ForeverPad
{
    static class Profiles
    {
        public const string Normal = "Normal";
        public const string Controller = "Controller";
        public static readonly string[] Folders = { "Interface", "WTF", "Cache" };

        public static string StoreRoot(string wowDir)
        {
            var d = wowDir.TrimEnd('\\', '/');
            return Path.Combine(Path.GetDirectoryName(d), Path.GetFileName(d) + "_profiles");
        }

        static string MarkerPath(string wowDir) { return Path.Combine(StoreRoot(wowDir), "active.txt"); }

        public static string Active(string wowDir)
        {
            try
            {
                var f = MarkerPath(wowDir);
                if (File.Exists(f))
                {
                    var v = File.ReadAllText(f).Trim();
                    if (v != "" && NameError(v) == null) return v;
                }
            }
            catch { }
            return Normal;
        }

        // ---- The profile list ----
        // Normal and Controller always exist (Controller is built on first use).
        // Any other profile is a folder in the store; a profile's folder stays
        // there even while it's active (it then only holds profile.ini).
        public static List<string> List(string wowDir)
        {
            var names = new List<string> { Normal, Controller };
            try
            {
                var store = StoreRoot(wowDir);
                if (Directory.Exists(store))
                    foreach (var d in Directory.GetDirectories(store).Select(Path.GetFileName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                        if (!names.Any(n => n.Equals(d, StringComparison.OrdinalIgnoreCase))) names.Add(d);
            }
            catch { }
            var act = Active(wowDir);
            if (!names.Contains(act)) names.Add(act);
            return names;
        }

        public static bool IsBuiltIn(string name) { return name == Normal || name == Controller; }

        // Match a typed name (e.g. a Steam launch argument) to a real profile.
        public static string Resolve(string wowDir, string typed, string fallback)
        {
            if (string.IsNullOrEmpty(typed)) return fallback;
            var hit = List(wowDir).FirstOrDefault(n => n.Equals(typed.Trim(), StringComparison.OrdinalIgnoreCase));
            return hit ?? fallback;
        }

        public static string NameError(string name)
        {
            name = (name ?? "").Trim();
            if (name == "") return "Give the profile a name.";
            if (name.Length > 40) return "Keep the name under 40 characters.";
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.StartsWith(".") || name.EndsWith("."))
                return "A profile name can't contain \\ / : * ? \" < > | or start or end with a dot.";
            return null;
        }

        static string IniPath(string wowDir, string name) { return Path.Combine(StoreRoot(wowDir), name, "profile.ini"); }

        // true = turn gamepad mode on when switching to it, false = turn it off,
        // null = leave it alone (Normal, unless you've changed it).
        public static bool? Gamepad(string wowDir, string name)
        {
            try
            {
                var f = IniPath(wowDir, name);
                if (File.Exists(f))
                    foreach (var l in File.ReadAllLines(f))
                        if (l.StartsWith("gamepad=")) return l.EndsWith("1");
            }
            catch { }
            if (name == Controller) return true;
            return null;
        }

        public static bool IsPad(string wowDir, string name) { return Gamepad(wowDir, name) == true; }

        public static void SetGamepad(string wowDir, string name, bool on)
        {
            var f = IniPath(wowDir, name);
            Directory.CreateDirectory(Path.GetDirectoryName(f));
            File.WriteAllText(f, "gamepad=" + (on ? "1" : "0") + "\r\n");
        }

        public static string Describe(string wowDir, string profile)
        {
            if (profile == Normal) return "Normal — your main addons, keybinds and settings";
            return profile + " — its own addons, keybinds and settings" + (IsPad(wowDir, profile) ? ", gamepad on" : "");
        }

        // Where a profile's Interface/WTF/Cache live right now.
        static string FolderOf(string wowDir, string name, string sub)
        {
            return Active(wowDir) == name ? Path.Combine(wowDir, sub) : Path.Combine(StoreRoot(wowDir), name, sub);
        }

        // New profile: blank (no addons, keybinds or cache) or a copy of another
        // profile's Interface + WTF. The source is only read, never moved.
        public static void Create(Settings s, string name, string copyFrom, bool gamepad, Action<string> progress)
        {
            name = name.Trim();
            var err = NameError(name);
            if (err != null) throw new InvalidOperationException(err);
            if (List(s.WowDir).Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("There's already a profile called " + name + ".");
            if (copyFrom != null && copyFrom == Active(s.WowDir) && GameRunning(s))
                throw new InvalidOperationException("Close WoW Forever first: " + copyFrom + " is in use.");

            string dir = Path.Combine(StoreRoot(s.WowDir), name);
            try
            {
                Directory.CreateDirectory(dir);
                if (copyFrom == null) SeedBlank(s, dir);
                else
                {
                    foreach (var sub in new[] { "Interface", "WTF" })
                    {
                        string src = FolderOf(s.WowDir, copyFrom, sub);
                        if (!Directory.Exists(src)) continue;
                        if (progress != null) progress("Copying " + copyFrom + "'s " + sub + "…");
                        CopyTree(src, Path.Combine(dir, sub));
                    }
                }
                SetGamepad(s.WowDir, name, gamepad);
            }
            catch
            {
                try { Directory.Delete(dir, true); } catch { }
                throw;
            }
        }

        static void CopyTree(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), false);
            foreach (var d in Directory.GetDirectories(src)) CopyTree(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        public static void Rename(Settings s, string from, string to)
        {
            to = to.Trim();
            if (IsBuiltIn(from)) throw new InvalidOperationException(from + " can't be renamed.");
            if (from == Active(s.WowDir)) throw new InvalidOperationException("Switch to another profile before renaming " + from + ".");
            var err = NameError(to);
            if (err != null) throw new InvalidOperationException(err);
            if (List(s.WowDir).Any(n => n.Equals(to, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("There's already a profile called " + to + ".");
            Directory.Move(Path.Combine(StoreRoot(s.WowDir), from), Path.Combine(StoreRoot(s.WowDir), to));
        }

        // Deleted profiles go to the Recycle Bin, so a mistake can be undone.
        public static void Delete(Settings s, string name)
        {
            if (IsBuiltIn(name)) throw new InvalidOperationException(name + " can't be deleted.");
            if (name == Active(s.WowDir)) throw new InvalidOperationException("Switch to another profile before deleting " + name + ".");
            string dir = Path.Combine(StoreRoot(s.WowDir), name);
            if (!Directory.Exists(dir)) return;
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(dir,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }

        // ---- Is the game running? ----
        public static bool GameRunning(Settings s)
        {
            return GameProcesses(s).Length > 0;
        }

        public static Process[] GameProcesses(Settings s)
        {
            if (string.IsNullOrEmpty(s.GameExe)) return new Process[0];
            Process[] all;
            try { all = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(s.GameExe)); }
            catch { return new Process[0]; }
            // Only count copies started from THIS folder (retail may share the exe name).
            string want = Path.GetFullPath(s.GameExe);
            return all.Where(p =>
            {
                try { return string.Equals(Path.GetFullPath(p.MainModule.FileName), want, StringComparison.OrdinalIgnoreCase); }
                catch { return true; } // can't read the path: assume it's ours, the safe answer
            }).ToArray();
        }

        // Cross-process lock so the tray and a Steam launch never switch at once.
        static readonly Mutex SwitchLock = new Mutex(false, @"Local\ForeverPad.Switch");

        public static void Switch(Settings s, string target)
        {
            if (!s.IsReady) throw new InvalidOperationException("Set your WoW Forever folder in Settings first.");
            if (GameRunning(s)) throw new InvalidOperationException("WoW Forever is running. Close it, then switch.");

            bool got;
            try { got = SwitchLock.WaitOne(10000); }
            catch (AbandonedMutexException) { got = true; }
            if (!got) throw new InvalidOperationException("Another switch is in progress.");
            try { SwitchLocked(s, target); }
            finally { SwitchLock.ReleaseMutex(); }
        }

        static void SwitchLocked(Settings s, string target)
        {
            string wow = s.WowDir;
            string cur = Active(wow);
            string store = StoreRoot(wow);
            Directory.CreateDirectory(store);

            if (cur != target)
            {
                string parkDir = Path.Combine(store, cur);
                string fromDir = Path.Combine(store, target);

                foreach (var f in Folders)
                    if (Directory.Exists(Path.Combine(parkDir, f)))
                        throw new InvalidOperationException(
                            "Can't park the " + cur + " profile: \"" + Path.Combine(parkDir, f) +
                            "\" already exists. Move it somewhere safe and try again.");

                // First time into Controller mode: build the blank slate.
                if (target == Controller && !Directory.Exists(fromDir))
                {
                    SeedBlank(s, fromDir);
                    SetGamepad(wow, Controller, true);
                }

                var done = new List<string[]>(); // {from, to}
                try
                {
                    foreach (var f in Folders)
                    {
                        string live = Path.Combine(wow, f);
                        if (!Directory.Exists(live)) continue;
                        Directory.CreateDirectory(parkDir);
                        Move(live, Path.Combine(parkDir, f), done);
                    }
                    foreach (var f in Folders)
                    {
                        string parked = Path.Combine(fromDir, f);
                        if (!Directory.Exists(parked)) continue;
                        Move(parked, Path.Combine(wow, f), done);
                    }
                    File.WriteAllText(MarkerPath(wow), target);
                }
                catch (Exception ex)
                {
                    for (int i = done.Count - 1; i >= 0; i--)
                    {
                        try { Directory.Move(done[i][1], done[i][0]); } catch { }
                    }
                    throw new InvalidOperationException(
                        "Switch failed and was undone: " + ex.Message +
                        "\n\nIf a folder is in use (Explorer window, CurseForge, an editor), close it and retry.", ex);
                }
            }

            var pad = Gamepad(wow, target);
            string cfg = Path.Combine(wow, "WTF", "Config.wtf");
            if (pad == true) SetCvar(cfg, "GamePadEnable", "1");
            else if (pad == false && File.Exists(cfg)) SetCvar(cfg, "GamePadEnable", "0");
        }

        static void Move(string from, string to, List<string[]> done)
        {
            Directory.Move(from, to);
            done.Add(new[] { from, to });
        }

        // Blank slate: no addons, no keybinds, no saved variables, no cache.
        // Config.wtf (login, graphics, audio) is copied from the live profile
        // unless the user turned that off.
        static void SeedBlank(Settings s, string dir)
        {
            Directory.CreateDirectory(Path.Combine(dir, "WTF"));
            Directory.CreateDirectory(Path.Combine(dir, "Interface", "AddOns"));
            string dst = Path.Combine(dir, "WTF", "Config.wtf");
            if (s.CopyConfigToController)
            {
                string src = Path.Combine(s.WowDir, "WTF", "Config.wtf");
                if (File.Exists(src)) File.Copy(src, dst, false);
            }
        }

        // Set (or add) `SET name "value"` in a Config.wtf.
        public static void SetCvar(string path, string name, string value)
        {
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
            var re = new Regex("^\\s*SET\\s+" + Regex.Escape(name) + "\\s", RegexOptions.IgnoreCase);
            string line = "SET " + name + " \"" + value + "\"";
            bool found = false;
            for (int i = 0; i < lines.Count; i++)
                if (re.IsMatch(lines[i])) { lines[i] = line; found = true; }
            if (!found) lines.Add(line);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, lines);
        }

        // ---- Battle.net launch (logs in for you) ----
        // Steam Input and the overlay only attach to processes descended from
        // the Steam launch. A Battle.net that's already running isn't one, so it
        // is closed and started again from here; the game it launches is then
        // part of the Steam process tree. Approach and the WoWF code are from
        // github.com/rynmon/bnet-steam-launcher.
        public static Process[] BattleNetProcesses()
        {
            try { return Process.GetProcessesByName("Battle.net"); } catch { return new Process[0]; }
        }

        public static void CloseBattleNet()
        {
            foreach (var p in BattleNetProcesses()) { try { p.Kill(); } catch { } }
            var sw = Stopwatch.StartNew();
            while (BattleNetProcesses().Length > 0 && sw.Elapsed.TotalSeconds < 15) Thread.Sleep(300);
        }

        static void SendBnet(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.WorkingDirectory = Path.GetDirectoryName(exe);
            psi.UseShellExecute = false;
            Process.Start(psi);
        }

        // Returns true once the game process is up.
        public static bool LaunchViaBattleNet(Settings s, int startupWaitSec)
        {
            string bnet = Settings.BattleNetExe();
            CloseBattleNet();
            SendBnet(bnet, "");

            // Wait for Battle.net to come up, then give it a moment to sign in.
            var sw = Stopwatch.StartNew();
            while (BattleNetProcesses().Length == 0 && sw.Elapsed.TotalSeconds < 20) Thread.Sleep(500);
            Thread.Sleep(3000);

            string launch = "--exec=\"launch " + s.BnetCode + "\"";
            SendBnet(bnet, launch);
            // Battle.net sometimes only SELECTS the game instead of starting it,
            // or ignores a command sent while it's still loading: resend until it starts.
            sw = Stopwatch.StartNew();
            double lastSend = 0;
            while (sw.Elapsed.TotalSeconds < startupWaitSec)
            {
                if (GameRunning(s)) return true;
                if (sw.Elapsed.TotalSeconds - lastSend >= 10)
                {
                    lastSend = sw.Elapsed.TotalSeconds;
                    try { SendBnet(bnet, launch); } catch { }
                }
                Thread.Sleep(500);
            }
            return GameRunning(s);
        }

        // Start the game exe directly so it is a child of this process. Steam
        // hooks the process tree it launched, which is what lets Steam Input
        // drive the game from a streamed controller.
        public static void LaunchGame(Settings s)
        {
            var psi = new ProcessStartInfo(s.GameExe, s.LaunchArgs ?? "");
            psi.WorkingDirectory = Path.GetDirectoryName(s.GameExe);
            psi.UseShellExecute = false;
            Process.Start(psi);
        }
    }
}
