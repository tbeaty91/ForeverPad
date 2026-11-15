// ForeverPad — self-update from GitHub releases.
//
// Asks the GitHub API for the latest release of Repo, compares its tag (vX.Y.Z)
// with this build's version, and if it's newer downloads the ForeverPad.exe
// attached to it. The swap: rename the running exe to ForeverPad.old.exe
// (Windows allows renaming a running exe), put the new one in its place, start
// it with --updated, exit. The new copy deletes the .old file on start, so the
// Steam shortcut and the startup entry keep pointing at the same path.
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace ForeverPad
{
    class UpdateInfo
    {
        public Version Version;
        public string Tag;
        public string Url;
        public string Notes;
    }

    static class Updater
    {
        public const string Repo = "tbeaty91/ForeverPad";
        public static string ReleasesPage { get { return "https://github.com/" + Repo + "/releases"; } }

        static WebClient Client()
        {
            // .NET Framework 4.x defaults can predate TLS 1.2, which GitHub requires.
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            var wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "ForeverPad-updater";
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return wc;
        }

        // Returns the newer release, or null if this build is current. Throws on network errors.
        public static UpdateInfo Check()
        {
            string json;
            using (var wc = Client())
                json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest");

            var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            var url = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*/ForeverPad\\.exe)\"");
            if (!tag.Success || !url.Success) return null;

            Version v;
            if (!Version.TryParse(tag.Groups[1].Value.TrimStart('v', 'V'), out v)) return null;
            if (v <= Normalize(Program.AssemblyVersion)) return null;

            var body = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return new UpdateInfo
            {
                Version = v,
                Tag = tag.Groups[1].Value,
                Url = url.Groups[1].Value,
                Notes = body.Success ? Regex.Unescape(body.Groups[1].Value) : "",
            };
        }

        // 1.2.3.0 and 1.2.3 must compare equal.
        static Version Normalize(Version v)
        {
            return new Version(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));
        }

        // Download, swap, restart. On success the caller should exit right away.
        public static void Apply(UpdateInfo u)
        {
            string exe = Program.ExePath;
            string dir = Path.GetDirectoryName(exe);
            string fresh = Path.Combine(dir, "ForeverPad.new.exe");
            string old = Path.Combine(dir, "ForeverPad.old.exe");

            using (var wc = Client())
            {
                wc.Headers[HttpRequestHeader.Accept] = "application/octet-stream";
                wc.DownloadFile(u.Url, fresh);
            }

            // Sanity check: a Windows program starts with "MZ" and isn't tiny.
            var head = new byte[2];
            using (var f = File.OpenRead(fresh))
            {
                if (f.Length < 20000 || f.Read(head, 0, 2) != 2 || head[0] != 'M' || head[1] != 'Z')
                {
                    f.Close();
                    File.Delete(fresh);
                    throw new InvalidDataException("The downloaded file isn't a Windows program. Nothing was changed.");
                }
            }

            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            try { File.Move(fresh, exe); }
            catch { File.Move(old, exe); throw; }

            Process.Start(new ProcessStartInfo(exe, "--updated") { UseShellExecute = false });
        }
    }
}
