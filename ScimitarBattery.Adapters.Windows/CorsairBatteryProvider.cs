using ScimitarBattery.Core;

namespace ScimitarBattery.Adapters.Windows;

/// <summary>
/// Windows implementation of IBatteryProvider using Corsair iCUE SDK.
/// DeviceKey must be in form "Corsair:&lt;id&gt;" (id = SDK device id).
/// </summary>
public sealed class CorsairBatteryProvider : IBatteryProvider
{
    /// <summary>
    /// Prefix for device keys from this adapter. Stored in config as "Corsair:&lt;id&gt;".
    /// </summary>
    public const string DeviceKeyPrefix = "Corsair:";

    public int? GetBatteryPercent(string deviceKey)
    {
        string? id = ToSdkId(deviceKey);
        if (string.IsNullOrEmpty(id))
            return null;

        int err = CorsairNative.CorsairReadDeviceProperty(
            id,
            CorsairNative.CorsairDevicePropertyId.CDPI_BatteryLevel,
            0,
            out var prop);

        if (err != 0)
            return null;

        try
        {
            if (prop.type != CorsairNative.CorsairDataType.CT_Int32)
                return null;
            return prop.value.int32;
        }
        finally
        {
            CorsairNative.CorsairFreeProperty(ref prop);
        }
    }

    internal static string? ToSdkId(string? deviceKey)
    {
        if (string.IsNullOrWhiteSpace(deviceKey) || !deviceKey.StartsWith(DeviceKeyPrefix, StringComparison.Ordinal))
            return null;
        var id = deviceKey.Substring(DeviceKeyPrefix.Length).Trim();
        return id.Length > 0 ? id : null;
    }
}
