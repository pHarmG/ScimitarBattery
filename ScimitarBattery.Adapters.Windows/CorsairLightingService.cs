using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ScimitarBattery.Core;

namespace ScimitarBattery.Adapters.Windows;

/// <summary>
/// Updates mouse LED color to indicate battery level (best-effort).
/// </summary>
public sealed class CorsairLightingService : ILightingService, IDisposable
{
    private readonly BatteryLedTarget _target;
    private readonly LightingControlMode _controlMode;
    private readonly bool _allowFallback;
    private readonly IReadOnlyList<int> _customLedIds;
    private readonly int _lowThreshold;
    private readonly int _criticalThreshold;
    private readonly (byte r, byte g, byte b) _colorHigh;
    private readonly (byte r, byte g, byte b) _colorMid;
    private readonly (byte r, byte g, byte b) _colorLow;
    private string? _deviceId;
    private int[]? _targetLedIds;
    private int[]? _allLedIds;
    private int? _lastBucket;
    private bool _exclusiveRequested;
    private bool _sharedRequested;
    private bool _layerPrioritySet;
    private string? _lastReadbackWarning;
    private bool _loggedDeviceDetails;

    public CorsairLightingService(
        BatteryLedTarget target,
        IReadOnlyList<int>? customLedIds,
        LightingControlMode controlMode,
        bool allowFallback,
        int lowThreshold,
        int criticalThreshold,
        string colorHigh,
        string colorMid,
        string colorLow)
    {
        _target = target;
        _customLedIds = customLedIds?.ToArray() ?? Array.Empty<int>();
        _controlMode = controlMode;
        _allowFallback = allowFallback;
        _lowThreshold = Math.Clamp(lowThreshold, 0, 100);
        _criticalThreshold = Math.Clamp(criticalThreshold, 0, 100);
        _colorHigh = ParseColor(colorHigh, (0, 255, 0));
        _colorMid = ParseColor(colorMid, (255, 255, 0));
        _colorLow = ParseColor(colorLow, (255, 0, 0));
    }

    public bool CanTest => true;

    public void UpdateBatteryLighting(string deviceKey, int? percent)
    {
        if (!percent.HasValue)
            return;

        string? deviceId = CorsairBatteryProvider.ToSdkId(deviceKey);
        if (string.IsNullOrWhiteSpace(deviceId))
            return;

        EnsureControl(deviceId, throwOnFailure: false);

        if (_deviceId != deviceId || _targetLedIds == null)
        {
            _deviceId = deviceId;
            _targetLedIds = ResolveTargetLeds(deviceId, _target);
            _allLedIds = null;
            _loggedDeviceDetails = false;
        }

        if ((_target != BatteryLedTarget.CustomSelection || _customLedIds.Count == 0) &&
            (_targetLedIds == null || _targetLedIds.Length == 0))
            return;

        int bucket = percent.Value / 5;
        if (_lastBucket == bucket)
            return;

        var color = BatteryColor(percent.Value, _lowThreshold, _criticalThreshold, _colorHigh, _colorMid, _colorLow);
        var resolvedTargetIds = _targetLedIds ?? Array.Empty<int>();
        var ids = ResolveIndicatorLedIds(deviceId, _target, resolvedTargetIds, _customLedIds);
        if (ids.Length == 0)
            return;

        var colors = new CorsairNative.CorsairLedColor[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            colors[i] = new CorsairNative.CorsairLedColor
            {
                ledId = ids[i],
                r = color.r,
                g = color.g,
                b = color.b,
                a = 255
            };
        }

        if (!_loggedDeviceDetails)
        {
            TryLogDeviceDetails(deviceId, "UpdateBatteryLighting");
            _loggedDeviceDetails = true;
        }

        if (_target == BatteryLedTarget.CustomSelection)
        {
            if (ApplySelectionOnly(deviceId, ids, colors))
                _lastBucket = bucket;
        }
        else if (_target == BatteryLedTarget.LogoBestGuess)
        {
            if (ApplyLogoOnly(deviceId, ids, colors))
                _lastBucket = bucket;
        }
        else
        {
            if (TrySetColors(deviceId, colors))
                _lastBucket = bucket;
        }
    }

