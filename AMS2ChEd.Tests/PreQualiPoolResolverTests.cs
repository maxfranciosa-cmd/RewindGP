using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    /// <summary>
    /// Tests for PreQualiPoolResolver.
    ///
    /// Key rules under test:
    ///   - Pre-qualifying is not applicable when total drivers &lt;= MaxDriversPerRace (default 26)
    ///   - Round 0  : flags refreshed using last year's standings (worst returning teams);
    ///                new teams (not in last year's standings) always enter pool
    ///   - Round N/2: flags refreshed; new teams that scored well may leave,
    ///                worst returning teams by current standings fill remaining slots
    ///   - All other rounds: flags unchanged — pool is read from DefaultPrequalifying as-is
    ///   - Pool expands when new teams exceed minimumPoolSize; PassCount adapts accordingly
    ///
    /// Baseline: 16 races (halfway = round 8), MaxDriversPerRace = 26,
    ///           14 two-driver returning teams ranked P1–P14 in both seasons.
    ///
    /// Naming convention: Resolve_Scenario_ExpectedBehaviour
    /// </summary>
    [TestClass]
    public class PreQualiPoolResolverTests
    {
        private PreQualiPoolResolver _resolver;

        [TestInitialize]
        public void Setup()
        {
            _resolver = new PreQualiPoolResolver();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Builder helpers
        // ─────────────────────────────────────────────────────────────────────

        private static TeamEntry MakeTeam(string teamId) =>
            new TeamEntry
            {
                TeamId = teamId,
                TeamName = teamId,
                Driver1Contract = new DriverContract { DriverId = $"{teamId}_d1", DriverNumber = 1 },
                Driver2Contract = new DriverContract { DriverId = $"{teamId}_d2", DriverNumber = 2 },
                DefaultPrequalifying = false
            };

        private static ConstructorStandingEntry MakeStanding(string teamId, int position) =>
            new ConstructorStandingEntry { TeamId = teamId, Position = position };

        private static HistoricalConstructorStanding MakeHistorical(
            int year,
            IEnumerable<(string teamId, int position)> entries) =>
            new HistoricalConstructorStanding
            {
                Year = year,
                Standing = entries
                    .Select(e => new HistoricalConstructorStandingEntry { TeamId = e.teamId, Position = e.position })
                    .ToList()
            };

        /// <summary>
        /// Baseline: 14 returning teams, 16 races.
        /// Last year: P1=team_01 … P14=team_14.
        /// Current standings mirror last year unless overridden.
        /// </summary>
        private SaveGame BuildBaseline(
            List<TeamEntry> teamsOverride = null,
            List<ConstructorStandingEntry> currentStandingsOverride = null,
            List<HistoricalConstructorStanding> historicalOverride = null,
            int raceCount = 16,
            int? maxDriversPerRace = 26)
        {
            var teams = teamsOverride
                ?? Enumerable.Range(1, 14).Select(i => MakeTeam($"team_{i:D2}")).ToList();

            var races = Enumerable.Range(1, raceCount)
                .Select(i => new Race { RaceId = i, RaceName = $"Round {i}", RaceDate = "1990-01-01" })
                .ToList();

            var current = currentStandingsOverride
                ?? teams.Select((t, i) => MakeStanding(t.TeamId, i + 1)).ToList();

            var historical = historicalOverride
                ?? new List<HistoricalConstructorStanding>
                   {
                       MakeHistorical(1989, teams.Select((t, i) => (t.TeamId, i + 1)))
                   };

            return new SaveGame
            {
                CurrentSeason = new Season
                {
                    Year = 1990,
                    Teams = teams.Cast<ITeamEntry>().ToList(),
                    Races = races,
                    MaxDriversPerRace = maxDriversPerRace
                },
                CurrentConstructorStandings = current,
                HistoricalConstructorStandings = historical,
                Drivers = new List<DriverData>(),
                PlayerData = new PlayerData { DriverId = "player", Name = "Player" }
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Not applicable
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Resolve_ExactlyAtLimit_ReturnsNotApplicable()
        {
            // 13 teams × 2 = 26 drivers — exactly at the limit
            var saveGame = BuildBaseline(
                teamsOverride: Enumerable.Range(1, 13).Select(i => MakeTeam($"team_{i:D2}")).ToList());

            var result = _resolver.Resolve(saveGame, roundIndex: 0);

            Assert.IsFalse(result.IsApplicable);
            Assert.AreEqual(0, result.PoolTeams.Count);
        }

        [TestMethod]
        public void Resolve_BelowLimit_ReturnsNotApplicable()
        {
            // 10 teams × 2 = 20 drivers
            var saveGame = BuildBaseline(
                teamsOverride: Enumerable.Range(1, 10).Select(i => MakeTeam($"team_{i:D2}")).ToList());

            var result = _resolver.Resolve(saveGame, roundIndex: 0);

            Assert.IsFalse(result.IsApplicable);
        }

        [TestMethod]
        public void Resolve_OneTeamOverLimit_IsApplicable()
        {
            // 14 teams × 2 = 28 drivers — 2 over limit
            var saveGame = BuildBaseline();

            var result = _resolver.Resolve(saveGame, roundIndex: 0);

            Assert.IsTrue(result.IsApplicable);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Round 0 — beginning of season
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Resolve_Round0_BottomTeamsByLastYearStandingsEnterPool()
        {
            // 14 teams, overflow = 2, minimumPoolSize = 4, teamsPoolSize = 2
            // P13 and P14 last year should be in pool
            var saveGame = BuildBaseline();

            var result = _resolver.Resolve(saveGame, roundIndex: 0);

            var poolIds = result.PoolTeams.Select(t => t.TeamId).ToHashSet();
            Assert.IsTrue(poolIds.Contains("team_13"), "P13 last year must pre-qualify");
            Assert.IsTrue(poolIds.Contains("team_14"), "P14 last year must pre-qualify");
            Assert.AreEqual(2, result.PoolTeams.Count);
        }

        [TestMethod]
        public void Resolve_Round0_TopTeamsDoNotEnterPool()
        {
            var saveGame = BuildBaseline();

            var result = _resolver.Resolve(saveGame, roundIndex: 0);

            var poolIds = result.PoolTeams.Select(t => t.TeamId).ToHashSet();
            Assert.IsFalse(poolIds.Contains("team_01"));
            Assert.IsFalse(poolIds.Contains("team_07"));
        }

        [TestMethod]
        public void Resolve_Round0_NewTeamAlwaysEntersPool()
        {
            // team_new is absent from 1989 standings — it is a brand-new entrant
            var returningTeams = Enumerable.Range(1, 13).Select(i => MakeTeam($"team_{i:D2}")).ToList();
            var newTeam = MakeTeam("team_new");
            var allTeams = returningTeams.Append(newTeam).ToList();

            var historical = new List<HistoricalConstructorStanding>
            {
                MakeHistorical(1989, returningTeams.Select((t, i) => (t.TeamId, i + 1)))
            };

            var saveGame = BuildBaseline(teamsOverride: allTeams, historicalOverride: historical);

            var result = _resolver.Resolve(saveGame, roundIndex: 0);

            Assert.IsTrue(
                result.PoolTeams.Any(t => t.TeamId == "team_new"),
                "A brand-new team must always pre-qualify at round 0");
        }

        [TestMethod]
        public void Resolve_Round0_MultipleNewTeams_AllEnterPoolRegardlessOfMinimumSize()
        {
            // 11 returning + 3 new = 14 teams, 28 drivers
            // overflow = 2, teamsPoolSize = 2, but 3 new teams > 2 — pool must expand
            var returningTeams = Enumerable.Range(1, 11).Select(i => MakeTeam($"team_{i:D2}")).ToList();
            var newTeams = new[] { MakeTeam("new_a"), MakeTeam("new_b"), MakeTeam("new_c") }.ToList();
            var allTeams = returningTeams.Concat(newTeams).ToList();

            var historical = new List<HistoricalConstructorStanding>
            {
                MakeHistorical(1989, returningTeams.Select((t, i) => (t.TeamId, i + 1)))
            };

            var saveGame = BuildBaseline(teamsOverride: allTeams, historicalOverride: historical);

            var result = _resolver.Resolve(saveGame, roundIndex: 0);

            var poolIds = result.PoolTeams.Select(t => t.TeamId).ToHashSet();
            Assert.IsTrue(poolIds.Contains("new_a"), "new_a must pre-qualify");
            Assert.IsTrue(poolIds.Contains("new_b"), "new_b must pre-qualify");
            Assert.IsTrue(poolIds.Contains("new_c"), "new_c must pre-qualify");
        }

        [TestMethod]
        public void Resolve_Round0_MultipleNewTeams_PassCountAdaptsToKeepGridAtLimit()
        {
            // 3 new teams force pool to 3 teams (6 drivers)
            // committed = 22 drivers, passCount must = 4 so committed + passCount = 26
            var returningTeams = Enumerable.Range(1, 11).Select(i => MakeTeam($"team_{i:D2}")).ToList();
            var newTeams = new[] { MakeTeam("new_a"), MakeTeam("new_b"), MakeTeam("new_c") }.ToList();
            var allTeams = returningTeams.Concat(newTeams).ToList();

            var historical = new List<HistoricalConstructorStanding>
            {
                MakeHistorical(1989, returningTeams.Select((t, i) => (t.TeamId, i + 1)))
            };

            var saveGame = BuildBaseline(teamsOverride: allTeams, historicalOverride: historical);

            var result = _resolver.Resolve(saveGame, roundIndex: 0);

            int committedDrivers = result.CommittedTeams.Count * 2;
            Assert.AreEqual(26, committedDrivers + result.PassCount,
                "committed drivers + passCount must always equal MaxDriversPerRace");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Rounds 1–7: flags frozen from round 0
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Resolve_FirstHalfRounds_FlagsUnchangedFromRound0()
        {
            var saveGame = BuildBaseline();
            _resolver.Resolve(saveGame, roundIndex: 0);

            var snapshot = saveGame.CurrentSeason.Teams
                .ToDictionary(t => t.TeamId, t => t.DefaultPrequalifying);

            for (int round = 1; round < 8; round++)
                _resolver.Resolve(saveGame, roundIndex: round);

            foreach (var team in saveGame.CurrentSeason.Teams)
                Assert.AreEqual(snapshot[team.TeamId], team.DefaultPrequalifying,
                    $"{team.TeamId}: flag must not change between round 0 and halfway");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Round 8: halfway refresh
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Resolve_HalfwayPoint_PoolRotatesToCurrentWorstTeams()
        {
            // Current standings flipped: team_01 and team_02 are now worst
            var teams = Enumerable.Range(1, 14).Select(i => MakeTeam($"team_{i:D2}")).ToList();

            // Build current standings: team_01 = P13, team_02 = P14
            var currentStandings = teams.Select((t, i) => MakeStanding(t.TeamId,
                t.TeamId == "team_01" ? 13 :
                t.TeamId == "team_02" ? 14 :
                t.TeamId == "team_13" ? 1 :
                t.TeamId == "team_14" ? 2 :
                i + 1)).ToList();

            var historical = new List<HistoricalConstructorStanding>
            {
                MakeHistorical(1989, teams.Select((t, i) => (t.TeamId, i + 1)))
            };

            var saveGame = BuildBaseline(
                teamsOverride: teams,
                currentStandingsOverride: currentStandings,
                historicalOverride: historical);

            _resolver.Resolve(saveGame, roundIndex: 0);
            _resolver.Resolve(saveGame, roundIndex: 8);

            var poolIds = saveGame.CurrentSeason.Teams
                .Where(t => t.DefaultPrequalifying)
                .Select(t => t.TeamId)
                .ToHashSet();

            Assert.IsTrue(poolIds.Contains("team_01"), "team_01 fell to worst — must now pre-qualify");
            Assert.IsTrue(poolIds.Contains("team_02"), "team_02 fell to worst — must now pre-qualify");
            Assert.IsFalse(poolIds.Contains("team_13"), "team_13 improved — must leave pool");
            Assert.IsFalse(poolIds.Contains("team_14"), "team_14 improved — must leave pool");
        }

        [TestMethod]
        public void Resolve_HalfwayPoint_NewTeamWithGoodResultsEscapesPool()
        {
            var returningTeams = Enumerable.Range(1, 13).Select(i => MakeTeam($"team_{i:D2}")).ToList();
            var newTeam = MakeTeam("team_new");
            var allTeams = returningTeams.Append(newTeam).ToList();

            var historical = new List<HistoricalConstructorStanding>
            {
                MakeHistorical(1989, returningTeams.Select((t, i) => (t.TeamId, i + 1)))
            };

            // team_new is P5 — doing well
            var currentStandings = allTeams.Select((t, i) =>
                MakeStanding(t.TeamId, t.TeamId == "team_new" ? 5 : i + 1)).ToList();

            var saveGame = BuildBaseline(
                teamsOverride: allTeams,
                currentStandingsOverride: currentStandings,
                historicalOverride: historical);

            _resolver.Resolve(saveGame, roundIndex: 0);

            Assert.IsTrue(
                saveGame.CurrentSeason.Teams.First(t => t.TeamId == "team_new").DefaultPrequalifying,
                "team_new must be in pool at round 0 as new entrant");

            _resolver.Resolve(saveGame, roundIndex: 8);

            Assert.IsFalse(
                saveGame.CurrentSeason.Teams.First(t => t.TeamId == "team_new").DefaultPrequalifying,
                "team_new scored well — must escape pool at halfway");
        }

        [TestMethod]
        public void Resolve_HalfwayPoint_NewTeamWithPoorResultsRemainsInPool()
        {
            var returningTeams = Enumerable.Range(1, 13).Select(i => MakeTeam($"team_{i:D2}")).ToList();
            var newTeam = MakeTeam("team_new");
            var allTeams = returningTeams.Append(newTeam).ToList();

            var historical = new List<HistoricalConstructorStanding>
            {
                MakeHistorical(1989, returningTeams.Select((t, i) => (t.TeamId, i + 1)))
            };

            // team_new is dead last
            var currentStandings = allTeams.Select((t, i) =>
                MakeStanding(t.TeamId, t.TeamId == "team_new" ? 14 : i + 1)).ToList();

            var saveGame = BuildBaseline(
                teamsOverride: allTeams,
                currentStandingsOverride: currentStandings,
                historicalOverride: historical);

            _resolver.Resolve(saveGame, roundIndex: 0);
            _resolver.Resolve(saveGame, roundIndex: 8);

            Assert.IsTrue(
                saveGame.CurrentSeason.Teams.First(t => t.TeamId == "team_new").DefaultPrequalifying,
                "team_new scored poorly — must remain in pool at halfway");
        }

        [TestMethod]
        public void Resolve_HalfwayPoint_MultipleNewTeamsAllDoingBadly_OnlyBottomRemainInPool()
        {
            var returningTeams = Enumerable.Range(1, 11).Select(i => MakeTeam($"team_{i:D2}")).ToList();
            var newTeams = new[] { MakeTeam("new_a"), MakeTeam("new_b"), MakeTeam("new_c") }.ToList();
            var allTeams = returningTeams.Concat(newTeams).ToList();

            var historical = new List<HistoricalConstructorStanding>
            {
                MakeHistorical(1989, returningTeams.Select((t, i) => (t.TeamId, i + 1)))
            };

            // All three new teams are at the bottom
            var currentStandings = allTeams.Select((t, i) => MakeStanding(t.TeamId,
                t.TeamId == "new_a" ? 12 :
                t.TeamId == "new_b" ? 13 :
                t.TeamId == "new_c" ? 14 : i + 1)).ToList();

            var saveGame = BuildBaseline(
                teamsOverride: allTeams,
                currentStandingsOverride: currentStandings,
                historicalOverride: historical);

            _resolver.Resolve(saveGame, roundIndex: 0);
            _resolver.Resolve(saveGame, roundIndex: 8);

            var poolIds = saveGame.CurrentSeason.Teams
                .Where(t => t.DefaultPrequalifying).Select(t => t.TeamId).ToHashSet();

            Assert.IsTrue(poolIds.Contains("new_b"));
            Assert.IsTrue(poolIds.Contains("new_c"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Rounds 9–15: flags frozen after halfway
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Resolve_SecondHalfRounds_FlagsUnchangedAfterHalfway()
        {
            var saveGame = BuildBaseline();
            _resolver.Resolve(saveGame, roundIndex: 0);
            _resolver.Resolve(saveGame, roundIndex: 8);

            var snapshot = saveGame.CurrentSeason.Teams
                .ToDictionary(t => t.TeamId, t => t.DefaultPrequalifying);

            for (int round = 9; round < 16; round++)
                _resolver.Resolve(saveGame, roundIndex: round);

            foreach (var team in saveGame.CurrentSeason.Teams)
                Assert.AreEqual(snapshot[team.TeamId], team.DefaultPrequalifying,
                    $"{team.TeamId}: flag must not change after halfway");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Pool/committed integrity
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Resolve_PoolAndCommittedAreDisjoint()
        {
            var saveGame = BuildBaseline();
            var result = _resolver.Resolve(saveGame, roundIndex: 0);

            var poolIds = result.PoolTeams.Select(t => t.TeamId).ToHashSet();
            var committedIds = result.CommittedTeams.Select(t => t.TeamId).ToHashSet();

            Assert.AreEqual(0, poolIds.Intersect(committedIds).Count(),
                "A team must not appear in both pool and committed");
        }

        [TestMethod]
        public void Resolve_PoolAndCommittedCoverAllTeams()
        {
            var saveGame = BuildBaseline();
            var result = _resolver.Resolve(saveGame, roundIndex: 0);

            Assert.AreEqual(
                saveGame.CurrentSeason.Teams.Count(),
                result.PoolTeams.Count + result.CommittedTeams.Count,
                "Pool + committed must account for every team");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Custom MaxDriversPerRace
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Resolve_CustomMaxDriversPerRace_PoolSizeAdjusts()
        {
            // 14 teams, MaxDriversPerRace = 24 → overflow = 4, teamsPoolSize = 3
            var saveGame = BuildBaseline(maxDriversPerRace: 24);

            var result = _resolver.Resolve(saveGame, roundIndex: 0);

            Assert.IsTrue(result.IsApplicable);
            Assert.AreEqual(3, result.PoolTeams.Count,
                "3 teams should be in pool when MaxDriversPerRace = 24");
        }

        [TestMethod]
        public void Resolve_NullMaxDriversPerRace_DefaultsTo26()
        {
            var saveGame = BuildBaseline(maxDriversPerRace: null);

            var result = _resolver.Resolve(saveGame, roundIndex: 0);

            Assert.IsTrue(result.IsApplicable);
            Assert.AreEqual(2, result.PoolTeams.Count,
                "Null MaxDriversPerRace must default to 26, giving pool of 2 teams");
        }
    }
}
