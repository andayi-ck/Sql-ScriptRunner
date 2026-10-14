using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KabarakScriptRunner.Data.Repositories;
using QuestPDF.Infrastructure;
using KabarakScriptRunnerApp.Startup;
using KabarakScriptRunnerApp.Services; // Imported for ICurrentUserService orchestration
using Serilog;

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

            // --- Serilog bootstrap (must be first) ---
            var logFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KabarakScriptRunner", "logs");

            Directory.CreateDirectory(logFolder);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Debug()
                .WriteTo.File(
                    Path.Combine(logFolder, "log-.txt"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("Application starting up");

            // --- Configuration ---
            try
            {
                Configuration = StartupValidator.LoadAndValidate();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Configuration validation failed");
                MessageBox.Show(
                    $"The application could not start:\n\n{ex.Message}",
                    "Configuration Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            // QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;

            // --- Global handlers now log through Serilog ---
            DispatcherUnhandledException += (_, args) =>
            {
                Log.Error(args.Exception, "UI thread exception");
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{args.Exception.Message}\n\n" +
                    "The error has been logged. If it persists, contact your administrator.",
                    "Unexpected Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    Log.Fatal(ex, "AppDomain unhandled exception");
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Log.Error(args.Exception, "Unobserved task exception");
                args.SetObserved();
            };

            // --- Continue with DI ---
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(Configuration);
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            Log.Information("Configuration loaded, DI container built");

            ShowLoginLoop();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Application shutting down");
            Log.CloseAndFlush();
            base.OnExit(e);
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

                // Hydrate the shared session infrastructure service with current user credentials
                var userService = ServiceProvider.GetRequiredService<ICurrentUserService>();
                userService.Set(login.AuthenticatedUser, login.SelectedDepartment);

                var main = ServiceProvider.GetRequiredService<MainWindow>();
                main.SetCurrentUser(login.AuthenticatedUser, login.SelectedDepartment);
                main.ShowDialog();

                if (main.LogoutRequested)
                {
                    // Evict identity contexts from the state machine before looping back
                    userService.Clear();
                    continue;
                }

                Shutdown();
                return;
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Infrastructure & Data Access Contexts
            services.AddSingleton<DatabaseContext>();
            services.AddTransient<ScriptRepository>();

            // Global State Management
            services.AddSingleton<ICurrentUserService, CurrentUserService>();

            // Client Presentation Workspace Windows
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<UsersWindow>();
            services.AddTransient<UserEditWindow>();
            services.AddTransient<AccessLogWindow>();
            services.AddTransient<AssignScriptsWindow>();
            services.AddTransient<ChangePasswordWindow>();
            services.AddTransient<OptionsWindow>();
            services.AddTransient<ScriptFinderWindow>();
        }
    }
}