// ForeverPad — add a "WoW Forever (Controller)" non-Steam game to Steam.
//
// Non-Steam games live in Steam\userdata\<accountid>\config\shortcuts.vdf, a
// binary KeyValues file. Steam rewrites that file when it exits, so it must be
// closed while we edit it. Every existing entry is read and written back
// untouched; ours is added, or updated in place if it's already there.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace ForeverPad
{
    static class Steam
    {
        public const string ShortcutName = "WoW Forever (Controller)";
        public const string PlayArgs = "--play controller";

        public static string InstallDir
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    {
                        var p = k == null ? null : k.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return p.Replace('/', '\\');
                    }
                }
                catch { }
                foreach (var p in new[] { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam" })
                    if (Directory.Exists(p)) return p;
                return null;
            }
        }

        public static string ExePath
        {
            get { var d = InstallDir; return d == null ? null : Path.Combine(d, "steam.exe"); }
        }

        public static bool IsRunning { get { return Process.GetProcessesByName("steam").Length > 0; } }

        public static bool Shutdown(int timeoutMs)
        {
            if (!IsRunning) return true;
            var exe = ExePath;
            if (exe != null && File.Exists(exe)) Process.Start(exe, "-shutdown");
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (!IsRunning) { Thread.Sleep(1500); return true; } // let it finish writing files
                Thread.Sleep(500);
            }
            return !IsRunning;
        }

        public static void Start()
        {
            var exe = ExePath;
            if (exe != null && File.Exists(exe)) Process.Start(exe);
        }

        // The Steam account folders to write into: the most recently signed-in
        // account if we can tell, otherwise every account on this PC.
        public static List<string> UserConfigDirs()
        {
            var result = new List<string>();
            var dir = InstallDir;
            if (dir == null) return result;
            var userdata = Path.Combine(dir, "userdata");
            if (!Directory.Exists(userdata)) return result;

            var all = Directory.GetDirectories(userdata)
                .Where(d => Regex.IsMatch(Path.GetFileName(d), "^\\d+$") && Path.GetFileName(d) != "0")
                .ToList();

            try
            {
                var login = File.ReadAllText(Path.Combine(dir, "config", "loginusers.vdf"));
                var m = Regex.Match(login, "\"(\\d{17})\"\\s*\\{[^}]*?\"MostRecent\"\\s*\"1\"", RegexOptions.Singleline);
                if (m.Success)
                {
                    long id64 = long.Parse(m.Groups[1].Value);
                    string acct = (id64 - 76561197960265728L).ToString();
                    var hit = all.FirstOrDefault(d => Path.GetFileName(d) == acct);
                    if (hit != null) all = new List<string> { hit };
                }
            }
            catch { }

            foreach (var d in all) result.Add(Path.Combine(d, "config"));
            return result;
        }

        // Our shortcut's Exe value from the first account that has it, or null.
        public static string ShortcutExe()
        {
            try
            {
                foreach (var cfg in UserConfigDirs())
                {
                    string file = Path.Combine(cfg, "shortcuts.vdf");
                    if (!File.Exists(file)) continue;
                    var list = Vdf.Read(File.ReadAllBytes(file)).GetMap("shortcuts");
                    if (list == null) continue;
                    foreach (var kv in list.Items)
                    {
                        var m = kv.Value as Vdf.Map;
                        if (m != null && (m.GetString("AppName") ?? m.GetString("appname")) == ShortcutName)
                            return m.GetString("Exe") ?? m.GetString("exe");
                    }
                }
            }
            catch { }
            return null;
        }

        // True when a shortcut exists but launches some other copy of ForeverPad.
        public static bool ShortcutIsStale()
        {
            var exe = ShortcutExe();
            return exe != null && !string.Equals(exe.Trim('"'), Program.ExePath, StringComparison.OrdinalIgnoreCase);
        }

        // Adds or updates the shortcut. Returns the number of accounts written.
        public static int AddShortcut(Settings s)
        {
            if (IsRunning) throw new InvalidOperationException("Steam is still running.");
            var dirs = UserConfigDirs();
            if (dirs.Count == 0) throw new InvalidOperationException(
                "Couldn't find a Steam account on this PC. Sign in to Steam once, then try again.");

            string exe = "\"" + Program.ExePath + "\"";
            string startDir = "\"" + Path.GetDirectoryName(Program.ExePath) + "\"";
            string icon = File.Exists(s.GameExe) ? s.GameExe : Program.ExePath;
            int appid = (int)(Crc32(Encoding.UTF8.GetBytes(exe + ShortcutName)) | 0x80000000u);

            foreach (var cfg in dirs)
            {
                Directory.CreateDirectory(cfg);
                string file = Path.Combine(cfg, "shortcuts.vdf");
                Vdf.Map root;
                if (File.Exists(file))
                {
                    File.Copy(file, file + ".foreverpad-backup", true);
                    root = Vdf.Read(File.ReadAllBytes(file));
                }
                else root = new Vdf.Map();

                var list = root.GetMap("shortcuts");
                if (list == null) { list = new Vdf.Map(); root.Set("shortcuts", list); }

                // Reuse our existing entry if there is one.
                Vdf.Map entry = null;
                foreach (var kv in list.Items)
                {
                    var m = kv.Value as Vdf.Map;
                    if (m == null) continue;
                    var name = m.GetString("AppName") ?? m.GetString("appname");
                    var target = m.GetString("Exe") ?? m.GetString("exe") ?? "";
                    if (name == ShortcutName || target.IndexOf("ForeverPad.exe", StringComparison.OrdinalIgnoreCase) >= 0)
                    { entry = m; break; }
                }
                if (entry == null)
                {
                    int next = 0;
                    foreach (var kv in list.Items) { int n; if (int.TryParse(kv.Key, out n) && n >= next) next = n + 1; }
                    entry = new Vdf.Map();
                    list.Set(next.ToString(), entry);
                }

                entry.Set("appid", appid);
                entry.Set("AppName", ShortcutName);
                entry.Set("Exe", exe);
                entry.Set("StartDir", startDir);
                entry.Set("icon", icon);
                entry.SetDefault("ShortcutPath", "");
                entry.Set("LaunchOptions", PlayArgs);
                entry.SetDefault("IsHidden", 0);
                entry.Set("AllowDesktopConfig", 1);
                entry.Set("AllowOverlay", 1);
                entry.SetDefault("OpenVR", 0);
                entry.SetDefault("Devkit", 0);
                entry.SetDefault("DevkitGameID", "");
                entry.SetDefault("DevkitOverrideAppID", 0);
                entry.SetDefault("LastPlayTime", 0);
                entry.SetDefault("FlatpakAppID", "");
                if (entry.GetMap("tags") == null) entry.Set("tags", new Vdf.Map());

                File.WriteAllBytes(file, Vdf.Write(root));
            }
            return dirs.Count;
        }

        static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
            return ~crc;
        }
    }

    // Minimal binary KeyValues (VDF) reader/writer that round-trips every type.
    static class Vdf
    {
        const byte TMap = 0, TString = 1, TInt = 2, TFloat = 3, TPtr = 4, TWString = 5, TColor = 6, TUInt64 = 7, TEnd = 8, TInt64 = 10;

        public class Raw { public byte Type; public byte[] Bytes; }

        public class Map
        {
            public List<KeyValuePair<string, object>> Items = new List<KeyValuePair<string, object>>();

            int Index(string key)
            {
                for (int i = 0; i < Items.Count; i++) if (Items[i].Key == key) return i;
                return -1;
            }
            public void Set(string key, object value)
            {
                int i = Index(key);
                var kv = new KeyValuePair<string, object>(key, value);
                if (i >= 0) Items[i] = kv; else Items.Add(kv);
            }
            public void SetDefault(string key, object value) { if (Index(key) < 0) Set(key, value); }
            public object Get(string key) { int i = Index(key); return i >= 0 ? Items[i].Value : null; }
            public Map GetMap(string key) { return Get(key) as Map; }
            public string GetString(string key) { return Get(key) as string; }
        }

        public static Map Read(byte[] data)
        {
            int pos = 0;
            return ReadMap(data, ref pos);
        }

        static Map ReadMap(byte[] d, ref int pos)
        {
            var m = new Map();
            while (pos < d.Length)
            {
                byte t = d[pos++];
                if (t == TEnd) break;
                string key = ReadCString(d, ref pos);
                switch (t)
                {
                    case TMap: m.Items.Add(new KeyValuePair<string, object>(key, ReadMap(d, ref pos))); break;
                    case TString: m.Items.Add(new KeyValuePair<string, object>(key, ReadCString(d, ref pos))); break;
                    case TInt: m.Items.Add(new KeyValuePair<string, object>(key, BitConverter.ToInt32(d, pos))); pos += 4; break;
                    case TFloat: case TPtr: case TColor: m.Items.Add(new KeyValuePair<string, object>(key, Take(d, ref pos, t, 4))); break;
                    case TUInt64: case TInt64: m.Items.Add(new KeyValuePair<string, object>(key, Take(d, ref pos, t, 8))); break;
                    default: throw new InvalidDataException("Unknown VDF type " + t + " in shortcuts.vdf");
                }
            }
            return m;
        }

        static Raw Take(byte[] d, ref int pos, byte t, int n)
        {
            var b = new byte[n];
            Array.Copy(d, pos, b, 0, n);
            pos += n;
            return new Raw { Type = t, Bytes = b };
        }

        static string ReadCString(byte[] d, ref int pos)
        {
            int start = pos;
            while (pos < d.Length && d[pos] != 0) pos++;
            string s = Encoding.UTF8.GetString(d, start, pos - start);
            pos++; // skip NUL
            return s;
        }

        public static byte[] Write(Map root)
        {
            var ms = new MemoryStream();
            WriteMap(ms, root);
            ms.WriteByte(TEnd); // closes the implicit root
            return ms.ToArray();
        }

        static void WriteMap(Stream s, Map m)
        {
            foreach (var kv in m.Items)
            {
                var v = kv.Value;
                if (v is Map) { s.WriteByte(TMap); CStr(s, kv.Key); WriteMap(s, (Map)v); s.WriteByte(TEnd); }
                else if (v is string) { s.WriteByte(TString); CStr(s, kv.Key); CStr(s, (string)v); }
                else if (v is int) { s.WriteByte(TInt); CStr(s, kv.Key); s.Write(BitConverter.GetBytes((int)v), 0, 4); }
                else if (v is Raw) { var r = (Raw)v; s.WriteByte(r.Type); CStr(s, kv.Key); s.Write(r.Bytes, 0, r.Bytes.Length); }
            }
        }

        static void CStr(Stream s, string str)
        {
            var b = Encoding.UTF8.GetBytes(str);
            s.Write(b, 0, b.Length);
            s.WriteByte(0);
        }
    }
}
