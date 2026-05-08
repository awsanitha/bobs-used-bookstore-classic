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
            foreach (var item in configuration.AsEnumerable())
            {
                if (item.Value == null) continue;

                if (item.Key.StartsWith("ConnectionStrings:"))
                {
                    var key = item.Key.Replace("ConnectionStrings:", string.Empty);
                    Instance._connectionStrings[key] = item.Value;
                }
                else
                {
                    Instance._appSettings[item.Key] = item.Value;

                    var envValue = Environment.GetEnvironmentVariable(item.Key);
                    if (envValue != null)
                        Instance._appSettings[item.Key] = envValue;
                }
            }
        }

        public static void AddSetting(string key, string value)
        {
            Instance._appSettings[key] = value;
        }

        public static string GetSetting(string key)
        {
            return Instance._appSettings.TryGetValue(key, out var value) ? value : string.Empty;
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
            return Instance._connectionStrings.TryGetValue(key, out var value) ? value : string.Empty;
        }
    }
}
