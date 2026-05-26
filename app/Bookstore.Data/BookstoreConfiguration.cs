using System;
using System.Collections.Generic;

namespace BobsBookstoreClassic.Data
{
    /// <summary>
    /// Static configuration store initialized from IConfiguration at startup.
    /// Preserves the existing call pattern throughout the application.
    /// </summary>
    public sealed class BookstoreConfiguration
    {
        private static readonly Lazy<BookstoreConfiguration> Lazy = new Lazy<BookstoreConfiguration>(() => new BookstoreConfiguration());
        private static BookstoreConfiguration Instance => Lazy.Value;

        private readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private BookstoreConfiguration() { }

        public static void Initialize(IEnumerable<KeyValuePair<string, string>> appSettings, IEnumerable<KeyValuePair<string, string>> connectionStrings)
        {
            foreach (var kv in appSettings)
                if (kv.Value != null) Instance._appSettings[kv.Key] = kv.Value;

            foreach (var kv in connectionStrings)
                if (kv.Value != null) Instance._connectionStrings[kv.Key] = kv.Value;
        }

        public static void AddSetting(string key, string value) => Instance._appSettings[key] = value;

        public static string GetSetting(string key)
        {
            Instance._appSettings.TryGetValue(key, out var value);
            return value ?? string.Empty;
        }

        public static T GetSetting<T>(string key)
        {
            var value = GetSetting(key);
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static void AddConnectionString(string key, string value) => Instance._connectionStrings[key] = value;

        public static string GetConnectionString(string key)
        {
            Instance._connectionStrings.TryGetValue(key, out var value);
            return value ?? string.Empty;
        }
    }
}
