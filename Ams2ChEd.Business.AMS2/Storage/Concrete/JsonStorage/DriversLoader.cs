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

                // As for the season: baked drivers, with an installed pack's helmets and portraits,
                // then those the app ships. What both lack may come from the other years.
                if (hasBaked)
                {
                    var cosmetics = SeasonCosmeticsFiles.EffectiveDrivers(SeasonCosmeticsFiles.Read(seasonYear), seasonYear);
                    return BakedSeasonMerge.MergeDrivers(Read(bakedPath), hasPack ? Read(driversPath) : null, cosmetics, seasonYear, ReadOtherPacks(seasonYear));
                }

                return Read(driversPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading drivers database: {ex.Message}", ex);
            }
        }

        // Every other year's drivers' artwork: its installed pack (as the pack alone), over the
        // artwork the app ships for it; a pack that can't be read is left out.
        private static Dictionary<int, Dictionary<string, Ams2DriverData>> ReadOtherPacks(int seasonYear)
        {
            var packs = new Dictionary<int, Dictionary<string, Ams2DriverData>>();
            foreach (var year in YearFolders(StoragePaths.SeasonsFolder).Union(YearFolders(StoragePaths.BakedSeasonsFolder)))
            {
                if (year == seasonYear)
                    continue;

                Dictionary<string, Ams2DriverData> pack = null;
                var path = StoragePaths.DriversFilePath(year);
                if (File.Exists(path))
                {
                    try
                    {
                        pack = Read(path);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceWarning($"The {year} season pack's drivers could not be read for cosmetic fallbacks: {ex.Message}");
                    }
                }

                var shipped = SeasonCosmeticsFiles.EffectiveDrivers(SeasonCosmeticsFiles.Read(year), year);
                var combined = BakedSeasonMerge.CombineDriverArtwork(pack, shipped.Count == 0 ? null : shipped);
                if (combined != null)
                    packs[year] = combined;
            }
            return packs;
        }

        private static IEnumerable<int> YearFolders(string folder)
        {
            if (!Directory.Exists(folder))
                return Enumerable.Empty<int>();

            return Directory.GetDirectories(folder)
                .Select(d => int.TryParse(Path.GetFileName(d), out var y) ? y : 0)
                .Where(y => y > 0);
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
