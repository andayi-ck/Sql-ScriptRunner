using System;
using System.Windows;
using Serilog;

namespace KabarakScriptRunnerApp.Services
{
    public static class ErrorHandler
    {
        /// <summary>
        /// EH-02: Now takes a caller-supplied user-safe message instead of
        /// interpolating ex.Message. If no message is supplied, the centralized
        /// SafeErrorMessage mapper is used. The full exception is still logged.
        /// </summary>
        public static void Show(Exception ex, string context, string? userMessage = null, bool isFatal = false)
        {
            var safeMessage = userMessage ?? SafeErrorMessage.For(ex, context);
            var title = isFatal ? "Fatal Error" : "Error";
            var icon = isFatal ? MessageBoxImage.Stop : MessageBoxImage.Error;
            MessageBox.Show(
                $"{safeMessage}\n\nDetails have been written to the application log.",
                title, MessageBoxButton.OK, icon);
        }

        public static void LogWarning(string message, Exception? ex = null)
        {
            if (ex != null) Log.Warning(ex, "{Message}", message);
            else Log.Warning("{Message}", message);
        }
    }
}