using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScimitarBattery
{
    /// <summary>
    /// Polls the selected device's battery level and triggers notifications when below threshold.
    /// </summary>
    internal sealed class BatteryMonitorService
    {
        private readonly MonitorSettings _settings;
        private readonly CorsairDeviceService _deviceService;
        private readonly NotificationService _notificationService;

        public BatteryMonitorService(
            MonitorSettings settings,
            CorsairDeviceService deviceService,
            NotificationService notificationService)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        /// <summary>
        /// Runs the monitoring loop until cancellation. Resolves device ID from settings (or discovers one) and polls battery.
        /// </summary>
        internal async Task RunAsync(CancellationToken cancellationToken = default)
        {
            string? deviceId = _settings.DeviceId;
            string? displayName = null;

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                deviceId = _deviceService.FindScimitarOrFirstMouseId();
                if (deviceId != null)
                {
                    var devices = _deviceService.GetMouseDevices(includeBattery: false);
                    var dev = devices.FirstOrDefault(d => string.Equals(d.Id, deviceId, StringComparison.Ordinal));
                    displayName = dev?.DisplayName ?? deviceId;
                }
            }
            else
            {
                var devices = _deviceService.GetMouseDevices(includeBattery: false);
                var dev = devices.FirstOrDefault(d => string.Equals(d.Id, deviceId, StringComparison.Ordinal));
                displayName = dev?.DisplayName ?? deviceId;
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                Console.WriteLine("No device selected and no mouse found. Select a device in settings.");
                return;
            }

            displayName ??= deviceId;

            using var timer = new PeriodicTimer(_settings.PollingInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                int? battery = _deviceService.TryGetBatteryPercent(deviceId);
                if (battery == null)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] battery= n/a (read failed)");
                    continue;
                }

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] battery= {battery}%");

                if (battery.Value <= _settings.AlertThresholdPercent)
                    _notificationService.NotifyLowBattery(displayName, battery.Value);
            }
        }
    }
}
