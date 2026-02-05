using ScimitarBattery.Core;

namespace ScimitarBattery;

internal static class PlatformAdapters
{
#if WINDOWS
    public static IDeviceEnumerator CreateDeviceEnumerator() => new ScimitarBattery.Adapters.Windows.CorsairDeviceEnumerator();
    public static IBatteryProvider CreateBatteryProvider() => new ScimitarBattery.Adapters.Windows.CorsairBatteryProvider();
    public static INotifier CreateNotifier() => new ScimitarBattery.Adapters.Windows.WindowsNotifierStub();
    public static bool SupportsLighting => true;
    public static bool SupportsStartup => true;
    public static ILightingService? CreateLightingService(MonitorSettings settings)
        => settings.EnableBatteryLed
            ? new ScimitarBattery.Adapters.Windows.CorsairLightingService(
                settings.BatteryLedTarget,
                settings.CustomLedIds,
                settings.LightingControlMode,
                settings.AllowExclusiveFallback,
                settings.LowThresholdPercent,
                settings.CriticalThresholdPercent,
                settings.BatteryColorHigh,
                settings.BatteryColorMid,
                settings.BatteryColorLow)
            : null;

    public static bool EnsureSdkConnected(ref bool connected, ref string? status)
    {
        if (connected)
            return true;

        int err = ScimitarBattery.Adapters.Windows.CorsairSdkBridge.Connect();
        if (err == 0)
        {
            connected = true;
            status = null;
            return true;
        }

        status = "iCUE SDK not available. Start iCUE and enable SDK in settings.";
        return false;
    }

    public static bool IsStartupEnabled()
        => ScimitarBattery.Adapters.Windows.StartupManager.IsEnabled();

    public static bool TrySetStartupEnabled(bool enabled, out string? error)
        => ScimitarBattery.Adapters.Windows.StartupManager.TrySetEnabled(enabled, out error);
#else
    public static IDeviceEnumerator CreateDeviceEnumerator() => new ScimitarBattery.Adapters.MacOS.MacDeviceEnumerator();
    public static IBatteryProvider CreateBatteryProvider() => new ScimitarBattery.Adapters.MacOS.MacBatteryProvider();
    public static INotifier CreateNotifier() => new ScimitarBattery.Adapters.MacOS.MacNotifierStub();
    public static bool SupportsLighting => false;
    public static bool SupportsStartup => false;
    public static ILightingService? CreateLightingService(MonitorSettings settings) => null;

    public static bool EnsureSdkConnected(ref bool connected, ref string? status)
    {
        connected = true;
        status = null;
        return true;
    }

    public static bool IsStartupEnabled() => false;

    public static bool TrySetStartupEnabled(bool enabled, out string? error)
    {
        error = "Startup control is not available on this platform.";
        return false;
    }
#endif
}
