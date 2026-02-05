using ScimitarBattery.Core;

namespace ScimitarBattery;

/// <summary>
/// No-op tray service used when the app runs without a tray icon.
/// </summary>
public sealed class NullTrayIconService : ITrayIconService
{
    public void UpdateBatteryState(string? deviceName, int? percent)
    {
        // Intentionally no-op.
    }
}
