using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace KabarakScriptRunnerApp.Startup
{
    public static class StartupValidator
    {
        /// <summary>
        /// Validates that appsettings.json exists and contains a valid connection string.
        /// Throws a descriptive exception if anything is wrong.
        /// </summary>
        public static IConfiguration LoadAndValidate()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var filePath = Path.Combine(basePath, "appsettings.json");

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    $"Configuration file not found at:\n{filePath}\n\n" +
                    "Please ensure 'appsettings.json' is present next to the application executable.",
                    filePath);
            }

            IConfiguration config;
            try
            {
                config = new ConfigurationBuilder()
                    .SetBasePath(basePath)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse appsettings.json.\n\n" +
                    $"The file may contain invalid JSON. Details:\n{ex.Message}", ex);
            }

            var connectionString = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "The 'DefaultConnection' connection string is missing from appsettings.json.\n\n" +
                    "Expected format:\n" +
                    "{\n" +
                    "  \"ConnectionStrings\": {\n" +
                    "    \"DefaultConnection\": \"Server=...;Database=...;...\"\n" +
                    "  }\n" +
                    "}");
            }

            return config;
        }
    }
}