using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private bool _lightingSupported;
    private readonly ObservableCollection<LedSelectionItem> _ledSelectionItems = new();

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
        _lightingSupported = PlatformAdapters.SupportsLighting;

        PollInterval.Value = _settings.PollingIntervalSeconds;
        LowThreshold.Value = _settings.LowThresholdPercent;
        CriticalThreshold.Value = _settings.CriticalThresholdPercent;
        NotifyLow.IsChecked = _settings.NotifyOnLow;
        NotifyCritical.IsChecked = _settings.NotifyOnCritical;
        NotificationRouteCombo.ItemsSource = NotificationRouteItem.CreateDefaultItems();
        NotificationRouteCombo.SelectedItem = NotificationRouteItem.FromRoute(_settings.NotificationRoute);
        EnableBatteryLed.IsChecked = _settings.EnableBatteryLed && _lightingSupported;
        if (PlatformAdapters.SupportsStartup)
        {
            var startupEnabled = PlatformAdapters.IsStartupEnabled();
            _settings.StartWithWindows = startupEnabled;
            StartWithWindows.IsChecked = startupEnabled;
            StartWithWindows.IsEnabled = true;
            StartupHintText.Text = "Runs quietly in the tray. You can open Settings from the tray icon.";
        }
        else
        {
            StartWithWindows.IsChecked = false;
            StartWithWindows.IsEnabled = false;
            StartupHintText.Text = "Start at login is currently unavailable on macOS.";
        }
        BatteryLedTargetCombo.ItemsSource = BatteryLedTargetItem.CreateDefaultItems();
        BatteryLedTargetCombo.SelectedItem = BatteryLedTargetItem.FromTarget(_settings.BatteryLedTarget);
        LightingControlModeCombo.ItemsSource = LightingControlModeItem.CreateDefaultItems();
        LightingControlModeCombo.SelectedItem = LightingControlModeItem.FromMode(_settings.LightingControlMode);
        BatteryColorHighPicker.Color = ParseColor(_settings.BatteryColorHigh, Colors.Lime);
        BatteryColorMidPicker.Color = ParseColor(_settings.BatteryColorMid, Colors.Yellow);
        BatteryColorLowPicker.Color = ParseColor(_settings.BatteryColorLow, Colors.Red);
        if (!string.IsNullOrWhiteSpace(_initialStatus))
            StatusText.Text = _initialStatus;
        else if (!_lightingSupported)
            StatusText.Text = "LED control is not available on this platform yet.";

        RefreshDevices.Click += (_, _) => PopulateDevices();
        DeviceCombo.SelectionChanged += (_, _) => UpdateLedSelectionList();
        TestLedButton.Click += OnTestLed;
        EnableBatteryLed.IsCheckedChanged += (_, _) => UpdateLedControlsEnabled();
        PopulateDevices();
        LedSelectionList.ItemsSource = _ledSelectionItems;
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
        DeviceCombo.SelectedItem = ResolvePreferredDeviceItem(items);
        StatusText.Text = devices.Count == 0
            ? "No compatible devices detected."
            : string.Empty;
        UpdateLedSelectionList();
    }

    private DeviceComboItem? ResolvePreferredDeviceItem(IReadOnlyList<DeviceComboItem> items)
    {
        if (_settings != null && !string.IsNullOrWhiteSpace(_settings.DeviceKey))
        {
            var saved = items.FirstOrDefault(i => i.DeviceKey == _settings.DeviceKey);
            if (saved != null)
                return saved;
        }

        var preferred = _enumerator?.GetDefaultDeviceKey();
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var match = items.FirstOrDefault(i => i.DeviceKey == preferred);
            if (match != null)
                return match;
        }

        var scimitar = items.FirstOrDefault(i =>
            !string.IsNullOrWhiteSpace(i.DisplayName) &&
            i.DisplayName.Contains("SCIMITAR", StringComparison.OrdinalIgnoreCase));
        if (scimitar != null)
            return scimitar;

        var mouse = items.FirstOrDefault(i =>
            !string.IsNullOrWhiteSpace(i.DisplayName) &&
            i.DisplayName.Contains("MOUSE", StringComparison.OrdinalIgnoreCase));
        if (mouse != null)
            return mouse;

        return items.FirstOrDefault(i => i.DeviceKey != null)
            ?? items.FirstOrDefault(i => i.DeviceKey == null);
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_settings == null || _onSaved == null) return;
        ApplySettingsFromUi(_settings);
        UpdateStartupSetting(_settings);
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
            var selected = _ledSelectionItems
                .Where(item => item.IsSelected && item.IsSelectable)
                .Select(item => item.LedId)
                .ToList();
            lighting.TestLighting(snapshot.DeviceKey, selected.Count > 0 ? selected : null);
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
        target.StartWithWindows = PlatformAdapters.SupportsStartup && (StartWithWindows.IsChecked ?? false);
        if (NotificationRouteCombo.SelectedItem is NotificationRouteItem routeItem)
            target.NotificationRoute = routeItem.Route;
        target.EnableBatteryLed = _lightingSupported && (EnableBatteryLed.IsChecked ?? false);
        if (BatteryLedTargetCombo.SelectedItem is BatteryLedTargetItem targetItem)
            target.BatteryLedTarget = targetItem.Target;
        if (LightingControlModeCombo.SelectedItem is LightingControlModeItem modeItem)
            target.LightingControlMode = modeItem.Mode;
        // Reduce UI confusion: fallback behavior is tied to control mode.
        target.AllowExclusiveFallback = target.LightingControlMode == LightingControlMode.Shared;
        target.CustomLedIds = _ledSelectionItems
            .Where(item => item.IsSelected && item.IsSelectable)
            .Select(item => item.LedId)
            .ToList();
        target.BatteryColorHigh = ToHex(BatteryColorHighPicker.Color);
        target.BatteryColorMid = ToHex(BatteryColorMidPicker.Color);
        target.BatteryColorLow = ToHex(BatteryColorLowPicker.Color);
    }

    private void UpdateStartupSetting(MonitorSettings settings)
    {
        if (!PlatformAdapters.SupportsStartup)
            return;

        // Avoid rewriting startup registration on every Save. On macOS this can
        // cause launchd to spawn another app instance when the agent is touched.
        if (PlatformAdapters.IsStartupEnabled() == settings.StartWithWindows)
            return;

        if (!PlatformAdapters.TrySetStartupEnabled(settings.StartWithWindows, out var error))
        {
            StatusText.Text = string.IsNullOrWhiteSpace(error)
                ? "Unable to update startup setting."
                : $"Unable to update startup setting: {error}";
        }
    }

    private void UpdateLedControlsEnabled()
    {
        EnableBatteryLed.IsEnabled = _lightingSupported;
        bool enabled = _lightingSupported && (EnableBatteryLed.IsChecked ?? false);
        BatteryLedTargetCombo.IsEnabled = enabled;
        LightingControlModeCombo.IsEnabled = enabled;
        TestLedButton.IsEnabled = enabled;
        BatteryColorHighPicker.IsEnabled = enabled;
        BatteryColorMidPicker.IsEnabled = enabled;
        BatteryColorLowPicker.IsEnabled = enabled;
        LedSelectionList.IsEnabled = enabled;
    }

    private void UpdateLedSelectionList()
    {
        _ledSelectionItems.Clear();
        if (!_lightingSupported || _settings == null)
            return;

        if (DeviceCombo.SelectedItem is not DeviceComboItem selected ||
            string.IsNullOrWhiteSpace(selected.DeviceKey))
            return;

        if (_ensureConnected != null && !_ensureConnected())
            return;

        var snapshot = _settings.Clone();
        ApplySettingsFromUi(snapshot);
        snapshot.EnableBatteryLed = true;

        var lighting = PlatformAdapters.CreateLightingService(snapshot);
        if (lighting == null)
            return;

        try
        {
            var leds = lighting.GetAvailableLeds(selected.DeviceKey);
            var preferred = _settings.CustomLedIds;
            foreach (var led in leds)
            {
                bool selectedByDefault = led.IsSelectable && (preferred.Count == 0 || preferred.Contains(led.LedId));
                var item = new LedSelectionItem(led.LedId, led.Label, led.IsSelectable)
                {
                    IsSelected = selectedByDefault
                };
                _ledSelectionItems.Add(item);
            }
        }
        finally
        {
            if (lighting is IDisposable disposable)
                disposable.Dispose();
        }
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
            new("All LEDs", BatteryLedTarget.AllLeds),
            new("Selected LEDs", BatteryLedTarget.CustomSelection)
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

    private sealed class LedSelectionItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public LedSelectionItem(int ledId, string label, bool isSelectable)
        {
            LedId = ledId;
            Label = label;
            IsSelectable = isSelectable;
            _isSelected = isSelectable;
        }

        public int LedId { get; }
        public string Label { get; }
        public bool IsSelectable { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (!IsSelectable)
                    return;
                if (_isSelected == value)
                    return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
