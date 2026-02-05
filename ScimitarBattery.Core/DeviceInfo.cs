namespace ScimitarBattery.Core;

/// <summary>
/// Platform-agnostic device info. DeviceKey is the stable identifier (e.g. "Corsair:id" on Windows).
/// </summary>
public sealed class DeviceInfo
{
    public string DeviceKey { get; }
    public string DisplayName { get; }
    public int? BatteryPercent { get; }

    public DeviceInfo(string deviceKey, string displayName, int? batteryPercent = null)
    {
        DeviceKey = deviceKey ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        BatteryPercent = batteryPercent;
    }
}
