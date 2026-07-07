using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace Bookstore.Data
{
    /// <summary>
    /// Provides a static/singleton-style accessor for application configuration.
    /// In ASP.NET Core, settings should be injected via IConfiguration.
    /// This class bridges the gap for code that cannot be easily refactored to use DI.
    /// </summary>
    public sealed class BookstoreConfiguration
    {
        private static readonly Lazy<BookstoreConfiguration> Lazy = new Lazy<BookstoreConfiguration>(() => new BookstoreConfiguration());

        private static BookstoreConfiguration Instance => Lazy.Value;

        private readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private BookstoreConfiguration() { }

        /// <summary>
        /// Initialise from ASP.NET Core IConfiguration. Call once at startup.
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            foreach (var setting in configuration.AsEnumerable())
            {
                if (setting.Value == null) continue;

                if (setting.Key.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase))
                {
                    var name = setting.Key["ConnectionStrings:".Length..];
                    Instance._connectionStrings[name] = setting.Value;
                }
                else
                {
                    // Normalize "Services:Authentication" → "Services/Authentication" for backward compat
                    var key = setting.Key.Replace(':', '/');
                    Instance._appSettings[key] = setting.Value;
                }
            }

            // Also check environment variables using slash-style keys
            foreach (var envVar in System.Environment.GetEnvironmentVariables().Keys)
            {
                var key = envVar?.ToString();
                if (string.IsNullOrEmpty(key)) continue;
                var value = System.Environment.GetEnvironmentVariable(key);
                if (value != null)
                {
                    Instance._appSettings[key] = value;
                }
            }
        }

        public static void AddSetting(string key, string value)
        {
            Instance._appSettings[key] = value;
        }

        public static string GetSetting(string key)
        {
            return Instance._appSettings.TryGetValue(key, out var val) ? val : string.Empty;
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
            return Instance._connectionStrings.TryGetValue(key, out var val) ? val : string.Empty;
        }
    }
}
