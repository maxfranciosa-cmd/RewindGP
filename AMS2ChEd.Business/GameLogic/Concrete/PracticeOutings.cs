using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;

namespace AMS2ChEd.Business.GameLogic.Concrete
{
    /// <summary>Where the outing driver ended FP1 against the benchmark.</summary>
    public enum PracticeOutcome
    {
        Ahead,
        WithinReach,
        FurtherBack
    }

    /// <summary>What a season of FP1 outings is worth at its end (PracticeOutings.RewardFor).</summary>
    public enum PracticeReward
    {
        None,
        NoDecay,
        YoungTalent,
        YoungChampionshipLevelUnproven
    }

    /// <summary>The player's answer to his FP1 outing (phase 5): drive it in the game, or have it simulated.</summary>
    public enum PracticeOutingChoice
    {
        Drive,
        Simulate
    }

    /// <summary>The car an outing driver takes, and the driver he's measured against.</summary>
    public readonly record struct PracticeOutingCar(string ReplacedDriverId, string BenchmarkDriverId);

    /// <summary>
    /// An outing with where its driver ended against the benchmark; a null Outcome for the player's
    /// outing still to be driven or simulated (phase 5).
    /// </summary>
    public readonly record struct PracticeOutingResult(PracticeOuting Outing, PracticeOutcome? Outcome);

    /// <summary>
    /// Rookie FP1 outings of the modern driver market: each team of the current season's academies
    /// lends its second car for first practice twice a season to an academy member without a race
    /// seat. The outing races are worked out from the calendar (nothing stored); who drives is
    /// decided race by race and kept on the save (ISaveGame.PracticeOutings) with its score, and the
    /// season's average score can raise an inactive young driver's reputation (EndOfSeasonManager).
    /// FP1 never touches race results, standings or the race weekend.
    /// </summary>
    public static class PracticeOutings
    {
        public const int PerTeam = 2;

        // "within reach": at most this many places behind the benchmark
        public const int WithinPositions = 3;

        // a season shorter than this has no outings
        public const int MinRaces = 3;

        #region Which races have outings

        /// <summary>
        /// The indexes (into season.Races) of the races where the team runs an outing: PerTeam of
        /// them, evenly spaced over the calendar after the first race, and offset by the team's index
        /// in its academy (parents first, then sisters) so one academy's teams don't share a race.
        /// None for a team of no academy, or in a season of fewer than MinRaces races.
        /// </summary>
        public static IReadOnlyList<int> OutingRaceIndexes(ISeason season, string teamId)
        {
            var raceCount = season?.Races?.Count() ?? 0;
            var academy = AcademyMembership.AcademyOfTeam(season, teamId);
            if (academy == null || raceCount < MinRaces)
                return Array.Empty<int>();

            var academyTeams = (academy.ParentTeams ?? new List<string>())
                .Concat(academy.SisterTeams ?? new List<string>())
                .Distinct()
                .ToList();
            var teamIndex = academyTeams.IndexOf(teamId);
            var teamCount = academyTeams.Count;

            // races 1..raceCount-1, cut into PerTeam stretches; the academy's teams share each
            // stretch out, every team taking the middle of its own slice
            var racesAfterTheFirst = raceCount - 1;
            var spacing = racesAfterTheFirst / (double)PerTeam;
            var offset = spacing * (2 * teamIndex + 1) / (2.0 * teamCount);

            return Enumerable.Range(0, PerTeam)
                .Select(k => 1 + (int)Math.Floor(k * spacing + offset))
                .ToList();
        }

        public static bool HasOutingAt(ISeason season, string teamId, int raceIndex) =>
            OutingRaceIndexes(season, teamId).Contains(raceIndex);

        #endregion

        #region The car and the benchmark

