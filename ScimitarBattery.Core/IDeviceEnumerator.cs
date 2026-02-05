namespace ScimitarBattery.Core;

/// <summary>
/// Enumerates available devices with stable keys and display names. Implemented per platform.
/// </summary>
public interface IDeviceEnumerator
{
    /// <summary>
    /// Returns all compatible devices (e.g. mice with battery). May optionally include current battery %.
    /// </summary>
    IReadOnlyList<DeviceInfo> GetDevices(bool includeBattery = false);

    /// <summary>
    /// Returns a default device key for first-run or when config has no selection (e.g. first Scimitar or first device).
    /// </summary>
    string? GetDefaultDeviceKey();
}
