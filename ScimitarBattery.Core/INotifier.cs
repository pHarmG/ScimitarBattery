namespace ScimitarBattery.Core;

/// <summary>
/// Optional notifications (e.g. low battery toast). Implemented per platform; stub acceptable.
/// </summary>
public interface INotifier
{
    void NotifyLowBattery(string deviceDisplayName, int batteryPercent, BatterySeverity severity, NotificationRoute route);
}
