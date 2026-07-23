using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class AccoladesCalculatorTests
    {
        #region GetDriverAccolades Tests

        [TestMethod]
        public void GetDriverAccolades_NoBaseline_CountsOnlySaveHistory_AndFlagsHasBaselineFalse()
        {
            var saveGame = CreateTestSaveGame(2024);
            saveGame.GrandPrixResults = new List<GrandPrixResult>
            {
                BuildResult(("D1", "T1", 1)),
                BuildResult(("D1", "T1", 2)),
                BuildResult(("D1", "T1", 5)),
            };

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, "D1");

            Assert.IsFalse(accolades.HasBaseline);
            Assert.AreEqual(1, accolades.Wins);
            Assert.AreEqual(2, accolades.Podiums);
        }

        [TestMethod]
        public void GetDriverAccolades_WithBaseline_AddsBaselineToSaveHistoryCounts_AndFlagsHasBaselineTrue()
        {
            var saveGame = CreateTestSaveGame(2024);
            saveGame.AccoladesAtStart = new HistoricalAccolades
            {
                DriverAccolades = new Dictionary<string, Accolades>
                {
                    ["D1"] = new Accolades { Wins = 10, Podiums = 20, PolePositions = 5, Championships = new List<int> { 2020 } }
                }
            };
            saveGame.GrandPrixResults = new List<GrandPrixResult> { BuildResult(("D1", "T1", 1)) };

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, "D1");

            Assert.IsTrue(accolades.HasBaseline);
            Assert.AreEqual(11, accolades.Wins);
            Assert.AreEqual(21, accolades.Podiums);
            CollectionAssert.AreEqual(new List<int> { 2020 }, accolades.ChampionshipYears);
        }

        [TestMethod]
        public void GetDriverAccolades_BaselineEntryAllZero_StillFlagsHasBaselineTrue()
        {
            var saveGame = CreateTestSaveGame(2024);
            saveGame.AccoladesAtStart = new HistoricalAccolades
            {
                DriverAccolades = new Dictionary<string, Accolades> { ["D1"] = new Accolades() }
            };

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, "D1");

            Assert.IsTrue(accolades.HasBaseline);
            Assert.AreEqual(0, accolades.Wins);
        }

        [TestMethod]
        public void GetDriverAccolades_JustClinchedChampionshipYear_AddedWhenNotAlreadyPresent()
        {
            var saveGame = CreateTestSaveGame(2024);
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                new HistoricalDriverStanding
                {
                    Year = 2023,
                    Standing = new List<HisoricalDriverStandingEntry> { new HisoricalDriverStandingEntry { DriverId = "D1", Position = 1 } }
                }
            };

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, "D1", justClinchedChampionshipYear: 2024);

            CollectionAssert.AreEqual(new List<int> { 2023, 2024 }, accolades.ChampionshipYears);
            Assert.AreEqual(2, accolades.Championships);
        }

        [TestMethod]
        public void GetDriverAccolades_JustClinchedChampionshipYear_NotDoubleCountedWhenAlreadyPresent()
        {
            var saveGame = CreateTestSaveGame(2024);
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                new HistoricalDriverStanding
                {
                    Year = 2024,
                    Standing = new List<HisoricalDriverStandingEntry> { new HisoricalDriverStandingEntry { DriverId = "D1", Position = 1 } }
                }
            };

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, "D1", justClinchedChampionshipYear: 2024);

            Assert.AreEqual(1, accolades.Championships);
        }

        #endregion

        #region GetTeamAccolades Tests

        [TestMethod]
        public void GetTeamAccolades_NoBaseline_CountsOnlySaveHistory_AndFlagsHasBaselineFalse()
        {
            var saveGame = CreateTestSaveGame(2024);
            saveGame.GrandPrixResults = new List<GrandPrixResult>
            {
                BuildResult(("D1", "T1", 1)),
                BuildResult(("D2", "T1", 3)),
            };

            var accolades = AccoladesCalculator.GetTeamAccolades(saveGame, "T1");

            Assert.IsFalse(accolades.HasBaseline);
            Assert.AreEqual(1, accolades.Wins);
            Assert.AreEqual(2, accolades.Podiums);
        }

        [TestMethod]
        public void GetTeamAccolades_WithBaseline_AddsBaselineToSaveHistoryCounts()
        {
            var saveGame = CreateTestSaveGame(2024);
            saveGame.AccoladesAtStart = new HistoricalAccolades
            {
                TeamsAccolades = new Dictionary<string, Accolades>
                {
                    ["T1"] = new Accolades { Wins = 3 }
                }
            };
            saveGame.GrandPrixResults = new List<GrandPrixResult> { BuildResult(("D1", "T1", 1)) };

            var accolades = AccoladesCalculator.GetTeamAccolades(saveGame, "T1");

            Assert.IsTrue(accolades.HasBaseline);
            Assert.AreEqual(4, accolades.Wins);
        }

        #endregion

        #region GetDriverWinStreak Tests

        [TestMethod]
        public void GetDriverWinStreak_ConsecutiveWinsAtTheEnd_CountsAllOfThem()
        {
            var saveGame = CreateTestSaveGame(2024);
            saveGame.GrandPrixResults = new List<GrandPrixResult>
            {
                BuildResult(("D1", "T1", 3)),
                BuildResult(("D1", "T1", 1)),
                BuildResult(("D1", "T1", 1)),
                BuildResult(("D1", "T1", 1)),
            };

            Assert.AreEqual(3, AccoladesCalculator.GetDriverWinStreak(saveGame, "D1"));
        }

        [TestMethod]
        public void GetDriverWinStreak_LossInTheMiddle_BreaksTheStreak()
        {
            var saveGame = CreateTestSaveGame(2024);
            saveGame.GrandPrixResults = new List<GrandPrixResult>
            {
                BuildResult(("D1", "T1", 1)),
                BuildResult(("D1", "T1", 2)),
                BuildResult(("D1", "T1", 1)),
            };

            Assert.AreEqual(1, AccoladesCalculator.GetDriverWinStreak(saveGame, "D1"));
        }

        [TestMethod]
        public void GetDriverWinStreak_NonParticipation_BreaksTheStreak()
        {
            var saveGame = CreateTestSaveGame(2024);
            saveGame.GrandPrixResults = new List<GrandPrixResult>
            {
                BuildResult(("D1", "T1", 1)),
                BuildResult(("D2", "T1", 1)), // D1 didn't enter this one
                BuildResult(("D1", "T1", 1)),
            };

            Assert.AreEqual(1, AccoladesCalculator.GetDriverWinStreak(saveGame, "D1"));
        }

        [TestMethod]
        public void GetDriverWinStreak_SpansSeasonBoundary_StillCountsContinuously()
        {
            var saveGame = CreateTestSaveGame(2025);
            saveGame.GrandPrixResults = new List<GrandPrixResult>
            {
                BuildResult(2024, ("D1", "T1", 1)),
                BuildResult(2025, ("D1", "T1", 1)),
            };

            Assert.AreEqual(2, AccoladesCalculator.GetDriverWinStreak(saveGame, "D1"));
        }

        #endregion

        #region GetStartYear Tests

        [TestMethod]
        public void GetStartYear_NoHistoricalStandings_ReturnsCurrentSeasonYear()
        {
            var saveGame = CreateTestSaveGame(2024);

            Assert.AreEqual(2024, AccoladesCalculator.GetStartYear(saveGame));
        }

        [TestMethod]
        public void GetStartYear_WithHistoricalStandings_ReturnsEarliestYear()
        {
            var saveGame = CreateTestSaveGame(2026);
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                new HistoricalDriverStanding { Year = 2025, Standing = new List<HisoricalDriverStandingEntry>() },
                new HistoricalDriverStanding { Year = 2024, Standing = new List<HisoricalDriverStandingEntry>() },
            };

            Assert.AreEqual(2024, AccoladesCalculator.GetStartYear(saveGame));
        }

        #endregion

        #region Helper Methods

        private ISaveGame CreateTestSaveGame(int year)
        {
            return new SaveGame
            {
                CurrentSeason = new Season { Year = year, Races = new List<Race>(), Teams = new List<ITeamEntry>(), Absences = new List<Absence>() },
                Drivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                NextGpIndex = 0,
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player", TeamId = "T1" }
            };
        }

        private GrandPrixResult BuildResult(params (string DriverId, string TeamId, int Position)[] entries) => BuildResult(0, entries);

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
