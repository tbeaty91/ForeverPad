# ForeverPad

A Windows tray toggle that switches **WoW Forever** between two setups:

- **Normal**: your usual `Interface` (addons), `WTF` (settings, keybinds, saved variables) and `Cache`.
- **Controller**: starts out blank (no addons, no keybinds, gamepad on) and keeps whatever you add to it.

It also adds a Steam shortcut, **WoW Forever (Controller)**. Launching that shortcut from Steam Link (for example on an Apple TV with a Bluetooth controller) switches WoW to Controller mode, runs the game under Steam Input, and switches back to Normal when you quit.

## How switching works

The live `_forever_` folder always holds the active profile's real folders. The inactive profile is parked in `World of Warcraft\_forever__profiles\<Name>\`. A switch renames those folders on the same drive, so it's instant and nothing gets deleted. If any rename fails, the ones already done are undone. You can't switch while the game is running.

The first switch to Controller creates the blank slate. By default it copies Normal's `Config.wtf` (login, graphics, sound), then sets `GamePadEnable 1`. You can turn the copy off in Settings.

## Use

1. Run `ForeverPad.exe`. It finds `World of Warcraft\_forever_` on its own. If it can't, it opens Settings so you can pick the folder.
2. Left-click the tray icon to open the toggle. Right-click it for the menu.
3. Open **Steam setup… → Add to Steam**. ForeverPad closes Steam if needed, then writes the shortcut and keeps a `shortcuts.vdf.foreverpad-backup`.

Command line: `ForeverPad.exe --play controller` (what Steam runs) and `ForeverPad.exe --switch normal|controller`.

## Build

Run `build.bat` on any Windows 10/11 PC. It uses the C# compiler that ships with Windows, so there's nothing to install. CI builds the exe on every push, and `v*` tags publish a release.
