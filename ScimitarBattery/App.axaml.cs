using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.Controls;
using ScimitarBattery.Core;
#if WINDOWS
using ScimitarBattery.Adapters.Windows;
#else
using ScimitarBattery.Adapters.MacOS;
#endif

namespace ScimitarBattery;

public partial class App : Application
{
    private Window? _settingsWindow;
    private MonitorSettings? _settings;
    private CancellationTokenSource? _monitorCts;
    private readonly INotifier _notifier = PlatformAdapters.CreateNotifier();
    private bool _sdkConnected;
    private string? _sdkStatus;
    private TrayIcon? _trayIcon;
    private NativeMenu? _trayMenu;
    private ILightingService? _lightingService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _settings = SettingsStorage.Load() ?? MonitorSettings.CreateDefaults();
            EnsureSdkConnected();
            InitializeTray(desktop);
            bool autoStart = desktop.Args != null &&
                             desktop.Args.Any(arg => string.Equals(arg, "--autostart", StringComparison.OrdinalIgnoreCase));
            if (!(_settings.StartWithWindows && autoStart))
                ShowSettingsWindow(desktop); // open settings on launch (as if from tray menu)
            StartMonitor(); // start monitoring immediately so LED applies on launch
            desktop.ShutdownRequested += OnShutdownRequested;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_trayIcon != null)
            return;

        _trayMenu = new NativeMenu();
        var settingsItem = new NativeMenuItem("Settings...");
        var exitItem = new NativeMenuItem("Exit");

        settingsItem.Click += (_, _) => ShowSettingsWindow(desktop);
        exitItem.Click += (_, _) =>
        {
            _trayIcon?.Dispose();
            if (_lightingService is IDisposable disposableLighting)
                disposableLighting.Dispose();
            desktop.Shutdown();
        };

        _trayMenu.Add(settingsItem);
        _trayMenu.Add(new NativeMenuItemSeparator());
        _trayMenu.Add(exitItem);

        desktop.ShutdownRequested += OnShutdownRequested;

        // Defer tray setup until after the first message pump.
        Dispatcher.UIThread.Post(() =>
        {
            _trayIcon = new TrayIcon
            {
                Icon = BatteryIconFactory.CreateIcon(null),
                ToolTipText = "Scimitar Battery",
                Menu = _trayMenu
            };
            var trayIcons = new TrayIcons();
            trayIcons.Add(_trayIcon);
            TrayIcon.SetIcons(this, trayIcons);
        });
    }

    private void ShowSettingsWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_settingsWindow != null && _settingsWindow.IsVisible)
        {
            _settingsWindow.Activate();
            return;
        }

        var enumerator = PlatformAdapters.CreateDeviceEnumerator();
        _settingsWindow = new SettingsWindow(
            enumerator,
            _notifier,
            EnsureSdkConnected,
            _sdkStatus,
            (_settings ?? MonitorSettings.CreateDefaults()).Clone(),
            saved =>
        {
            _settings = saved;
            SettingsStorage.Save(_settings);
            StartMonitor();
        });
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        desktop.MainWindow = _settingsWindow;
        _settingsWindow.Show();
    }

    private void StartMonitor()
    {
        if (!EnsureSdkConnected())
            return;

        _monitorCts?.Cancel();
        _monitorCts = new CancellationTokenSource();
        var token = _monitorCts.Token;
        ITrayIconService trayService = _trayIcon != null
            ? new AvaloniaTrayIconService(_trayIcon)
            : new NullTrayIconService();
        void DispatchToUi(Action a) => Dispatcher.UIThread.Post(a);
        var batteryProvider = PlatformAdapters.CreateBatteryProvider();
        var deviceEnumerator = PlatformAdapters.CreateDeviceEnumerator();
        var settings = _settings ?? MonitorSettings.CreateDefaults();
        if (_lightingService is IDisposable disposableLighting)
            disposableLighting.Dispose();
        _lightingService = PlatformAdapters.CreateLightingService(settings);
        ApplyLightingImmediately(settings, deviceEnumerator, batteryProvider, _lightingService);
        var monitor = new BatteryMonitorService(settings, batteryProvider, deviceEnumerator, trayService, DispatchToUi, _notifier, _lightingService);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(800, token).ConfigureAwait(false);
                await monitor.RunAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    private bool EnsureSdkConnected()
    {
        return PlatformAdapters.EnsureSdkConnected(ref _sdkConnected, ref _sdkStatus);
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _monitorCts?.Cancel();
        if (_lightingService is IDisposable disposableLighting)
            disposableLighting.Dispose();
    }

    private static void ApplyLightingImmediately(
        MonitorSettings settings,
        IDeviceEnumerator deviceEnumerator,
        IBatteryProvider batteryProvider,
        ILightingService? lightingService)
    {
        if (lightingService == null || !settings.EnableBatteryLed)
            return;

        string? deviceKey = string.IsNullOrWhiteSpace(settings.DeviceKey)
            ? deviceEnumerator.GetDefaultDeviceKey()
            : settings.DeviceKey;

        if (string.IsNullOrWhiteSpace(deviceKey))
            return;

        int? percent = batteryProvider.GetBatteryPercent(deviceKey);
        lightingService.UpdateBatteryLighting(deviceKey, percent);
    }
}
