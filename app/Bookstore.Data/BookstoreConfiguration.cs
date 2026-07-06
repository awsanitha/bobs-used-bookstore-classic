using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace BobsBookstoreClassic.Data
{
    public sealed class BookstoreConfiguration
    {
        private static readonly Lazy<BookstoreConfiguration> Lazy = new Lazy<BookstoreConfiguration>(() => new BookstoreConfiguration());

        private static BookstoreConfiguration Instance => Lazy.Value;

        private readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>();

        private BookstoreConfiguration() { }

        /// <summary>
        /// Initialize BookstoreConfiguration from ASP.NET Core IConfiguration.
        /// Call this from Program.cs before any services use this class.
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            // Load app settings
            foreach (var kvp in configuration.AsEnumerable())
            {
                if (kvp.Value != null)
                {
                    Instance._appSettings[kvp.Key] = kvp.Value;

                    // Also allow environment variable overrides (key with "/" replaced by "_" and ":")
                    var envKey = kvp.Key.Replace("/", "__").Replace(":", "__");
                    var envValue = Environment.GetEnvironmentVariable(envKey);
                    if (envValue != null)
                    {
                        Instance._appSettings[kvp.Key] = envValue;
                    }
                }
            }

            // Load connection strings from ConnectionStrings section
            var connectionStringsSection = configuration.GetSection("ConnectionStrings");
            foreach (var child in connectionStringsSection.GetChildren())
            {
                if (child.Value != null)
                {
                    Instance._connectionStrings[child.Key] = child.Value;
                }
            }

            // Also check environment variables for Services/* keys
            foreach (var key in new[] {
                "Services/Authentication", "Services/Database", "Services/FileService",
                "Services/ImageValidationService", "Services/LoggingService"
            })
            {
                var envKey = key.Replace("/", "__");
                var envValue = Environment.GetEnvironmentVariable(envKey);
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
            // Also try colon-separated form (ASP.NET Core config uses : as separator)
            var colonKey = key.Replace("/", ":");
            if (Instance._appSettings.TryGetValue(colonKey, out var colonValue))
                return colonValue;
            return string.Empty;
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
            return string.Empty;
        }
    }
}
