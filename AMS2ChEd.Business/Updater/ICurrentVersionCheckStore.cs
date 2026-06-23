using System.Text.Json;

namespace AMS2ChEd.Business.Updater
{
    /// <summary>
    /// Simple key-value store used to cache update check timestamps.
    /// </summary>
    public interface ICurrentVersionCheckStore
    {
        string? GetString(string key);
        void SetString(string key, string value);
        DateTime? GetDateTime(string key);
        void SetDateTime(string key, DateTime value);
    }

    /// <summary>
    /// Production implementation backed by a JSON file in %LocalAppData%\RewindGP.
    /// </summary>
    public class JsonCurrentVersionCheckStore : ICurrentVersionCheckStore
    {
        private readonly string _filePath;
        private Dictionary<string, string> _data;

        public JsonCurrentVersionCheckStore(string filePath)
        {
            _filePath = filePath;
            _data = Load();
        }

        public string? GetString(string key) =>
            _data.TryGetValue(key, out var v) ? v : null;

        public void SetString(string key, string value)
        {
            _data[key] = value;
            Save();
        }

        public DateTime? GetDateTime(string key)
        {
            var raw = GetString(key);
            if (raw == null) return null;
            return DateTime.TryParse(raw, out var dt) ? dt : null;
        }

        public void SetDateTime(string key, DateTime value)
            => SetString(key, value.ToString("O"));

        private Dictionary<string, string> Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return new();
                return JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(_filePath)) ?? new();
            }
            catch { return new(); }
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_data));
        }
    }

    /// <summary>
    /// In-memory implementation for unit tests.
    /// </summary>
    public class InMemoryCurrentVersionCheckStore : ICurrentVersionCheckStore
    {
        private readonly Dictionary<string, string> _data = new();

        public string? GetString(string key) =>
            _data.TryGetValue(key, out var v) ? v : null;

        public void SetString(string key, string value) => _data[key] = value;

        public DateTime? GetDateTime(string key)
        {
            var raw = GetString(key);
            if (raw == null) return null;
            return DateTime.TryParse(raw, out var dt) ? dt : null;
        }

        public void SetDateTime(string key, DateTime value)
            => SetString(key, value.ToString("O"));
    }
}