        /// <summary>
        /// The car an outing takes at this race (the second driver's), and the benchmark (the first
        /// driver), as the entry list has them: a stand-in counts for the driver he replaces. The
        /// contracts say which car is the second one: a SECOND_DRIVER contract, else for EQUAL ones
        /// the driver who isn't first-driver material; on a tie, Driver2Contract's car. Null for a
        /// team running one car.
        /// </summary>
        public static PracticeOutingCar? CarFor(ITeamEntry team, EntryListEntry entry, Func<string, DriverReputation> reputationOf)
        {
            if (team == null || entry == null || string.IsNullOrEmpty(entry.Driver1Id) || string.IsNullOrEmpty(entry.Driver2Id))
                return null;

            var driver1Rank = Rank(team.Driver1Contract, entry.Driver1Id, team.Reputation, reputationOf);
            var driver2Rank = Rank(team.Driver2Contract, entry.Driver2Id, team.Reputation, reputationOf);

            return driver1Rank < driver2Rank
                ? new PracticeOutingCar(entry.Driver1Id, entry.Driver2Id)
                : new PracticeOutingCar(entry.Driver2Id, entry.Driver1Id);
        }

        // how "first" the contract's driver is: the role first, first-driver material second
        private static int Rank(DriverContract contract, string driverInTheCar, TeamReputation teamReputation, Func<string, DriverReputation> reputationOf)
        {
            var roleRank = contract?.Role switch
            {
                ContractRole.FIRST_DRIVER => 2,
                ContractRole.SECOND_DRIVER => 0,
                _ => 1 // EQUAL, and UNDEFINED from older data
            };
            var contractDriver = string.IsNullOrEmpty(contract?.DriverId) ? driverInTheCar : contract.DriverId;
            var material = DriverHirer.IsFirstDriverMaterial(reputationOf(contractDriver), teamReputation) ? 1 : 0;
            return roleRank * 2 + material;
        }

        #endregion

        #region Who runs an outing

        /// <summary>
        /// Allocates this race's outings, adds them to the save (unscored) and returns them. For each
        /// team with an outing at this race, in the season's team order, the driver is an eligible
        /// member of its academy: a member this season, with no race seat, not on this weekend's
        /// entry list nor standing in at this race, not out of action, and without another outing at
        /// this race. The fewest outings this season go first, then the team's own reserve at a parent
        /// team, then chance. A team with no eligible member, one car, or an outing already allocated
        /// for this race (the entry list drawn up again) gets none.
        /// </summary>
        public static List<PracticeOuting> Allocate(ISaveGame saveGame, int raceIndex, IEnumerable<EntryListEntry> entryList, Random random)
        {
            var allocated = new List<PracticeOuting>();
            var season = saveGame?.CurrentSeason;
            var race = season?.Races?.ElementAtOrDefault(raceIndex);
            if (race == null || season.Academies == null || season.Academies.Count == 0)
                return allocated;

            var entries = (entryList ?? Enumerable.Empty<EntryListEntry>()).ToList();
            var outings = saveGame.PracticeOutings ?? new List<PracticeOuting>();
            var thisSeason = outings.Where(o => o.Year == season.Year).ToList();

            var racingThisWeekend = entries.SelectMany(e => new[] { e.Driver1Id, e.Driver2Id })
                .Concat((season.Absences ?? Enumerable.Empty<Absence>()).Where(a => a.RaceId == race.RaceId).Select(a => a.DriverIn))
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();
            var raceDriverIds = season.RaceDriverIds();
            var lastTeams = AcademyMembership.LastTeams(saveGame);
            var drivers = new Dictionary<string, IDriverData>();
            foreach (var d in saveGame.Drivers ?? Enumerable.Empty<IDriverData>())
            {
                if (d?.DriverId != null)
                    drivers.TryAdd(d.DriverId, d);
            }
            var playerId = saveGame.PlayerData?.DriverId;
            var playerAcademyId = saveGame.PlayerData?.AcademyId;
            var busyThisRace = thisSeason.Where(o => o.RaceId == race.RaceId).Select(o => o.DriverId).ToHashSet();

            DriverReputation ReputationOf(string id) =>
                id != null && drivers.TryGetValue(id, out var d) ? d.Reputation : DriverReputation.PAY_DRIVER_SEASON;

            foreach (var team in season.Teams ?? Enumerable.Empty<ITeamEntry>())
            {
                if (!HasOutingAt(season, team.TeamId, raceIndex))
                    continue;
                if (thisSeason.Any(o => o.RaceId == race.RaceId && o.TeamId == team.TeamId))
                    continue;

                var car = CarFor(team, entries.FirstOrDefault(e => e.TeamId == team.TeamId), ReputationOf);
                if (car == null)
                    continue;

                var academy = AcademyMembership.AcademyOfTeam(season, team.TeamId);
                var candidates = (academy.Drivers ?? new List<string>())
                    .Concat(!string.IsNullOrEmpty(playerId) && playerAcademyId == academy.Id ? new[] { playerId } : Array.Empty<string>())
                    .Distinct()
                    .Where(id => drivers.TryGetValue(id, out var d) &&
                                 AcademyMembership.AcademyOf(season, id, d.YearOfBirth, playerId, playerAcademyId, lastTeams.GetValueOrDefault(id))?.Id == academy.Id)
                    .Where(id => !raceDriverIds.Contains(id) &&
                                 !racingThisWeekend.Contains(id) &&
                                 !busyThisRace.Contains(id) &&
                                 !saveGame.IsOutOfAction(id, race.RaceId))
                    .ToList();
                if (candidates.Count == 0)
                    continue;

                var driverId = Pick(candidates, id => thisSeason.Count(o => o.DriverId == id), AcademyMembership.IsParentTeam(academy, team.TeamId) ? team.ReserveDriverId() : null, random);

                var outing = new PracticeOuting
                {
                    Year = season.Year,
                    RaceId = race.RaceId,
                    TeamId = team.TeamId,
                    DriverId = driverId,
                    ReplacedDriverId = car.Value.ReplacedDriverId,
                    BenchmarkDriverId = car.Value.BenchmarkDriverId
                };
                outings.Add(outing);
                thisSeason.Add(outing);
                allocated.Add(outing);
                busyThisRace.Add(driverId);
            }

            if (allocated.Any())
                saveGame.PracticeOutings = outings;
            return allocated;
        }

