using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ScimitarBattery.Core;
// Platform-specific notification availability is handled by NotificationSupport.

namespace ScimitarBattery;

public partial class SettingsWindow : Window
{
    private IDeviceEnumerator? _enumerator;
    private MonitorSettings? _settings;
    private Action<MonitorSettings>? _onSaved;
    private INotifier? _notifier;
    private Func<bool>? _ensureConnected;
    private string? _initialStatus;
    private ILightingService? _lightingService;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(
        IDeviceEnumerator enumerator,
        INotifier notifier,
        Func<bool> ensureConnected,
        string? initialStatus,
        MonitorSettings settings,
        Action<MonitorSettings> onSaved)
        : this()
    {
        _enumerator = enumerator;
        _notifier = notifier;
        _ensureConnected = ensureConnected;
        _initialStatus = initialStatus;
        _settings = settings.Clone();
        _onSaved = onSaved;
        _lightingService = PlatformAdapters.CreateLightingService(_settings);

        PollInterval.Value = _settings.PollingIntervalSeconds;
        LowThreshold.Value = _settings.LowThresholdPercent;
        CriticalThreshold.Value = _settings.CriticalThresholdPercent;
        NotifyLow.IsChecked = _settings.NotifyOnLow;
        NotifyCritical.IsChecked = _settings.NotifyOnCritical;
        NotificationRouteCombo.ItemsSource = NotificationRouteItem.CreateDefaultItems();
        NotificationRouteCombo.SelectedItem = NotificationRouteItem.FromRoute(_settings.NotificationRoute);
        EnableBatteryLed.IsChecked = _settings.EnableBatteryLed;
        BatteryLedTargetCombo.ItemsSource = BatteryLedTargetItem.CreateDefaultItems();
        BatteryLedTargetCombo.SelectedItem = BatteryLedTargetItem.FromTarget(_settings.BatteryLedTarget);
        LightingControlModeCombo.ItemsSource = LightingControlModeItem.CreateDefaultItems();
        LightingControlModeCombo.SelectedItem = LightingControlModeItem.FromMode(_settings.LightingControlMode);
        BatteryColorHighPicker.Color = ParseColor(_settings.BatteryColorHigh, Colors.Lime);
        BatteryColorMidPicker.Color = ParseColor(_settings.BatteryColorMid, Colors.Yellow);
        BatteryColorLowPicker.Color = ParseColor(_settings.BatteryColorLow, Colors.Red);
        if (!string.IsNullOrWhiteSpace(_initialStatus))
            StatusText.Text = _initialStatus;

        RefreshDevices.Click += (_, _) => PopulateDevices();
        TestLedButton.Click += OnTestLed;
        EnableBatteryLed.IsCheckedChanged += (_, _) => UpdateLedControlsEnabled();
        PopulateDevices();
        UpdateLedControlsEnabled();
    }

    private void PopulateDevices()
    {
        if (_enumerator == null || _settings == null) return;
        if (_ensureConnected != null && !_ensureConnected())
        {
            StatusText.Text = "iCUE SDK not available. Start iCUE and enable SDK in settings.";
            DeviceCombo.ItemsSource = new List<DeviceComboItem> { new("(No devices)", null, null) };
            DeviceCombo.SelectedIndex = 0;
            return;
        }

        var devices = _enumerator.GetDevices(includeBattery: true).ToList();
        var items = new List<DeviceComboItem> { new("(Auto-detect)", null, null) };
        items.AddRange(devices.Select(d =>
        {
            string label = d.DisplayName;
            if (d.BatteryPercent.HasValue)
                label = $"{label} - {d.BatteryPercent.Value}%";
            else
                label = $"{label} - n/a";
            return new DeviceComboItem(label, d.DeviceKey, d.DisplayName);
        }));

        DeviceCombo.ItemsSource = items;
        DeviceCombo.SelectedItem = items.FirstOrDefault(i => i.DeviceKey == _settings.DeviceKey)
            ?? items.FirstOrDefault(i => i.DeviceKey == null);
        StatusText.Text = devices.Count == 0
            ? "No compatible devices detected."
            : string.Empty;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_settings == null || _onSaved == null) return;
        ApplySettingsFromUi(_settings);
        _lightingService = PlatformAdapters.CreateLightingService(_settings);
        _onSaved(_settings.Clone());
        StatusText.Text = "Settings saved.";
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnTestNotifications(object? sender, RoutedEventArgs e)
    {
        if (_notifier == null || _settings == null)
            return;

        if (!NotificationSupport.IsAvailable)
        {
            StatusText.Text = "Toast notifications are not available on this platform/build.";
            return;
        }

        var route = NotificationRoute.ToastAndSound;
        if (NotificationRouteCombo.SelectedItem is NotificationRouteItem routeItem)
            route = routeItem.Route;

        int percent = (int)(CriticalThreshold.Value ?? _settings.CriticalThresholdPercent);
        _notifier.NotifyLowBattery("Scimitar Battery", percent, BatterySeverity.Critical, route);
    }

