// ForeverPad — install into %LOCALAPPDATA%\Programs\ForeverPad, and uninstall.
//
// Running a downloaded ForeverPad.exe from anywhere copies it into the install
// folder, adds a Start Menu shortcut and an "Apps & features" entry, then
// starts the installed copy and exits. Running a newer download over an older
// install replaces it. Uninstall never touches WoW: both profiles stay where
// they are, and it switches back to Normal first.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ForeverPad
{
    static class Installer
    {
        public static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ForeverPad"); }
        }
        public static string Exe { get { return Path.Combine(Dir, "ForeverPad.exe"); } }
        static string StartMenuLink
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "ForeverPad.lnk"); }
        }
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ForeverPad";

        public static bool IsInstalledCopy
        {
            get { return string.Equals(Path.GetFullPath(Program.ExePath), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase); }
        }

        // Other ForeverPad processes (not this one).
        public static Process[] Others()
        {
            int me = Process.GetCurrentProcess().Id;
            return Process.GetProcessesByName("ForeverPad").Where(p => p.Id != me).ToArray();
        }

        static void StopOthers()
        {
            foreach (var p in Others()) { try { p.Kill(); p.WaitForExit(5000); } catch { } }
        }

        // Copy this exe into the install folder and start it. Returns the exit code.
        public static int InstallAndRelaunch()
        {
            if (Profiles.GameRunning(Settings.Load()))
            {
                MessageBox.Show("Close WoW Forever before installing or updating ForeverPad.", "ForeverPad");
                return 1;
            }
            try
            {
                StopOthers(); // an older installed copy may be running in the tray
                Directory.CreateDirectory(Dir);
                if (File.Exists(Exe))
                {
                    // Renaming a running exe is allowed; overwriting it isn't.
                    string old = Path.Combine(Dir, "ForeverPad.old.exe");
                    try { if (File.Exists(old)) File.Delete(old); } catch { }
                    try { File.Move(Exe, old); } catch { }
                }
                File.Copy(Program.ExePath, Exe, true);
                Register();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't install ForeverPad: " + ex.Message, "ForeverPad");
                return 1;
            }
            Process.Start(new ProcessStartInfo(Exe, "--installed") { UseShellExecute = false });
            return 0;
        }

        // Start Menu shortcut + Apps & features entry. Safe to repeat.
        public static void Register()
        {
            try { MakeShortcut(StartMenuLink, Exe, "Switch WoW Forever between Normal and Controller setups"); } catch { }
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(UninstallKey))
                {
                    k.SetValue("DisplayName", "ForeverPad");
                    k.SetValue("DisplayVersion", Program.Version);
                    k.SetValue("Publisher", "ForeverPad");
                    k.SetValue("DisplayIcon", Exe);
                    k.SetValue("InstallLocation", Dir);
                    k.SetValue("UninstallString", "\"" + Exe + "\" --uninstall");
                    k.SetValue("NoModify", 1);
                    k.SetValue("NoRepair", 1);
                }
            }
            catch { }
            if (Settings.StartWithWindows) Settings.StartWithWindows = true; // re-point at the installed exe
        }

        // Late-bound WScript.Shell, so no COM interop assembly is needed.
        static void MakeShortcut(string lnkPath, string target, string description)
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
            var lt = lnk.GetType();
            lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { target });
            lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { Path.GetDirectoryName(target) });
            lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { description });
            lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { target + ",0" });
            lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
        }

        // Leftover from an update or reinstall: delete it once it's no longer running.
        public static void CleanupOld()
        {
            try
            {
                var old = Path.Combine(Path.GetDirectoryName(Program.ExePath), "ForeverPad.old.exe");
                for (int i = 0; i < 10 && File.Exists(old); i++)
                {
                    try { File.Delete(old); } catch { Thread.Sleep(500); }
                }
            }
            catch { }
        }

        public static int Uninstall()
        {
            var s = Settings.Load();
            var ok = MessageBox.Show(
                "Uninstall ForeverPad?\n\nYour WoW folders are left alone. If another profile is active it switches back to Normal first. " +
                "Your other profiles stay in the _forever__profiles folder (and Normal's backups in _forever__backups) next to _forever_, so you can delete them yourself if you want." +
                "\n\nThe \"" + Steam.ShortcutName + "\" entry in Steam stays too; remove it from Steam if you don't want it.",
                "ForeverPad", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (ok != DialogResult.OK) return 1;

            if (s.IsReady && Profiles.Active(s.WowDir) != Profiles.Normal)
            {
                try { Profiles.Switch(s, Profiles.Normal); }
                catch (Exception ex)
                {
                    MessageBox.Show("Couldn't switch back to Normal, so nothing was uninstalled:\n\n" + ex.Message, "ForeverPad");
                    return 1;
                }
            }

            StopOthers();
            try { Settings.StartWithWindows = false; } catch { }
            try { File.Delete(StartMenuLink); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
            try { Directory.Delete(Settings.Dir, true); } catch { }

            MessageBox.Show("ForeverPad is uninstalled.", "ForeverPad");

            // This exe can't delete its own folder while it runs: let cmd do it after we exit.
            var psi = new ProcessStartInfo("cmd.exe",
                "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + Dir + "\"");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WorkingDirectory = Path.GetTempPath();
            try { Process.Start(psi); } catch { }
            return 0;
        }
    }
}
