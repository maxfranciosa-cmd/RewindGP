using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using Ams2ChEd.Business.AMS2.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AMS2ChEd.SeasonPackEditor.MainWindow;

namespace AMS2ChEd.SeasonPackEditor.Services
{
    /// <summary>
    /// Closes the loop between the points/position-derived target competitiveness score
    /// (<see cref="TeamTargetScoreService"/>) and what a team's power/weight/drag scalars actually
    /// produce in AMS2, by comparing that target against lap times captured from a real in-sim
    /// session and nudging the scalars toward the observed gap - instead of hand-guessing new
    /// numbers after every test session.
    /// </summary>
    public static class PerformanceCalibrationService
    {
        public const double DefaultGain = 0.5;

        /// <summary>
        /// One team's calibration car: a single AI seat driven by a clone of that team's real seat-1
        /// driver, with RatingValues neutralised so on-track pace reflects only the team's
        /// power/weight/drag scalar. The exact same list drives both the CustomAI/livery export
        /// (<see cref="GenerateCalibrationCustomAi"/>) and the shared-memory listening roster
        /// (<see cref="BuildParticipantRoster"/>), so the driver name string used to tag the livery
        /// and the name AMS2 reports over shared memory are guaranteed to match.
        /// </summary>
        public class CalibrationEntry
        {
            public string TeamId { get; set; }
            public string TeamName { get; set; }
            public string DriverId { get; set; }
            public int DriverNumber { get; set; }
            public Ams2DriverData Driver { get; set; }
        }

        public static List<CalibrationEntry> BuildCalibrationEntries(
            IEnumerable<Ams2TeamEntry> teams,
            IEnumerable<Ams2DriverData> drivers)
        {
            var driversById = drivers
                .Where(d => !string.IsNullOrEmpty(d.DriverId))
                .ToDictionary(d => d.DriverId, StringComparer.OrdinalIgnoreCase);

            var entries = new List<CalibrationEntry>();

            foreach (var team in teams)
            {
                var driverId = team.Driver1Contract?.DriverId;
                if (string.IsNullOrEmpty(driverId) || !driversById.TryGetValue(driverId, out var baseDriver))
                    continue;

                var clone = baseDriver.DeepClone();
                clone.RatingValues = null;
                clone.Name = $"{baseDriver.Name} (Calibration)";

                entries.Add(new CalibrationEntry
                {
                    TeamId = team.TeamId,
                    TeamName = team.TeamName,
                    DriverId = driverId,
                    DriverNumber = team.Driver1Contract?.DriverNumber ?? 0,
                    Driver = clone
                });
            }

            return entries;
        }

        /// <summary>
        /// Builds the participant roster <see cref="IRaceDataService.InitializeRaceWeekend"/> needs to
        /// resolve AI driver names (as AMS2 reports them over shared memory) back to a team. Exactly
        /// one seat per team - the same set <see cref="GenerateCalibrationCustomAi"/> exports - so any
        /// car not exported by this calibration run (including whichever car the tester personally
        /// drives) is naturally left unidentified rather than being folded into a real team's result.
        /// </summary>
        public static List<ParticipantData> BuildParticipantRoster(IEnumerable<CalibrationEntry> calibrationEntries)
        {
            int position = 1;
            return calibrationEntries.Select(entry => new ParticipantData
            {
                Position = position++,
                Number = entry.DriverNumber,
                DriverId = entry.DriverId,
                DriverName = entry.Driver.Name,
                TeamId = entry.TeamId,
                TeamName = entry.TeamName
            }).ToList();
        }

