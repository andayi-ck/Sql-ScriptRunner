using System;
using System.Windows;
using Serilog;

namespace KabarakScriptRunnerApp.Services
{
    public static class ErrorHandler
    {
        public static void Show(Exception ex, string context, bool isFatal = false)
        {
            Log.Error(ex, "Error in {Context}", context);
            var title = isFatal ? "Fatal Error" : "Error";
            var icon = isFatal ? MessageBoxImage.Stop : MessageBoxImage.Error;
            MessageBox.Show(
                $"An error occurred in {context}:\n\n{ex.Message}\n\n" +
                "Details have been written to the application log.",
                title, MessageBoxButton.OK, icon);
        }

        public static void LogWarning(string message, Exception? ex = null)
        {
            if (ex != null) Log.Warning(ex, "{Message}", message);
            else Log.Warning("{Message}", message);
        }
    }
}