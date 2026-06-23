using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AMS2ChEd.SeasonPackEditor
{
    /// <summary>
    /// Builds a SaveGame from a ScenarioSaveConfig snapshot.
    /// The original season is never mutated.
    /// </summary>
    public static class ScenarioSaveBuilder
    {
        public static SaveGame Build(
            Ams2Season originalSeason,
            List<Ams2DriverData> allDrivers,
            ScenarioSaveConfig config)
        {
            var season = DeepCopySeason(originalSeason, config);
            var drivers = BuildDriverList(allDrivers, config, originalSeason.Year);
            int currentRound = ComputeCurrentRound(config, season);

            var saveGame = new SaveGame
            {
                CurrentSeason = season,
                Drivers = drivers,
                RetiredDrivers = Enumerable.Empty<IDriverData>(),
                NextGpIndex = currentRound,
                NextGpEntryList = new List<EntryListEntry>(),
                PlayerData = BuildPlayerData(config, allDrivers, season),
                GrandPrixResults = BuildGrandPrixResults(config, season, drivers),
                CurrentDriverStandings = BuildInitialDriverStandings(season, config, drivers),
                CurrentConstructorStandings = BuildInitialConstructorStandings(season, config),
                HistoricalDriverStandings = Enumerable.Empty<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = Enumerable.Empty<HistoricalConstructorStanding>(),
                Timestamp = DateTime.UtcNow,
                PreQualiStatus = PreQualiStatus.NotApplicable,
                PreQualiPoolEntries = new List<EntryListEntry>(),
                CurrentPreQualiDnpqResults = new List<ParticipantData>()
            };

            // Apply standings via the real StandingsManager for each completed race
            var standingsManager = new StandingsManager();
            foreach (var gpResult in saveGame.GrandPrixResults.ToList())
            {
                standingsManager.UpdateStandings(saveGame, gpResult);
            }

            return saveGame;
        }

        // ─── Season deep-copy with contract overrides ───────────────────────

        private static Ams2Season DeepCopySeason(Ams2Season original, ScenarioSaveConfig config)
        {
            // Build teams with scenario contracts, copying all other team fields
            var teams = original.Teams
                .Select(t => BuildScenarioTeamEntry(t as Ams2TeamEntry ?? MapToAms2TeamEntry(t), config))
                .ToList();

            // Add teams present in config but not in original season (custom/fictional teams)
            var originalTeamIds = original.Teams.Select(t => t.TeamId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var extraTeamIds = config.DriverSlots
                .Select(s => s.TeamId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(id => !originalTeamIds.Contains(id));

            foreach (var extraId in extraTeamIds)
            {
                var slots = config.DriverSlots.Where(s => s.TeamId.Equals(extraId, StringComparison.OrdinalIgnoreCase)).ToList();
                teams.Add(BuildMinimalTeamEntry(extraId, slots));
            }

            return new Ams2Season
            {
                Year = original.Year,
                OriginalYear = original.OriginalYear,
                Ams2Class = original.Ams2Class,
                PointsSystem = original.PointsSystem != null
                    ? new Dictionary<string, int>(original.PointsSystem)
                    : new Dictionary<string, int>(),
                PointsForFastestLap = original.PointsForFastestLap,
                Races = original.Races.ToList(),
                Teams = teams,
                Absences = config.Absences.ToList()
            };
        }

        private static Ams2TeamEntry BuildScenarioTeamEntry(Ams2TeamEntry original, ScenarioSaveConfig config)
        {
            var slot1 = config.DriverSlots.FirstOrDefault(s =>
                s.TeamId.Equals(original.TeamId, StringComparison.OrdinalIgnoreCase) && s.Slot == 1);
            var slot2 = config.DriverSlots.FirstOrDefault(s =>
                s.TeamId.Equals(original.TeamId, StringComparison.OrdinalIgnoreCase) && s.Slot == 2);

            return new Ams2TeamEntry
            {
                // Copy all non-contract team fields
                TeamId = original.TeamId,
                TeamName = original.TeamName,
                TeamPrincipal = original.TeamPrincipal,
                Reputation = original.Reputation,
                Color = original.Color,
                DefaultPrequalifying = original.DefaultPrequalifying,
                Ams2Car = original.Ams2Car,
                Ams2CarPerformanceMalus = original.Ams2CarPerformanceMalus,
                BaseLiveryDriver1 = original.BaseLiveryDriver1,
                BaseLiveryDriver2 = original.BaseLiveryDriver2,
                HelmetSponsors = original.HelmetSponsors,
                VisorSponsors = original.VisorSponsors,
                DriversSpecificHelmet = original.DriversSpecificHelmet,
                NumbersPlacements = original.NumbersPlacements,
                LiveryOverrides = original.LiveryOverrides,
                LiveryPreview = original.LiveryPreview,

                // Override contracts from scenario config
                Driver1Contract = slot1 != null
                    ? new DriverContract { DriverId = slot1.DriverId, DriverNumber = slot1.CarNumber, Races = slot1.Races }
                    : original.Driver1Contract,
                Driver2Contract = slot2 != null
                    ? new DriverContract { DriverId = slot2.DriverId, DriverNumber = slot2.CarNumber, Races = slot2.Races }
                    : original.Driver2Contract,
            };
        }

        private static Ams2TeamEntry BuildMinimalTeamEntry(string teamId, List<ScenarioDriverSlot> slots)
        {
            var slot1 = slots.FirstOrDefault(s => s.Slot == 1);
            var slot2 = slots.FirstOrDefault(s => s.Slot == 2);
            return new Ams2TeamEntry
            {
                TeamId = teamId,
                TeamName = teamId,
                Driver1Contract = slot1 != null
                    ? new DriverContract { DriverId = slot1.DriverId, DriverNumber = slot1.CarNumber, Races = slot1.Races }
                    : null,
                Driver2Contract = slot2 != null
                    ? new DriverContract { DriverId = slot2.DriverId, DriverNumber = slot2.CarNumber, Races = slot2.Races }
                    : null,
            };
        }

        private static Ams2TeamEntry MapToAms2TeamEntry(ITeamEntry t) => new Ams2TeamEntry
        {
            TeamId = t.TeamId,
            TeamName = t.TeamName,
            TeamPrincipal = t.TeamPrincipal,
            Reputation = t.Reputation,
            Color = t.Color,
            DefaultPrequalifying = t.DefaultPrequalifying,
            Driver1Contract = t.Driver1Contract,
            Driver2Contract = t.Driver2Contract
        };

        // ─── Driver list ────────────────────────────────────────────────────

        private static List<IDriverData> BuildDriverList(
            List<Ams2DriverData> allDrivers,
            ScenarioSaveConfig config,
            int year)
        {
            var usedIds = config.DriverSlots
                .Select(s => s.DriverId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Also include any substitute drivers referenced in absences
            foreach (var absence in config.Absences)
                if (!string.IsNullOrWhiteSpace(absence.DriverIn))
                    usedIds.Add(absence.DriverIn);

            var list = allDrivers
                .Where(d => usedIds.Contains(d.DriverId))
                .Cast<IDriverData>()
                .ToList();

            if (config.NewPlayerDriver != null)
            {
                var np = config.NewPlayerDriver;
                string RelPath(string fullPath) => string.IsNullOrWhiteSpace(fullPath) ? null
                    : $"../../Seasons/{year}/Scenarios/Helmets/{System.IO.Path.GetFileName(fullPath)}";

                list.Add(new Ams2DriverData
                {
                    DriverId = np.DriverId,
                    Name = $"{np.FirstName} {np.LastName}",
                    Nationality = np.Nationality,
                    BaseHelmetFile = RelPath(np.BaseHelmetFileFullPath),
                    BaseVisorFile = RelPath(np.BaseVisorFileFullPath),
                    BaseHelmetFile90s = RelPath(np.BaseHelmetFile90sFullPath),
                    BaseHelmetFile80s = RelPath(np.BaseHelmetFile80sFullPath),
                    BaseVisorFile80s = RelPath(np.BaseVisorFile80sFullPath),
                    BaseHelmetFile70s = RelPath(np.BaseHelmetFile70sFullPath),
                    BaseVisorFile70s = RelPath(np.BaseVisorFile70sFullPath),
                });
            }

            return list;
        }

        // ─── PlayerData ─────────────────────────────────────────────────────

        private static IPlayerData BuildPlayerData(
            ScenarioSaveConfig config,
            List<Ams2DriverData> allDrivers,
            ISeason season)
        {
            var driverId = config.PlayerDriverId;
            var teamId = config.DriverSlots
                .FirstOrDefault(s => s.DriverId.Equals(driverId, StringComparison.OrdinalIgnoreCase))
                ?.TeamId ?? "";

            string name, nationality;
            if (config.NewPlayerDriver != null &&
                config.NewPlayerDriver.DriverId.Equals(driverId, StringComparison.OrdinalIgnoreCase))
            {
                name = $"{config.NewPlayerDriver.FirstName} {config.NewPlayerDriver.LastName}";
                nationality = config.NewPlayerDriver.Nationality ?? "";
            }
            else
            {
                var d = allDrivers.FirstOrDefault(x =>
                    x.DriverId.Equals(driverId, StringComparison.OrdinalIgnoreCase));
                name = d?.Name ?? driverId;
                nationality = d?.Nationality ?? "";
            }

            return new PlayerData
            {
                DriverId = driverId,
                Name = name,
                Nationality = nationality,
                TeamId = teamId
            };
        }

        // ─── Grand Prix Results ─────────────────────────────────────────────

        private static IEnumerable<GrandPrixResult> BuildGrandPrixResults(
    ScenarioSaveConfig config,
    ISeason season,
    IEnumerable<IDriverData> drivers)
        {
            var results = new List<GrandPrixResult>();

            foreach (var predefined in config.PredefinedResults)
            {
                var race = season.Races.FirstOrDefault(r => r.RaceId == predefined.RaceId);
                if (race == null) continue;

                // Split into finishers and non-finishers
                // Finishers: have a positive position and are not DNF/DNQ
                // Non-finishers: DNF, DNQ, or position == 0
                var finishers = predefined.Results
                    .Where(r => !r.DNF && !r.DidNotPreQualify && r.Position > 0)
                    .OrderBy(r => r.Position)
                    .ToList();

                var nonFinishers = predefined.Results
                    .Where(r => r.DNF || r.DidNotPreQualify || r.Position == 0)
                    .ToList();

                int pos = 1;
                var sessionResults = new List<SessionResult>();

                foreach (var r in finishers)
                    sessionResults.Add(new SessionResult
                    {
                        DriverId = r.DriverId,
                        TeamId = r.TeamId,
                        Position = pos++,
                        DNF = false,
                        DidNotPreQualify = false,
                        FastestLap = r.FastestLap,
                        Points = 0
                    });

                foreach (var r in nonFinishers.Where(r => r.DNF))
                    sessionResults.Add(new SessionResult
                    {
                        DriverId = r.DriverId,
                        TeamId = r.TeamId,
                        Position = pos++,
                        DNF = true,
                        DidNotPreQualify = false,
                        FastestLap = false,
                        Points = 0
                    });

                foreach (var r in nonFinishers.Where(r => r.DidNotPreQualify))
                    sessionResults.Add(new SessionResult
                    {
                        DriverId = r.DriverId,
                        TeamId = r.TeamId,
                        Position = pos++,
                        DNF = false,
                        DidNotPreQualify = true,
                        FastestLap = false,
                        Points = 0
                    });

                results.Add(new GrandPrixResult
                {
                    Year = season.Year,
                    GrandPrixName = race.RaceName,
                    QualifyingResults = new List<SessionResult>(),
                    RaceResults = sessionResults
                });
            }

            return results;
        }

        // ─── Initial standings (empty, StandingsManager fills them) ─────────

        private static IEnumerable<HistoricalDriverStandingEntry> BuildInitialDriverStandings(
            ISeason season,
            ScenarioSaveConfig config,
            IEnumerable<IDriverData> drivers)
        {
            int pos = 1;
            return config.DriverSlots
                .GroupBy(s => s.DriverId)
                .Select(g =>
                {
                    var teamId = g.First().TeamId;
                    return new HistoricalDriverStandingEntry
                    {
                        Position = pos++,
                        DriverId = g.Key,
                        TeamId = teamId,
                        Points = 0,
                        PositionsTally = new PositionsTally()
                    };
                }).ToList();
        }

        private static IEnumerable<ConstructorStandingEntry> BuildInitialConstructorStandings(
            ISeason season,
            ScenarioSaveConfig config)
        {
            int pos = 1;
            return config.DriverSlots
                .Select(s => s.TeamId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(teamId => new ConstructorStandingEntry
                {
                    Position = pos++,
                    TeamId = teamId,
                    Points = 0,
                    PositionsTally = new PositionsTally()
                }).ToList();
        }

        // ─── currentRound calculation ────────────────────────────────────────

        private static int ComputeCurrentRound(ScenarioSaveConfig config, ISeason season)
        {
            if (!config.PredefinedResults.Any()) return 0;

            var completedRaceIds = config.PredefinedResults.Select(r => r.RaceId).ToHashSet();
            var races = season.Races.OrderBy(r => r.RaceId).ToList();

            for (int i = 0; i < races.Count; i++)
            {
                if (!completedRaceIds.Contains(races[i].RaceId))
                    return i;
            }

            return races.Count; // All races completed
        }

        // ─── Validation ─────────────────────────────────────────────────────

        public static List<string> ValidateConfig(
            ScenarioSaveConfig config,
            Ams2Season season,
            List<Ams2DriverData> allDrivers)
        {
            var errors = new List<string>();

            var allDriverIds = allDrivers.Select(d => d.DriverId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var allRaceIds = season.Races.Select(r => r.RaceId).ToHashSet();

            foreach (var slot in config.DriverSlots)
            {
                if (config.NewPlayerDriver != null &&
                    slot.DriverId.Equals(config.NewPlayerDriver.DriverId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!allDriverIds.Contains(slot.DriverId))
                    errors.Add($"Driver '{slot.DriverId}' in team '{slot.TeamId}' slot {slot.Slot} no longer exists in the season.");
            }

            foreach (var abs in config.Absences)
            {
                if (!allRaceIds.Contains(abs.RaceId))
                    errors.Add($"Absence references race ID {abs.RaceId} which no longer exists in the calendar.");
            }

            foreach (var result in config.PredefinedResults)
            {
                if (!allRaceIds.Contains(result.RaceId))
                    errors.Add($"Result references race ID {result.RaceId} which no longer exists in the calendar.");

                // Must have at least one classified finisher
                var finisherCount = result.Results.Count(r => !r.DNF && !r.DidNotPreQualify && r.Position > 0);
                if (finisherCount == 0)
                    errors.Add($"Race {result.RaceId} has no classified finishers. At least one driver must finish.");

                // Check for duplicate finishing positions within the race
                var duplicatePositions = result.Results
                    .Where(r => !r.DNF && !r.DidNotPreQualify && r.Position > 0)
                    .GroupBy(r => r.Position)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicatePositions.Any())
                    errors.Add($"Race {result.RaceId} has duplicate finishing position(s): {string.Join(", ", duplicatePositions)}.");
            }

            if (string.IsNullOrWhiteSpace(config.PlayerDriverId))
                errors.Add("No player driver selected.");

            if (config.NewPlayerDriver != null)
            {
                if (string.IsNullOrWhiteSpace(config.NewPlayerDriver.BaseHelmetFileFullPath))
                    errors.Add("New player driver requires a default helmet file.");
                if (string.IsNullOrWhiteSpace(config.NewPlayerDriver.BaseVisorFileFullPath))
                    errors.Add("New player driver requires a default visor file.");
            }

            return errors;
        }

        public static List<string> GetWarnings(
            ScenarioSaveConfig config,
            Ams2Season season,
            List<Ams2DriverData> allDrivers)
        {
            // Returns non-blocking warnings for stale detection (Option C - warn on open)
            var warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(config.SeasonSyncedAt))
            {
                warnings.Add("Scenario config has not been synced with the current season data.");
                return warnings;
            }

            var allDriverIds = allDrivers.Select(d => d.DriverId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var allRaceIds = season.Races.Select(r => r.RaceId).ToHashSet();

            var brokenDrivers = config.DriverSlots
                .Where(s => config.NewPlayerDriver == null ||
                            !s.DriverId.Equals(config.NewPlayerDriver.DriverId, StringComparison.OrdinalIgnoreCase))
                .Where(s => !allDriverIds.Contains(s.DriverId))
                .Select(s => s.DriverId)
                .Distinct()
                .ToList();

            if (brokenDrivers.Any())
                warnings.Add($"Driver(s) no longer in season: {string.Join(", ", brokenDrivers)}");

            var brokenRaces = config.PredefinedResults
                .Where(r => !allRaceIds.Contains(r.RaceId))
                .Select(r => r.RaceId.ToString())
                .ToList();

            if (brokenRaces.Any())
                warnings.Add($"Result race ID(s) no longer in calendar: {string.Join(", ", brokenRaces)}");

            return warnings;
        }
    }
}