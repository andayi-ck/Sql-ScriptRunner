using System;
using System.Windows;
using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Position;
using ToastNotifications.Messages;

namespace KabarakScriptRunnerApp.Services
{
    /// <summary>
    /// Central wrapper around the ToastNotifications library.
    /// Call Initialize() once at app startup.
    /// </summary>
    public static class ToastService
    {
        private static Notifier? _notifierTopRight;
        private static Notifier? _notifierBottomRight;

        public static void Initialize()
        {
            if (_notifierTopRight != null) return;

            _notifierTopRight = new Notifier(cfg =>
            {
                cfg.PositionProvider = new PrimaryScreenPositionProvider(
                    corner: Corner.TopRight,
                    offsetX: 12,
                    offsetY: 12);

                cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                    notificationLifetime: TimeSpan.FromSeconds(4),
                    maximumNotificationCount: MaximumNotificationCount.FromCount(4));

                cfg.Dispatcher = Application.Current.Dispatcher;
            });

            _notifierBottomRight = new Notifier(cfg =>
            {
                cfg.PositionProvider = new PrimaryScreenPositionProvider(
                    corner: Corner.BottomRight,
                    offsetX: 12,
                    offsetY: 12);

                cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(
                    notificationLifetime: TimeSpan.FromSeconds(6),
                    maximumNotificationCount: MaximumNotificationCount.FromCount(3));

                cfg.Dispatcher = Application.Current.Dispatcher;
            });
        }

        public static void Success(string message) => _notifierTopRight?.ShowSuccess(message);
        public static void Error(string message) => _notifierTopRight?.ShowError(message);
        public static void Warning(string message) => _notifierTopRight?.ShowWarning(message);
        public static void Information(string message) => _notifierTopRight?.ShowInformation(message);
        public static void Progress(string message) => _notifierBottomRight?.ShowInformation(message);
    }
}