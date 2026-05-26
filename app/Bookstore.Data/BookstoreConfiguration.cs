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

        public static void Initialize(IConfiguration configuration)
        {
            foreach (var kvp in configuration.AsEnumerable())
            {
                if (kvp.Value == null) continue;

                if (kvp.Key.StartsWith("ConnectionStrings:", StringComparison.OrdinalIgnoreCase))
                {
                    var name = kvp.Key.Substring("ConnectionStrings:".Length);
                    Instance._connectionStrings[name] = kvp.Value;
                }
                else
                {
                    // Normalize "Services/Authentication" style keys from appsettings
                    Instance._appSettings[kvp.Key] = kvp.Value;
                }
            }

            // Also load environment variable overrides
            foreach (var key in new List<string>(Instance._appSettings.Keys))
            {
                var envVal = Environment.GetEnvironmentVariable(key);
                if (envVal != null) Instance._appSettings[key] = envVal;
            }
        }

        public static void AddSetting(string key, string value)
        {
            Instance._appSettings[key] = value;
        }

        public static string GetSetting(string key)
        {
            Instance._appSettings.TryGetValue(key, out var value);
            return value;
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
            Instance._connectionStrings.TryGetValue(key, out var value);
            return value;
        }
    }
}
