using ScimitarBattery.Core;

namespace ScimitarBattery.Adapters.Windows;

/// <summary>
/// Windows implementation of IDeviceEnumerator using Corsair iCUE SDK.
/// Returns device keys as "Corsair:&lt;id&gt;".
/// </summary>
public sealed class CorsairDeviceEnumerator : IDeviceEnumerator
{
    private const int MaxDevices = 64;

    public IReadOnlyList<DeviceInfo> GetDevices(bool includeBattery = false)
    {
        var filter = new CorsairNative.CorsairDeviceFilter
        {
            deviceTypeMask = (int)CorsairNative.CorsairDeviceType.CDT_Mouse
        };

        var devices = new CorsairNative.CorsairDeviceInfo[MaxDevices];
        int size = 0;

        int err = CorsairNative.CorsairGetDevices(ref filter, devices.Length, devices, ref size);
        if (err != 0 || size <= 0)
            return Array.Empty<DeviceInfo>();

        var result = new List<DeviceInfo>(size);
        var provider = new CorsairBatteryProvider();

        for (int i = 0; i < size; i++)
        {
            var d = devices[i];
            if (d.type != CorsairNative.CorsairDeviceType.CDT_Mouse)
                continue;

            string deviceKey = CorsairBatteryProvider.DeviceKeyPrefix + d.id;
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
        return scimitar?.DeviceKey ?? mice[0].DeviceKey;
    }
}
