using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater.Models;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage
{
    public class SeasonLoader : ISeasonLoader<Ams2Season>
    {
        public Ams2Season LoadSeason(int seasonYear)
        {
            try
            {
                var seasonPath = StoragePaths.SeasonFilePath(seasonYear);
                var bakedPath = StoragePaths.BakedSeasonFilePath(seasonYear);
                bool hasPack = File.Exists(seasonPath), hasBaked = File.Exists(bakedPath);
                if (!hasPack && !hasBaked)
                {
                    throw new FileNotFoundException($"Season file not found at: {seasonPath}");
                }

                // Baked data is the season; an installed pack only lays its artwork over it, then
                // the artwork the app ships for it (where its skin pack has been imported). With
                // no baked data for the year, the pack is the whole season, as before.
                Ams2Season seasonData;
                if (hasBaked)
                {
                    var warnings = new List<string>();
                    var cosmetics = SeasonCosmeticsFiles.EffectiveSeason(SeasonCosmeticsFiles.Read(seasonYear), seasonYear);
                    seasonData = BakedSeasonMerge.MergeSeason(Read(bakedPath), hasPack ? Read(seasonPath) : null, cosmetics, OtherPackReader(), warnings);
                    foreach (var warning in warnings)
                        Trace.TraceWarning(warning);
                }
                else
                {
                    seasonData = Read(seasonPath);
                }

                // A team with no second car for the season is represented as a real
                // Driver2Contract with an empty DriverId (so its reserved race number
                // still has somewhere to live), not a null contract. Normalize here in
                // case a hand-edited season.json omits the key entirely.
                if (seasonData?.Teams != null)
                {
                    foreach (var team in seasonData.Teams)
                    {
                        team.Driver2Contract ??= new DriverContract();
                    }
                }

                return seasonData;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading season data: {ex.Message}", ex);
            }
        }

        private static Ams2Season Read(string path) =>
            JsonSerializer.Deserialize<Ams2Season>(File.ReadAllText(path), DefaultJsonSerializerOptions.Instance);

        // Another year's artwork: its pack, over the artwork the app ships for it (never merged
        // over that year's baked data, so fallbacks don't chain), read once per load; null when
        // that year has neither.
        private static Func<int, Ams2Season> OtherPackReader()
        {
            var cache = new Dictionary<int, Ams2Season>();
            return year =>
            {
                if (cache.TryGetValue(year, out var cached))
                    return cached;

                Ams2Season pack = null;
                var path = StoragePaths.SeasonFilePath(year);
                if (File.Exists(path))
                {
                    try
                    {
                        pack = Read(path);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceWarning($"The {year} season pack could not be read for cosmetic fallbacks: {ex.Message}");
                    }
                }
                var cosmetics = SeasonCosmeticsFiles.EffectiveSeason(SeasonCosmeticsFiles.Read(year), year);
                return cache[year] = BakedSeasonMerge.CombineArtworkSources(pack, cosmetics);
            };
        }

        public IEnumerable<string> GetAvailableSeasons()
        {
            try
            {
                // a Seasons/<year> folder is a pack only with its season.json: an imported skin
                // pack's files alone (a baked year) are not one
                return YearFolders(StoragePaths.BakedSeasonsFolder, StoragePaths.BakedSeasonFilePath)
                    .Union(YearFolders(StoragePaths.SeasonsFolder, StoragePaths.SeasonFilePath))
                    .OrderBy(s => s)
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting available seasons: {ex.Message}", ex);
            }
        }

        public IEnumerable<string> GetInstalledPackSeasons()
        {
            try
            {
                // Locally installed seasons not in the manifest
                return YearFolders(StoragePaths.SeasonsFolder, StoragePaths.SeasonFilePath).OrderBy(s => s).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting installed season packs: {ex.Message}", ex);
            }
        }

        // Year-named folders; with requiredFile, only those holding that year's file.
        private static IEnumerable<string> YearFolders(string folder, Func<int, string> requiredFile)
        {
            if (!Directory.Exists(folder))
                return Enumerable.Empty<string>();

            return Directory.GetDirectories(folder)
                .Select(d => int.TryParse(Path.GetFileName(d), out var y) ? y : 0)
                .Where(y => y > 0 && (requiredFile == null || File.Exists(requiredFile(y))))
                .Select(y => y.ToString());
        }

        public DateTime GetSeasonUpdateDate(int seasonYear)
        {
            // A missing file reads as 1601, so the newer of the two is the one that exists.
            var baked = File.GetLastWriteTimeUtc(StoragePaths.BakedSeasonFilePath(seasonYear));
            var pack = File.GetLastWriteTimeUtc(StoragePaths.SeasonFilePath(seasonYear));
            return baked > pack ? baked : pack;
        }

        public DateTime GetPackUpdateDate(int seasonYear)
        {
            return File.GetLastWriteTimeUtc(StoragePaths.SeasonFilePath(seasonYear));
        }

        public IEnumerable<string> GetBakedSeasons()
        {
            return YearFolders(StoragePaths.BakedSeasonsFolder, StoragePaths.BakedSeasonFilePath).OrderBy(s => s).ToList();
        }

        public SeasonDataStamp GetSeasonDataStamp(int seasonYear)
        {
            var baked = Fingerprint(StoragePaths.BakedSeasonFilePath(seasonYear), StoragePaths.BakedDriversFilePath(seasonYear));

            // Over baked data, the artwork may also come from other years' packs (cosmetic
            // fallbacks) and from the cosmetics the app ships, each counting once its skin pack is
            // imported: all of them together, in year order.
            string cosmetics = null;
            if (baked != null)
            {
                var otherPacks = YearFolders(StoragePaths.SeasonsFolder, null)
                    .Select(int.Parse)
                    .Where(y => y != seasonYear)
                    .SelectMany(y => new[] { (y, StoragePaths.SeasonFilePath(y)), (y, StoragePaths.DriversFilePath(y)) });
                var shipped = YearFolders(StoragePaths.BakedSeasonsFolder, StoragePaths.CosmeticsFilePath)
                    .Select(int.Parse)
                    .SelectMany(y => new[] { (y, StoragePaths.CosmeticsFilePath(y)), (y, StoragePaths.SkinPackRecordPath(y)) });
                cosmetics = Fingerprint(otherPacks.Concat(shipped)
                    .OrderBy(f => f.Item1)
                    .Select(f => f.Item2)
                    .ToArray());
            }

            return new SeasonDataStamp
            {
                Year = seasonYear,
                Baked = baked,
                Pack = Fingerprint(StoragePaths.SeasonFilePath(seasonYear), StoragePaths.DriversFilePath(seasonYear)),
                Cosmetics = cosmetics
            };
        }

        // The given files hashed together, null when none exists. Only the json matters: a save
        // holds the paths of the artwork, not the artwork itself.
        private static string Fingerprint(params string[] paths)
        {
            var existing = paths.Where(File.Exists).ToList();
            if (existing.Count == 0)
                return null;

            using var sha = SHA256.Create();
            foreach (var path in existing)
            {
                var bytes = File.ReadAllBytes(path);
                sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash, 0, 8);
        }

        // The scenarios shipped with the app's baked data, then those of installed packs for the
        // years the app ships none of: a pack's copies of a baked year's scenarios are left out.
        public IEnumerable<(int Year, string Folder)> GetScenarioFolders()
        {
            var baked = ISeasonLoader.ScenarioFolders(StoragePaths.BakedSeasonsFolder).ToList();
            var bakedYears = baked.Select(b => b.Year).ToHashSet();
            return baked
                .Concat(ISeasonLoader.ScenarioFolders(StoragePaths.SeasonsFolder).Where(p => !bakedYears.Contains(p.Year)))
                .OrderBy(s => s.Year)
                .ToList();
        }

        public ISeason LoadBaseSeason(int seasonYear)
        {
            return LoadSeason(seasonYear);
        }
    }
}