        // the fewest outings first, then the parent team's own reserve, then chance
        private static string Pick(List<string> candidates, Func<string, int> outingsSoFar, string ownReserveId, Random random)
        {
            var fewest = candidates.Min(outingsSoFar);
            var group = candidates.Where(c => outingsSoFar(c) == fewest).ToList();
            if (ownReserveId != null && group.Contains(ownReserveId))
                return ownReserveId;
            return group[random.Next(group.Count)];
        }

        #endregion

        #region Running and scoring an outing

        /// <summary>The FP1 field: the race's entry list with every outing driver in the car he takes.</summary>
        public static List<EntryListEntry> PracticeField(IEnumerable<EntryListEntry> entryList, IEnumerable<PracticeOuting> outings)
        {
            var outingList = (outings ?? Enumerable.Empty<PracticeOuting>()).ToList();
            string Swap(string teamId, string driverId) =>
                outingList.FirstOrDefault(o => o.TeamId == teamId && o.ReplacedDriverId == driverId)?.DriverId ?? driverId;

            return (entryList ?? Enumerable.Empty<EntryListEntry>()).Select(e => new EntryListEntry
            {
                TeamId = e.TeamId,
                Driver1Id = Swap(e.TeamId, e.Driver1Id),
                Driver1Reputation = e.Driver1Reputation,
                Driver1Number = e.Driver1Number,
                Driver2Id = Swap(e.TeamId, e.Driver2Id),
                Driver2Reputation = e.Driver2Reputation,
                Driver2Number = e.Driver2Number
            }).ToList();
        }

