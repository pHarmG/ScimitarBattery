using System;

namespace ScimitarBattery
{
    /// <summary>
    /// User-configurable settings for battery monitoring and notifications.
    /// </summary>
    public sealed class MonitorSettings
    {
        /// <summary>Selected Corsair device ID to monitor (e.g. mouse).</summary>
        public string? DeviceId { get; set; }

        /// <summary>Human-readable device name for display (e.g. model name).</summary>
        public string? DeviceDisplayName { get; set; }

        /// <summary>How often to poll battery level, in seconds. Clamped to 1–3600.</summary>
        public int PollingIntervalSeconds
        {
            get => _pollingIntervalSeconds;
            set => _pollingIntervalSeconds = Math.Clamp(value, 1, 3600);
        }
        private int _pollingIntervalSeconds = 5;

        /// <summary>Alert when battery falls at or below this percentage (0–100).</summary>
        public int AlertThresholdPercent
        {
            get => _alertThresholdPercent;
            set => _alertThresholdPercent = Math.Clamp(value, 0, 100);
        }
        private int _alertThresholdPercent = 15;

        /// <summary>Show a toast (or console) notification when battery is low.</summary>
        public bool UseToastNotification { get; set; } = true;

        /// <summary>Play an alert sound when battery is low.</summary>
        public bool UseAlertSound { get; set; } = true;

        public TimeSpan PollingInterval => TimeSpan.FromSeconds(PollingIntervalSeconds);

        public MonitorSettings Clone()
        {
            return new MonitorSettings
            {
                DeviceId = DeviceId,
                DeviceDisplayName = DeviceDisplayName,
                _pollingIntervalSeconds = _pollingIntervalSeconds,
                _alertThresholdPercent = _alertThresholdPercent,
                UseToastNotification = UseToastNotification,
                UseAlertSound = UseAlertSound
            };
        }
    }
}
