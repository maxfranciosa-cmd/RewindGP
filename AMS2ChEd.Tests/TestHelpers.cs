using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater;
using AMS2ChEd.Business.Updater.Models;
using System.Text.Json;

namespace AMS2ChEd.Tests
{
    public class FakeSaveFile : SaveGame
    {
        public bool WasMigrated { get; private set; }

        public void MarkMigrated() => WasMigrated = true;
    }

    // -------------------------------------------------------------------------
    // Manifest JSON builder
    // -------------------------------------------------------------------------

    public static class ManifestBuilder
    {
        public static string Build(params (int Year, DateTime LastUpdated, string Url)[] seasons)
        {
            var entries = new List<object>();
            foreach (var (year, lastUpdated, url) in seasons)
            {
                entries.Add(new
                {
                    year,
                    displayName = $"{year} Formula 1 Season",
                    lastUpdated = lastUpdated.ToString("O"),
                    downloadUrl = url,
                    fileSizeBytes = 50_000_000
                });
            }

            return JsonSerializer.Serialize(new { seasons = entries });
        }
    }

    public class MockSeasonLoader : ISeasonLoader
    {
        private readonly Dictionary<string, DateTime> _seasonsWithUpdateDates = new Dictionary<string, DateTime>();
        public void AddSeason(string year, DateTime updateDate)
        {
            _seasonsWithUpdateDates.Add(year, updateDate);
        }

        public void RemoveSeason(string year)
        {
            _seasonsWithUpdateDates.Remove(year);
        }

        // Seasons the app ships data for, with no pack installed: playable, not "installed".
        private readonly HashSet<string> _bakedOnlySeasons = new HashSet<string>();
        public void AddBakedOnlySeason(string year)
        {
            _bakedOnlySeasons.Add(year);
        }

        public IEnumerable<string> GetAvailableSeasons()
        {
            return _seasonsWithUpdateDates.Keys.Union(_bakedOnlySeasons);
        }

        public IEnumerable<string> GetInstalledPackSeasons()
        {
            return _seasonsWithUpdateDates.Keys;
        }

        // Seasons the app ships data for, whether a pack is installed over them or not.
        private readonly HashSet<string> _bakedSeasons = new HashSet<string>();
        public void AddBakedSeason(string year)
        {
            _bakedSeasons.Add(year);
        }

        public IEnumerable<string> GetBakedSeasons()
        {
            return _bakedSeasons.Union(_bakedOnlySeasons);
        }

        private readonly Dictionary<int, SeasonDataStamp> _stamps = new Dictionary<int, SeasonDataStamp>();
        public void SetSeasonDataStamp(int year, string baked, string pack)
        {
            _stamps[year] = new SeasonDataStamp { Year = year, Baked = baked, Pack = pack };
        }

        public SeasonDataStamp GetSeasonDataStamp(int seasonYear)
        {
            return _stamps.TryGetValue(seasonYear, out var stamp) ? stamp : null;
        }

        public DateTime GetSeasonUpdateDate(int seasonYear)
        {
            return _seasonsWithUpdateDates.ContainsKey(seasonYear.ToString()) ? _seasonsWithUpdateDates[seasonYear.ToString()] : DateTime.MinValue;
        }

        public ISeason LoadBaseSeason(int seasonYear)
        {
            throw new NotImplementedException();
        }
    }

    // -------------------------------------------------------------------------
    // Factory for SeasonManifestService wired to MockFileSystem
    // -------------------------------------------------------------------------

    public static class SeasonManifestServiceFactory
    {
        private const string ManifestPath = "/app/seasons.json";

        public static SeasonManifestService Create(
            string manifestJson, MockSeasonLoader mockSeasonLoader)
        {
            var service = new SeasonManifestService(ManifestPath, mockSeasonLoader, (path) => path == ManifestPath ? manifestJson : "{}", false);
            return service;
        }
    }
}

namespace AMS2ChEd.Tests
{
    // The helmet-preview maps, where the app looks for them (beside the test assembly): a tiny
    // all-shell view of the Modern helmet, written once for the tests running in parallel.
    public static class HelmetPreviewTestMaps
    {
        private static readonly Lazy<bool> Written = new Lazy<bool>(() =>
        {
            string modern = Path.Combine(Ams2ChEd.Business.AMS2.Helpers.StoragePaths.HelmetPreviewsFolder, "Modern");
            Directory.CreateDirectory(modern);
            using (var uv = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba64>(4, 4, new SixLabors.ImageSharp.PixelFormats.Rgba64(32768, 32768, 0, 65535)))
                SixLabors.ImageSharp.ImageExtensions.SaveAsPng(uv, Path.Combine(modern, "uv.png"));
            using (var shading = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(4, 4, new SixLabors.ImageSharp.PixelFormats.Rgba32(200, 200, 200, 255)))
                SixLabors.ImageSharp.ImageExtensions.SaveAsPng(shading, Path.Combine(modern, "shading.png"));
            // a tiny Modern generic design, all red key
            string designs = Ams2ChEd.Business.AMS2.Helpers.StoragePaths.GenericHelmetDesignsFolder;
            Directory.CreateDirectory(designs);
            using (var design = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(8, 8, new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 0, 0, 255)))
                SixLabors.ImageSharp.ImageExtensions.SaveAsPng(design, Path.Combine(designs, TestDesign + ".png"));
            return true;
        });

        public const string TestDesign = "zz_test_design";

        public static void Ensure() => _ = Written.Value;
    }
}
