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
    private int? _lastBucket;
    private WindowIcon? _lastIcon;

    public AvaloniaTrayIconService(TrayIcon trayIcon)
    {
        _trayIcon = trayIcon ?? throw new ArgumentNullException(nameof(trayIcon));
    }

    public void UpdateBatteryState(string? deviceName, int? percent)
    {
        int? bucket = percent.HasValue ? (percent.Value / IconBucketStep) * IconBucketStep : null;
        if (_lastIcon == null || _lastBucket != bucket)
        {
            _lastIcon = BatteryIconFactory.CreateIcon(bucket);
            _lastBucket = bucket;
        }

        _trayIcon.Icon = _lastIcon;
        _trayIcon.ToolTipText = string.IsNullOrEmpty(deviceName)
            ? "Scimitar Battery"
            : percent.HasValue
                ? $"{deviceName}: {percent}%"
                : $"{deviceName}: n/a";
    }
}