        /// <summary>
        /// The outing driver's place against the benchmark's in an FP1 classification (driver ids,
        /// fastest first): ahead, at most WithinPositions places behind, or further back. An outing
        /// driver missing from it is further back; a benchmark missing from it is beaten.
        /// </summary>
        public static PracticeOutcome Outcome(IReadOnlyList<string> classification, string driverId, string benchmarkDriverId)
        {
            var position = classification?.ToList().IndexOf(driverId) ?? -1;
            var benchmarkPosition = classification?.ToList().IndexOf(benchmarkDriverId) ?? -1;
            if (position < 0)
                return PracticeOutcome.FurtherBack;
            if (benchmarkPosition < 0 || position < benchmarkPosition)
                return PracticeOutcome.Ahead;
            return position - benchmarkPosition <= WithinPositions ? PracticeOutcome.WithinReach : PracticeOutcome.FurtherBack;
        }

        /// <summary>
        /// The score of one outing, 0-3: the outcome weighed by the benchmark's standing
        /// (DriverHirer.ReputationScore): championship level 3/2/0, strong midfield 2/1/0, below 1/0/0.
        /// </summary>
        public static int Score(PracticeOutcome outcome, DriverReputation benchmarkReputation)
        {
            var level = DriverHirer.ReputationScore(benchmarkReputation);
            return (level, outcome) switch
            {
                ( >= 5, PracticeOutcome.Ahead) => 3,
                ( >= 5, PracticeOutcome.WithinReach) => 2,
                (4, PracticeOutcome.Ahead) => 2,
                (4, PracticeOutcome.WithinReach) => 1,
                ( <= 3, PracticeOutcome.Ahead) => 1,
                _ => 0
            };
        }

        /// <summary>
        /// Runs the outings by simulation, all in one FP1 session of the race's field (qualifying's
        /// performance model), and scores each against its benchmark.
        /// </summary>
        public static List<PracticeOutingResult> RunSimulated(ISaveGame saveGame, IEnumerable<EntryListEntry> entryList, IReadOnlyList<PracticeOuting> outings) =>
            RunSimulated(saveGame, entryList, outings, outings);

        /// <summary>
        /// One simulated FP1 session with every outing of fieldOutings in the car it takes, scoring
        /// only the outings of toScore (the others were run already, or wait for the player).
        /// </summary>
        public static List<PracticeOutingResult> RunSimulated(ISaveGame saveGame, IEnumerable<EntryListEntry> entryList, IReadOnlyList<PracticeOuting> fieldOutings, IReadOnlyList<PracticeOuting> toScore)
        {
            if (toScore == null || toScore.Count == 0)
                return new List<PracticeOutingResult>();

            var field = PracticeField(entryList, fieldOutings);
            var simulation = new SimulatedRaceDataService(saveGame);
            simulation.InitializeRaceWeekend(Participants(saveGame, field));
            var classification = simulation.SimulateQualifyingOnly()
                .OrderBy(p => p.Position)
                .Select(p => p.DriverId)
                .ToList();

            return ScoreFrom(saveGame, toScore, classification);
        }

        /// <summary>
        /// Scores each outing from an FP1 classification (driver ids, fastest first), against the
        /// benchmark's reputation at the time.
        /// </summary>
        public static List<PracticeOutingResult> ScoreFrom(ISaveGame saveGame, IEnumerable<PracticeOuting> outings, IReadOnlyList<string> classification)
        {
            var results = new List<PracticeOutingResult>();
            foreach (var outing in outings)
            {
                var benchmark = saveGame.Drivers?.FirstOrDefault(d => d.DriverId == outing.BenchmarkDriverId);
                var benchmarkReputation = benchmark?.Reputation ?? DriverReputation.PAY_DRIVER_SEASON;
                var outcome = Outcome(classification, outing.DriverId, outing.BenchmarkDriverId);
                outing.Score = Score(outcome, benchmarkReputation);
                results.Add(new PracticeOutingResult(outing, outcome));
            }
            return results;
        }

