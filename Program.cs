using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ScimitarBattery
{
    internal static class Program
    {
        private static CorsairNative.CorsairSessionStateChangedHandler? _stateHandler;

        private static void OnSessionStateChanged(IntPtr ctx, ref CorsairNative.CorsairSessionStateChanged ev)
        {
            Console.WriteLine(
                $"[STATE] {ev.state} | client={ev.details.clientVersion} " +
                $"server={ev.details.serverVersion} icue={ev.details.serverHostVersion}");
        }

        public static async Task Main(string[] args)
        {
            Console.WriteLine("Scimitar Battery Monitor (iCUE SDK)");
            Console.WriteLine("Ensure iCUE is running and SDK is enabled.");
            Console.WriteLine();

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                Console.WriteLine("\nStopping...");
            };

            _stateHandler = OnSessionStateChanged;
            int err = CorsairNative.CorsairConnect(_stateHandler, IntPtr.Zero);
            Console.WriteLine($"CorsairConnect returned: {err}");
            if (err != 0)
            {
                Console.WriteLine("Cannot connect to iCUE. Exiting.");
                return;
            }

            await Task.Delay(800).ConfigureAwait(false);

            var deviceService = new CorsairDeviceService();
            var settings = SettingsStorage.Load() ?? new MonitorSettings();

            // ----- Console UI: configure settings -----
            await RunSettingsMenuAsync(deviceService, settings, cts.Token).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(settings.DeviceId))
            {
                settings.DeviceId = deviceService.FindScimitarOrFirstMouseId();
                if (!string.IsNullOrWhiteSpace(settings.DeviceId))
                {
                    var devices = deviceService.GetMouseDevices(includeBattery: false);
                    var dev = devices.FirstOrDefault(d => string.Equals(d.Id, settings.DeviceId, StringComparison.Ordinal));
                    settings.DeviceDisplayName = dev?.DisplayName;
                }
            }

            if (string.IsNullOrWhiteSpace(settings.DeviceId))
            {
                Console.WriteLine("No device selected and no mouse found. Exiting.");
                return;
            }

            var notificationService = new NotificationService(settings.UseToastNotification, settings.UseAlertSound);
            var monitor = new BatteryMonitorService(settings, deviceService, notificationService);

            Console.WriteLine();
            Console.WriteLine($"Monitoring: {GetDeviceDisplayText(settings)}");
            Console.WriteLine($"Polling every {settings.PollingIntervalSeconds}s, alert when ≤ {settings.AlertThresholdPercent}%");
            Console.WriteLine($"Toast: {settings.UseToastNotification}, Sound: {settings.UseAlertSound}");
            Console.WriteLine("Press Ctrl+C to stop.");
            Console.WriteLine();

            try
            {
                await monitor.RunAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on Ctrl+C
            }

            Console.WriteLine("Exited cleanly.");
        }

        private static async Task RunSettingsMenuAsync(
            CorsairDeviceService deviceService,
            MonitorSettings settings,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                PrintMainMenu(settings);
                string? input = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(input))
                    continue;

                switch (input.ToUpperInvariant())
                {
                    case "1":
                        await SelectDeviceAsync(deviceService, settings, cancellationToken).ConfigureAwait(false);
                        break;
                    case "2":
                        SetPollingFrequency(settings);
                        break;
                    case "3":
                        SetAlertThreshold(settings);
                        break;
                    case "4":
                        SetNotificationPreferences(settings);
                        break;
                    case "S":
                    case "START":
                        SettingsStorage.Save(settings);
                        return;
                    case "Q":
                    case "QUIT":
                        Environment.Exit(0);
                        break;
                    default:
                        Console.WriteLine("Unknown option.");
                        break;
                }

                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }

        private static string GetDeviceDisplayText(MonitorSettings settings)
        {
            if (string.IsNullOrEmpty(settings.DeviceId))
                return "(auto-detect)";
            return settings.DeviceDisplayName ?? settings.DeviceId;
        }

        private static void PrintMainMenu(MonitorSettings settings)
        {
            Console.WriteLine("--- Settings ---");
            Console.WriteLine($"  1) Device .............. {GetDeviceDisplayText(settings)}");
            Console.WriteLine($"  2) Polling frequency ... {settings.PollingIntervalSeconds} sec");
            Console.WriteLine($"  3) Alert threshold ...... ≤ {settings.AlertThresholdPercent}%");
            Console.WriteLine($"  4) Notifications ........ Toast: {(settings.UseToastNotification ? "on" : "off")}, Sound: {(settings.UseAlertSound ? "on" : "off")}");
            Console.WriteLine();
            Console.WriteLine("  S) Start monitoring     Q) Quit");
            Console.Write("Choice [1-4, S, Q]: ");
        }

        private static async Task SelectDeviceAsync(
            CorsairDeviceService deviceService,
            MonitorSettings settings,
            CancellationToken cancellationToken)
        {
            var devices = deviceService.GetMouseDevices(includeBattery: true);
            if (devices.Count == 0)
            {
                Console.WriteLine("No Corsair mouse devices found.");
                return;
            }

            Console.WriteLine("Available devices:");
            for (int i = 0; i < devices.Count; i++)
            {
                var d = devices[i];
                string batt = d.BatteryPercent.HasValue ? $"{d.BatteryPercent}%" : "n/a";
                Console.WriteLine($"  {i + 1}) {d.DisplayName}  [battery: {batt}]");
            }
            Console.WriteLine($"  0) Auto-detect (Scimitar or first mouse)");
            Console.Write("Select number [0-{0}]: ", devices.Count);

            string? line = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(line))
                return;

            if (!int.TryParse(line, out int choice))
                return;

            if (choice == 0)
            {
                settings.DeviceId = null;
                settings.DeviceDisplayName = null;
                SettingsStorage.Save(settings);
                Console.WriteLine("Device set to auto-detect.");
                return;
            }

            if (choice >= 1 && choice <= devices.Count)
            {
                var chosen = devices[choice - 1];
                settings.DeviceId = chosen.Id;
                settings.DeviceDisplayName = chosen.DisplayName;
                SettingsStorage.Save(settings);
                Console.WriteLine($"Device set to: {chosen.DisplayName}");
            }
            else
                Console.WriteLine("Invalid selection.");

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static void SetPollingFrequency(MonitorSettings settings)
        {
            Console.Write($"Polling interval in seconds [1-3600] (current: {settings.PollingIntervalSeconds}): ");
            string? line = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(line))
                return;
            if (int.TryParse(line, out int sec))
            {
                settings.PollingIntervalSeconds = sec;
                SettingsStorage.Save(settings);
                Console.WriteLine($"Polling set to every {settings.PollingIntervalSeconds} sec.");
            }
            else
                Console.WriteLine("Invalid number.");
        }

        private static void SetAlertThreshold(MonitorSettings settings)
        {
            Console.Write($"Alert when battery is at or below [0-100]% (current: {settings.AlertThresholdPercent}): ");
            string? line = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(line))
                return;
            if (int.TryParse(line, out int pct))
            {
                settings.AlertThresholdPercent = pct;
                SettingsStorage.Save(settings);
                Console.WriteLine($"Alert threshold set to ≤ {settings.AlertThresholdPercent}%.");
            }
            else
                Console.WriteLine("Invalid number.");
        }

        private static void SetNotificationPreferences(MonitorSettings settings)
        {
            Console.WriteLine("Notification preferences:");
            Console.Write($"  Show toast when low battery? [Y/n] (current: {(settings.UseToastNotification ? "Y" : "n")}): ");
            string? toast = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(toast))
                settings.UseToastNotification = !toast.StartsWith("n", StringComparison.OrdinalIgnoreCase);

            Console.Write($"  Play alert sound when low battery? [Y/n] (current: {(settings.UseAlertSound ? "Y" : "n")}): ");
            string? sound = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(sound))
                settings.UseAlertSound = !sound.StartsWith("n", StringComparison.OrdinalIgnoreCase);

            SettingsStorage.Save(settings);
            Console.WriteLine($"Toast: {(settings.UseToastNotification ? "on" : "off")}, Sound: {(settings.UseAlertSound ? "on" : "off")}");
        }
    }
}
