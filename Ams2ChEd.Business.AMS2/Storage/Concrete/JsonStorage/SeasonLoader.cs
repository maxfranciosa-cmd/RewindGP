using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
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
                if (!File.Exists(seasonPath))
                {
                    throw new FileNotFoundException($"Season file not found at: {seasonPath}");
                }

                string json = File.ReadAllText(seasonPath);
                var seasonData = JsonSerializer.Deserialize<Ams2Season>(json, DefaultJsonSerializerOptions.Instance);

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

        public IEnumerable<string> GetAvailableSeasons()
        {
            try
            {
                // Locally installed seasons not in the manifest
                var fromDisk = Directory.Exists(StoragePaths.SeasonsFolder)
                    ? Directory.GetDirectories(StoragePaths.SeasonsFolder)
                        .Select(d => int.TryParse(Path.GetFileName(d), out var y) ? y.ToString() : "")
                        .Where(y => !string.IsNullOrEmpty(y))
                    : Enumerable.Empty<string>();

                return fromDisk.OrderBy(s => s).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error getting available seasons: {ex.Message}", ex);
            }
        }

        public DateTime GetSeasonUpdateDate(int seasonYear)
        {
            return File.GetLastWriteTimeUtc(StoragePaths.SeasonFilePath(seasonYear));
        }

        public ISeason LoadBaseSeason(int seasonYear)
        {
            return LoadSeason(seasonYear);
        }
    }
}
