namespace ScimitarBattery.Core;

/// <summary>
/// Optional lighting control (e.g., device LED battery indicator).
/// </summary>
public interface ILightingService
{
    void UpdateBatteryLighting(string deviceKey, int? percent);
    bool CanTest { get; }
    void TestLighting(string deviceKey, IReadOnlyList<int>? ledIds = null);
    IReadOnlyList<LedInfo> GetAvailableLeds(string deviceKey);
}
