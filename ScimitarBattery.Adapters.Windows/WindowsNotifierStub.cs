using System;
using CommunityToolkit.WinUI.Notifications;
using Windows.UI.Notifications;
using ScimitarBattery.Core;

namespace ScimitarBattery.Adapters.Windows;

/// <summary>
/// Windows notifier using Windows App SDK toasts.
/// </summary>
public sealed class WindowsNotifierStub : INotifier
{
    private static bool _registered;
    public static bool IsAvailable => true;
    private const string ToastAppId = "ScimitarBattery";

    public void NotifyLowBattery(string deviceDisplayName, int batteryPercent, BatterySeverity severity, NotificationRoute route)
    {
        if (route == NotificationRoute.None)
            return;

        EnsureRegistered();
        var toast = BuildToast(deviceDisplayName, batteryPercent, severity, route);
        ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
    }

    private static void EnsureRegistered()
    {
        if (_registered)
            return;
        ToastNotificationManagerCompat.OnActivated += _ => { };
        _registered = true;
    }

    private static ToastNotification BuildToast(string deviceDisplayName, int batteryPercent, BatterySeverity severity, NotificationRoute route)
    {
        string title = $"{deviceDisplayName} battery";
        string body = $"{severity} - {batteryPercent}% remaining";
        string sound = route is NotificationRoute.Sound or NotificationRoute.ToastAndSound
            ? "ms-winsoundevent:Notification.Looping.Alarm2"
            : string.Empty;

        var builder = new ToastContentBuilder()
            .AddText(title)
            .AddText(body);

        if (!string.IsNullOrEmpty(sound))
            builder.AddAudio(new ToastAudio { Src = new Uri(sound) });
        else
            builder.AddAudio(new ToastAudio { Silent = true });

        var xml = builder.GetToastContent().GetXml();
        var toast = new ToastNotification(xml);
        if (route == NotificationRoute.Sound)
            toast.SuppressPopup = true;
        return toast;
    }
}