    private void OnTestLed(object? sender, RoutedEventArgs e)
    {
        if (_settings == null)
        {
            StatusText.Text = "LED control is not available.";
            return;
        }

        if (_ensureConnected != null && !_ensureConnected())
        {
            StatusText.Text = "iCUE SDK not available. Start iCUE and enable SDK in settings.";
            return;
        }

        var snapshot = _settings.Clone();
        ApplySettingsFromUi(snapshot);

        if (!snapshot.EnableBatteryLed)
        {
            StatusText.Text = "Enable the LED indicator first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(snapshot.DeviceKey))
        {
            StatusText.Text = "Select a device first.";
            return;
        }

        var lighting = PlatformAdapters.CreateLightingService(snapshot);
        if (lighting == null)
        {
            StatusText.Text = "LED control is not available.";
            return;
        }

        try
        {
            lighting.TestLighting(snapshot.DeviceKey);
            StatusText.Text = "LED test sent (high color).";
        }
        catch (InvalidOperationException ex)
        {
            var debugInfo = TryGetLightingDebugInfo(lighting, snapshot.DeviceKey);
            StatusText.Text = string.IsNullOrWhiteSpace(debugInfo)
                ? ex.Message
                : $"{ex.Message} {debugInfo}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "LED test failed. See startup-error.txt for details.";
            Program.ShowStartupError(ex);
        }
        finally
        {
            if (lighting is IDisposable disposable)
                disposable.Dispose();
        }
    }

    private void ApplySettingsFromUi(MonitorSettings target)
    {
        if (DeviceCombo.SelectedItem is DeviceComboItem selected)
        {
            target.DeviceKey = selected.DeviceKey;
            target.DeviceDisplayName = selected.DisplayName;
        }
        target.PollingIntervalSeconds = (int)(PollInterval.Value ?? target.PollingIntervalSeconds);
        target.LowThresholdPercent = (int)(LowThreshold.Value ?? target.LowThresholdPercent);
        target.CriticalThresholdPercent = (int)(CriticalThreshold.Value ?? target.CriticalThresholdPercent);
        target.NotifyOnLow = NotifyLow.IsChecked ?? true;
        target.NotifyOnCritical = NotifyCritical.IsChecked ?? true;
        if (NotificationRouteCombo.SelectedItem is NotificationRouteItem routeItem)
            target.NotificationRoute = routeItem.Route;
        target.EnableBatteryLed = EnableBatteryLed.IsChecked ?? false;
        if (BatteryLedTargetCombo.SelectedItem is BatteryLedTargetItem targetItem)
            target.BatteryLedTarget = targetItem.Target;
        if (LightingControlModeCombo.SelectedItem is LightingControlModeItem modeItem)
            target.LightingControlMode = modeItem.Mode;
        // Reduce UI confusion: fallback behavior is tied to control mode.
        target.AllowExclusiveFallback = target.LightingControlMode == LightingControlMode.Shared;
        target.BatteryColorHigh = ToHex(BatteryColorHighPicker.Color);
        target.BatteryColorMid = ToHex(BatteryColorMidPicker.Color);
        target.BatteryColorLow = ToHex(BatteryColorLowPicker.Color);
    }

    private void UpdateLedControlsEnabled()
    {
        bool enabled = EnableBatteryLed.IsChecked ?? false;
        BatteryLedTargetCombo.IsEnabled = enabled;
        LightingControlModeCombo.IsEnabled = enabled;
        TestLedButton.IsEnabled = enabled;
        BatteryColorHighPicker.IsEnabled = enabled;
        BatteryColorMidPicker.IsEnabled = enabled;
        BatteryColorLowPicker.IsEnabled = enabled;
    }

    private static Color ParseColor(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return fallback;
        try
        {
            return Color.Parse(hex);
        }
        catch
        {
            return fallback;
        }
    }

    private static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static string? TryGetLightingDebugInfo(ILightingService? lighting, string deviceKey)
    {
        if (lighting == null)
            return null;

        try
        {
            var method = lighting.GetType().GetMethod("GetDebugInfo");
            if (method == null)
                return null;
            return method.Invoke(lighting, new object[] { deviceKey }) as string;
        }
        catch
        {
            return null;
        }
    }

    private sealed record DeviceComboItem(string Label, string? DeviceKey, string? DisplayName)
    {
        public override string ToString() => Label;
    }

    private sealed record NotificationRouteItem(string Label, NotificationRoute Route)
    {
        public override string ToString() => Label;

        public static List<NotificationRouteItem> CreateDefaultItems() => new()
        {
            new("Toast + sound", NotificationRoute.ToastAndSound),
            new("Toast only", NotificationRoute.Toast),
            new("Sound only", NotificationRoute.Sound),
            new("Off", NotificationRoute.None)
        };

        public static NotificationRouteItem FromRoute(NotificationRoute route)
        {
            return CreateDefaultItems().First(i => i.Route == route);
        }
    }

    private sealed record BatteryLedTargetItem(string Label, BatteryLedTarget Target)
    {
        public override string ToString() => Label;

        public static List<BatteryLedTargetItem> CreateDefaultItems() => new()
        {
            new("Logo (best guess)", BatteryLedTarget.LogoBestGuess),
            new("All LEDs", BatteryLedTarget.AllLeds)
        };

        public static BatteryLedTargetItem FromTarget(BatteryLedTarget target)
        {
            return CreateDefaultItems().First(i => i.Target == target);
        }
    }

    private sealed record LightingControlModeItem(string Label, LightingControlMode Mode)
    {
        public override string ToString() => Label;

        public static List<LightingControlModeItem> CreateDefaultItems() => new()
        {
            new("Shared (recommended)", LightingControlMode.Shared),
            new("Exclusive (overrides iCUE lighting)", LightingControlMode.Exclusive)
        };

        public static LightingControlModeItem FromMode(LightingControlMode mode)
        {
            return CreateDefaultItems().First(i => i.Mode == mode);
        }
    }
}
