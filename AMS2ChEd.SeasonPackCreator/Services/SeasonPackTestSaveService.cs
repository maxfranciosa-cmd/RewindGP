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

            foreach (var teamEntry in season.Teams.OfType<Ams2TeamEntry>())
            {
                // Embed livery XML content
                if (project.XmlFiles.TryGetValue(teamEntry.TeamId, out var xmlContent)
                    && !string.IsNullOrWhiteSpace(xmlContent))
                {
                    teamEntry.LiveryXml = ResolveXmlTexturePaths(xmlContent);
                }

                // Resolve team-level paths
                teamEntry.BaseLiveryDriver1 = Resolve(teamEntry.BaseLiveryDriver1, project.TextureFiles);
                teamEntry.BaseLiveryDriver2 = Resolve(teamEntry.BaseLiveryDriver2, project.TextureFiles);
                teamEntry.HelmetSponsors = Resolve(teamEntry.HelmetSponsors, project.TextureFiles);
                teamEntry.VisorSponsors = Resolve(teamEntry.VisorSponsors, project.TextureFiles);
                teamEntry.LiveryPreview = Resolve(teamEntry.LiveryPreview, project.TextureFiles);

                if (teamEntry.DriversSpecificHelmet != null)
                    teamEntry.DriversSpecificHelmet = teamEntry.DriversSpecificHelmet
                        .ToDictionary(kvp => kvp.Key, kvp => Resolve(kvp.Value, project.TextureFiles));

                if (teamEntry.NumbersPlacements != null)
                    foreach (var p in teamEntry.NumbersPlacements)
                        p.NumbersTexture = Resolve(p.NumbersTexture, project.TextureFiles);

                if (teamEntry.LiveryOverrides != null)
                {
                    foreach (var ov in teamEntry.LiveryOverrides)
                    {
                        ov.Driver1Livery = Resolve(ov.Driver1Livery, project.TextureFiles);
                        ov.Driver2Livery = Resolve(ov.Driver2Livery, project.TextureFiles);
                        ov.HelmetSponsors = Resolve(ov.HelmetSponsors, project.TextureFiles);
                        ov.VisorSponsors = Resolve(ov.VisorSponsors, project.TextureFiles);
                        ov.LiveryPreview = Resolve(ov.LiveryPreview, project.TextureFiles);

                        if (ov.DriversSpecificHelmet != null)
                            ov.DriversSpecificHelmet = ov.DriversSpecificHelmet
                                .ToDictionary(kvp => kvp.Key, kvp => Resolve(kvp.Value, project.TextureFiles));

                        if (ov.NumbersPlacements != null)
                            foreach (var p in ov.NumbersPlacements)
                                p.NumbersTexture = Resolve(p.NumbersTexture, project.TextureFiles);
                    }
                }
            }

            var drivers = project.Drivers.DeepClone();

            foreach (var driver in drivers.OfType<Ams2DriverData>())
            {
                driver.PictureUrl = Resolve(driver.PictureUrl, project.TextureFiles);
                driver.BaseHelmetFile = Resolve(driver.BaseHelmetFile, project.TextureFiles);
                driver.BaseVisorFile = Resolve(driver.BaseVisorFile, project.TextureFiles);
                driver.BaseHelmetFile90s = Resolve(driver.BaseHelmetFile90s, project.TextureFiles);
                driver.BaseHelmetFile80s = Resolve(driver.BaseHelmetFile80s, project.TextureFiles);
                driver.BaseVisorFile80s = Resolve(driver.BaseVisorFile80s, project.TextureFiles);
                driver.BaseHelmetFile70s = Resolve(driver.BaseHelmetFile70s, project.TextureFiles);
                driver.BaseVisorFile70s = Resolve(driver.BaseVisorFile70s, project.TextureFiles);
            }

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

        private static string ResolveXmlTexturePaths(string xmlContent)
        {
            string sampleBodiesPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "SampleBodies");

            return System.Text.RegularExpressions.Regex.Replace(
                xmlContent,
                @"(?i)(PATH="")(Driver\\)([^""]*"")",
                m => m.Groups[1].Value
                    + Path.Combine(sampleBodiesPath, m.Groups[3].Value.TrimEnd('"'))
                    + "\"");
        }

        private static string Resolve(string relativePath, Dictionary<string, string> textureFiles)
        {
            if (string.IsNullOrEmpty(relativePath)) return relativePath;
            return textureFiles.TryGetValue(relativePath, out var absolute) ? absolute : relativePath;
        }
    }
}