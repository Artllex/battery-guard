# Installer verification

Built with Inno Setup 7.1.0. Tested on Windows 11 on 2026-09-26.

- Per-user silent installation in a dedicated test directory: exit 0.
- Windows uninstall registration: BatteryGuard, version 0.1.0, application icon, normal and quiet uninstall commands.
- Installed application self-test: exit 0; all three embedded icons, threshold boundaries, alarm transitions passed.
- Uninstall invoked via the command registered for Windows Installed apps while the installed monitor was running: exit 0.
- Verified removal of application, uninstall registry entry, startup shortcut and the app's VBS startup entry; installed process stopped.
- Restored portable monitor after the test. Test installation is removed.

The Windows Settings interface was not visually inspected. Its underlying uninstall registration and command were tested. Installer and application use the full red battery as their executable icon; the tray continues to select one of three icons by battery level.
