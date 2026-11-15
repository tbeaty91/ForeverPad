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
