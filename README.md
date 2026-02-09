# Scimitar Battery Monitor (Avalonia Tray App)

Designed for **Corsair Scimitar RGB Elite Wireless** (Windows + iCUE SDK).

A tray utility that shows Corsair mouse (e.g. Scimitar) battery level via the iCUE SDK. Built with **Avalonia** for cross-platform UI.

**macOS status:** the app UI and tray run on macOS and will attempt to enumerate Corsair HID devices via `ioreg`. Battery reporting is best-effort and may return `n/a` depending on the device/firmware.

## Architecture

- **ScimitarBattery.Core** – Platform-agnostic logic: interfaces (`IBatteryProvider`, `IDeviceEnumerator`, `INotifier`, `ITrayIconService`), settings, config persistence, polling, and state machine (Normal/Low/Critical). No Windows-only APIs.
- **ScimitarBattery.Adapters.Windows** – Windows-only: Corsair iCUE SDK P/Invoke and implementations of `IBatteryProvider`, `IDeviceEnumerator`, and `INotifier` (stub). Add a macOS adapter later by implementing the same interfaces.
- **ScimitarBattery** – Avalonia UI: tray icon (battery fill + tooltip), NativeMenu (Settings…, Exit), settings window, and wiring to Core + Windows adapter. On Windows, a small **starter window** appears once at launch (“Running in system tray – close this window”) so the Win32 message loop initializes; closing it leaves the app running in the tray only.

## Requirements


- Windows (iCUE installed and running, SDK enabled)
- Corsair iCUE SDK DLL (see below)
- .NET 8 (to build locally)

This app is tuned for the Scimitar RGB Elite Wireless LED layout (Logo + Side).
Other Corsair devices may enumerate LEDs differently and are not guaranteed to work.

## Troubleshooting

- **“iCUE SDK not available”**: ensure iCUE is running and SDK is enabled in iCUE settings (Settings➡️SDK➡️Enable iCUE SDK).
- **Ensure Device Memory Mode is Disabled**
- **No LEDs**: pick the correct device and test LED from Settings.
- **Logo vs Side LEDs**: use the “LEDs to test” list to target Logo or Side explicitly.

## Portable release (recommended)

This project distributes portable ZIP releases:

1. Download the latest `ScimitarBattery-win-x64.zip` from [Releases](https://github.com/pHarmG/ScimitarBattery/releases).
2. Extract it to any folder.
3. Run `ScimitarBattery.exe`.

If Windows shows SmartScreen warning, click **More info** → **Run anyway**.

For macOS:

1. Download `ScimitarBattery-osx-arm64.zip` (Apple Silicon) or `ScimitarBattery-osx-x64.zip` (Intel).
2. Extract the ZIP (contains a single `ScimitarBattery.app`).
3. Run:
   ```bash
   xattr -dr com.apple.quarantine ScimitarBattery.app
   open ScimitarBattery.app
   ```
4. If blocked, approve once in **System Settings → Privacy & Security → Open Anyway**.

Note: macOS builds are currently unsigned/not notarized with Apple Developer ID.

### Start with Windows (optional)

Open Settings and enable **Start with Windows (background)**.
This registers the app to start in the tray on login.

If you move the folder, re‑toggle the setting so the startup path is updated.

## Run in development

```bash
dotnet run --project ScimitarBattery
```

Or use the repo helper that prefers Homebrew `.NET 8` (`dotnet@8`) when present:

```bash
./scripts/build.sh
```

A small “Scimitar Battery is running in the system tray” window appears once; close it to keep the app running in the tray only (no main window). Ensure iCUE is running and the SDK is enabled, or the tray tooltip will show "iCUE not available".

If the app exits immediately or nothing appears, check for an error message box. On startup failure, the app also writes `%AppData%\ScimitarBattery\startup-error.txt` with the exception details. If you see platform or rendering errors, try a **self-contained** publish (`--self-contained true`).

## Run on macOS (experimental)

```bash
dotnet run --project ScimitarBattery -f net8.0
```

If no compatible devices are detected, the settings UI will show "No compatible devices detected."

## Publish for Windows

```bash
dotnet publish ScimitarBattery/ScimitarBattery.csproj -c Release -r win-x64 --self-contained false -o publish
```

Or self-contained:

```bash
dotnet publish ScimitarBattery/ScimitarBattery.csproj -c Release -r win-x64 --self-contained true -o publish
```

Output is in `publish/`. Run `ScimitarBattery.exe` from there.

## Publish for macOS (experimental)

Apple Silicon:

```bash
dotnet publish ScimitarBattery/ScimitarBattery.csproj -c Release -f net8.0 -r osx-arm64 --self-contained true -o publish/osx-arm64
```

Intel:

```bash
dotnet publish ScimitarBattery/ScimitarBattery.csproj -c Release -f net8.0 -r osx-x64 --self-contained true -o publish/osx-x64
```

## Build the portable ZIP (maintainers)

```powershell
.\scripts\publish-zip.ps1
```

Output: `artifacts\ScimitarBattery-win-x64.zip`

## GitHub Release (maintainers)

The release workflow lives at `scripts/release-workflow.yml`.
To enable it on GitHub, copy it to `.github/workflows/release.yml` and push (requires workflow scope).

Then push a tag like `v1.0.0` and GitHub Actions will attach the ZIP automatically.

## Config file

- **Location:** `%AppData%\ScimitarBattery\settings.json`
- **Keys:**
  - `DeviceKey` – Stable device identifier (e.g. `Corsair:<id>` on Windows). Empty or missing = auto-detect first Scimitar/first mouse.
  - `DeviceDisplayName` – Human-readable name for tooltip.
  - `PollingIntervalSeconds` – Poll interval (1–3600). Default 240.
  - `LowThresholdPercent` – Low battery threshold (0–100). Default 30.
  - `CriticalThresholdPercent` – Critical threshold (0–100). Default 15.

First run: if the file does not exist, defaults are poll=240s, low=30%, critical=15%, and the first compatible device is chosen.

## Corsair SDK DLL

Place **iCUESDK.x64_2019.dll** in the app output directory (e.g. next to `ScimitarBattery.exe`). The project already copies it from:

- `ScimitarBattery/iCUESDK.x64_2019.dll` (app project), or
- `ScimitarBattery.Adapters.Windows/iCUESDK.x64_2019.dll` (adapter project)

when building. If you obtain the DLL from Corsair’s SDK, put it in `ScimitarBattery/` or `ScimitarBattery.Adapters.Windows/` and ensure it is set to **Copy to output directory** (e.g. via `<None Update="iCUESDK.x64_2019.dll" CopyToOutputDirectory="PreserveNewest" />`).

## Behaviour

- **Tray:** Battery outline icon with fill proportional to charge (updates on bucket steps; tooltip shows exact percent).
- **Tooltip:** `"<DeviceName>: <Percent>%"` or `"<DeviceName>: n/a"` if unknown/missing.
- **Menu:** Settings… (opens settings window), Exit (shuts down cleanly).
- **Settings:** Device dropdown (from enumerator), poll interval, low/critical thresholds; Save persists to config and restarts polling.
- **Unplug / device off:** App keeps running; tray shows unknown/missing state without crashing.

## macOS adapter TODO

- Improve HID matching (per-device serial/location keys).
- Add macOS notification support using UserNotifications.
