using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using System.IO;
using System.Text.Json;
using static AMS2ChEd.SeasonPackEditor.MainWindow;

namespace AMS2ChEd.SeasonPackEditor.Services
{
    public static class SeasonPackTestSaveService
    {
        public static void GenerateTestSave(
            SeasonPackProject project,
            string playerDriverId,
            string outputPath)
        {
            var options = DefaultJsonSerializerOptions.Instance;

            var season = project.Season.DeepClone();
            var resolvedTeams = SeasonPackPathResolver.ResolveTeams(season.Teams.OfType<Ams2TeamEntry>(), project);
            season.Teams = resolvedTeams;

            var drivers = SeasonPackPathResolver.ResolveDrivers(project.Drivers.OfType<Ams2DriverData>(), project.TextureFiles)
                .Cast<IDriverData>()
                .ToList();

            // Resolve player data from the season
            var playerTeam = season.Teams.OfType<Ams2TeamEntry>().FirstOrDefault(t =>
            t.Driver1Contract?.DriverId == playerDriverId ||
            t.Driver2Contract?.DriverId == playerDriverId);

            var playerDriver = project.Drivers.FirstOrDefault(d => d.DriverId == playerDriverId);

            var playerData = new PlayerData
            {
                DriverId = playerDriverId,
                Name = playerDriver?.Name ?? playerDriverId,
                Nationality = playerDriver?.Nationality ?? string.Empty,
                TeamId = playerTeam?.TeamId ?? string.Empty
            };

            // Build zero-point standings for all contracted drivers
            var allContractedSlots = season.Teams.OfType<Ams2TeamEntry>()
                .SelectMany(t => new[]
                {
                    (DriverId: t.Driver1Contract?.DriverId, t.TeamId),
                    (DriverId: t.Driver2Contract?.DriverId, t.TeamId)
                })
                .Where(s => !string.IsNullOrEmpty(s.DriverId))
                .ToList();

            int driverPos = 1;
            var driverStandings = allContractedSlots
                .Select(s => new HistoricalDriverStandingEntry
                {
                    Position = driverPos++,
                    DriverId = s.DriverId,
                    TeamId = s.TeamId,
                    Points = 0,
                    PositionsTally = new PositionsTally()
                }).ToList();

            int constructorPos = 1;
            var constructorStandings = allContractedSlots
                .Select(s => s.TeamId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(teamId => new ConstructorStandingEntry
                {
                    Position = constructorPos++,
                    TeamId = teamId,
                    Points = 0,
                    PositionsTally = new PositionsTally()
                }).ToList();

            // Build a minimal save game shell wrapping the test season
            var saveGame = new SaveGame
            {
                CurrentSeason = season,
                Drivers = drivers,
                PlayerData = playerData,
                NextGpIndex = 0,
                NextGpEntryList = season.Races.Any()
                    ? season.Teams.OfType<Ams2TeamEntry>()
                        .Select(t => new EntryListEntry
                        {
                            TeamId = t.TeamId,
                            Driver1Id = t.Driver1Contract?.DriverId,
                            Driver1Number = t.Driver1Contract?.DriverNumber ?? 0,
                            Driver2Id = t.Driver2Contract?.DriverId,
                            Driver2Number = t.Driver2Contract?.DriverNumber ?? 0,
                        }).ToList()
                    : new List<EntryListEntry>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                CurrentDriverStandings = driverStandings,
                CurrentConstructorStandings = constructorStandings,
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                Timestamp = DateTime.UtcNow,
                PreQualiStatus = PreQualiStatus.NotApplicable
            };

            var result = JsonSerializer.Serialize(saveGame, options);
            File.WriteAllText(outputPath, result);
        }
    }
}