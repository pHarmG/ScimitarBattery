## Changes in this release

- **Tray icon reliability**
  - First battery poll runs immediately on startup (no delay), so the tray shows the correct level sooner and avoids the "unknown" (diagonal) icon at launch.
  - When the device is asleep or briefly disconnected, the tray keeps showing the last known battery level instead of switching to the unknown icon until the next successful read.
  - After wake-from-sleep, a single bogus low reading from the SDK is ignored (suspicious drop); the tray keeps showing the previous level until the next poll returns a plausible value, so the correct level comes back without opening Settings.
- **Tray double-click**
  - Double-clicking the tray icon now opens Settings (in addition to right-click → Settings…).
- **Battery % in Settings**
  - Fixed regression where battery percent disappeared in the Settings device list (and sometimes stayed broken). The provider no longer invalidates the SDK connection on a failed read, so one temporary failure (e.g. device sleeping) doesn’t break the session for all callers.
- **Build / SDK**
  - `global.json` now uses `rollForward: latestMajor` so the project builds with .NET 8 or .NET 10 SDK, reducing version headaches on Windows and macOS.

Release assets are built by the [Release workflow](.github/workflows/release.yml).
