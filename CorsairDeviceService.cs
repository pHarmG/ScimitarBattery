using System;
using System.Collections.Generic;
using System.Linq;

namespace ScimitarBattery
{
    /// <summary>
    /// Represents a Corsair device with its ID, model, and optional battery level.
    /// </summary>
    internal sealed class CorsairDeviceInfoDto
    {
        public string Id { get; }
        public string Model { get; }
        public string Serial { get; }
        internal CorsairNative.CorsairDeviceType Type { get; }
        public int? BatteryPercent { get; set; }

        internal CorsairDeviceInfoDto(string id, string model, string serial, CorsairNative.CorsairDeviceType type, int? batteryPercent = null)
        {
            Id = id ?? string.Empty;
            Model = model ?? string.Empty;
            Serial = serial ?? string.Empty;
            Type = type;
            BatteryPercent = batteryPercent;
        }

        public string DisplayName => string.IsNullOrWhiteSpace(Model) ? Id : $"{Model} ({Id})";
    }

    /// <summary>
    /// Handles Corsair device enumeration and battery level reading via the iCUE SDK.
    /// </summary>
    internal sealed class CorsairDeviceService
    {
        private const int MaxDevices = 64;

        /// <summary>
        /// Enumerates all Corsair mouse devices (optionally with battery levels).
        /// </summary>
        internal IReadOnlyList<CorsairDeviceInfoDto> GetMouseDevices(bool includeBattery = true)
        {
            var filter = new CorsairNative.CorsairDeviceFilter
            {
                deviceTypeMask = (int)CorsairNative.CorsairDeviceType.CDT_Mouse
            };

            var devices = new CorsairNative.CorsairDeviceInfo[MaxDevices];
            int size = 0;

            int err = CorsairNative.CorsairGetDevices(ref filter, devices.Length, devices, ref size);
            if (err != 0 || size <= 0)
                return Array.Empty<CorsairDeviceInfoDto>();

            var result = new List<CorsairDeviceInfoDto>(size);
            for (int i = 0; i < size; i++)
            {
                var d = devices[i];
                if (d.type != CorsairNative.CorsairDeviceType.CDT_Mouse)
                    continue;

                int? battery = includeBattery ? TryGetBatteryPercent(d.id) : null;
                result.Add(new CorsairDeviceInfoDto(d.id, d.model, d.serial, d.type, battery));
            }

            return result;
        }

        /// <summary>
        /// Finds the first Scimitar mouse device ID, or the first mouse if none match.
        /// </summary>
        internal string? FindScimitarOrFirstMouseId()
        {
            var mice = GetMouseDevices(includeBattery: false);
            if (mice.Count == 0)
                return null;

            var scimitar = mice.FirstOrDefault(m =>
                m.Model.Contains("SCIMITAR", StringComparison.OrdinalIgnoreCase));
            return scimitar?.Id ?? mice[0].Id;
        }

        /// <summary>
        /// Reads the battery level (0–100) for the given device ID. Returns null on error.
        /// </summary>
        internal int? TryGetBatteryPercent(string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return null;

            int err = CorsairNative.CorsairReadDeviceProperty(
                deviceId,
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
    }
}
