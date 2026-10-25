using Notification.Core;
using Notification.Wpf; // Ensure this is the correct namespace for your version
using System;

namespace KabarakScriptRunnerApp.Services
{
    public static class ToastService
    {
        private static readonly NotificationManager _manager = new();

        public static void Initialize()
        {
            // Kept for compatibility with App.xaml.cs
        }

        public static void Success(string message)
        {
            // Using the simple overload: Title, Message, Type
            _manager.Show("Success", message, NotificationType.Success);
        }

        public static void Error(string message)
        {
            _manager.Show("Error", message, NotificationType.Error);
        }

        public static void Warning(string message)
        {
            _manager.Show("Warning", message, NotificationType.Warning);
        }

        public static void Information(string message)
        {
            _manager.Show("Info", message, NotificationType.Information);
        }
    }
}