        /// <summary>
        /// Writes a CustomAI roster + livery for one AI car per team directly into a real AMS2
        /// install - no save file, no separate app to load it into. Every car uses the team's normal
        /// livery (so it's a recognisable in-game team car) and the neutral-ratings driver clone from
        /// <paramref name="calibrationEntries"/>, targeting the season's first scheduled race. This
        /// overwrites the AMS2 class's existing UserData/CustomAIDrivers/{class}.xml (AMS2LiveryService
        /// doesn't merge), so any prior roster there is backed up to a .bak file first.
        /// </summary>
        public static void GenerateCalibrationCustomAi(
            SeasonPackProject project,
            List<CalibrationEntry> calibrationEntries,
            string ams2InstallationFolder)
        {
            if (string.IsNullOrWhiteSpace(ams2InstallationFolder) || !Directory.Exists(ams2InstallationFolder))
                throw new InvalidOperationException("Configure a valid AMS2 install folder before exporting.");

            var firstRace = project.Season.Races?.FirstOrDefault();
            if (firstRace == null)
                throw new InvalidOperationException("Season has no races configured.");

            var raceEntryList = calibrationEntries.Select(entry => new EntryListEntry
            {
                TeamId = entry.TeamId,
                Driver1Id = entry.DriverId,
                Driver1Number = entry.DriverNumber
            }).ToList();

            var resolvedTeams = SeasonPackPathResolver.ResolveTeams(project.Season.Teams.OfType<Ams2TeamEntry>(), project);
            var resolvedDrivers = SeasonPackPathResolver.ResolveDrivers(
                calibrationEntries.Select(e => e.Driver), project.TextureFiles);

            var ams2Class = project.Season.Ams2Class;
            var modelCapacities = new CarModelCapacityLoader().GetModelsForClass(ams2Class);

            var liveryService = new Ams2LiveryService(
                project.Season.Year,
                ams2Class,
                resolvedDrivers,
                resolvedTeams,
                modelCapacities);

            BackupExistingCustomAiFile(ams2InstallationFolder, ams2Class);

            var tempSeasonDir = Path.Combine(Path.GetTempPath(), $"CalibrationSeason_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempSeasonDir);
            try
            {
                SeasonDirectoryScaffoldService.BuildStaticAssetsOnly(project, tempSeasonDir);
                liveryService.GenerateRaceFiles(firstRace.RaceId, raceEntryList, tempSeasonDir, ams2InstallationFolder);
            }
            finally
            {
                if (Directory.Exists(tempSeasonDir))
                    Directory.Delete(tempSeasonDir, true);
            }
        }

        /// <summary>
        /// Same as <see cref="GenerateCalibrationCustomAi"/>, but for a season pack that already lives
        /// on disk in the canonical Seasons/&lt;year&gt;/ layout (an installed season pack), so team/driver
        /// livery references can be resolved straight against <paramref name="seasonDirectory"/> - no
        /// SeasonPackPathResolver/TextureFiles lookup or temp static-assets scaffold needed.
        /// </summary>
        public static void GenerateCalibrationCustomAiForInstalledSeason(
            Ams2Season season,
            List<CalibrationEntry> calibrationEntries,
            string seasonDirectory,
            string ams2InstallationFolder)
        {
            if (string.IsNullOrWhiteSpace(ams2InstallationFolder) || !Directory.Exists(ams2InstallationFolder))
                throw new InvalidOperationException("Configure a valid AMS2 install folder before exporting.");

            var firstRace = season.Races?.FirstOrDefault();
            if (firstRace == null)
                throw new InvalidOperationException("Season has no races configured.");

            var raceEntryList = calibrationEntries.Select(entry => new EntryListEntry
            {
                TeamId = entry.TeamId,
                Driver1Id = entry.DriverId,
                Driver1Number = entry.DriverNumber
            }).ToList();

            var teams = season.Teams.OfType<Ams2TeamEntry>().ToList();
            var drivers = calibrationEntries.Select(e => e.Driver).ToList();

            var ams2Class = season.Ams2Class;
            var modelCapacities = new CarModelCapacityLoader().GetModelsForClass(ams2Class);

            var liveryService = new Ams2LiveryService(season.Year, ams2Class, drivers, teams, modelCapacities);

            BackupExistingCustomAiFile(ams2InstallationFolder, ams2Class);

            liveryService.GenerateRaceFiles(firstRace.RaceId, raceEntryList, seasonDirectory, ams2InstallationFolder);
        }

        private static void BackupExistingCustomAiFile(string ams2InstallationFolder, string ams2Class)
        {
            var customAiPath = Path.Combine(ams2InstallationFolder, "UserData", "CustomAIDrivers", $"{ams2Class}.xml");
            if (File.Exists(customAiPath))
            {
                File.Copy(customAiPath, customAiPath + ".bak", true);
            }
        }

        /// <summary>
        /// Normalizes a captured session's best lap times into the same 0 (slowest team observed) -
        /// 1 (fastest team observed) scale <see cref="DriverPerformanceGenerator.ComputeCompetitivenessScore"/>
        /// produces for the target, so "actual" and "target" are directly comparable without needing a
        /// physically-grounded seconds-to-scalar curve. Only cars belonging to one of
        /// <paramref name="validTeamIds"/> (the teams this calibration run actually exported) are
        /// considered - any other car on track (the tester's own, stray default-grid AI) reports back
        /// over shared memory with an unmatched-name placeholder team id and would otherwise skew the
        /// fastest/slowest spread every real team's score is normalized against.
        /// </summary>
        public static Dictionary<string, double> ComputeActualScores(
            IEnumerable<ParticipantData> finalStandings,
            IEnumerable<string> validTeamIds)
        {
            var validSet = new HashSet<string>(validTeamIds, StringComparer.OrdinalIgnoreCase);

            var bestLapByTeam = finalStandings
                .Where(p => !string.IsNullOrEmpty(p.TeamId) && validSet.Contains(p.TeamId) && TryParseLapTime(p.BestLapTime, out _))
                .GroupBy(p => p.TeamId, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    TeamId = g.Key,
                    BestLap = g.Min(p => TryParseLapTime(p.BestLapTime, out var seconds) ? seconds : double.MaxValue)
                })
                .Where(x => x.BestLap < double.MaxValue)
                .ToList();

            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (!bestLapByTeam.Any())
                return result;

            double fastest = bestLapByTeam.Min(x => x.BestLap);
            double slowest = bestLapByTeam.Max(x => x.BestLap);
            double spread = slowest - fastest;

            foreach (var entry in bestLapByTeam)
            {
                double score = spread > 0 ? 1.0 - (entry.BestLap - fastest) / spread : 1.0;
                result[entry.TeamId] = score;
            }

            return result;
        }