    public void TestLighting(string deviceKey, IReadOnlyList<int>? ledIds = null)
    {
        string? deviceId = CorsairBatteryProvider.ToSdkId(deviceKey);
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new InvalidOperationException("Unable to resolve device for LED test.");

        EnsureControl(deviceId, throwOnFailure: true);

        if (_deviceId != deviceId || _targetLedIds == null)
        {
            _deviceId = deviceId;
            _targetLedIds = ResolveTargetLeds(deviceId, _target);
            _allLedIds = null;
            _loggedDeviceDetails = false;
        }
        if (ledIds == null &&
            _target == BatteryLedTarget.CustomSelection &&
            _customLedIds.Count > 0)
        {
            // Ok: custom selection will drive the test.
        }
        else if (_targetLedIds == null || _targetLedIds.Length == 0)
        {
            throw new InvalidOperationException("No LED targets resolved for this device.");
        }

        TryLogDeviceDetails(deviceId, "TestLighting");

        var resolvedTargetIdsForTest = _targetLedIds ?? Array.Empty<int>();
        var selectedIds = ledIds != null && ledIds.Count > 0
            ? ledIds.Where(id => id > 0).Distinct().ToArray()
            : resolvedTargetIdsForTest;

        if (selectedIds.Length == 0)
            throw new InvalidOperationException("No LED targets selected for test.");

        var colors = selectedIds.Select(id => new CorsairNative.CorsairLedColor
        {
            ledId = id,
            r = _colorHigh.r,
            g = _colorHigh.g,
            b = _colorHigh.b,
            a = 255
        }).ToArray();

        if (ledIds != null && ledIds.Count > 0)
        {
            if (!ApplySelectionOnly(deviceId, selectedIds, colors))
                throw new InvalidOperationException("Failed to apply LED test color.");
        }
        else if (_target == BatteryLedTarget.CustomSelection && _customLedIds.Count > 0)
        {
            var ids = _customLedIds.Where(id => id > 0).Distinct().ToArray();
            if (ids.Length == 0)
                throw new InvalidOperationException("No custom LED ids configured.");
            if (!ApplySelectionOnly(deviceId, ids, colors))
                throw new InvalidOperationException("Failed to apply LED test color.");
        }
        else if (_target == BatteryLedTarget.LogoBestGuess)
        {
            if (!ApplyLogoOnly(deviceId, resolvedTargetIdsForTest, colors))
                throw new InvalidOperationException("Failed to apply LED test color.");
        }
        else
        {
            if (!TrySetColors(deviceId, colors))
                throw new InvalidOperationException("Failed to apply LED test color.");
        }

        if (!TryReadBack(deviceId, colors, out string? failure))
            _lastReadbackWarning = failure ?? "LED readback mismatch.";
        else
            _lastReadbackWarning = null;
    }

    public string GetDebugInfo(string deviceKey)
    {
        var sb = new StringBuilder();
        string? deviceId = CorsairBatteryProvider.ToSdkId(deviceKey);
        if (string.IsNullOrWhiteSpace(deviceId))
            return "Device id not resolved.";

        sb.Append("DeviceId=").Append(deviceId);
        if (!string.IsNullOrWhiteSpace(_lastReadbackWarning))
            sb.Append(" | Readback=").Append(_lastReadbackWarning);

        int posSize = 0;
        var posBuffer = new CorsairNative.CorsairLedPosition[512];
        int posErr = CorsairNative.CorsairGetLedPositions(deviceId, posBuffer.Length, posBuffer, ref posSize);
        sb.Append(" | LedPositions err=").Append(posErr)
            .Append(" (").Append(((CorsairNative.CorsairError)posErr).ToString()).Append(')')
            .Append(" size=").Append(posSize);

        var positions = posErr == 0 && posSize > 0 ? posBuffer.Take(posSize).ToArray() : Array.Empty<CorsairNative.CorsairLedPosition>();
        if (positions.Length > 0)
        {
            var ids = positions.Select(p => p.ledId).Distinct().OrderBy(id => id).Take(10).ToArray();
            sb.Append(" ids=");
            sb.Append(string.Join(",", ids));
        }

        int devSize = 0;
        var filter = new CorsairNative.CorsairDeviceFilter { deviceTypeMask = (int)CorsairNative.CorsairDeviceType.CDT_All };
        var devices = new CorsairNative.CorsairDeviceInfo[32];
        int devErr = CorsairNative.CorsairGetDevices(ref filter, devices.Length, devices, ref devSize);
        sb.Append(" | Devices err=").Append(devErr)
            .Append(" (").Append(((CorsairNative.CorsairError)devErr).ToString()).Append(')')
            .Append(" size=").Append(devSize);
        if (devErr == 0 && devSize > 0)
        {
            var match = devices.Take(devSize).FirstOrDefault(d =>
                string.Equals(d.id, deviceId, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.id))
            {
                sb.Append(" ledCount=").Append(match.ledCount)
                  .Append(" model=").Append(match.model);
            }
        }

        var targets = ResolveTargetLeds(deviceId, _target);
        if (targets == null || targets.Length == 0)
        {
            sb.Append(" | Targets=none");
        }
        else
        {
            sb.Append(" | Targets=").Append(targets.Length)
              .Append(" ids=").Append(string.Join(",", targets.Take(10)));
        }

        return sb.ToString();
    }

