using Avalonia.Controls.Notifications;
using System;
using Notification = Ursa.Controls.Notification;
using WindowNotificationManager = Ursa.Controls.WindowNotificationManager;
namespace NBLApp.Controls;
public class Notify
{
    private static WindowNotificationManager? _wnm;
    public static TimeSpan Delay = TimeSpan.FromSeconds(7);
    public static void Init(WindowNotificationManager wnm)
    {
        if (_wnm is not null) throw new Exception($"Initialization already completed");
        Notify._wnm = wnm;
    }
    public static void ShowInfo(string header, string content, bool showIcon = true, bool showClose = true)
    {
        Notify._wnm?.Show(
            new Notification(header, content),
            showIcon: showIcon,
            showClose: showClose,
            expiration: Notify.Delay,
            type: NotificationType.Information);
    }
    public static void ShowError(string header, string content, bool showIcon = true, bool showClose = true)
    {
        Notify._wnm?.Show(
            new Notification(header, content),
            showIcon: showIcon,
            showClose: showClose,
            expiration: Notify.Delay,
            type: NotificationType.Error);
    }
    public static void ShowWarning(string header, string content, bool showIcon = true, bool showClose = true)
    {
        Notify._wnm?.Show(
            new Notification(header, content),
            showIcon: showIcon,
            showClose: showClose,
            expiration: Notify.Delay,
            type: NotificationType.Warning);
    }
}