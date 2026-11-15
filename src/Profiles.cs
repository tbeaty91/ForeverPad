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
                    if (v == Controller) return Controller;
                }
            }
            catch { }
            return Normal;
        }

        public static string Describe(string profile)
        {
            return profile == Controller
                ? "Controller — its own addons, keybinds and settings, gamepad on"
                : "Normal — your usual addons, keybinds and settings";
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
                    SeedController(s, fromDir);

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

            if (target == Controller)
                SetCvar(Path.Combine(wow, "WTF", "Config.wtf"), "GamePadEnable", "1");
        }

        static void Move(string from, string to, List<string[]> done)
        {
            Directory.Move(from, to);
            done.Add(new[] { from, to });
        }

        // Blank slate: no addons, no keybinds, no saved variables, no cache.
        // Config.wtf is copied from Normal (login, graphics, audio) unless the
        // user turned that off, then gamepad mode is switched on.
        static void SeedController(Settings s, string dir)
        {
            Directory.CreateDirectory(Path.Combine(dir, "WTF"));
            Directory.CreateDirectory(Path.Combine(dir, "Interface", "AddOns"));
            string dst = Path.Combine(dir, "WTF", "Config.wtf");
            if (s.CopyConfigToController)
            {
                // Normal is the live profile whenever Controller doesn't exist yet.
                string src = Path.Combine(s.WowDir, "WTF", "Config.wtf");
                if (File.Exists(src)) File.Copy(src, dst, false);
            }
            SetCvar(dst, "GamePadEnable", "1");
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
