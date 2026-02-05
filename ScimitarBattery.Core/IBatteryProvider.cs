namespace ScimitarBattery.Core;

/// <summary>
/// Provides battery percent for a selected device. Implemented per platform (e.g. Corsair SDK on Windows).
/// </summary>
public interface IBatteryProvider
{
    /// <summary>
    /// Reads battery level (0–100) for the given device key. Returns null if unavailable or on error.
    /// </summary>
    int? GetBatteryPercent(string deviceKey);
}
