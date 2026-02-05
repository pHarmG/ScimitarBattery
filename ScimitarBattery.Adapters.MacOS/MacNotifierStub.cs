using ScimitarBattery.Core;

namespace ScimitarBattery.Adapters.MacOS;

/// <summary>
/// macOS notifier stub. Implement with UserNotifications framework.
/// </summary>
public sealed class MacNotifierStub : INotifier
{
    public static bool IsAvailable => false;

    public void NotifyLowBattery(string deviceDisplayName, int batteryPercent, BatterySeverity severity, NotificationRoute route)
    {
        // No-op until macOS notifications are implemented.
    }
}