        /// <summary>
        /// Allocates this race's outings and runs the AI drivers' by simulation, in one session with
        /// every allocated outing on track. The player's outing waits for his choice (drive it in the
        /// game, or SimulatePlayerOuting): it comes back unscored, with a null Outcome.
        /// </summary>
        public static List<PracticeOutingResult> AllocateAndSimulate(ISaveGame saveGame, int raceIndex, IEnumerable<EntryListEntry> entryList, Random random)
        {
            var entries = (entryList ?? Enumerable.Empty<EntryListEntry>()).ToList();
            var allocated = Allocate(saveGame, raceIndex, entries, random);
            var playerId = saveGame?.PlayerData?.DriverId;
            var aiOutings = allocated.Where(o => o.DriverId != playerId).ToList();

            var results = RunSimulated(saveGame, entries, allocated, aiOutings);
            results.AddRange(allocated.Where(o => o.DriverId == playerId).Select(o => new PracticeOutingResult(o, null)));
            return results;
        }

        /// <summary>The player's outing at that race still to be run (Score null), or null.</summary>
        public static PracticeOuting PendingPlayerOuting(ISaveGame saveGame, int raceId)
        {
            var playerId = saveGame?.PlayerData?.DriverId;
            var year = saveGame?.CurrentSeason?.Year;
            return (saveGame?.PracticeOutings ?? new List<PracticeOuting>())
                .FirstOrDefault(o => o.Year == year && o.RaceId == raceId && o.DriverId == playerId && o.Score == null);
        }

        /// <summary>
        /// The player chose not to drive his outing: it's simulated in a session with every outing
        /// of the race on track, as the AI ones were. Null when he has none pending at that race.
        /// </summary>
        public static PracticeOutingResult? SimulatePlayerOuting(ISaveGame saveGame, int raceId, IEnumerable<EntryListEntry> entryList)
        {
            var outing = PendingPlayerOuting(saveGame, raceId);
            if (outing == null)
                return null;

            var fieldOutings = saveGame.PracticeOutings.Where(o => o.Year == outing.Year && o.RaceId == raceId).ToList();
            return RunSimulated(saveGame, entryList, fieldOutings, new[] { outing }).Single();
        }

        /// <summary>
        /// Drops the player's pending outing at that race: he took a race seat for the weekend (a
        /// paid ride), so he isn't free for FP1 any more. True when there was one.
        /// </summary>
        public static bool CancelPendingPlayerOuting(ISaveGame saveGame, int raceId)
        {
            var outing = PendingPlayerOuting(saveGame, raceId);
            return outing != null && saveGame.PracticeOutings.Remove(outing);
        }

        /// <summary>
        /// The grid of the FP1 session the player drives: a copy of the race's entry list with him
        /// in the replaced driver's car. The save's own entry list is left alone.
        /// </summary>
        public static List<EntryListEntry> PracticeGrid(IEnumerable<EntryListEntry> entryList, PracticeOuting outing) =>
            PracticeField(entryList, outing == null ? Array.Empty<PracticeOuting>() : new[] { outing });

        /// <summary>
        /// A practice session's classification, by best lap: the fastest first, cars without a lap
        /// time last (in the order the game listed them).
        /// </summary>
        public static List<string> ClassificationByBestLap(IEnumerable<ParticipantData> standings) =>
            (standings ?? Enumerable.Empty<ParticipantData>())
                .Where(p => !string.IsNullOrEmpty(p.DriverId))
                .Select((p, i) => (p.DriverId, Lap: LapSeconds(p.BestLapTime), i))
                .OrderBy(p => p.Lap ?? double.MaxValue)
                .ThenBy(p => p.i)
                .Select(p => p.DriverId)
                .ToList();

        /// <summary>Scores the player's outing from the practice session he drove.</summary>
        public static PracticeOutingResult ScoreDriven(ISaveGame saveGame, PracticeOuting outing, IEnumerable<ParticipantData> practiceStandings) =>
            ScoreFrom(saveGame, new[] { outing }, ClassificationByBestLap(practiceStandings)).Single();

        // "m:ss.fff" in seconds, null for no time ("--:--.---", empty, unreadable)
        private static double? LapSeconds(string lapTime)
        {
            if (string.IsNullOrWhiteSpace(lapTime))
                return null;

            var parts = lapTime.Split(':');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var minutes) ||
                !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                return null;

