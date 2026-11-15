// ForeverPad — close the CurseForge app around a profile switch and reopen it.
//
// CurseForge works out which addons are installed by scanning
// Interface\AddOns, and it rescans when it starts. Restarting it after a
// switch makes it show the profile that's now active instead of the one it
// scanned earlier. Closing it first also means it isn't holding files in the
// folders being moved, and can't be halfway through an update while they move.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace ForeverPad
{
    static class CurseForgeApp
    {
        static Process[] Procs()
        {
            try { return Process.GetProcessesByName("CurseForge"); } catch { return new Process[0]; }
        }

        public static bool IsRunning { get { return Procs().Length > 0; } }

        // Closes CurseForge if it's open. Returns its exe path, to reopen it with, or null.
        public static string Close()
        {
            var ps = Procs();
            if (ps.Length == 0) return null;
            string exe = null;
            foreach (var p in ps)
            {
                try { if (exe == null) exe = p.MainModule.FileName; } catch { }
            }

            // Ask nicely first (it's an Electron app: one window, several helper processes).
            foreach (var p in ps) { try { p.CloseMainWindow(); } catch { } }
            var sw = Stopwatch.StartNew();
            while (IsRunning && sw.Elapsed.TotalSeconds < 5) Thread.Sleep(250);
            // Closing the window may only hide it to the tray: end it for real.
            foreach (var p in Procs()) { try { p.Kill(); } catch { } }
            sw.Restart();
            while (IsRunning && sw.Elapsed.TotalSeconds < 10) Thread.Sleep(250);
            Thread.Sleep(500);
            return exe;
        }

        // Start it again as an ordinary app (through Explorer, so it isn't a
        // child of ForeverPad or of a Steam session).
        public static void Reopen(string exe)
        {
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return;
            try { Process.Start("explorer.exe", "\"" + exe + "\""); } catch { }
        }
    }
}

namespace ForeverPad
{
    // Finds where CurseForge keeps its record of installed WoW addons: any file
    // in its data folders that mentions the _forever_ folder. Read-only.
    static class CurseForgeProbe
    {
        public static string Run(Settings s)
        {
            var sb = new System.Text.StringBuilder();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var roots = new System.Collections.Generic.List<string>();
            foreach (var parent in new[] { appData, local, home, docs })
            {
                try
                {
                    foreach (var d in Directory.GetDirectories(parent))
                    {
                        var n = Path.GetFileName(d).ToLowerInvariant();
                        if ((n.Contains("curse") || n.Contains("overwolf")) && !d.StartsWith(Path.Combine(local, "Programs"), StringComparison.OrdinalIgnoreCase))
                            roots.Add(d);
                    }
                }
                catch { }
            }

            sb.AppendLine("ForeverPad v" + Program.Version + " CurseForge probe");
            sb.AppendLine("WoW folder: " + s.WowDir);
            sb.AppendLine("Active profile: " + (s.IsReady ? Profiles.Active(s.WowDir) : "?"));
            sb.AppendLine("CurseForge running: " + CurseForgeApp.IsRunning);
            foreach (var p in Process.GetProcessesByName("CurseForge").Take(1))
            {
                try { sb.AppendLine("CurseForge exe: " + p.MainModule.FileName); } catch { }
            }
            sb.AppendLine();
            sb.AppendLine("Searched folders:");
            foreach (var r in roots) sb.AppendLine("  " + r);
            if (roots.Count == 0) sb.AppendLine("  (none found named *curse* or *overwolf*)");
            sb.AppendLine();

            string needle = Path.GetFileName(s.WowDir.TrimEnd('\\', '/')); // "_forever_"
            var ascii = System.Text.Encoding.ASCII.GetBytes(needle);
            var wide = System.Text.Encoding.Unicode.GetBytes(needle);
            int scanned = 0, hits = 0;
            sb.AppendLine("Files mentioning \"" + needle + "\" (size, last changed, path):");
            foreach (var r in roots)
            {
                foreach (var f in Walk(r, 0))
                {
                    try
                    {
                        var fi = new FileInfo(f);
                        if (fi.Length == 0 || fi.Length > 300L * 1024 * 1024) continue;
                        scanned++;
                        var bytes = File.ReadAllBytes(f);
                        if (IndexOf(bytes, ascii) >= 0 || IndexOf(bytes, wide) >= 0)
                        {
                            hits++;
                            sb.AppendLine("  " + (fi.Length / 1024) + " KB  " + fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm") + "  " + f);
                        }
                    }
                    catch { } // locked or unreadable: skip
                }
            }
            if (hits == 0) sb.AppendLine("  (none)");
            sb.AppendLine();
            sb.AppendLine(scanned + " files checked.");

            // Also: anything CurseForge may have left inside the WoW folder itself.
            sb.AppendLine();
            sb.AppendLine("Non-addon files directly in _forever_\\Interface and Interface\\AddOns:");
            foreach (var d in new[] { Path.Combine(s.WowDir, "Interface"), Path.Combine(s.WowDir, "Interface", "AddOns") })
            {
                try { foreach (var f in Directory.GetFiles(d)) sb.AppendLine("  " + f); } catch { }
            }
            return sb.ToString();
        }

        static System.Collections.Generic.IEnumerable<string> Walk(string dir, int depth)
        {
            string[] files = new string[0], dirs = new string[0];
            try { files = Directory.GetFiles(dir); } catch { }
            foreach (var f in files) yield return f;
            if (depth >= 8) yield break;
            try { dirs = Directory.GetDirectories(dir); } catch { }
            foreach (var d in dirs)
            {
                var n = Path.GetFileName(d).ToLowerInvariant();
                if (n == "cache" || n == "code cache" || n == "gpucache" || n == "logs" || n == "crashpad") continue;
                foreach (var f in Walk(d, depth + 1)) yield return f;
            }
        }

        static int IndexOf(byte[] hay, byte[] needle)
        {
            for (int i = 0; i <= hay.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && (hay[i + j] | 0x20) == (needle[j] | 0x20)) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }
    }
}
