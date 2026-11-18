using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Moq;
using System.Text.Json;

namespace AMS2ChEd.Tests
{
    // Phase 4 of the modern driver market: rookie FP1 outings at the academies' teams, simulated,
    // and the season-end reward of their average score.
    [TestClass]
    public class PracticeOutingsTests
    {
        private const int Year = 2024;
        private static readonly AgeEra Modern = AgeEras.For(Year);
        private const int RaceCount = 10;

        // RBR (parent) runs its outings at race indexes 2 and 6, VCARB (sister) at 4 and 8
        private const int RbrRace = 2;
        private const int VcarbRace = 4;

        #region Which races have outings

        [TestMethod]
        public void OutingRaces_TwoPerTeam_SpacedOut_AndOffsetBetweenTheAcademysTeams()
        {
            var season = Season();

            CollectionAssert.AreEqual(new[] { 2, 6 }, PracticeOutings.OutingRaceIndexes(season, "RBR").ToArray());
            CollectionAssert.AreEqual(new[] { 4, 8 }, PracticeOutings.OutingRaceIndexes(season, "VCARB").ToArray());
        }

        [TestMethod]
        public void OutingRaces_AnyCalendar_PerTeamDistinctRaces_NeverTheFirst()
        {
            foreach (var raceCount in Enumerable.Range(PracticeOutings.MinRaces, 28))
            {
                foreach (var teamCount in new[] { 1, 2, 3, 4 })
                {
                    var teams = Enumerable.Range(0, teamCount).Select(i => $"T{i}").ToList();
                    var season = new Season
                    {
                        Year = Year,
                        Races = Races(raceCount),
                        Academies = new List<Academy> { new() { Id = "a", ParentTeams = teams.Take(1).ToList(), SisterTeams = teams.Skip(1).ToList(), Drivers = new List<string>() } }
                    };

                    foreach (var team in teams)
                    {
                        var races = PracticeOutings.OutingRaceIndexes(season, team);
                        Assert.AreEqual(PracticeOutings.PerTeam, races.Distinct().Count(), $"{raceCount} races, {team}");
                        Assert.IsTrue(races.All(r => r >= 1 && r < raceCount), $"{raceCount} races, {team}");
                    }
                }
            }
        }

        [TestMethod]
        public void OutingRaces_NoneOutsideTheAcademies_InAShortSeason_OrWithoutAcademies()
        {
            Assert.AreEqual(0, PracticeOutings.OutingRaceIndexes(Season(), "OTHER").Count);
            Assert.AreEqual(0, PracticeOutings.OutingRaceIndexes(Season(raceCount: PracticeOutings.MinRaces - 1), "RBR").Count);

            var withoutAcademies = Season();
            withoutAcademies.Academies = null;
            Assert.AreEqual(0, PracticeOutings.OutingRaceIndexes(withoutAcademies, "RBR").Count);
        }

        #endregion

        #region Who runs an outing

        [TestMethod]
        public void Allocate_TieOnOutings_AtTheParentTeam_GoesToItsOwnReserve_InTheSecondCar()
        {
            var saveGame = SaveGame();

            var outing = PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1)).Single();

