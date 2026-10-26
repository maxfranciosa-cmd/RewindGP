using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using System.Diagnostics;
using System.Text.Json;

namespace AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage
{
    public class DriversLoader : IDriversLoader<Ams2DriverData>
    {
        public Dictionary<string, Ams2DriverData> LoadDrivers(int seasonYear)
        {
            try
            {
                var driversPath = StoragePaths.DriversFilePath(seasonYear);
                var bakedPath = StoragePaths.BakedDriversFilePath(seasonYear);
                bool hasPack = File.Exists(driversPath), hasBaked = File.Exists(bakedPath);
                if (!hasPack && !hasBaked)
                {
                    throw new FileNotFoundException($"Drivers database not found at: {driversPath}");
                }

                // As for the season: baked drivers, with an installed pack's helmets and portraits.
                // What the pack lacks may come from the other years' packs.
                if (hasBaked)
                    return BakedSeasonMerge.MergeDrivers(Read(bakedPath), hasPack ? Read(driversPath) : null, seasonYear, ReadOtherPacks(seasonYear));

                return Read(driversPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading drivers database: {ex.Message}", ex);
            }
        }

        // The drivers of every other year's installed pack, as the pack alone; a pack that can't
        // be read is left out.
        private static Dictionary<int, Dictionary<string, Ams2DriverData>> ReadOtherPacks(int seasonYear)
        {
            var packs = new Dictionary<int, Dictionary<string, Ams2DriverData>>();
            if (!Directory.Exists(StoragePaths.SeasonsFolder))
                return packs;

            foreach (var folder in Directory.GetDirectories(StoragePaths.SeasonsFolder))
            {
                if (!int.TryParse(Path.GetFileName(folder), out var year) || year <= 0 || year == seasonYear)
                    continue;

                var path = StoragePaths.DriversFilePath(year);
                if (!File.Exists(path))
                    continue;

                try
                {
                    packs[year] = Read(path);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"The {year} season pack's drivers could not be read for cosmetic fallbacks: {ex.Message}");
                }
            }
            return packs;
        }

        private static Dictionary<string, Ams2DriverData> Read(string path)
        {
            var driversDb = JsonSerializer.Deserialize<DriverRatingsDatabase>(File.ReadAllText(path), DefaultJsonSerializerOptions.Instance);
            return driversDb.Drivers.ToDictionary(d => d.DriverId, d => (Ams2DriverData)d);
        }

        public Dictionary<string, IDriverData> LoadDriversBase(int seasonYear)
        {
            return LoadDrivers(seasonYear).ToDictionary(kv => kv.Key, kv => (IDriverData)kv.Value);
        }
    }
}
