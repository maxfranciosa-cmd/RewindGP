using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class HallOfFameCalculatorTests
    {
        [TestMethod]
        public void Build_LegendOnlyInAllTimeRecords_GetsHisPastAndIsHistoric()
        {
            var saveGame = CreateTestSaveGame(1991);
            var allTime = Records(Record("jim_clark", "Jim Clark", new() { [1963] = 7, [1965] = 6 }, 1963, 1965));

            var entry = HallOfFameCalculator.Build(saveGame, allTime).Single();

            Assert.AreEqual("Jim Clark", entry.Name);
            Assert.AreEqual(13, entry.Wins);
            CollectionAssert.AreEqual(new List<int> { 1963, 1965 }, entry.ChampionshipYears);
            Assert.AreEqual(HallOfFameStatus.HISTORIC, entry.Status);
            Assert.IsFalse(entry.IsPlayer);
        }

        [TestMethod]
        public void Build_AllTimeSeasonsFromTheStartYearOn_AreIgnored()
        {
            // the save began in 1991: what really happened in 1991 and after never happened here
            var saveGame = CreateTestSaveGame(1993);
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                new HistoricalDriverStanding { Year = 1991, Standing = new List<HisoricalDriverStandingEntry>() },
                new HistoricalDriverStanding { Year = 1992, Standing = new List<HisoricalDriverStandingEntry>() }
            };
            var allTime = Records(Record("late_bloomer", "Late Bloomer", new() { [1990] = 2, [1991] = 5, [1992] = 9 }, 1990, 1992));

            var entry = HallOfFameCalculator.Build(saveGame, allTime).Single();

            Assert.AreEqual(2, entry.Wins);
            CollectionAssert.AreEqual(new List<int> { 1990 }, entry.ChampionshipYears);
        }

        [TestMethod]
        public void Build_DriverOfTheSave_GetsAllTimePastPlusSaveResults()
        {
            var saveGame = CreateTestSaveGame(1991);
            saveGame.Drivers = new List<IDriverData> { new DriverData { DriverId = "ayrton_senna", Name = "Ayrton Senna" } };
            saveGame.GrandPrixResults = new List<GrandPrixResult> { BuildResult(1991, ("ayrton_senna", "mclaren", 1)) };
            var allTime = Records(Record("ayrton_senna", "Ayrton Senna", new() { [1985] = 2, [1988] = 8, [1990] = 6, [1991] = 7 }, 1988, 1990, 1991));

            var entry = HallOfFameCalculator.Build(saveGame, allTime).Single();

            Assert.AreEqual(17, entry.Wins);
            CollectionAssert.AreEqual(new List<int> { 1988, 1990 }, entry.ChampionshipYears);
            Assert.AreEqual(HallOfFameStatus.ACTIVE, entry.Status);
        }

        [TestMethod]
        public void Build_DriverWhoJoinedFromALaterPack_GetsAllTimePastPlusSaveResults()
        {
            var saveGame = CreateTestSaveGame(1992);
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                new HistoricalDriverStanding
                {
                    Year = 1991,
                    Standing = new List<HisoricalDriverStandingEntry> { new HisoricalDriverStandingEntry { DriverId = "comeback", Position = 1 } }
                }
            };
            saveGame.Drivers = new List<IDriverData> { new DriverData { DriverId = "comeback", Name = "Come Back" } };
            saveGame.GrandPrixResults = new List<GrandPrixResult>
            {
                BuildResult(1991, ("comeback", "T1", 1)),
                BuildResult(1992, ("comeback", "T1", 1))
            };
            var allTime = Records(Record("comeback", "Come Back", new() { [1985] = 4 }, 1985));

            var entry = HallOfFameCalculator.Build(saveGame, allTime).Single();

            Assert.AreEqual(6, entry.Wins);
            CollectionAssert.AreEqual(new List<int> { 1985, 1991 }, entry.ChampionshipYears);
        }

        [TestMethod]
        public void Build_RetiredDriver_KeepsHisTotalsAndIsRetired()
        {
            var saveGame = CreateTestSaveGame(1992);
            saveGame.RetiredDrivers = new List<IDriverData> { new DriverData { DriverId = "old_hand", Name = "Old Hand" } };
            saveGame.GrandPrixResults = new List<GrandPrixResult> { BuildResult(1991, ("old_hand", "T1", 1)) };
            var allTime = Records(Record("old_hand", "Old Hand", new() { [1980] = 12 }));

            var entry = HallOfFameCalculator.Build(saveGame, allTime).Single();

            Assert.AreEqual("Old Hand", entry.Name);
            Assert.AreEqual(13, entry.Wins);
            Assert.AreEqual(HallOfFameStatus.RETIRED, entry.Status);
        }

        [TestMethod]
        public void Build_Player_AppearsOnceHeHasWon_WithHisOwnName()
        {
            var saveGame = CreateTestSaveGame(1991);
            Assert.AreEqual(0, HallOfFameCalculator.Build(saveGame, Records()).Count);

            saveGame.GrandPrixResults = new List<GrandPrixResult> { BuildResult(1991, ("PLAYER", "T1", 1)) };

            var entry = HallOfFameCalculator.Build(saveGame, Records()).Single();

            Assert.AreEqual("Test Player", entry.Name);
            Assert.AreEqual(1, entry.Wins);
            Assert.IsTrue(entry.IsPlayer);
        }

        [TestMethod]
        public void Build_DriversWithNoWinsAndNoTitles_AreLeftOut()
        {
            var saveGame = CreateTestSaveGame(1991);
            saveGame.Drivers = new List<IDriverData> { new DriverData { DriverId = "nobody", Name = "No Body" } };
            var allTime = Records(new AllTimeDriverRecord { DriverId = "nobody", Name = "No Body", PodiumsByYear = new() { [1989] = 3 } });

            Assert.AreEqual(0, HallOfFameCalculator.Build(saveGame, allTime).Count);
        }

        [TestMethod]
        public void Build_NoAllTimeRecords_StillReturnsTheDriversOfTheSave()
        {
            var saveGame = CreateTestSaveGame(1991);
            saveGame.Drivers = new List<IDriverData> { new DriverData { DriverId = "D1", Name = "Driver One" } };
            saveGame.GrandPrixResults = new List<GrandPrixResult> { BuildResult(1991, ("D1", "T1", 1)) };

            var entry = HallOfFameCalculator.Build(saveGame, null).Single();

            Assert.AreEqual("Driver One", entry.Name);
            Assert.AreEqual(1, entry.Wins);
        }

        #region Helper Methods

        private static AllTimeRecords Records(params AllTimeDriverRecord[] drivers) => new AllTimeRecords { UpTo = 2025, Drivers = drivers.ToList() };

        private static AllTimeDriverRecord Record(string id, string name, Dictionary<int, int> winsByYear, params int[] championships)
        {
            return new AllTimeDriverRecord { DriverId = id, Name = name, Nationality = "GBR", WinsByYear = winsByYear, Championships = championships.ToList() };
        }

        private ISaveGame CreateTestSaveGame(int year)
        {
            return new SaveGame
            {
                CurrentSeason = new Season { Year = year, Races = new List<Race>(), Teams = new List<ITeamEntry>(), Absences = new List<Absence>() },
                Drivers = new List<IDriverData>(),
                RetiredDrivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                NextGpIndex = 0,
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player", TeamId = "T1" }
            };
        }

        private GrandPrixResult BuildResult(int year, params (string DriverId, string TeamId, int Position)[] entries)
        {
            return new GrandPrixResult
            {
                Year = year,
                QualifyingResults = new List<SessionResult>(),
                RaceResults = entries.Select(e => new SessionResult
                {
                    DriverId = e.DriverId,
                    TeamId = e.TeamId,
                    Position = e.Position
                }).ToList()
            };
        }

        #endregion
    }
}
