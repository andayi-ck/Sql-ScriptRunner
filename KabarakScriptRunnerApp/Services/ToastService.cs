using Notification.Wpf;
using System.Windows;

namespace KabarakScriptRunnerApp.Services
{
    public static class ToastService
    {
        private static readonly NotificationManager _manager = new();

        public static void Success(string message)
        {
            _manager.Show("Success", message, NotificationType.Success, "ToastTopRight",
                TimeSpan.FromSeconds(4), new NotificationContent { Title = "Success", Message = message });
        }

        public static void Error(string message)
        {
            _manager.Show("Error", message, NotificationType.Error, "ToastTopRight",
                TimeSpan.FromSeconds(5), new NotificationContent { Title = "Error", Message = message });
        }

        public static void Warning(string message)
        {
            _manager.Show("Warning", message, NotificationType.Warning, "ToastTopRight",
                TimeSpan.FromSeconds(4), new NotificationContent { Title = "Warning", Message = message });
        }

        public static void Information(string message)
        {
            _manager.Show("Info", message, NotificationType.Information, "ToastTopRight",
                TimeSpan.FromSeconds(4), new NotificationContent { Title = "Info", Message = message });
        }
    }
}