            var total = minutes * 60 + seconds;
            return total > 0 ? total : null;
        }

        private static List<ParticipantData> Participants(ISaveGame saveGame, IEnumerable<EntryListEntry> field)
        {
            var participants = new List<ParticipantData>();
            foreach (var entry in field)
            {
                var teamName = saveGame.CurrentSeason.Teams.FirstOrDefault(t => t.TeamId == entry.TeamId)?.TeamName ?? entry.TeamId ?? "";
                foreach (var (driverId, number) in new[] { (entry.Driver1Id, entry.Driver1Number), (entry.Driver2Id, entry.Driver2Number) })
                {
                    if (string.IsNullOrEmpty(driverId))
                        continue;
                    var name = driverId == saveGame.PlayerData?.DriverId ? saveGame.PlayerData.Name : null;
                    name ??= saveGame.Drivers?.FirstOrDefault(d => d.DriverId == driverId)?.Name ?? driverId;
                    participants.Add(new ParticipantData { DriverId = driverId, DriverName = name ?? driverId, TeamId = entry.TeamId, TeamName = teamName, Number = number });
                }
            }
            return participants;
        }

        #endregion

        #region End-of-season reward

        /// <summary>Every driver's scored outings of that season.</summary>
        public static Dictionary<string, List<int>> ScoresOf(ISaveGame saveGame, int year) =>
            (saveGame?.PracticeOutings ?? new List<PracticeOuting>())
                .Where(o => o.Year == year && o.Score.HasValue && !string.IsNullOrEmpty(o.DriverId))
                .GroupBy(o => o.DriverId)
                .ToDictionary(g => g.Key, g => g.Select(o => o.Score.Value).ToList());

        public const double YoungChampionshipLevelUnprovenAverage = 2.0;
        public const double YoungTalentAverage = 1.0;
        public const double NoDecayAverage = 0.5;

        /// <summary>
        /// The reward of a season's average score: 2.0 (within reach of a championship-level first
        /// driver at every outing) makes him YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, 1.0 YOUNG_TALENT, 0.5
        /// spares him the inactive decay; below that, nothing. Calibrated on the simulation: at 2.5
        /// a YOUNG_TALENT junior reached the top tier in 1-6% of seasons, now 3-17%, and never
        /// automatically.
        /// </summary>
        public static PracticeReward RewardFor(double averageScore) => averageScore switch
        {
            >= YoungChampionshipLevelUnprovenAverage => PracticeReward.YoungChampionshipLevelUnproven,
            >= YoungTalentAverage => PracticeReward.YoungTalent,
            >= NoDecayAverage => PracticeReward.NoDecay,
            _ => PracticeReward.None
        };

        public static PracticeReward RewardFor(IReadOnlyCollection<int> scores) =>
            scores == null || scores.Count == 0 ? PracticeReward.None : RewardFor(scores.Average());

        /// <summary>
        /// An inactive driver's new reputation with his FP1 reward: the higher (by
        /// DriverHirer.ReputationScore) of the usual inactive result and the reward's, the usual one
        /// on a tie, so FP1 only ever raises. The young tiers need a driver still under 25 next
        /// season (ageNextSeason); for an older one they only spare him the decay.
        /// </summary>
        public static DriverReputation Raise(DriverReputation usual, PracticeReward reward, DriverReputation currentReputation, int ageNextSeason, IReputationUpdater reputationUpdater)
        {
            var young = ageNextSeason < ReputationUpdater.YOUNG_DRIVER_AGE;
            DriverReputation? earned = reward switch
            {
                PracticeReward.YoungChampionshipLevelUnproven when young => DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN,
                PracticeReward.YoungTalent when young => DriverReputation.YOUNG_TALENT,
                PracticeReward.None => null,
                _ => reputationUpdater.GetNewReputationForInactiveDriver(currentReputation, ageNextSeason, decays: false)
            };

            return earned != null && DriverHirer.ReputationScore(earned.Value) > DriverHirer.ReputationScore(usual) ? earned.Value : usual;
        }

        #endregion
    }
}
