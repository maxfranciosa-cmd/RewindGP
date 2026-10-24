using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using System.Text.Json;

namespace AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage
{
    /// <summary>
    /// The all-time records with each driver's name and nationality as the app's baked season data
    /// gives them, for everyone who appears in a baked season (the latest season's spelling when
    /// several do); the records file's own names only for people in none. So the hall of fame's
    /// champions of the past read the same as the drivers of the save, whatever the records file
    /// or a season pack calls them.
    /// </summary>
    public class BakedNamesAllTimeRecordsLoader : IAllTimeRecordsLoader
    {
        private readonly IAllTimeRecordsLoader _inner;
        private readonly string _bakedSeasonsFolder;
        private readonly Lazy<AllTimeRecords> _records;

        public BakedNamesAllTimeRecordsLoader(IAllTimeRecordsLoader inner)
            : this(inner, StoragePaths.BakedSeasonsFolder)
        {
        }

        public BakedNamesAllTimeRecordsLoader(IAllTimeRecordsLoader inner, string bakedSeasonsFolder)
        {
            _inner = inner;
            _bakedSeasonsFolder = bakedSeasonsFolder;
            _records = new Lazy<AllTimeRecords>(Build);
        }

        public AllTimeRecords Load() => _records.Value;

        private AllTimeRecords Build()
        {
            var records = _inner.Load();
            var names = BakedDriverNames();
            if (names.Count == 0)
                return records;

            // a copy: the inner loader's records are shared
            records = records.DeepClone();
            foreach (var record in records.Drivers ?? new List<AllTimeDriverRecord>())
            {
                if (record?.DriverId == null || !names.TryGetValue(record.DriverId, out var baked))
                    continue;
                if (!string.IsNullOrEmpty(baked.Name))
                    record.Name = baked.Name;
                if (!string.IsNullOrEmpty(baked.Nationality))
                    record.Nationality = baked.Nationality;
            }
            return records;
        }

        // Every baked driver's name and nationality by id, later seasons over earlier ones.
        private Dictionary<string, (string Name, string Nationality)> BakedDriverNames()
        {
            var names = new Dictionary<string, (string, string)>();
            if (!Directory.Exists(_bakedSeasonsFolder))
                return names;

            var years = Directory.GetDirectories(_bakedSeasonsFolder)
                .Select(d => int.TryParse(Path.GetFileName(d), out int y) ? y : 0)
                .Where(y => y > 0)
                .OrderBy(y => y);
            foreach (int year in years)
            {
                string file = Path.Combine(_bakedSeasonsFolder, year.ToString(), "drivers.json");
                if (!File.Exists(file))
                    continue;
                try
                {
                    var drivers = JsonSerializer.Deserialize<DriverRatingsDatabase>(File.ReadAllText(file), DefaultJsonSerializerOptions.Instance)?.Drivers;
                    foreach (var driver in drivers ?? Enumerable.Empty<IDriverData>())
                        if (!string.IsNullOrEmpty(driver?.DriverId))
                            names[driver.DriverId] = (driver.Name, driver.Nationality);
                }
                catch (Exception ex) when (ex is JsonException || ex is IOException)
                {
                    // a baked season that can't be read only costs its names
                }
            }
            return names;
        }
    }
}
