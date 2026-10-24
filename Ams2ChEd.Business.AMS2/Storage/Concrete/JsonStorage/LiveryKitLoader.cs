using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Contracts;
using AMS2ChEd.Business.Helpers;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage
{
    public class LiveryKitLoader : ILiveryKitLoader
    {
        private const string KitFileName = "kit.json";
        private const string NumberStylesFileName = "number_styles.json";
        private const string OutfitsFolderName = "outfits";
        private const string OutfitFileName = "outfit.json";

        private readonly string _kitsFolder;
        private readonly string _tyreSetsFile;
        private readonly string _tyreFilesFolder;
        private readonly Lazy<List<LiveryKit>> _kits;
        private readonly Lazy<Dictionary<string, NumberStyle>> _numberStyles;
        private readonly Lazy<Dictionary<string, Dictionary<string, string>>> _tyreSets;
        private readonly ConcurrentDictionary<string, OutfitSet> _outfitSets = new ConcurrentDictionary<string, OutfitSet>(StringComparer.OrdinalIgnoreCase);

        public LiveryKitLoader()
            : this(StoragePaths.LiveryKitsFolder, StoragePaths.TyreSetsFilePath, StoragePaths.TyreFilesFolder)
        {
        }

        public LiveryKitLoader(string kitsFolder, string tyreSetsFile, string tyreFilesFolder)
        {
            _kitsFolder = kitsFolder;
            _tyreSetsFile = tyreSetsFile;
            _tyreFilesFolder = tyreFilesFolder;
            _kits = new Lazy<List<LiveryKit>>(LoadKits);
            _numberStyles = new Lazy<Dictionary<string, NumberStyle>>(LoadNumberStyles);
            _tyreSets = new Lazy<Dictionary<string, Dictionary<string, string>>>(LoadTyreSets);
        }

        public LiveryKit FindKit(string ams2Car)
        {
            if (string.IsNullOrEmpty(ams2Car))
                return null;
            return _kits.Value.FirstOrDefault(k => k.CarModels != null && k.CarModels.Contains(ams2Car, StringComparer.OrdinalIgnoreCase));
        }

        public OutfitSet FindOutfitSet(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            return _outfitSets.GetOrAdd(name, n =>
            {
                string folder = Path.Combine(_kitsFolder, OutfitsFolderName, n);
                string path = Path.Combine(folder, OutfitFileName);
                if (!File.Exists(path))
                    return null;
                var set = JsonSerializer.Deserialize<OutfitSet>(File.ReadAllText(path), DefaultJsonSerializerOptions.Instance);
                if (set != null)
                    set.Folder = folder;
                return set;
            });
        }

        public NumberStyle FindNumberStyle(string name) =>
            !string.IsNullOrEmpty(name) && _numberStyles.Value.TryGetValue(name, out var style) ? style : null;

        public string NumberStylePath(string name)
        {
            var style = FindNumberStyle(name);
            return string.IsNullOrEmpty(style?.File) ? null : Path.Combine(_kitsFolder, style.File);
        }

        public IReadOnlyDictionary<string, string> FindTyreSet(string id)
        {
            if (string.IsNullOrEmpty(id) || !_tyreSets.Value.TryGetValue(id, out var files))
                return null;
            return files.ToDictionary(f => f.Key, f => Path.Combine(_tyreFilesFolder, f.Value), StringComparer.OrdinalIgnoreCase);
        }

        // Kits, styles and tyre sets ship with the app; a missing folder or file means "none",
        // which makes every team fall back to its solid colour rather than fail the race.
        private List<LiveryKit> LoadKits()
        {
            var kits = new List<LiveryKit>();
            if (!Directory.Exists(_kitsFolder))
                return kits;

            foreach (var folder in Directory.GetDirectories(_kitsFolder))
            {
                string path = Path.Combine(folder, KitFileName);
                if (!File.Exists(path))
                    continue;
                var kit = JsonSerializer.Deserialize<LiveryKit>(File.ReadAllText(path), DefaultJsonSerializerOptions.Instance);
                if (kit == null)
                    continue;
                kit.Folder = folder;
                kits.Add(kit);
            }
            return kits;
        }

        private Dictionary<string, NumberStyle> LoadNumberStyles()
        {
            string path = Path.Combine(_kitsFolder, NumberStylesFileName);
            if (!File.Exists(path))
                return new Dictionary<string, NumberStyle>(StringComparer.OrdinalIgnoreCase);
            var styles = JsonSerializer.Deserialize<Dictionary<string, NumberStyle>>(File.ReadAllText(path), DefaultJsonSerializerOptions.Instance);
            return new Dictionary<string, NumberStyle>(styles ?? new Dictionary<string, NumberStyle>(), StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, Dictionary<string, string>> LoadTyreSets()
        {
            if (!File.Exists(_tyreSetsFile))
                return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var sets = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(_tyreSetsFile), DefaultJsonSerializerOptions.Instance);
            return new Dictionary<string, Dictionary<string, string>>(sets ?? new Dictionary<string, Dictionary<string, string>>(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
