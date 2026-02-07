using ScimitarBattery.Core;

namespace ScimitarBattery.Adapters.MacOS;

public sealed class MacDeviceEnumerator : IDeviceEnumerator
{
    private const int MaxDevices = 64;

    public IReadOnlyList<DeviceInfo> GetDevices(bool includeBattery = false)
    {
        string? status = null;
        if (!MacCorsairSdkBridge.EnsureConnected(ref status))
            return Array.Empty<DeviceInfo>();

        var filter = new MacCorsairNative.CorsairDeviceFilter
        {
            // Start wide on macOS: inspect everything iCUE exposes, then narrow.
            deviceTypeMask = (int)MacCorsairNative.CorsairDeviceType.CDT_All
        };

        var devices = new MacCorsairNative.CorsairDeviceInfo[MaxDevices];
        int size = 0;

        int err = MacCorsairNative.CorsairGetDevices(ref filter, devices.Length, devices, ref size);
        if (err != 0 || size <= 0)
        {
            MacCorsairLog.Write($"GetDevices failed: err={err} size={size}");
            return Array.Empty<DeviceInfo>();
        }

        var result = new List<DeviceInfo>(size);
        var provider = new MacBatteryProvider();

        for (int i = 0; i < size; i++)
        {
            var d = devices[i];
            MacCorsairLog.Write($"GetDevices[{i}]: type={d.type} id={d.id} model={d.model} ledCount={d.ledCount}");

            string deviceKey = MacBatteryProvider.DeviceKeyPrefix + d.id;
            string displayName = string.IsNullOrWhiteSpace(d.model) ? d.id : d.model;
            int? battery = includeBattery ? provider.GetBatteryPercent(deviceKey) : null;

            result.Add(new DeviceInfo(deviceKey, displayName, battery));
        }

        return result;
    }

    public string? GetDefaultDeviceKey()
    {
        var mice = GetDevices(includeBattery: false);
        if (mice.Count == 0)
            return null;

        var scimitar = mice.FirstOrDefault(m =>
            m.DisplayName.Contains("SCIMITAR", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(scimitar?.DeviceKey))
            return scimitar.DeviceKey;

        var mouse = mice.FirstOrDefault(m =>
            m.DisplayName.Contains("MOUSE", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(mouse?.DeviceKey))
            return mouse.DeviceKey;

        return mice[0].DeviceKey;
    }
}