            Assert.AreEqual("JUN1", outing.DriverId);
            Assert.AreEqual("RBR", outing.TeamId);
            Assert.AreEqual("PER", outing.ReplacedDriverId);
            Assert.AreEqual("VER", outing.BenchmarkDriverId);
            Assert.AreEqual(Year, outing.Year);
            Assert.AreEqual(RaceId(RbrRace), outing.RaceId);
            Assert.IsNull(outing.Score);
            Assert.AreSame(outing, saveGame.PracticeOutings.Single());
        }

        [TestMethod]
        public void Allocate_FewestOutingsFirst_BeatsTheReserve()
        {
            var saveGame = SaveGame();
            saveGame.PracticeOutings = new List<PracticeOuting> { new() { Year = Year, RaceId = RaceId(1), TeamId = "VCARB", DriverId = "JUN1", Score = 1 } };

            var outing = PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1)).Single();

            Assert.AreEqual("JUN2", outing.DriverId);
        }

        [TestMethod]
        public void Allocate_OutingsOfEarlierSeasons_DontCount()
        {
            var saveGame = SaveGame();
            saveGame.PracticeOutings = new List<PracticeOuting> { new() { Year = Year - 1, RaceId = RaceId(1), TeamId = "VCARB", DriverId = "JUN1", Score = 1 } };

            Assert.AreEqual("JUN1", PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1)).Single().DriverId);
        }

        [TestMethod]
        public void Allocate_AMemberRacingAsAStandIn_IsExcluded()
        {
            var saveGame = SaveGame();
            var entryList = EntryList(saveGame);
            entryList.Single(e => e.TeamId == "OTHER").Driver2Id = "JUN1";

            Assert.AreEqual("JUN2", PracticeOutings.Allocate(saveGame, RbrRace, entryList, new Random(1)).Single().DriverId);
        }

        [TestMethod]
        public void Allocate_AMemberStandingInPerTheAbsences_IsExcluded()
        {
            var saveGame = SaveGame();
            saveGame.CurrentSeason.Absences = new List<Absence> { new() { RaceId = RaceId(RbrRace), TeamId = "OTHER", DriverOut = "OT2", DriverIn = "JUN1" } };

            Assert.AreEqual("JUN2", PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1)).Single().DriverId);
        }

        [TestMethod]
        public void Allocate_AMemberOutOfAction_IsExcluded()
        {
            var saveGame = SaveGame();
            saveGame.Casualties = new List<Casualty> { new() { DriverId = "JUN1", Year = Year, Kind = CasualtyKind.INJURED, RaceIdsOut = new List<int> { RaceId(RbrRace) } } };

            Assert.AreEqual("JUN2", PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1)).Single().DriverId);
        }

        [TestMethod]
        public void Allocate_ARaceDriver_AnAgedOutJunior_AndAJuniorWhoSignedOutside_AreExcluded()
        {
            var saveGame = SaveGame();
            // JUN1 holds a race seat at OTHER, JUN2 is 25
            var other = saveGame.CurrentSeason.Teams.Single(t => t.TeamId == "OTHER");
            other.Driver2Contract.DriverId = "JUN1";
            ((DriverData)saveGame.Drivers.Single(d => d.DriverId == "JUN2")).YearOfBirth = Year - 25;

            Assert.AreEqual(0, PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1)).Count);
            Assert.IsNull(saveGame.PracticeOutings);
        }

        [TestMethod]
        public void Allocate_OneDriverGetsAtMostOneOutingPerRace()
        {
            // three races: both academy teams have an outing at race 1, and only JUN1 is free
            var saveGame = SaveGame(raceCount: 3);
            ((DriverData)saveGame.Drivers.Single(d => d.DriverId == "JUN2")).YearOfBirth = Year - 30;
            Assert.IsTrue(PracticeOutings.HasOutingAt(saveGame.CurrentSeason, "RBR", 1) && PracticeOutings.HasOutingAt(saveGame.CurrentSeason, "VCARB", 1));

            var outings = PracticeOutings.Allocate(saveGame, 1, EntryList(saveGame), new Random(1));

            Assert.AreEqual(1, outings.Count);
            Assert.AreEqual("JUN1", outings.Single().DriverId);
        }

        [TestMethod]
        public void Allocate_TheEntryListDrawnUpTwice_AllocatesEachOutingOnce()
        {
            var saveGame = SaveGame();

            PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1));
            var second = PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(2));

            Assert.AreEqual(0, second.Count);
            Assert.AreEqual(1, saveGame.PracticeOutings.Count);
        }

        [TestMethod]
        public void Allocate_NoOutingAtThisRace_OrNoAcademies_ChangesNothing()
        {
            var saveGame = SaveGame();
            Assert.AreEqual(0, PracticeOutings.Allocate(saveGame, 0, EntryList(saveGame), new Random(1)).Count);

            saveGame.CurrentSeason.Academies = null;
            Assert.AreEqual(0, PracticeOutings.AllocateAndSimulate(saveGame, RbrRace, EntryList(saveGame), new Random(1)).Count);
            Assert.IsNull(saveGame.PracticeOutings);
        }

        [TestMethod]
        public void Allocate_ThePlayerInTheAcademy_IsAMember_OutOfItHeIsNot()
        {
            var saveGame = SaveGame();
            // only the player is free: both AI juniors have aged out
            foreach (var id in new[] { "JUN1", "JUN2" })
                ((DriverData)saveGame.Drivers.Single(d => d.DriverId == id)).YearOfBirth = Year - 30;

            Assert.AreEqual(0, PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1)).Count);

            saveGame.PlayerData.AcademyId = "rb";
            Assert.AreEqual("PLAYER", PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1)).Single().DriverId);
        }

        #endregion

        #region The car and the benchmark

        [TestMethod]
        [DataRow(ContractRole.FIRST_DRIVER, ContractRole.SECOND_DRIVER, "B", "A")]
        [DataRow(ContractRole.SECOND_DRIVER, ContractRole.FIRST_DRIVER, "A", "B")]
        public void Car_TheSecondDriversCar_TheFirstDriverIsTheBenchmark(ContractRole role1, ContractRole role2, string replaced, string benchmark)
        {
            var car = PracticeOutings.CarFor(Team("T", TeamReputation.MIDFIELD, ("A", role1), ("B", role2)), Entry("T", "A", "B"), _ => DriverReputation.PRIME_MIDFIELD);

            Assert.AreEqual(new PracticeOutingCar(replaced, benchmark), car);
        }

        [TestMethod]
        public void Car_EqualContracts_TheDriverWhoIsntFirstDriverMaterial_IsReplaced()
        {
            var team = Team("T", TeamReputation.MIDFIELD, ("A", ContractRole.EQUAL), ("B", ContractRole.EQUAL));
            Func<string, DriverReputation> reputations = id => id == "B" ? DriverReputation.PRIME_CHAMPIONSHIP_LEVEL : DriverReputation.PAY_DRIVER_SEASON;

            Assert.AreEqual(new PracticeOutingCar("A", "B"), PracticeOutings.CarFor(team, Entry("T", "A", "B"), reputations));
        }

        [TestMethod]
        [DataRow(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL)]
        [DataRow(DriverReputation.PAY_DRIVER_SEASON)]
        public void Car_EqualContracts_BothOrNeitherFirstDriverMaterial_Driver2sCarIsReplaced(DriverReputation both)
        {
            var team = Team("T", TeamReputation.MIDFIELD, ("A", ContractRole.EQUAL), ("B", ContractRole.EQUAL));

            Assert.AreEqual(new PracticeOutingCar("B", "A"), PracticeOutings.CarFor(team, Entry("T", "A", "B"), _ => both));
        }

        [TestMethod]
        public void Car_AStandInInTheSecondCar_IsReplaced_AStandInInTheFirstIsTheBenchmark()
        {
            var team = Team("T", TeamReputation.MIDFIELD, ("A", ContractRole.FIRST_DRIVER), ("B", ContractRole.SECOND_DRIVER));

            Assert.AreEqual(new PracticeOutingCar("SUB", "A"), PracticeOutings.CarFor(team, Entry("T", "A", "SUB"), _ => DriverReputation.PRIME_MIDFIELD));
            Assert.AreEqual(new PracticeOutingCar("B", "SUB"), PracticeOutings.CarFor(team, Entry("T", "SUB", "B"), _ => DriverReputation.PRIME_MIDFIELD));
        }

        [TestMethod]
        public void Car_AOneCarTeam_HasNoOuting()
        {
            var team = Team("T", TeamReputation.MIDFIELD, ("A", ContractRole.FIRST_DRIVER), ("", ContractRole.SECOND_DRIVER));

            Assert.IsNull(PracticeOutings.CarFor(team, Entry("T", "A", ""), _ => DriverReputation.PRIME_MIDFIELD));
        }

        #endregion

        #region Running and scoring

        [TestMethod]
        public void Outcome_AheadWithinReachFurtherBack()
        {
            var classification = new[] { "X1", "BENCH", "X2", "X3", "X4", "X5" };

            Assert.AreEqual(PracticeOutcome.Ahead, PracticeOutings.Outcome(new[] { "JUN", "BENCH" }, "JUN", "BENCH"));
            Assert.AreEqual(PracticeOutcome.WithinReach, PracticeOutings.Outcome(classification, "X4", "BENCH"));
            Assert.AreEqual(PracticeOutcome.FurtherBack, PracticeOutings.Outcome(classification, "X5", "BENCH"));
            Assert.AreEqual(PracticeOutcome.FurtherBack, PracticeOutings.Outcome(classification, "MISSING", "BENCH"));
            Assert.AreEqual(PracticeOutcome.Ahead, PracticeOutings.Outcome(classification, "X5", "MISSING"));
        }

        [TestMethod]
        [DataRow(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 3, 2, 0)]
        [DataRow(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, 3, 2, 0)]
        [DataRow(DriverReputation.PRIME_STRONG_MIDFIELD, 2, 1, 0)]
        [DataRow(DriverReputation.PRIME_MIDFIELD, 1, 0, 0)]
        [DataRow(DriverReputation.PAY_DRIVER_SEASON, 1, 0, 0)]
        public void Score_FollowsTheTable(DriverReputation benchmark, int ahead, int within, int back)
        {
            Assert.AreEqual(ahead, PracticeOutings.Score(PracticeOutcome.Ahead, benchmark));
            Assert.AreEqual(within, PracticeOutings.Score(PracticeOutcome.WithinReach, benchmark));
            Assert.AreEqual(back, PracticeOutings.Score(PracticeOutcome.FurtherBack, benchmark));
        }

        [TestMethod]
        public void AllocateAndSimulate_ScoresEveryOuting_AndLeavesTheRaceWeekendAlone()
        {
            var saveGame = SaveGame();
            var entryList = EntryList(saveGame);

            var results = PracticeOutings.AllocateAndSimulate(saveGame, RbrRace, entryList, new Random(1));

            var outing = results.Single().Outing;
            Assert.AreEqual(PracticeOutings.Score(results.Single().Outcome.Value, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), outing.Score);
            Assert.AreEqual(0, saveGame.GrandPrixResults.Count());
            Assert.AreEqual("PER", entryList.Single(e => e.TeamId == "RBR").Driver2Id);
        }

        [TestMethod]
        public void PracticeField_PutsEachOutingDriverInTheCarHeTakes()
        {
            var saveGame = SaveGame();
            var field = PracticeOutings.PracticeField(EntryList(saveGame), new[] { new PracticeOuting { TeamId = "RBR", DriverId = "JUN1", ReplacedDriverId = "PER" } });

            var rbr = field.Single(e => e.TeamId == "RBR");
            Assert.AreEqual("VER", rbr.Driver1Id);
            Assert.AreEqual("JUN1", rbr.Driver2Id);
        }

        #endregion

        #region Driven FP1 (phase 5)

        // the player alone in the academy, Red Bull's outing at race index 2 goes to him
        private static SaveGame PlayerOutingSave()
        {
            var saveGame = SaveGame();
            saveGame.PlayerData.AcademyId = "rb";
            foreach (var id in new[] { "JUN1", "JUN2" })
                ((DriverData)saveGame.Drivers.Single(d => d.DriverId == id)).YearOfBirth = Year - 30;
            return saveGame;
        }

        [TestMethod]
        public void AllocateAndSimulate_ThePlayersOuting_WaitsForHisChoice()
        {
            var saveGame = PlayerOutingSave();

            var result = PracticeOutings.AllocateAndSimulate(saveGame, RbrRace, EntryList(saveGame), new Random(1)).Single();

            Assert.AreEqual("PLAYER", result.Outing.DriverId);
            Assert.IsNull(result.Outcome);
            Assert.IsNull(result.Outing.Score);
            Assert.AreSame(result.Outing, PracticeOutings.PendingPlayerOuting(saveGame, RaceId(RbrRace)));
        }

        [TestMethod]
        public void AllocateAndSimulate_AiOutingsAreScored_ThePlayersNot()
        {
            // JUN1 is back in the academy: three races, both teams have an outing at race 1
            var threeRaces = PlayerOutingSave();
            threeRaces.CurrentSeason.Races = Races(3);
            ((DriverData)threeRaces.Drivers.Single(d => d.DriverId == "JUN1")).YearOfBirth = 2002;

            var results = PracticeOutings.AllocateAndSimulate(threeRaces, 1, EntryList(threeRaces), new Random(1));

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.Single(r => r.Outing.DriverId == "PLAYER").Outcome == null);
            Assert.IsNotNull(results.Single(r => r.Outing.DriverId == "JUN1").Outing.Score);
        }

        [TestMethod]
        public void Simulate_ScoresThePlayersOuting_AsInPhase4()
        {
            var saveGame = PlayerOutingSave();
            PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1));

            var result = PracticeOutings.SimulatePlayerOuting(saveGame, RaceId(RbrRace), EntryList(saveGame));

            Assert.IsNotNull(result);
            Assert.AreEqual(PracticeOutings.Score(result.Value.Outcome.Value, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL), result.Value.Outing.Score);
            Assert.IsNull(PracticeOutings.PendingPlayerOuting(saveGame, RaceId(RbrRace)));
            Assert.IsNull(PracticeOutings.SimulatePlayerOuting(saveGame, RaceId(RbrRace), EntryList(saveGame)));
        }

        [TestMethod]
        public void CancelPendingPlayerOuting_DropsIt_OnlyThePlayers()
        {
            var saveGame = PlayerOutingSave();
            PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1));

            Assert.IsTrue(PracticeOutings.CancelPendingPlayerOuting(saveGame, RaceId(RbrRace)));
            Assert.AreEqual(0, saveGame.PracticeOutings.Count);
            Assert.IsFalse(PracticeOutings.CancelPendingPlayerOuting(saveGame, RaceId(RbrRace)));
        }

        [TestMethod]
        public void PracticeGrid_PutsThePlayerInTheReplacedDriversCar_AndLeavesTheSavesEntryListAlone()
        {
            var saveGame = PlayerOutingSave();
            saveGame.NextGpEntryList = EntryList(saveGame);
            var outing = PracticeOutings.Allocate(saveGame, RbrRace, saveGame.NextGpEntryList, new Random(1)).Single();

            var grid = PracticeOutings.PracticeGrid(saveGame.NextGpEntryList, outing);

            var rbr = grid.Single(e => e.TeamId == "RBR");
            Assert.AreEqual("VER", rbr.Driver1Id);
            Assert.AreEqual("PLAYER", rbr.Driver2Id);
            Assert.IsFalse(grid.SelectMany(e => new[] { e.Driver1Id, e.Driver2Id }).Contains("PER"));
            Assert.AreEqual("PER", saveGame.NextGpEntryList.Single(e => e.TeamId == "RBR").Driver2Id);
            Assert.AreEqual(saveGame.NextGpEntryList.Count(), grid.Count);
        }

        [TestMethod]
        public void ClassificationByBestLap_FastestFirst_CarsWithoutATimeLast()
        {
            var standings = new[]
            {
                new ParticipantData { DriverId = "SLOW", Position = 1, BestLapTime = "1:31.500" },
                new ParticipantData { DriverId = "NOTIME", Position = 2, BestLapTime = "--:--.---" },
                new ParticipantData { DriverId = "FAST", Position = 3, BestLapTime = "1:29.900" },
                new ParticipantData { DriverId = "MID", Position = 4, BestLapTime = "1:30.050" },
                new ParticipantData { DriverId = "EMPTY", Position = 5, BestLapTime = null }
            };

            CollectionAssert.AreEqual(new[] { "FAST", "MID", "SLOW", "NOTIME", "EMPTY" }, PracticeOutings.ClassificationByBestLap(standings));
        }

        [TestMethod]
        [DataRow("1:29.000", "1:30.000", PracticeOutcome.Ahead, 3)]
        [DataRow("1:31.000", "1:30.000", PracticeOutcome.WithinReach, 2)]
        [DataRow("1:40.000", "1:30.000", PracticeOutcome.FurtherBack, 0)]
        [DataRow("--:--.---", "1:30.000", PracticeOutcome.FurtherBack, 0)]
        public void ScoreDriven_ASessionsClassification_GivesTheOutcomeAndScore(string playerLap, string benchmarkLap, PracticeOutcome outcome, int score)
        {
            var saveGame = PlayerOutingSave();
            var outing = PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1)).Single();
            // VER the benchmark (championship level); the other cars between 1:30.1 and 1:39
            var standings = new List<ParticipantData>
            {
                new() { DriverId = "PLAYER", BestLapTime = playerLap },
                new() { DriverId = "VER", BestLapTime = benchmarkLap },
                new() { DriverId = "TSU", BestLapTime = "1:30.500" },
                new() { DriverId = "RIC", BestLapTime = "1:32.000" },
                new() { DriverId = "OT1", BestLapTime = "1:35.000" },
                new() { DriverId = "OT2", BestLapTime = "1:39.000" }
            };

            var result = PracticeOutings.ScoreDriven(saveGame, outing, standings);

            Assert.AreEqual(outcome, result.Outcome);
            Assert.AreEqual(score, outing.Score);
            Assert.IsNull(PracticeOutings.PendingPlayerOuting(saveGame, RaceId(RbrRace)));
        }

        [TestMethod]
        public void PaddockOpportunities_ReportThePendingOuting_AndHaveChoicesWithoutAPaidRide()
        {
            var saveGame = PlayerOutingSave();
            saveGame.CurrentSeason.Year = 2024;
            Assert.IsFalse(PaddockOpportunities.For(saveGame, RbrRace).HasChoices);

            var outing = PracticeOutings.Allocate(saveGame, RbrRace, EntryList(saveGame), new Random(1)).Single();
            var opportunities = PaddockOpportunities.For(saveGame, RbrRace);

            Assert.AreSame(outing, opportunities.PracticeOuting);
            Assert.AreEqual(0, opportunities.PaidRideOffers.Count);
            Assert.IsTrue(opportunities.HasChoices);

            // once run, nothing is left to choose
            PracticeOutings.SimulatePlayerOuting(saveGame, RaceId(RbrRace), EntryList(saveGame));
            Assert.IsFalse(PaddockOpportunities.For(saveGame, RbrRace).HasChoices);
        }

        #endregion

        #region End-of-season reward

        [TestMethod]
        [DataRow(3.0, PracticeReward.YoungChampionshipLevelUnproven)]
        [DataRow(2.0, PracticeReward.YoungChampionshipLevelUnproven)]
        [DataRow(1.5, PracticeReward.YoungTalent)]
        [DataRow(1.0, PracticeReward.YoungTalent)]
        [DataRow(0.75, PracticeReward.NoDecay)]
        [DataRow(0.5, PracticeReward.NoDecay)]
        [DataRow(0.25, PracticeReward.None)]
        public void RewardFor_TheAverageScore(double average, PracticeReward expected)
        {
            Assert.AreEqual(expected, PracticeOutings.RewardFor(average));
        }

        [TestMethod]
        public void Raise_OnlyEverRaises_AndKeepsTheUsualResultOnATie()
        {
            var updater = new ReputationUpdater();

            // a young driver: the reward's tier
            Assert.AreEqual(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, PracticeOutings.Raise(DriverReputation.PAY_DRIVER_SEASON, PracticeReward.YoungChampionshipLevelUnproven, DriverReputation.YOUNG_TALENT, 21, Year, updater));
            // never lower than the usual result
            Assert.AreEqual(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, PracticeOutings.Raise(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, PracticeReward.YoungTalent, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, 21, Year, updater));
            // a tie on ReputationScore: PRIME_MIDFIELD and YOUNG_TALENT are both 3, the usual one stands
            Assert.AreEqual(DriverReputation.PRIME_MIDFIELD, PracticeOutings.Raise(DriverReputation.PRIME_MIDFIELD, PracticeReward.YoungTalent, DriverReputation.PAY_DRIVER_SEASON, 21, Year, updater));
            // no reward: the usual result
            Assert.AreEqual(DriverReputation.PAY_DRIVER_SEASON, PracticeOutings.Raise(DriverReputation.PAY_DRIVER_SEASON, PracticeReward.None, DriverReputation.YOUNG_TALENT, 21, Year, updater));
            // no decay
            Assert.AreEqual(DriverReputation.YOUNG_TALENT, PracticeOutings.Raise(DriverReputation.PAY_DRIVER_SEASON, PracticeReward.NoDecay, DriverReputation.YOUNG_TALENT, 21, Year, updater));
        }

        [TestMethod]
        public void Raise_TheYoungTiersNeedADriverStillYoungNextSeason_OtherwiseOnlyNoDecay()
        {
            var updater = new ReputationUpdater();
            var usual = updater.GetNewReputationForInactiveDriver(DriverReputation.YOUNG_TALENT, Modern.YoungDriverAge, decays: true, Year);
            var noDecay = updater.GetNewReputationForInactiveDriver(DriverReputation.YOUNG_TALENT, Modern.YoungDriverAge, decays: false, Year);

            var raised = PracticeOutings.Raise(usual, PracticeReward.YoungChampionshipLevelUnproven, DriverReputation.YOUNG_TALENT, Modern.YoungDriverAge, Year, updater);

            Assert.AreNotEqual(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, raised);
            Assert.AreEqual(noDecay, raised);
        }

        [TestMethod]
        public void SeasonEnd_AnInactiveJuniorsAverage_RaisesHisReputation()
        {
            var saveGame = SaveGame();
            // JUN2 is no reserve: without FP1 a YOUNG_TALENT decays to PAY_DRIVER_SEASON
            saveGame.PracticeOutings = Outings("JUN2", 3, 2);

            EndOfSeasonManager().UpdateDriversPoolForNextSeason(Year + 1, saveGame, new Dictionary<string, IDriverData>());

            Assert.AreEqual(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, Reputation(saveGame, "JUN2"));
        }

        [TestMethod]
        public void SeasonEnd_ALowAverage_LeavesTheUsualInactivePath()
        {
            var saveGame = SaveGame();
            saveGame.PracticeOutings = Outings("JUN2", 0, 0);

            EndOfSeasonManager().UpdateDriversPoolForNextSeason(Year + 1, saveGame, new Dictionary<string, IDriverData>());

            Assert.AreEqual(DriverReputation.PAY_DRIVER_SEASON, Reputation(saveGame, "JUN2"));
        }

        [TestMethod]
        public void SeasonEnd_OutingsOfAnotherSeason_DontCount()
        {
            var saveGame = SaveGame();
            saveGame.PracticeOutings = Outings("JUN2", 3, 3);
            saveGame.PracticeOutings.ForEach(o => o.Year = Year - 1);

            EndOfSeasonManager().UpdateDriversPoolForNextSeason(Year + 1, saveGame, new Dictionary<string, IDriverData>());

            Assert.AreEqual(DriverReputation.PAY_DRIVER_SEASON, Reputation(saveGame, "JUN2"));
        }

        [TestMethod]
        public void SeasonEnd_AnActiveDriver_IgnoresHisOutings()
        {
            var withOutings = ActiveJuniorSave();
            withOutings.PracticeOutings = Outings("JUN2", 3, 3);
            var without = ActiveJuniorSave();

            var manager = EndOfSeasonManager();
            var expected = manager.ProjectNewReputations(without, Year + 1)["JUN2"];

            Assert.AreEqual(expected, manager.ProjectNewReputations(withOutings, Year + 1)["JUN2"]);
            Assert.AreNotEqual(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, expected);
        }

        [TestMethod]
        public void ProjectedReputations_MidSeason_CountTheOutingsScoredSoFar()
        {
            var saveGame = SaveGame();
            saveGame.PracticeOutings = Outings("JUN2", 3);
            // the next one is allocated but not run yet: it doesn't count
            saveGame.PracticeOutings.Add(new PracticeOuting { Year = Year, RaceId = RaceId(6), TeamId = "RBR", DriverId = "JUN2", Score = null });

            var projected = EndOfSeasonManager().ProjectNewReputations(saveGame, Year + 1);

            Assert.AreEqual(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, projected["JUN2"]);
            Assert.AreEqual(DriverReputation.PAY_DRIVER_SEASON, EndOfSeasonManager().ProjectNewReputations(SaveGame(), Year + 1)["JUN2"]);
        }

        #endregion

        #region Storage

        [TestMethod]
        public void Json_ASaveWithoutPracticeOutings_LoadsAsNull_AndWritesNoKey()
        {
            var saveGame = SaveGame();

            var json = JsonSerializer.Serialize<ISaveGame>(saveGame, DefaultJsonSerializerOptions.Instance);
            var loaded = JsonSerializer.Deserialize<ISaveGame>(json, DefaultJsonSerializerOptions.Instance);

            Assert.IsFalse(json.Contains("practice_outings"));
            Assert.IsNull(loaded.PracticeOutings);
        }

        [TestMethod]
        public void Json_PracticeOutings_RoundTrip_InTheDesignsShape()
        {
            var saveGame = SaveGame();
            saveGame.PracticeOutings = new List<PracticeOuting>
            {
                new() { Year = Year, RaceId = 7, TeamId = "RBR", DriverId = "JUN1", ReplacedDriverId = "PER", BenchmarkDriverId = "VER", Score = 2 },
                new() { Year = Year, RaceId = 9, TeamId = "VCARB", DriverId = "JUN2", ReplacedDriverId = "RIC", BenchmarkDriverId = "TSU" }
            };

            var json = JsonSerializer.Serialize<ISaveGame>(saveGame, DefaultJsonSerializerOptions.Instance);
            var loaded = JsonSerializer.Deserialize<ISaveGame>(json, DefaultJsonSerializerOptions.Instance);

            foreach (var key in new[] { "\"practice_outings\"", "\"race_id\"", "\"team_id\"", "\"replaced_driver_id\"", "\"benchmark_driver_id\"", "\"score\"" })
                StringAssert.Contains(json, key);
            Assert.AreEqual(2, loaded.PracticeOutings.Count);
            Assert.AreEqual(2, loaded.PracticeOutings[0].Score);
            Assert.AreEqual("VER", loaded.PracticeOutings[0].BenchmarkDriverId);
            Assert.IsNull(loaded.PracticeOutings[1].Score);
        }

        #endregion

        #region Helpers

        private static int RaceId(int raceIndex) => 100 + raceIndex;

        private static List<Race> Races(int count) =>
            Enumerable.Range(0, count).Select(i => new Race { RaceId = RaceId(i), RaceName = $"Race {i}", RaceDate = "2024-03-01" }).ToList();

        private static Season Season(int raceCount = RaceCount) => new()
        {
            Year = Year,
            Races = Races(raceCount),
            Absences = new List<Absence>(),
            Academies = new List<Academy>
            {
                new() { Id = "rb", ParentTeams = new List<string> { "RBR" }, SisterTeams = new List<string> { "VCARB" }, Drivers = new List<string> { "JUN1", "JUN2" } }
            },
            Teams = new List<ITeamEntry>
            {
                Team("RBR", TeamReputation.TOP_TEAM, ("VER", ContractRole.FIRST_DRIVER), ("PER", ContractRole.SECOND_DRIVER), reserve: "JUN1"),
                Team("VCARB", TeamReputation.MIDFIELD, ("TSU", ContractRole.EQUAL), ("RIC", ContractRole.EQUAL)),
                Team("OTHER", TeamReputation.MIDFIELD, ("OT1", ContractRole.FIRST_DRIVER), ("OT2", ContractRole.SECOND_DRIVER))
            }
        };

        // a 2024 season before its first race; JUN1 is Red Bull's reserve, JUN2 seatless
        private static SaveGame SaveGame(int raceCount = RaceCount) => new()
        {
            CurrentSeason = Season(raceCount),
            Drivers = new List<IDriverData>
            {
                Driver("VER", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 1997),
                Driver("PER", DriverReputation.AGEING_STRONG_MIDFIELD, 1990),
                Driver("TSU", DriverReputation.PRIME_STRONG_MIDFIELD, 2000),
                Driver("RIC", DriverReputation.AGEING_MIDFIELD, 1989),
                Driver("OT1", DriverReputation.PRIME_MIDFIELD, 1995),
                Driver("OT2", DriverReputation.PRIME_MIDFIELD, 1995),
                Driver("JUN1", DriverReputation.YOUNG_TALENT, 2002),
                Driver("JUN2", DriverReputation.YOUNG_TALENT, 2005),
                Driver("PLAYER", DriverReputation.YOUNG_TALENT, 2004)
            },
            PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Player" },
            RetiredDrivers = new List<IDriverData>(),
            GrandPrixResults = new List<GrandPrixResult>(),
            CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
            HistoricalDriverStandings = new List<HistoricalDriverStanding>()
        };

        // JUN2 stood in at OTHER for two races
        private static SaveGame ActiveJuniorSave()
        {
            var saveGame = SaveGame();
            saveGame.GrandPrixResults = Enumerable.Range(0, 2).Select(i => new GrandPrixResult
            {
                Year = Year,
                RaceId = RaceId(i),
                RaceResults = new List<SessionResult>
                {
                    new() { DriverId = "OT1", TeamId = "OTHER", Position = 9 },
                    new() { DriverId = "JUN2", TeamId = "OTHER", Position = 12 }
                }
            }).ToList();
            saveGame.CurrentDriverStandings = new List<HistoricalDriverStandingEntry>
            {
                new() { DriverId = "OT1", TeamId = "OTHER", Position = 12, PositionsTally = new PositionsTally() },
                new() { DriverId = "JUN2", TeamId = "OTHER", Position = 18, PositionsTally = new PositionsTally() }
            };
            return saveGame;
        }

        private static List<PracticeOuting> Outings(string driverId, params int[] scores) =>
            scores.Select((s, i) => new PracticeOuting { Year = Year, RaceId = RaceId(1 + i), TeamId = "VCARB", DriverId = driverId, ReplacedDriverId = "RIC", BenchmarkDriverId = "TSU", Score = s }).ToList();

        private static List<EntryListEntry> EntryList(ISaveGame saveGame) =>
            saveGame.CurrentSeason.Teams.Select(t => Entry(t.TeamId, t.Driver1Contract.DriverId, t.Driver2Contract.DriverId)).ToList();

        private static EntryListEntry Entry(string teamId, string driver1, string driver2) => new() { TeamId = teamId, Driver1Id = driver1, Driver2Id = driver2 };

        private static TeamEntry Team(string teamId, TeamReputation reputation, (string Id, ContractRole Role) driver1, (string Id, ContractRole Role) driver2, string reserve = null) => new()
        {
            TeamId = teamId,
            TeamName = teamId,
            Reputation = reputation,
            Driver1Contract = new DriverContract { DriverId = driver1.Id, Role = driver1.Role, Races = RaceCount },
            Driver2Contract = new DriverContract { DriverId = driver2.Id, Role = driver2.Role, Races = RaceCount },
            ReserveContract = reserve == null ? null : new DriverContract { DriverId = reserve, Races = RaceCount }
        };

        private static DriverData Driver(string id, DriverReputation reputation, int yearOfBirth) => new()
        {
            DriverId = id,
            Name = id,
            Reputation = reputation,
            YearOfBirth = yearOfBirth
        };

        private static DriverReputation Reputation(ISaveGame saveGame, string driverId) =>
            saveGame.Drivers.Single(d => d.DriverId == driverId).Reputation;

        private static EndOfSeasonManager EndOfSeasonManager()
        {
            var driverHirer = new DriverHirer();
            return new EndOfSeasonManager(new ReputationUpdater(), new OffSeasonMovements(new DriverFirer(), driverHirer), new Mock<IRandomDriverGenerator>().Object, driverHirer);
        }

        #endregion
    }
}
