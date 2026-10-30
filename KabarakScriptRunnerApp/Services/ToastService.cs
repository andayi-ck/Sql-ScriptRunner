using System;
using System.Linq;
using System.Windows;
using KabarakScriptRunnerApp.Controls;

namespace KabarakScriptRunnerApp.Services
{
    public static class ToastService
    {
        public static void Initialize()
        {
            // Kept for compatibility with App.xaml.cs
        }

        public static void Success(string message) =>
            Show("Success", message, ToastKind.Success);

        public static void Success(string title, string message) =>
            Show(title, message, ToastKind.Success);

        public static void Error(string message) =>
            Show("Error", message, ToastKind.Error);

        public static void Error(string title, string message) =>
            Show(title, message, ToastKind.Error);

        public static void Warning(string message) =>
            Show("Warning", message, ToastKind.Warning);

        public static void Warning(string title, string message) =>
            Show(title, message, ToastKind.Warning);

        public static void Information(string message) =>
            Show("Information", message, ToastKind.Information);

        public static void Information(string title, string message) =>
            Show(title, message, ToastKind.Information);

        private static void Show(string title, string message, ToastKind kind)
        {
            var app = Application.Current;
            if (app == null) return;

            void Open()
            {
                var owner = app.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
                            ?? app.Windows.OfType<Window>().LastOrDefault(w => w.IsVisible);

                var duration = kind == ToastKind.Error
                    ? TimeSpan.FromSeconds(6)
                    : TimeSpan.FromSeconds(4);

                var toast = new AppToastWindow(title, message, kind, duration);
                if (owner != null)
                    toast.Owner = owner;
                toast.Show();
            }

            if (app.Dispatcher.CheckAccess())
                Open();
            else
                app.Dispatcher.Invoke(Open);
        }
    }
}
