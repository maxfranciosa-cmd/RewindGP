using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using AMS2ChEd.Business.Updater;
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

        public IEnumerable<string> GetAvailableSeasons()
        {
            return _seasonsWithUpdateDates.Keys;
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
            var service = new SeasonManifestService("", ManifestPath, mockSeasonLoader , (path) => path == ManifestPath ? manifestJson : "{}", false);
            return service;
        }
    }
}
