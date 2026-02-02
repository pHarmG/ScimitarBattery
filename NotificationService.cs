using System;

namespace ScimitarBattery
{
    /// <summary>
    /// Handles low-battery notifications: toast message and alert sound.
    /// </summary>
    public sealed class NotificationService
    {
        private readonly bool _useToast;
        private readonly bool _useSound;
        private readonly object _lastAlertLock = new();
        private DateTime _lastAlertTime = DateTime.MinValue;
        private const int MinSecondsBetweenAlerts = 60;

        public NotificationService(bool useToast, bool useSound)
        {
            _useToast = useToast;
            _useSound = useSound;
        }

        /// <summary>
        /// Fires a low-battery alert (toast and/or sound) if enabled and not recently shown.
        /// </summary>
        public void NotifyLowBattery(string deviceDisplayName, int batteryPercent)
        {
            lock (_lastAlertLock)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastAlertTime).TotalSeconds < MinSecondsBetweenAlerts)
                    return;
                _lastAlertTime = now;
            }

            string message = $"Battery low: {deviceDisplayName} at {batteryPercent}%";

            if (_useToast)
                ShowToast(message);

            if (_useSound)
                PlayAlertSound();
        }

        private void ShowToast(string message)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                    ShowWindowsToast(message);
                else
                    ShowConsoleToast(message);
            }
            catch
            {
                ShowConsoleToast(message);
            }
        }

        private void ShowConsoleToast(string message)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[ALERT] {message}");
            Console.ForegroundColor = prev;
        }

        private void ShowWindowsToast(string message)
        {
            try
            {
#if WINDOWS_TOAST
                WindowsToastHelper.Show(message);
#else
                ShowConsoleToast(message);
#endif
            }
            catch
            {
                ShowConsoleToast(message);
            }
        }

        private void PlayAlertSound()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Console.Beep(800, 200);
                    Console.Beep(600, 200);
                }
            }
            catch
            {
                // Ignore sound failures (e.g. no console).
            }
        }
    }
}
