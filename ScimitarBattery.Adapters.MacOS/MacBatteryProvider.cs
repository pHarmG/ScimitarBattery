using ScimitarBattery.Core;

namespace ScimitarBattery.Adapters.MacOS;

/// <summary>
/// macOS battery provider stub. Implement with IOKit or powerd parsing.
/// </summary>
public sealed class MacBatteryProvider : IBatteryProvider
{
    public int? GetBatteryPercent(string deviceKey)
    {
        return null;
    }
}
