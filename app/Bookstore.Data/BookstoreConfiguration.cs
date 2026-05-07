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
                    // Store both the raw key and a normalized version (replacing : with /)
                    Instance._appSettings[kvp.Key] = kvp.Value;
                    var normalizedKey = kvp.Key.Replace(":", "/");
                    if (normalizedKey != kvp.Key)
                        Instance._appSettings[normalizedKey] = kvp.Value;
                }
            }

            // Override with environment variables
            foreach (var kvp in configuration.AsEnumerable())
            {
                if (kvp.Value == null) continue;
                var envValue = Environment.GetEnvironmentVariable(kvp.Key);
                if (envValue != null)
                    Instance._appSettings[kvp.Key] = envValue;
            }
        }

        public static void AddSetting(string key, string value)
        {
            Instance._appSettings[key] = value;
        }

        public static string GetSetting(string key)
        {
            return Instance._appSettings.TryGetValue(key, out var value) ? value : null;
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
            return Instance._connectionStrings.TryGetValue(key, out var value) ? value : null;
        }
    }
}
