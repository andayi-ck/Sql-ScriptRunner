using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KabarakScriptRunner.Data.Repositories;
using QuestPDF.Infrastructure;
using KabarakScriptRunnerApp.Startup;
using KabarakScriptRunnerApp.Services;
using Serilog;
using Sentry; // <-- ADDED THIS

namespace KabarakScriptRunnerApp
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;
        public static IConfiguration Configuration { get; private set; } = null!;

        public static string ConnectionString =>
            Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json.");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // --- 1. Initialize Sentry FIRST (before anything else) ---
            SentrySdk.Init(options =>
            {
                options.Dsn = "https://bd56e5a195b8278925ccee373025da6c@o4511828680704000.ingest.de.sentry.io/4512289773387856";
                // Set to true only during initial testing to see SDK activity in the console
                options.Debug = false;
                options.IsGlobalModeEnabled = true; // Ensures we catch AppDomain/Task exceptions too
            });

            // --- 2. Serilog bootstrap ---
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

            // --- 3. Configuration ---
            try
            {
                Configuration = StartupValidator.LoadAndValidate();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Configuration validation failed");
                SentrySdk.CaptureException(ex); // Send config errors to Sentry too
                MessageBox.Show(
                    $"The application could not start:\n\n{ex.Message}",
                    "Configuration Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            // QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;

            // Toast notifications
            KabarakScriptRunnerApp.Services.ToastService.Initialize();

            // --- 4. Global Exception Handlers (Both Serilog & Sentry) ---

            // UI Thread Crashes
            DispatcherUnhandledException += (_, args) =>
            {
                Log.Error(args.Exception, "UI thread exception");
                SentrySdk.CaptureException(args.Exception); // Send to Sentry

                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{args.Exception.Message}\n\n" +
                    "The error has been logged. If it persists, contact your administrator.",
                    "Unexpected Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            // Background Thread Crashes
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    Log.Fatal(ex, "AppDomain unhandled exception");
                    SentrySdk.CaptureException(ex); // Send to Sentry
                }
            };

            // Unobserved Task Crashes
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Log.Error(args.Exception, "Unobserved task exception");
                SentrySdk.CaptureException(args.Exception); // Send to Sentry
                args.SetObserved();
            };

            // --- 5. Continue with DI ---
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

            // Ensure Sentry flushes any pending errors before the app closes
            SentrySdk.Close();

            Log.CloseAndFlush();
            base.OnExit(e);
        }

        // ... (keep your ShowLoginLoop and ConfigureServices methods exactly as they are) ...

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

                var userService = ServiceProvider.GetRequiredService<ICurrentUserService>();
                userService.Set(login.AuthenticatedUser, login.SelectedDepartment);

                var main = ServiceProvider.GetRequiredService<MainWindow>();
                main.SetCurrentUser(login.AuthenticatedUser, login.SelectedDepartment);
                main.ShowDialog();

                if (main.LogoutRequested)
                {
                    userService.Clear();
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
            services.AddTransient<DashboardRepository>();
            services.AddTransient<DashboardWindow>();
            services.AddTransient<BatchExportWindow>();
            services.AddSingleton<ICurrentUserService, CurrentUserService>();

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