// ForeverPad — tray toggle between a Normal and a Controller WoW Forever setup.
//
//   ForeverPad.exe                     tray icon (one instance)
//   ForeverPad.exe --boot              same, started with Windows: asks which setup to use
//   ForeverPad.exe --play controller   what the Steam shortcut runs: switch, launch,
//                                      wait for the game to close, switch back
//   ForeverPad.exe --play "<profile>"  same for any profile (case doesn't matter)
//   ForeverPad.exe --switch <profile>
//   ForeverPad.exe --uninstall         what "Apps & features" runs
// Run from anywhere else, it installs itself to %LOCALAPPDATA%\Programs\ForeverPad first.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("ForeverPad")]
[assembly: AssemblyProduct("ForeverPad")]
// AssemblyVersion comes from Version.g.cs, which build.bat writes.

namespace ForeverPad
{
    static class Program
    {
        public static string ExePath { get { return Application.ExecutablePath; } }
        public static System.Version AssemblyVersion { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        public static string Version
        {
            get { var v = AssemblyVersion; return v.Major + "." + v.Minor + "." + v.Build; }
        }

        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            string arg = args.Length > 1 ? string.Join(" ", args, 1, args.Length - 1) : "";

            if (mode == "--play")
            {
                var ps = Settings.Load();
                return Play(ps.IsReady ? Profiles.Resolve(ps.WowDir, arg, Profiles.Controller) : Profiles.Controller);
            }
            if (mode == "--switch")
            {
                try
                {
                    var ss = Settings.Load();
                    if (!ss.IsReady) throw new InvalidOperationException("Set your WoW Forever folder in Settings first.");
                    string want = Profiles.Resolve(ss.WowDir, arg, null);
                    if (want == null) throw new InvalidOperationException("There's no profile called \"" + arg + "\".");
                    Profiles.Switch(ss, want);
                    return 0;
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "ForeverPad"); return 1; }
            }

            if (mode == "--uninstall") return Installer.Uninstall();

            Installer.CleanupOld();
            if (!Installer.IsInstalledCopy && mode != "--portable") return Installer.InstallAndRelaunch();
            if (mode == "--installed" || mode == "--updated") Installer.Register();

            bool first;
            using (var m = new Mutex(true, @"Local\ForeverPad.Tray", out first))
            {
                if (!first)
                {
                    // Just installed or updated: the old copy is on its way out, wait for it.
                    bool got = false;
                    if (mode == "--installed" || mode == "--updated")
                    {
                        try { got = m.WaitOne(15000); }
                        catch (AbandonedMutexException) { got = true; } // old copy was stopped
                    }
                    if (!got)
                    {
                        MessageBox.Show("ForeverPad is already running. Its icon is in the taskbar tray (check the ^ overflow).", "ForeverPad");
                        return 0;
                    }
                }
                Application.Run(new TrayApp(mode));
                GC.KeepAlive(m);
            }
            return 0;
        }

        // ---- Steam launch: switch -> run game -> wait -> switch back ----
        static int Play(string profile)
        {
            var s = Settings.Load();
            if (!s.IsReady)
            {
                MessageBox.Show("ForeverPad doesn't know where WoW Forever is yet. Run ForeverPad on the PC once and set the folder in Settings.", "ForeverPad");
                return 1;
            }

            var icon = new NotifyIcon { Icon = Icons.For(profile, Profiles.IsPad(s.WowDir, profile)), Text = ("WoW Forever: " + profile + " session").Length > 63 ? "WoW Forever session" : "WoW Forever: " + profile + " session", Visible = true };
            int code = 0;
            var ctx = new ApplicationContext();
            var worker = new Thread(() =>
            {
                try { code = PlayWorker(s, profile); }
                catch (Exception ex) { code = 1; MessageBox.Show(ex.Message, "ForeverPad"); }
                finally { ctx.ExitThread(); }
            });
            worker.IsBackground = true;
            worker.Start();
            Application.Run(ctx);
            icon.Visible = false;
            icon.Dispose();
            return code;
        }

        static int PlayWorker(Settings s, string profile)
        {
            string before = Profiles.Active(s.WowDir);

            if (Profiles.GameRunning(s))
            {
                if (before != profile)
                {
                    MessageBox.Show("WoW Forever is already running in " + before + " mode. Close it and launch again.", "ForeverPad");
                    return 1;
                }
                // Already running in the right mode: keep Steam's session alive until it closes.
                WaitForGameExit(s);
                return 0;
            }

            Profiles.Switch(s, profile);

            bool viaBnet = s.UseBattleNet;
            bool bnetWasOpen = viaBnet && Profiles.BattleNetProcesses().Length > 0;
            bool started;
            if (viaBnet) started = Profiles.LaunchViaBattleNet(s, 120);
            else
            {
                Profiles.LaunchGame(s);
                var sw = Stopwatch.StartNew();
                while (!Profiles.GameRunning(s) && sw.Elapsed.TotalSeconds < 120) Thread.Sleep(500);
                started = Profiles.GameRunning(s);
            }

            if (started) WaitForGameExit(s);
            else MessageBox.Show("WoW Forever didn't start within 2 minutes." +
                (viaBnet ? "\n\nCheck Battle.net: it may want an update, a login, or the WoW Forever version picked in its dropdown." : ""),
                "ForeverPad");

            if (viaBnet)
            {
                // Close the Battle.net we started so Steam sees the session end.
                // If it was open before, reopen it outside Steam (via Explorer).
                Profiles.CloseBattleNet();
                if (bnetWasOpen)
                {
                    try { Process.Start("explorer.exe", "\"" + Settings.BattleNetExe() + "\""); } catch { }
                }
            }

            if (s.RestoreAfterPlay && before != profile)
            {
                for (int i = 0; ; i++)
                {
                    try { Profiles.Switch(s, before); break; }
                    catch (Exception)
                    {
                        if (i >= 9) throw;
                        Thread.Sleep(2000); // files can stay locked for a moment after exit
                    }
                }
            }
            return 0;
        }

        static void WaitForGameExit(Settings s)
        {
            // Require 10 quiet seconds in a row so a self-restart doesn't count as quitting.
            int quiet = 0;
            while (quiet < 10)
            {
                Thread.Sleep(1000);
                quiet = Profiles.GameRunning(s) ? 0 : quiet + 1;
            }
        }
    }
}