        /// <summary>
        /// Nudges an existing power/weight scalar pair toward closing the gap between the target
        /// (points/position) score and the actual (in-sim) score, damped by <paramref name="gain"/> so
        /// a single noisy session doesn't overcorrect, and clamped to the same safety envelope
        /// DriverPerformanceGenerator.Generate already enforces. Drag is left untouched - there's no
        /// independent signal for it, only a combined pace observation.
        /// </summary>
        public static Dictionary<string, double> CorrectScalars(
            IReadOnlyDictionary<string, double> currentMalus,
            double targetScore,
            double actualScore,
            double gain = DefaultGain)
        {
            double error = targetScore - actualScore; // positive: team looked weaker in-sim than it should be

            double power = currentMalus.TryGetValue("power_scalar", out var p) ? p : -1.0;
            double weight = currentMalus.TryGetValue("weight_scalar", out var w) ? w : -1.0;
            double drag = currentMalus.TryGetValue("drag_scalar", out var d) ? d : -1.0;

            // DriverPerformanceGenerator.Generate stores a STRONGER team's power_scalar as MORE
            // negative (best: -1.030, worst: -0.970) and its weight_scalar as LESS negative (best:
            // -0.970, worst: -1.030) - see PowerBest/PowerWorst/WeightBest/WeightWorst. So a positive
            // error (team looked weaker in-sim than it should be) needs power to move further negative
            // and weight to move less negative, to make the team stronger.
            double step = gain * error * 0.06;
            power = DriverPerformanceGenerator.ClampPowerScalar(power - step);
            weight = DriverPerformanceGenerator.ClampWeightScalar(weight + step);

            return new Dictionary<string, double>
            {
                ["power_scalar"] = Math.Round(power, 3),
                ["weight_scalar"] = Math.Round(weight, 3),
                ["drag_scalar"] = Math.Round(drag, 3)
            };
        }

        public static bool TryParseLapTime(string lapTime, out double seconds)
        {
            seconds = 0;
            if (string.IsNullOrEmpty(lapTime) || lapTime == "--:--.---")
                return false;

            var parts = lapTime.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var minutes))
                return false;

            var secondsParts = parts[1].Split('.');
            if (secondsParts.Length != 2
                || !int.TryParse(secondsParts[0], out var wholeSeconds)
                || !int.TryParse(secondsParts[1], out var milliseconds))
                return false;

            seconds = (minutes * 60.0) + wholeSeconds + (milliseconds / 1000.0);
            return true;
        }
    }
}
