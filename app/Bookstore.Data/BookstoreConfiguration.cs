using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace BobsBookstoreClassic.Data
{
    /// <summary>
    /// Static configuration wrapper. Call Initialize(IConfiguration) once at startup before using GetSetting/GetConnectionString.
    /// Supports runtime additions via AddSetting/AddConnectionString (used by ConfigurationSetup for AWS SSM parameters).
    /// </summary>
    public sealed class BookstoreConfiguration
    {
        private static readonly Lazy<BookstoreConfiguration> Lazy = new Lazy<BookstoreConfiguration>(() => new BookstoreConfiguration());
        private static BookstoreConfiguration Instance => Lazy.Value;

        private readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private BookstoreConfiguration() { }

        /// <summary>
        /// Initializes the configuration from the ASP.NET Core IConfiguration. Call once during application startup.
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            // Load appSettings-equivalent keys from IConfiguration root
            foreach (var kvp in configuration.AsEnumerable())
            {
                if (kvp.Value != null)
                {
                    Instance._appSettings[kvp.Key] = kvp.Value;
                }
            }

            // Load connection strings
            var connStrings = configuration.GetSection("ConnectionStrings");
            foreach (var item in connStrings.GetChildren())
            {
                Instance._connectionStrings[item.Key] = item.Value ?? string.Empty;
            }

            // Allow environment variables to override any setting
            foreach (var key in new List<string>(Instance._appSettings.Keys))
            {
                // Environment variables use double-underscore as separator for nested keys
                var envKey = key.Replace(":", "__").Replace("/", "__");
                var envValue = Environment.GetEnvironmentVariable(envKey)
                    ?? Environment.GetEnvironmentVariable(key);
                if (envValue != null)
                {
                    Instance._appSettings[key] = envValue;
                }
            }
        }

        public static void AddSetting(string key, string value)
        {
            Instance._appSettings[key] = value;
        }

        public static string GetSetting(string key)
        {
            if (Instance._appSettings.TryGetValue(key, out var value))
                return value;
            // Fall back to environment variable
            return Environment.GetEnvironmentVariable(key) ?? string.Empty;
        }

        public static T GetSetting<T>(string key)
        {
            var value = GetSetting(key);
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static void AddConnectionString(string key, string value)
        {
            Instance._connectionStrings[key] = value;
        }

        public static string GetConnectionString(string key)
        {
            if (Instance._connectionStrings.TryGetValue(key, out var value))
                return value;
            return Environment.GetEnvironmentVariable(key) ?? string.Empty;
        }
    }
}
