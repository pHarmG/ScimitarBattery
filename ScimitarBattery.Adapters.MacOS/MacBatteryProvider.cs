using ScimitarBattery.Core;

namespace ScimitarBattery.Adapters.MacOS;

/// <summary>
/// macOS implementation of IBatteryProvider using Corsair iCUE SDK.
/// DeviceKey must be in form "Corsair:&lt;id&gt;" (id = SDK device id).
/// </summary>
public sealed class MacBatteryProvider : IBatteryProvider
{
    public const string DeviceKeyPrefix = "Corsair:";

    public int? GetBatteryPercent(string deviceKey)
    {
        string? id = ToSdkId(deviceKey);
        if (string.IsNullOrEmpty(id))
            return null;

        string? status = null;
        if (!MacCorsairSdkBridge.EnsureConnected(ref status))
        {
            if (!string.IsNullOrWhiteSpace(status))
                MacCorsairLog.Write($"Battery: not connected ({status})");
            return null;
        }

        int err = MacCorsairNative.CorsairReadDeviceProperty(
            id,
            MacCorsairNative.CorsairDevicePropertyId.CDPI_BatteryLevel,
            0,
            out var prop);

        if (err != 0)
        {
            MacCorsairLog.Write($"Battery: read failed err={err}");
            return null;
        }

        try
        {
            if (prop.type != MacCorsairNative.CorsairDataType.CT_Int32)
                return null;
            return prop.value.int32;
        }
        finally
        {
            MacCorsairNative.CorsairFreeProperty(ref prop);
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