    public IReadOnlyList<LedInfo> GetAvailableLeds(string deviceKey)
    {
        string? deviceId = CorsairBatteryProvider.ToSdkId(deviceKey);
        if (string.IsNullOrWhiteSpace(deviceId))
            return Array.Empty<LedInfo>();

        var allIds = ResolveAllLeds(deviceId);
        var ids = allIds.Length > 0
            ? allIds
            : GetLedPositions(deviceId)
                ?.Select(p => p.ledId)
                .Distinct()
                .OrderBy(id => id)
                .ToArray()
              ?? Array.Empty<int>();

        var customLabels = BuildCustomLedLabels(ids);
        var list = new List<LedInfo>(ids.Length);
        foreach (var id in ids)
        {
            bool selectable = id > 0;
            var label = customLabels.TryGetValue(id, out var custom)
                ? custom
                : FormatLedLabel(id);
            list.Add(new LedInfo(id, label, selectable));
        }
        return list;
    }

    public void Dispose()
    {
        if ((_exclusiveRequested || _sharedRequested) && _deviceId != null)
        {
            CorsairNative.CorsairReleaseControl(_deviceId);
        }
    }

    private static int[]? ResolveTargetLeds(string deviceId, BatteryLedTarget target)
    {
        var positions = GetLedPositions(deviceId);
        int? ledCount = GetLedCount(deviceId);
        if (positions == null || positions.Length == 0)
        {
            if (!ledCount.HasValue || ledCount.Value <= 0 || ledCount.Value > 512)
                return null;

            if (target == BatteryLedTarget.AllLeds)
                return Enumerable.Range(1, ledCount.Value).ToArray();

            return null;
        }

        if (target == BatteryLedTarget.AllLeds)
        {
            return GetAllLedIds(positions, ledCount);
        }
        var logoId = ResolveLogoLedId(positions, ledCount);
        if (!logoId.HasValue)
            return null;

        return new[] { logoId.Value };
    }

    private static CorsairNative.CorsairLedPosition[]? GetLedPositions(string deviceId)
    {
        int size = 0;
        var buffer = new CorsairNative.CorsairLedPosition[512];
        int err = CorsairNative.CorsairGetLedPositions(deviceId, buffer.Length, buffer, ref size);
        if (err != 0 || size <= 0)
            return null;
        return buffer.Take(size).ToArray();
    }

    private static int? GetLedCount(string deviceId)
    {
        var filter = new CorsairNative.CorsairDeviceFilter
        {
            deviceTypeMask = (int)CorsairNative.CorsairDeviceType.CDT_All
        };
        int size = 0;
        var devices = new CorsairNative.CorsairDeviceInfo[32];
        int err = CorsairNative.CorsairGetDevices(ref filter, devices.Length, devices, ref size);
        if (err != 0 || size <= 0)
            return null;
        for (int i = 0; i < size; i++)
        {
            if (string.Equals(devices[i].id, deviceId, StringComparison.OrdinalIgnoreCase))
                return devices[i].ledCount;
        }
        return null;
    }

    private static (byte r, byte g, byte b) BatteryColor(
        int percent,
        int lowThreshold,
        int criticalThreshold,
        (byte r, byte g, byte b) colorHigh,
        (byte r, byte g, byte b) colorMid,
        (byte r, byte g, byte b) colorLow)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (lowThreshold <= criticalThreshold)
        {
            // Fallback to simple red/yellow/green if thresholds are inverted.
            if (percent <= criticalThreshold) return colorLow;
            if (percent <= lowThreshold) return colorMid;
            return colorHigh;
        }

