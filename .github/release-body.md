## Changes in this release

- **Tray icon reliability**
  - First battery poll runs immediately on startup (no delay), so the tray shows the correct level sooner and avoids the "unknown" (diagonal) icon at launch.
  - When the device is asleep or briefly disconnected, the tray keeps showing the last known battery level instead of switching to the unknown icon until the next successful read.
- **Tray double-click**
  - Double-clicking the tray icon now opens Settings (in addition to right-click → Settings…).
- **Build / SDK**
  - `global.json` now uses `rollForward: latestMajor` so the project builds with .NET 8 or .NET 10 SDK, reducing version headaches on Windows and macOS.

Release assets are built by the [Release workflow](.github/workflows/release.yml).
