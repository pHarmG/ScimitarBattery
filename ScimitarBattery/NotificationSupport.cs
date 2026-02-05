#if WINDOWS
using ScimitarBattery.Adapters.Windows;
#else
using ScimitarBattery.Adapters.MacOS;
#endif

namespace ScimitarBattery;

internal static class NotificationSupport
{
#if WINDOWS
    public static bool IsAvailable => WindowsNotifierStub.IsAvailable;
#else
    public static bool IsAvailable => MacNotifierStub.IsAvailable;
#endif
}
