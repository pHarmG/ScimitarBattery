using ScimitarBattery.Core;

namespace ScimitarBattery.Adapters.MacOS;

/// <summary>
/// macOS device enumerator stub.
/// </summary>
public sealed class MacDeviceEnumerator : IDeviceEnumerator
{
    public IReadOnlyList<DeviceInfo> GetDevices(bool includeBattery = false)
    {
        return Array.Empty<DeviceInfo>();
    }

    public string? GetDefaultDeviceKey()
    {
        return null;
    }
}
