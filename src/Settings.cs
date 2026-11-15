// ForeverPad — settings stored as key=value in %APPDATA%\ForeverPad\settings.ini
// C# 5 only: this file must compile with the csc.exe that ships with Windows.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace ForeverPad
{
    class Settings
    {
        public string WowDir = "";          // the _forever_ folder
        public string GameExe = "";         // full path to the game exe (auto-detected)
        public string LaunchArgs = "";      // extra args for the game exe
        public bool RestoreAfterPlay = true; // after a Steam session, switch back to Normal
        public bool CopyConfigToController = true; // seed controller Config.wtf from Normal

        public static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ForeverPad"); }
        }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (File.Exists(FilePath))
                {
                    foreach (var raw in File.ReadAllLines(FilePath))
                    {
                        int eq = raw.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = raw.Substring(0, eq).Trim(), v = raw.Substring(eq + 1).Trim();
                        if (k == "WowDir") s.WowDir = v;
                        else if (k == "GameExe") s.GameExe = v;
                        else if (k == "LaunchArgs") s.LaunchArgs = v;
                        else if (k == "RestoreAfterPlay") s.RestoreAfterPlay = v == "1";
                        else if (k == "CopyConfigToController") s.CopyConfigToController = v == "1";
                    }
                }
            }
            catch { }
            if (!IsWowDir(s.WowDir)) s.WowDir = DetectWowDir() ?? "";
            if (s.WowDir != "" && !File.Exists(s.GameExe)) s.GameExe = DetectGameExe(s.WowDir) ?? "";
            return s;
        }

        public void Save()
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllLines(FilePath, new[] {
                "WowDir=" + WowDir,
                "GameExe=" + GameExe,
                "LaunchArgs=" + LaunchArgs,
                "RestoreAfterPlay=" + (RestoreAfterPlay ? "1" : "0"),
                "CopyConfigToController=" + (CopyConfigToController ? "1" : "0"),
            });
        }

        public bool IsReady { get { return IsWowDir(WowDir) && File.Exists(GameExe); } }

        // A game folder is one that holds a game exe (WTF/Interface may not exist yet).
        public static bool IsWowDir(string dir)
        {
            return !string.IsNullOrEmpty(dir) && Directory.Exists(dir) && DetectGameExe(dir) != null;
        }

        public static string DetectWowDir()
        {
            var roots = new List<string>();
            foreach (var key in new[] {
                @"SOFTWARE\WOW6432Node\Blizzard Entertainment\World of Warcraft",
                @"SOFTWARE\Blizzard Entertainment\World of Warcraft" })
            {
                try
                {
                    using (var k = Registry.LocalMachine.OpenSubKey(key))
                    {
                        var p = k == null ? null : k.GetValue("InstallPath") as string;
                        if (!string.IsNullOrEmpty(p))
                        {
                            p = p.TrimEnd('\\', '/');
                            // InstallPath usually points at _retail_; the base folder is its parent.
                            if (Path.GetFileName(p).StartsWith("_")) p = Path.GetDirectoryName(p);
                            roots.Add(p);
                        }
                    }
                }
                catch { }
            }
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                    string r = d.RootDirectory.FullName;
                    roots.Add(Path.Combine(r, @"Program Files (x86)\World of Warcraft"));
                    roots.Add(Path.Combine(r, @"Program Files\World of Warcraft"));
                    roots.Add(Path.Combine(r, @"World of Warcraft"));
                    roots.Add(Path.Combine(r, @"Games\World of Warcraft"));
                    roots.Add(Path.Combine(r, @"Battle.net\World of Warcraft"));
                }
                catch { }
            }
            foreach (var root in roots)
            {
                var f = Path.Combine(root, "_forever_");
                if (IsWowDir(f)) return f;
            }
            return null;
        }

        // The game exe is the biggest Wow*.exe in the folder that isn't a helper tool.
        public static string DetectGameExe(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return null;
                var bad = new[] { "error", "crash", "launcher", "uninstall", "repair", "setup", "update", "agent" };
                return Directory.GetFiles(dir, "*.exe")
                    .Where(f =>
                    {
                        var n = Path.GetFileName(f).ToLowerInvariant();
                        return n.StartsWith("wow") && !bad.Any(b => n.Contains(b));
                    })
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        // ---- Start with Windows (HKCU Run key) ----
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool StartWithWindows
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue("ForeverPad") != null;
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue("ForeverPad", "\"" + Program.ExePath + "\"");
                    else k.DeleteValue("ForeverPad", false);
                }
            }
        }
    }
}
