namespace ScimitarBattery.Core;

/// <summary>
/// Tray icon updates (icon bitmap + tooltip). Implemented by the UI layer (e.g. Avalonia TrayIcon).
/// </summary>
public interface ITrayIconService
{
    /// <summary>
    /// Updates tray icon fill and tooltip. percent is 0–100 or null for unknown/missing device.
    /// Tooltip should show: "&lt;deviceName&gt;: &lt;percent&gt;%" or "&lt;deviceName&gt;: n/a".
    /// </summary>
    void UpdateBatteryState(string? deviceName, int? percent);
}