        if (percent >= lowThreshold)
        {
            // Mid -> High between low..100
            double t = (percent - lowThreshold) / (100.0 - lowThreshold);
            return Lerp(colorMid, colorHigh, t);
        }
        if (percent >= criticalThreshold)
        {
            // Low -> Mid between critical..low
            double t = (percent - criticalThreshold) / (double)(lowThreshold - criticalThreshold);
            return Lerp(colorLow, colorMid, t);
        }
        return colorLow;
    }

    private bool EnsureControl(string deviceId, bool throwOnFailure)
    {
        bool success = false;
        if (_controlMode == LightingControlMode.Shared)
        {
            // Per SDK docs, shared control is granted by default; no request needed.
            success = true;
            if (!_layerPrioritySet)
            {
                // Ensure our SDK layer is visible above iCUE layers.
                _layerPrioritySet = CorsairNative.CorsairSetLayerPriority(255) == 0;
            }
        }
        else if (_controlMode == LightingControlMode.Exclusive && !_exclusiveRequested)
        {
            int ex = CorsairNative.CorsairRequestControl(CorsairNative.CorsairAccessLevel.CAL_ExclusiveLightingControl);
            _exclusiveRequested = ex == 0;
            if (!_exclusiveRequested && _allowFallback && !_sharedRequested)
            {
                int req = CorsairNative.CorsairRequestControl(CorsairNative.CorsairAccessLevel.CAL_Shared);
                _sharedRequested = req == 0;
            }
        }

        success = _controlMode == LightingControlMode.Exclusive
            ? _exclusiveRequested
            : success || _sharedRequested || _exclusiveRequested;

        if (throwOnFailure && !success)
            throw new InvalidOperationException("Unable to acquire lighting control from iCUE.");

        return success;
    }

    private bool TrySetColors(string deviceId, CorsairNative.CorsairLedColor[] colors)
    {
        int err = CorsairNative.CorsairSetLedColors(deviceId, colors.Length, colors);
        if (err == 0)
        {
            TryFlush();
            return true;
        }

        if (_allowFallback && !_exclusiveRequested)
        {
            int ex = CorsairNative.CorsairRequestControl(CorsairNative.CorsairAccessLevel.CAL_ExclusiveLightingControl);
            _exclusiveRequested = ex == 0;
            if (_exclusiveRequested)
            {
                int retry = CorsairNative.CorsairSetLedColors(deviceId, colors.Length, colors);
                if (retry == 0)
                {
                    TryFlush();
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryReadBack(string deviceId, CorsairNative.CorsairLedColor[] expected, out string? failure)
    {
        failure = null;
        var buffer = expected.Select(c => new CorsairNative.CorsairLedColor { ledId = c.ledId }).ToArray();
        int err = CorsairNative.CorsairGetLedColors(deviceId, buffer.Length, buffer);
        if (err != 0)
        {
            failure = $"LED readback failed (error {err}).";
            return false;
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i].r != expected[i].r ||
                buffer[i].g != expected[i].g ||
                buffer[i].b != expected[i].b)
            {
                failure = $"LED readback mismatch for id {buffer[i].ledId}: " +
                          $"expected {expected[i].r},{expected[i].g},{expected[i].b} " +
                          $"got {buffer[i].r},{buffer[i].g},{buffer[i].b}.";
                return false;
            }
        }

        return true;
    }

    private static int[] FilterValidLedIds(int[] ids)
    {
        var filtered = ids.Where(id => id > 0).Distinct().ToArray();
        return filtered.Length > 0 ? filtered : ids.Distinct().ToArray();
    }

    private static int[] GetAllLedIds(CorsairNative.CorsairLedPosition[] positions, int? ledCount)
    {
        var ids = positions.Select(p => p.ledId).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
            return Array.Empty<int>();

        if (ledCount.HasValue && ledCount.Value > 0 && ledCount.Value <= 512)
        {
            var groups = ids
                .Select(id => (ushort)(unchecked((uint)id) >> 16))
                .Distinct()
                .ToArray();

            if (groups.Length == 1)
            {
                int group = groups[0];
                return Enumerable.Range(1, ledCount.Value)
                    .Select(index => (group << 16) | index)
                    .ToArray();
            }
        }

        return ids;
    }

    private static string FormatLedLabel(int id)
    {
        if (id == 0)
            return "LED 0 (invalid id)";
        uint uid = unchecked((uint)id);
        ushort group = (ushort)(uid >> 16);
        ushort index = (ushort)(uid & 0xFFFF);
        return $"LED {id} (G{group} I{index})";
    }

    private static Dictionary<int, string> BuildCustomLedLabels(int[] ids)
    {
        var labels = new Dictionary<int, string>();
        if (ids.Length != 2)
            return labels;

        if (!TryGetGroupIndex(ids[0], out var group0, out var index0) ||
            !TryGetGroupIndex(ids[1], out var group1, out var index1) ||
            group0 != group1)
            return labels;

        var ordered = ids
            .Select(id =>
            {
                TryGetGroupIndex(id, out var group, out var index);
                return (id, group, index);
            })
            .OrderBy(item => item.index)
            .ToArray();

        labels[ordered[0].id] = $"Logo (G{ordered[0].group} I{ordered[0].index})";
        labels[ordered[1].id] = $"Side (G{ordered[1].group} I{ordered[1].index})";
        return labels;
    }

    private static bool TryGetGroupIndex(int id, out ushort group, out ushort index)
    {
        if (id <= 0)
        {
            group = 0;
            index = 0;
            return false;
        }
        uint uid = unchecked((uint)id);
        group = (ushort)(uid >> 16);
        index = (ushort)(uid & 0xFFFF);
        return index > 0;
    }

    private static int? ResolveLogoLedId(CorsairNative.CorsairLedPosition[] positions, int? ledCount)
    {
        var all = GetAllLedIds(positions, ledCount);
        if (all.Length > 0)
            return all.Min();

        var valid = positions.Select(p => p.ledId).Where(id => id > 0).Distinct().ToArray();
        if (valid.Length == 0)
            return null;

        return valid.Min();
    }

    private bool ApplyLogoOnly(string deviceId, int[] targetLedIds, CorsairNative.CorsairLedColor[] targetColors)
    {
        var all = _allLedIds ??= ResolveAllLeds(deviceId);
        if (all.Length == 0)
            return false;

        var off = new CorsairNative.CorsairLedColor[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            off[i] = new CorsairNative.CorsairLedColor
            {
                ledId = all[i],
                r = 0,
                g = 0,
                b = 0,
                a = 255
            };
        }

        if (!TrySetColors(deviceId, off))
            return false;

        return TrySetColors(deviceId, targetColors);
    }

    private static int[] ResolveIndicatorLedIds(
        string deviceId,
        BatteryLedTarget target,
        int[] resolvedIds,
        IReadOnlyList<int> customIds)
    {
        if (target == BatteryLedTarget.CustomSelection)
        {
            var ids = customIds.Where(id => id > 0).Distinct().ToArray();
            return ids;
        }
        return resolvedIds;
    }

    private bool ApplySelectionOnly(string deviceId, int[] selectedIds, CorsairNative.CorsairLedColor[] targetColors)
    {
        var all = _allLedIds ??= ResolveAllLeds(deviceId);
        if (all.Length == 0)
            return TrySetColors(deviceId, targetColors);

        var selected = new HashSet<int>(selectedIds);
        var offIds = all.Where(id => !selected.Contains(id)).ToArray();
        if (offIds.Length > 0)
        {
            var off = new CorsairNative.CorsairLedColor[offIds.Length];
            for (int i = 0; i < offIds.Length; i++)
            {
                off[i] = new CorsairNative.CorsairLedColor
                {
                    ledId = offIds[i],
                    r = 0,
                    g = 0,
                    b = 0,
                    a = 255
                };
            }
            if (!TrySetColors(deviceId, off))
                return false;
        }

        return TrySetColors(deviceId, targetColors);
    }

    private static int[] ResolveAllLeds(string deviceId)
    {
        var positions = GetLedPositions(deviceId);
        int? ledCount = GetLedCount(deviceId);
        if (positions != null && positions.Length > 0)
        {
            return GetAllLedIds(positions, ledCount);
        }

        if (!ledCount.HasValue || ledCount.Value <= 0 || ledCount.Value > 512)
            return Array.Empty<int>();

        // If no positions, fall back to a contiguous range starting at 1.
        return Enumerable.Range(1, ledCount.Value).ToArray();
    }

    private void TryLogDeviceDetails(string deviceId, string context)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ScimitarBattery");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "lighting-debug.txt");

            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("s"))
              .Append(" | ").Append(context)
              .Append(" | DeviceId=").Append(deviceId)
              .Append(" | Target=").Append(_target)
              .Append(" | ControlMode=").Append(_controlMode);

            int posSize = 0;
            var posBuffer = new CorsairNative.CorsairLedPosition[512];
            int posErr = CorsairNative.CorsairGetLedPositions(deviceId, posBuffer.Length, posBuffer, ref posSize);
            sb.Append(" | LedPositions err=").Append(posErr)
              .Append(" (").Append(((CorsairNative.CorsairError)posErr).ToString()).Append(')')
              .Append(" size=").Append(posSize);

            var positions = posErr == 0 && posSize > 0 ? posBuffer.Take(posSize).ToArray() : Array.Empty<CorsairNative.CorsairLedPosition>();
            if (positions.Length > 0)
            {
                var ids = positions.Select(p => p.ledId).Distinct().OrderBy(id => id).ToArray();
                sb.Append(" ids=").Append(string.Join(",", ids));
            }

            int devSize = 0;
            var filter = new CorsairNative.CorsairDeviceFilter { deviceTypeMask = (int)CorsairNative.CorsairDeviceType.CDT_All };
            var devices = new CorsairNative.CorsairDeviceInfo[32];
            int devErr = CorsairNative.CorsairGetDevices(ref filter, devices.Length, devices, ref devSize);
            sb.Append(" | Devices err=").Append(devErr)
              .Append(" (").Append(((CorsairNative.CorsairError)devErr).ToString()).Append(')')
              .Append(" size=").Append(devSize);
            if (devErr == 0 && devSize > 0)
            {
                var match = devices.Take(devSize).FirstOrDefault(d =>
                    string.Equals(d.id, deviceId, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match.id))
                {
                    sb.Append(" ledCount=").Append(match.ledCount)
                      .Append(" model=").Append(match.model);
                }
            }

            var targets = ResolveTargetLeds(deviceId, _target);
            if (targets == null || targets.Length == 0)
            {
                sb.Append(" | Targets=none");
            }
            else
            {
                sb.Append(" | Targets=").Append(targets.Length)
                  .Append(" ids=").Append(string.Join(",", targets.Take(32)));
            }

            var all = _allLedIds ?? ResolveAllLeds(deviceId);
            if (all.Length > 0)
            {
                sb.Append(" | All=").Append(all.Length)
                  .Append(" ids=").Append(string.Join(",", all.Take(32)));
            }

            if (!string.IsNullOrWhiteSpace(_lastReadbackWarning))
                sb.Append(" | Readback=").Append(_lastReadbackWarning);

            File.AppendAllText(path, sb.ToString() + Environment.NewLine);
        }
        catch
        {
            // Swallow logging failures.
        }
    }

    private static (byte r, byte g, byte b) Lerp((int r, int g, int b) a, (int r, int g, int b) b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return (
            (byte)(a.r + (b.r - a.r) * t),
            (byte)(a.g + (b.g - a.g) * t),
            (byte)(a.b + (b.b - a.b) * t)
        );
    }

    private static void TryFlush()
    {
        try
        {
            CorsairNative.CorsairSetLedColorsFlushBuffer();
        }
        catch (EntryPointNotFoundException)
        {
            // Older SDKs don't expose the flush entry point.
        }
        catch (DllNotFoundException)
        {
            // Ignore if the SDK isn't available.
        }
    }

    private static (byte r, byte g, byte b) ParseColor(string? hex, (byte r, byte g, byte b) fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;
        string s = hex.Trim();
        if (s.StartsWith("#", StringComparison.Ordinal))
            s = s.Substring(1);
        if (s.Length == 8)
        {
            // Ignore alpha if provided as AARRGGBB.
            s = s.Substring(2);
        }
        if (s.Length != 6)
            return fallback;
        if (!byte.TryParse(s.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r))
            return fallback;
        if (!byte.TryParse(s.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g))
            return fallback;
        if (!byte.TryParse(s.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            return fallback;
        return (r, g, b);
    }
}
