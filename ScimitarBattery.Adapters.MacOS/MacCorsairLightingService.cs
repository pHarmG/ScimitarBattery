using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ScimitarBattery.Core;

namespace ScimitarBattery.Adapters.MacOS;

/// <summary>
/// Updates mouse LED color to indicate battery level (best-effort) using iCUE SDK on macOS.
/// </summary>
public sealed class MacCorsairLightingService : ILightingService, IDisposable
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

    public MacCorsairLightingService(
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

        string? deviceId = MacBatteryProvider.ToSdkId(deviceKey);
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

        var color = BatteryColor(percent.Value, _lowThreshold, _criticalThreshold, _colorHigh, _colorMid, _colorLow);
        var resolvedTargetIds = _targetLedIds ?? Array.Empty<int>();
        var ids = ResolveIndicatorLedIds(deviceId, _target, resolvedTargetIds, _customLedIds);
        if (ids.Length == 0)
            return;

        var colors = new MacCorsairNative.CorsairLedColor[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            colors[i] = new MacCorsairNative.CorsairLedColor
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

        if (_lastBucket == bucket)
        {
            // Lightweight keepalive push each poll: refresh target LEDs without full re-application.
            TrySetColors(deviceId, colors);
            return;
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
        string? deviceId = MacBatteryProvider.ToSdkId(deviceKey);
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

        var colors = selectedIds.Select(id => new MacCorsairNative.CorsairLedColor
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

    public IReadOnlyList<LedInfo> GetAvailableLeds(string deviceKey)
    {
        string? deviceId = MacBatteryProvider.ToSdkId(deviceKey);
        if (string.IsNullOrWhiteSpace(deviceId))
            return Array.Empty<LedInfo>();

        var ids = ResolveAllLeds(deviceId);
        if (ids.Length == 0)
            return Array.Empty<LedInfo>();

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
        if (_deviceId != null)
            TryReleaseControl(_deviceId);
    }

    private static (byte r, byte g, byte b) BatteryColor(
        int percent,
        int lowThreshold,
        int criticalThreshold,
        (byte r, byte g, byte b) colorHigh,
        (byte r, byte g, byte b) colorMid,
        (byte r, byte g, byte b) colorLow)
    {
        if (percent <= criticalThreshold)
            return colorLow;
        if (percent <= lowThreshold)
            return colorMid;
        return colorHigh;
    }

    private static (byte r, byte g, byte b) ParseColor(string? hex, (byte r, byte g, byte b) fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;
        try
        {
            if (hex.StartsWith('#'))
                hex = hex.Substring(1);
            if (hex.Length != 6)
                return fallback;
            var r = Convert.ToByte(hex.Substring(0, 2), 16);
            var g = Convert.ToByte(hex.Substring(2, 2), 16);
            var b = Convert.ToByte(hex.Substring(4, 2), 16);
            return (r, g, b);
        }
        catch
        {
            return fallback;
        }
    }

    private int[] ResolveTargetLeds(string deviceId, BatteryLedTarget target)
    {
        if (target == BatteryLedTarget.CustomSelection)
            return _customLedIds.Where(id => id > 0).Distinct().ToArray();

        var ledIds = ResolveAllLeds(deviceId);
        if (ledIds.Length == 0)
            return Array.Empty<int>();

        if (target == BatteryLedTarget.AllLeds)
            return ledIds;

        // Best-guess: pick a small subset (first few) for logo.
        return ledIds.Take(3).ToArray();
    }

    private int[] ResolveAllLeds(string deviceId)
    {
        if (_allLedIds != null)
            return _allLedIds;

        int size = 0;
        var buffer = new MacCorsairNative.CorsairLedPosition[512];
        int err = MacCorsairNative.CorsairGetLedPositions(deviceId, buffer.Length, buffer, ref size);
        if (err != 0 || size <= 0)
        {
            MacCorsairLog.Write($"GetLedPositions failed: err={err} size={size}");
            _allLedIds = Array.Empty<int>();
            return _allLedIds;
        }

        _allLedIds = buffer.Take(size)
            .Select(p => p.ledId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        return _allLedIds;
    }

    private int[] ResolveIndicatorLedIds(
        string deviceId,
        BatteryLedTarget target,
        int[] resolvedTargetIds,
        IReadOnlyList<int> customLedIds)
    {
        if (target == BatteryLedTarget.CustomSelection)
        {
            return customLedIds.Where(id => id > 0).Distinct().ToArray();
        }

        if (target == BatteryLedTarget.LogoBestGuess)
            return resolvedTargetIds;

        return resolvedTargetIds;
    }

    private bool TrySetColors(string deviceId, MacCorsairNative.CorsairLedColor[] colors)
    {
        int err = MacCorsairNative.CorsairSetLedColors(deviceId, colors.Length, colors);
        if (err != 0)
        {
            MacCorsairLog.Write($"SetLedColors failed: err={err}");
            return false;
        }
        return true;
    }

    private bool ApplySelectionOnly(string deviceId, int[] ids, MacCorsairNative.CorsairLedColor[] colors)
    {
        var all = _allLedIds ??= ResolveAllLeds(deviceId);
        if (all.Length == 0)
            return TrySetColors(deviceId, colors);

        var selected = new HashSet<int>(ids);
        var offIds = all.Where(id => !selected.Contains(id)).ToArray();
        if (offIds.Length > 0)
        {
            var off = BuildOffColors(offIds);
            if (!TrySetColors(deviceId, off))
                return false;
        }

        return TrySetColors(deviceId, colors);
    }

    private bool ApplyLogoOnly(string deviceId, int[] ids, MacCorsairNative.CorsairLedColor[] colors)
    {
        var all = _allLedIds ??= ResolveAllLeds(deviceId);
        if (all.Length == 0)
            return TrySetColors(deviceId, colors);

        var off = BuildOffColors(all);
        if (!TrySetColors(deviceId, off))
            return false;

        return TrySetColors(deviceId, colors);
    }

    private static MacCorsairNative.CorsairLedColor[] BuildOffColors(int[] ledIds)
    {
        var off = new MacCorsairNative.CorsairLedColor[ledIds.Length];
        for (int i = 0; i < ledIds.Length; i++)
        {
            off[i] = new MacCorsairNative.CorsairLedColor
            {
                ledId = ledIds[i],
                r = 0,
                g = 0,
                b = 0,
                a = 255
            };
        }

        return off;
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

    private bool TryReadBack(string deviceId, MacCorsairNative.CorsairLedColor[] colors, out string? failure)
    {
        failure = null;
        var readBack = new MacCorsairNative.CorsairLedColor[colors.Length];
        Array.Copy(colors, readBack, colors.Length);
        int err = MacCorsairNative.CorsairGetLedColors(deviceId, readBack.Length, readBack);
        if (err != 0)
        {
            failure = $"GetLedColors err={err}";
            return false;
        }

        for (int i = 0; i < colors.Length; i++)
        {
            if (readBack[i].r != colors[i].r ||
                readBack[i].g != colors[i].g ||
                readBack[i].b != colors[i].b)
                return false;
        }

        return true;
    }

    private void EnsureControl(string deviceId, bool throwOnFailure)
    {
        string? status = null;
        if (!MacCorsairSdkBridge.EnsureConnected(ref status))
        {
            if (throwOnFailure)
                throw new InvalidOperationException(status ?? "iCUE not connected.");
            return;
        }

        if (_controlMode == LightingControlMode.Exclusive)
        {
            if (_exclusiveRequested)
                return;
            int err = MacCorsairNative.CorsairRequestControl(deviceId, MacCorsairNative.CorsairAccessLevel.CAL_ExclusiveLightingControl);
            if (err == 0)
                _exclusiveRequested = true;
            else
                HandleControlError(err, throwOnFailure);
        }
        else
        {
            if (_sharedRequested)
                return;
            int err = MacCorsairNative.CorsairRequestControl(deviceId, MacCorsairNative.CorsairAccessLevel.CAL_Shared);
            if (err == 0)
                _sharedRequested = true;
            else
                HandleControlError(err, throwOnFailure);
        }

        if (!_layerPrioritySet)
        {
            MacCorsairNative.CorsairSetLayerPriority(128);
            _layerPrioritySet = true;
        }
    }

    private void HandleControlError(int err, bool throwOnFailure)
    {
        MacCorsairLog.Write($"RequestControl failed: err={err}");
        if (throwOnFailure)
            throw new InvalidOperationException($"Unable to control LEDs (error {err}).");
        if (_allowFallback && _controlMode == LightingControlMode.Exclusive)
        {
            _sharedRequested = MacCorsairNative.CorsairRequestControl(_deviceId ?? string.Empty, MacCorsairNative.CorsairAccessLevel.CAL_Shared) == 0;
        }
    }

    private void TryReleaseControl(string deviceId)
    {
        MacCorsairNative.CorsairReleaseControl(deviceId);
    }

    private void TryLogDeviceDetails(string deviceId, string context)
    {
        try
        {
            int devErr = MacCorsairNative.CorsairGetDeviceInfo(deviceId, out var info);
            MacCorsairLog.Write($"{context}: DeviceInfo err={devErr} id={info.id} model={info.model} ledCount={info.ledCount}");
        }
        catch (Exception ex)
        {
            MacCorsairLog.Write($"{context}: DeviceInfo exception {ex.GetType().Name}");
        }
    }
}
