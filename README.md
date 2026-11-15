# ForeverPad

A Windows tray toggle that switches **WoW Forever** between two setups:

- **Normal**: your usual `Interface` (addons), `WTF` (settings, keybinds, saved variables) and `Cache`.
- **Controller**: starts out blank (no addons, no keybinds, gamepad on) and keeps whatever you add to it.

It also adds a Steam shortcut, **WoW Forever (Controller)**. Launching that shortcut from Steam Link (for example on an Apple TV with a Bluetooth controller) switches WoW to Controller mode, then restarts Battle.net under Steam and launches WoW Forever through it (launch code `WoWF`). Battle.net logs you in, and because WoW is part of the Steam launch, Steam Input still reads your controller. When you quit, it closes that Battle.net, reopens it normally if it was open before, and switches back to Normal. Clear the launch code in Settings to start the game exe directly instead (that asks for a login).

## More profiles

Normal and Controller are always there. **+ New profile…** in the tray popup makes more. A new profile can start **blank** or as a **copy** of any profile's addons, keybinds and settings. Use a copy to try new addons, because the original is only read and never changes. Each profile has its own colour in the tray and can turn gamepad mode on or off. **Manage…** switches, duplicates, renames, deletes (to the Recycle Bin) and sets gamepad mode. Normal and Controller can't be renamed or deleted. `--play "<profile>"` and `--switch <profile>` take any profile name.

## CurseForge

CurseForge works out your installed addons by scanning `Interface\AddOns` when it starts. If it's open during a switch, ForeverPad closes it first and reopens it afterwards, so it rescans and shows the new profile's addons. You can turn this off in Settings. Each profile's addons are managed separately: updating in CurseForge only changes the active profile.

## How switching works

The live `_forever_` folder always holds the active profile's real folders. The inactive profile is parked in `World of Warcraft\_forever__profiles\<Name>\`. A switch renames those folders on the same drive, so it's instant and nothing gets deleted. If any rename fails, the ones already done are undone. You can't switch while the game is running.

The first switch to Controller creates the blank slate. By default it copies Normal's `Config.wtf` (login, graphics, sound), then sets `GamePadEnable 1`. You can turn the copy off in Settings.

## Install and update

Run the downloaded `ForeverPad.exe` from anywhere. It copies itself to `%LOCALAPPDATA%\Programs\ForeverPad`, adds a Start Menu shortcut and an Apps & features entry (for uninstalling), starts from there and says so in a tray notification. You can delete the downloaded copy afterwards. Running a newer download installs over the old copy.

It checks this repo's GitHub releases 15 seconds after starting and every 6 hours after that, and offers new versions from a tray notification or from **Check for updates** in the menu. Updating swaps the exe in place, so Steam and Windows startup keep working. Releases come from pushing a `vX.Y.Z` tag. CI builds and attaches `ForeverPad.exe`, and the repo must be public for the updater to see them.

## Use

1. Run `ForeverPad.exe`. It starts with Windows from then on and asks Normal or Controller at each boot (turn that off in the menu or Settings). It finds `World of Warcraft\_forever_` on its own. If it can't, it opens Settings so you can pick the folder.
2. Left-click the tray icon to open the toggle. Right-click it for the menu.
3. Open **Steam setup… → Add to Steam**. ForeverPad closes Steam if needed, then writes the shortcut and keeps a `shortcuts.vdf.foreverpad-backup`.

Command line: `ForeverPad.exe --play controller` (what Steam runs) and `ForeverPad.exe --switch normal|controller`.

## Build

Run `build.bat` on any Windows 10/11 PC. It uses the C# compiler that ships with Windows, so there's nothing to install. CI builds the exe on every push. Tagging `vX.Y.Z` publishes a release, which installed copies pick up as an update. A local `build.bat` is stamped 0.0.0, so it offers the latest release.
