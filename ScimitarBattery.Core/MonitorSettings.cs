using System.Text.Json.Serialization;

namespace ScimitarBattery.Core;

/// <summary>
/// User-configurable settings for battery monitoring. Platform-agnostic.
/// First-run defaults: poll=240s, low=30%, critical=15%.
/// </summary>
public sealed class MonitorSettings
{
    /// <summary>Stable device key (e.g. "Corsair:id" on Windows).</summary>
    public string? DeviceKey { get; set; }

    /// <summary>Human-readable device name for display.</summary>
    public string? DeviceDisplayName { get; set; }

    /// <summary>Poll interval in seconds. Clamped to 1–3600. Default 240.</summary>
    public int PollingIntervalSeconds
    {
        get => _pollingIntervalSeconds;
        set => _pollingIntervalSeconds = Math.Clamp(value, 1, 3600);
    }
    private int _pollingIntervalSeconds = 240;

    /// <summary>Low threshold (0–100). Default 30.</summary>
    public int LowThresholdPercent
    {
        get => _lowThresholdPercent;
        set => _lowThresholdPercent = Math.Clamp(value, 0, 100);
    }
    private int _lowThresholdPercent = 30;

    /// <summary>Critical threshold (0–100). Default 15.</summary>
    public int CriticalThresholdPercent
    {
        get => _criticalThresholdPercent;
        set => _criticalThresholdPercent = Math.Clamp(value, 0, 100);
    }
    private int _criticalThresholdPercent = 15;

    /// <summary>Show low-battery notifications (default true).</summary>
    public bool NotifyOnLow { get; set; } = true;

    /// <summary>Show critical-battery notifications (default true).</summary>
    public bool NotifyOnCritical { get; set; } = true;

    /// <summary>Start the app with Windows and stay in the tray.</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>Notification route (sound/toast/both/none).</summary>
    public NotificationRoute NotificationRoute { get; set; } = NotificationRoute.ToastAndSound;

    /// <summary>Use device LED to indicate battery state.</summary>
    public bool EnableBatteryLed { get; set; } = false;

    /// <summary>Which LED(s) to control for battery indication.</summary>
    public BatteryLedTarget BatteryLedTarget { get; set; } = BatteryLedTarget.LogoBestGuess;

    /// <summary>Custom LED ids to control when BatteryLedTarget is CustomSelection.</summary>
    public List<int> CustomLedIds { get; set; } = new();

    /// <summary>Lighting control mode (shared/exclusive).</summary>
    public LightingControlMode LightingControlMode { get; set; } = LightingControlMode.Shared;

    /// <summary>Fallback to exclusive control if shared appears ineffective.</summary>
    public bool AllowExclusiveFallback { get; set; } = false;

    /// <summary>Battery color: high (green) hex.</summary>
    public string BatteryColorHigh { get; set; } = "#00FF00";

    /// <summary>Battery color: mid (yellow) hex.</summary>
    public string BatteryColorMid { get; set; } = "#FFFF00";

    /// <summary>Battery color: low (red) hex.</summary>
    public string BatteryColorLow { get; set; } = "#FF0000";

    // Legacy color fields for migration from 4-color scheme. Not written on save.
    [JsonPropertyName("BatteryColorMidHigh")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyBatteryColorMidHigh { get; set; }

    [JsonPropertyName("BatteryColorMidLow")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyBatteryColorMidLow { get; set; }

    public TimeSpan PollingInterval => TimeSpan.FromSeconds(PollingIntervalSeconds);

    public MonitorSettings Clone()
    {
        return new MonitorSettings
        {
            DeviceKey = DeviceKey,
            DeviceDisplayName = DeviceDisplayName,
            _pollingIntervalSeconds = _pollingIntervalSeconds,
            _lowThresholdPercent = _lowThresholdPercent,
            _criticalThresholdPercent = _criticalThresholdPercent,
            NotifyOnLow = NotifyOnLow,
            NotifyOnCritical = NotifyOnCritical,
            StartWithWindows = StartWithWindows,
            NotificationRoute = NotificationRoute,
            EnableBatteryLed = EnableBatteryLed,
            BatteryLedTarget = BatteryLedTarget,
            CustomLedIds = new List<int>(CustomLedIds),
            LightingControlMode = LightingControlMode,
            AllowExclusiveFallback = AllowExclusiveFallback,
            BatteryColorHigh = BatteryColorHigh,
            BatteryColorMid = BatteryColorMid,
            BatteryColorLow = BatteryColorLow
        };
    }

    /// <summary>Creates first-run defaults: poll=240s, low=30%, critical=15%.</summary>
    public static MonitorSettings CreateDefaults() => new();
}

public enum NotificationRoute
{
    None = 0,
    Toast = 1,
    Sound = 2,
    ToastAndSound = 3
}

public enum BatteryLedTarget
{
    LogoBestGuess = 0,
    AllLeds = 1,
    CustomSelection = 2
}

public enum LightingControlMode
{
    Shared = 0,
    Exclusive = 1
}
