namespace ScimitarBattery.Core;

/// <summary>
/// Polls the selected device's battery on a background schedule, tracks severity state,
/// and notifies the tray service on the UI thread. Platform-agnostic; uses interfaces only.
/// </summary>
public sealed class BatteryMonitorService
{
    private readonly MonitorSettings _settings;
    private readonly IBatteryProvider _batteryProvider;
    private readonly IDeviceEnumerator _deviceEnumerator;
    private readonly ITrayIconService _trayIconService;
    private readonly INotifier? _notifier;
    private readonly Action<Action> _dispatchToUi;
    private readonly ILightingService? _lightingService;

    private int? _lastPercent;
    private BatterySeverity _lastSeverity = BatterySeverity.Unknown;
    private const int IconBucketStep = 10; // Update icon on 10% steps
    /// <summary>Drop (last - current) larger than this in one poll is treated as likely bogus (e.g. wake-from-sleep glitch).</summary>
    private const int SuspiciousDropThreshold = 40;

    public BatteryMonitorService(
        MonitorSettings settings,
        IBatteryProvider batteryProvider,
        IDeviceEnumerator deviceEnumerator,
        ITrayIconService trayIconService,
        Action<Action> dispatchToUi,
        INotifier? notifier = null,
        ILightingService? lightingService = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _batteryProvider = batteryProvider ?? throw new ArgumentNullException(nameof(batteryProvider));
        _deviceEnumerator = deviceEnumerator ?? throw new ArgumentNullException(nameof(deviceEnumerator));
        _trayIconService = trayIconService ?? throw new ArgumentNullException(nameof(trayIconService));
        _dispatchToUi = dispatchToUi ?? throw new ArgumentNullException(nameof(dispatchToUi));
        _notifier = notifier;
        _lightingService = lightingService;
    }

    /// <summary>
    /// Resolves device key from settings or uses default, then runs the poll loop until cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        string? deviceKey = string.IsNullOrWhiteSpace(_settings.DeviceKey)
            ? _deviceEnumerator.GetDefaultDeviceKey()
            : _settings.DeviceKey;

        string? displayName = _settings.DeviceDisplayName;
        if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(deviceKey))
        {
            var devices = _deviceEnumerator.GetDevices(includeBattery: false);
            var dev = devices.FirstOrDefault(d => string.Equals(d.DeviceKey, deviceKey, StringComparison.Ordinal));
            displayName = dev?.DisplayName ?? deviceKey;
        }
        displayName ??= deviceKey ?? "Unknown device";

        if (string.IsNullOrWhiteSpace(deviceKey))
        {
            _dispatchToUi(() => _trayIconService.UpdateBatteryState("No device selected", null));
            return;
        }

        // Do an immediate update on start so UI/LED aren't delayed by the first timer tick.
        await UpdateOnce(deviceKey, displayName, cancellationToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(_settings.PollingInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            await UpdateOnce(deviceKey, displayName, cancellationToken).ConfigureAwait(false);
        }
    }

    private Task UpdateOnce(string deviceKey, string displayName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int? percent = _batteryProvider.GetBatteryPercent(deviceKey);
        BatterySeverity severity = ComputeSeverity(percent);
        // Keep device lighting alive every polling cycle (helps after transient reconnects).
        _lightingService?.UpdateBatteryLighting(deviceKey, percent);

        // After wake-from-sleep the SDK sometimes returns a bogus low value once. Treat a large
        // single-poll drop as suspicious and keep showing last-known in the tray until next poll.
        bool suspiciousDrop = percent.HasValue && _lastPercent.HasValue &&
            (_lastPercent.Value - percent.Value) > SuspiciousDropThreshold;

        int? trayPercent;
        if (suspiciousDrop)
            trayPercent = _lastPercent;
        else
            trayPercent = percent ?? _lastPercent;

        _dispatchToUi(() => _trayIconService.UpdateBatteryState(displayName, trayPercent));

        if (suspiciousDrop)
        {
            // Don't update _lastPercent or _lastSeverity; next poll may return the real value.
            return Task.CompletedTask;
        }

        bool severityChanged = severity != _lastSeverity;
        int? lastBucket = _lastPercent.HasValue ? Bucket(_lastPercent.Value) : null;
        int? currentBucket = percent.HasValue ? Bucket(percent.Value) : null;
        bool bucketChanged = lastBucket != currentBucket;
        bool percentChanged = _lastPercent != percent;
        bool stateChanged = severityChanged || bucketChanged || percentChanged;

        if (stateChanged)
        {
            _lastPercent = percent;
            _lastSeverity = severity;

            if (percent.HasValue)
            {
                if (severity == BatterySeverity.Low && _settings.NotifyOnLow)
                    _notifier?.NotifyLowBattery(displayName, percent.Value, severity, _settings.NotificationRoute);
                else if (severity == BatterySeverity.Critical && _settings.NotifyOnCritical)
                    _notifier?.NotifyLowBattery(displayName, percent.Value, severity, _settings.NotificationRoute);
            }
        }

        return Task.CompletedTask;
    }

    private BatterySeverity ComputeSeverity(int? percent)
    {
        if (!percent.HasValue)
            return BatterySeverity.Unknown;
        if (percent.Value <= _settings.CriticalThresholdPercent)
            return BatterySeverity.Critical;
        if (percent.Value <= _settings.LowThresholdPercent)
            return BatterySeverity.Low;
        return BatterySeverity.Normal;
    }

    private static int Bucket(int percent) => (percent / IconBucketStep) * IconBucketStep;
}
