using Avalonia.Controls;
using ScimitarBattery.Core;

namespace ScimitarBattery;

/// <summary>
/// Allows monitor updates to start before the actual tray icon is created.
/// Replays the most recent battery state when a tray icon is attached.
/// </summary>
public sealed class DeferredTrayIconService : ITrayIconService
{
    private readonly object _gate = new();
    private ITrayIconService _inner = new NullTrayIconService();
    private string? _lastDeviceName;
    private int? _lastPercent;
    private bool _hasState;

    public void AttachTrayIcon(TrayIcon? trayIcon)
    {
        lock (_gate)
        {
            _inner = trayIcon != null
                ? new AvaloniaTrayIconService(trayIcon)
                : new NullTrayIconService();

            if (_hasState)
                _inner.UpdateBatteryState(_lastDeviceName, _lastPercent);
        }
    }

    public void UpdateBatteryState(string? deviceName, int? percent)
    {
        lock (_gate)
        {
            _lastDeviceName = deviceName;
            _lastPercent = percent;
            _hasState = true;
            _inner.UpdateBatteryState(deviceName, percent);
        }
    }
}
