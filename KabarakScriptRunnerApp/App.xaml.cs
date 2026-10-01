using System;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KabarakScriptRunner.Data.Repositories;
using QuestPDF.Infrastructure;

namespace KabarakScriptRunnerApp
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;
        public static IConfiguration Configuration { get; private set; } = null!;

        /// <summary>
        /// Convenience accessor for the SQL Server connection string.
        /// </summary>
        public static string ConnectionString =>
            Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json.");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // QuestPDF license (required before any PDF generation)
            QuestPDF.Settings.License = LicenseType.Community;

            // ---------- Global exception handling ----------

            // UI-thread exceptions: log + show message, but don't crash the app.
            DispatcherUnhandledException += (_, args) =>
            {
                LogException(args.Exception, "DispatcherUnhandledException");
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{args.Exception.Message}\n\n" +
                    "The error has been logged. If the problem persists, please contact your administrator.",
                    "Unexpected Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // Non-UI thread exceptions: log only. Do NOT shut down.
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    LogException(ex, "AppDomainUnhandledException");
            };

            // Unobserved task exceptions (common during shutdown — log only).
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                LogException(args.Exception, "UnobservedTaskException");
                args.SetObserved();   // suppress finalizer rethrow
            };

            // ---------- Configuration ----------

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();

            // ---------- DI container ----------

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(Configuration);
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            // ---------- Login loop ----------

            ShowLoginLoop();
        }

        private void ShowLoginLoop()
        {
            while (true)
            {
                var login = ServiceProvider.GetRequiredService<LoginWindow>();
                bool? result = login.ShowDialog();

                if (result != true || login.AuthenticatedUser == null || login.SelectedDepartment == null)
                {
                    Shutdown();
                    return;
                }

                var main = ServiceProvider.GetRequiredService<MainWindow>();
                main.SetCurrentUser(login.AuthenticatedUser, login.SelectedDepartment);
                main.ShowDialog();

                if (main.LogoutRequested)
                {
                    // Loop back to login
                    continue;
                }

                Shutdown();
                return;
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<DatabaseContext>();
            services.AddTransient<ScriptRepository>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
        }

        /// <summary>
        /// Writes the exception to a log file under %LOCALAPPDATA%\KabarakScriptRunner\logs.
        /// Never throws — if logging fails, we swallow the failure.
        /// </summary>
        private static void LogException(Exception ex, string source)
        {
            try
            {
                var logDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "KabarakScriptRunner", "logs");
                System.IO.Directory.CreateDirectory(logDir);

                var logPath = System.IO.Path.Combine(logDir, $"error_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                System.IO.File.AppendAllText(logPath,
                    $"Timestamp: {DateTime.Now:O}\n" +
                    $"Source:    {source}\n" +
                    $"Message:   {ex.Message}\n" +
                    $"Type:      {ex.GetType().FullName}\n" +
                    $"Stack:     {ex.StackTrace}\n" +
                    $"Inner:     {ex.InnerException}\n" +
                    new string('-', 80) + "\n");
            }
            catch
            {
                // Never throw from a crash logger.
            }
        }
    }
}