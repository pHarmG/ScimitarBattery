using Avalonia.Controls;
using ScimitarBattery.Core;

namespace ScimitarBattery;

/// <summary>
/// Implements ITrayIconService using Avalonia TrayIcon. Updates icon (bucketed) and tooltip (exact percent) on UI thread.
/// </summary>
public sealed class AvaloniaTrayIconService : ITrayIconService
{
    private const int IconBucketStep = 10;
    private readonly TrayIcon _trayIcon;

    public AvaloniaTrayIconService(TrayIcon trayIcon)
    {
        _trayIcon = trayIcon ?? throw new ArgumentNullException(nameof(trayIcon));
    }

    public void UpdateBatteryState(string? deviceName, int? percent)
    {
        int? bucket = percent.HasValue ? (percent.Value / IconBucketStep) * IconBucketStep : null;
        _trayIcon.Icon = BatteryIconFactory.CreateIcon(bucket);
        _trayIcon.ToolTipText = string.IsNullOrEmpty(deviceName)
            ? "Scimitar Battery"
            : percent.HasValue
                ? $"{deviceName}: {percent}%"
                : $"{deviceName}: n/a";
    }
}
