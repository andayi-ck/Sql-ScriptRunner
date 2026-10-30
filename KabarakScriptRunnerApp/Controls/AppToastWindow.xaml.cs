using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace KabarakScriptRunnerApp.Controls
{
    public enum ToastKind
    {
        Information,
        Success,
        Warning,
        Error
    }

    public partial class AppToastWindow : Window
    {
        private static readonly List<AppToastWindow> OpenToasts = new();
        private readonly DispatcherTimer _timer;

        public AppToastWindow(string title, string message, ToastKind kind, TimeSpan duration)
        {
            InitializeComponent();
            lblTitle.Text = title;
            lblMessage.Text = message;
            AccentBar.Background = new SolidColorBrush(KindColor(kind));

            _timer = new DispatcherTimer { Interval = duration };
            _timer.Tick += (_, _) => Close();

            Loaded += (_, _) =>
            {
                OpenToasts.Add(this);
                RepositionAll();
                _timer.Start();
            };

            Closed += (_, _) =>
            {
                _timer.Stop();
                OpenToasts.Remove(this);
                RepositionAll();
            };
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private static Color KindColor(ToastKind kind) => kind switch
        {
            ToastKind.Success => Color.FromRgb(0x2E, 0x7D, 0x32),
            ToastKind.Warning => Color.FromRgb(0xE6, 0x51, 0x00),
            ToastKind.Error => Color.FromRgb(0xC6, 0x28, 0x28),
            _ => Color.FromRgb(0x15, 0x65, 0xC0)
        };

        private static void RepositionAll()
        {
            for (int i = 0; i < OpenToasts.Count; i++)
            {
                var toast = OpenToasts[i];
                var owner = toast.Owner;
                if (owner == null || !owner.IsVisible)
                    continue;

                toast.Left = owner.Left + owner.ActualWidth - toast.ActualWidth - 12;
                toast.Top = owner.Top + 48 + i * (toast.ActualHeight + 6);

                if (toast.Left < owner.Left)
                    toast.Left = owner.Left + 12;
                if (toast.Top < owner.Top)
                    toast.Top = owner.Top + 40;
            }
        }
    }
}
