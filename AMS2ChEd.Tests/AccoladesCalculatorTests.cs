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
        public void GetDriverAccolades_NoAllTimeRecords_CountsOnlySaveHistory_AndFlagsHasBaselineFalse()
        {
            var saveGame = CreateTestSaveGame(2024);
            saveGame.GrandPrixResults = new List<GrandPrixResult>
            {
                BuildResult(("D1", "T1", 1)),
                BuildResult(("D1", "T1", 2)),
                BuildResult(("D1", "T1", 5)),
            };

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, null, "D1");

            Assert.IsFalse(accolades.HasBaseline);
            Assert.AreEqual(1, accolades.Wins);
            Assert.AreEqual(2, accolades.Podiums);
        }

        [TestMethod]
        public void GetDriverAccolades_WithRecord_AddsSeasonsBeforeTheSaveToSaveHistoryCounts_AndFlagsHasBaselineTrue()
        {
            var saveGame = CreateTestSaveGame(2024);
            var allTime = CreateAllTimeRecords(2023);
            allTime.Drivers.Add(new AllTimeDriverRecord
            {
                DriverId = "D1",
                WinsByYear = new() { [2020] = 6, [2021] = 4 },
                PodiumsByYear = new() { [2019] = 3, [2020] = 9, [2021] = 8 },
                PolesByYear = new() { [2020] = 5 },
                Championships = new List<int> { 2020 }
            });
            saveGame.GrandPrixResults = new List<GrandPrixResult> { BuildResult(("D1", "T1", 1)) };
            saveGame.GrandPrixResults.First().QualifyingResults.Add(new SessionResult { DriverId = "D1", TeamId = "T1", Position = 1 });

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, allTime, "D1");

            Assert.IsTrue(accolades.HasBaseline);
            Assert.AreEqual(11, accolades.Wins);
            Assert.AreEqual(21, accolades.Podiums);
            Assert.AreEqual(6, accolades.PolePositions);
            CollectionAssert.AreEqual(new List<int> { 2020 }, accolades.ChampionshipYears);
        }

        [TestMethod]
        public void GetDriverAccolades_RecordSeasonsFromTheStartYearOn_AreIgnored()
        {
            // the save began in 2022: what really happened in 2022 and after never happened here
            var saveGame = CreateTestSaveGame(2024);
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>
            {
                new HistoricalDriverStanding { Year = 2022, Standing = new List<HisoricalDriverStandingEntry>() },
                new HistoricalDriverStanding { Year = 2023, Standing = new List<HisoricalDriverStandingEntry>() }
            };
            var allTime = CreateAllTimeRecords(2023);
            allTime.Drivers.Add(new AllTimeDriverRecord
            {
                DriverId = "D1",
                WinsByYear = new() { [2021] = 2, [2022] = 5, [2023] = 9 },
                PodiumsByYear = new() { [2021] = 4, [2022] = 10 },
                PolesByYear = new() { [2021] = 1, [2023] = 7 },
                Championships = new List<int> { 2021, 2023 }
            });

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, allTime, "D1");

            Assert.AreEqual(2022, accolades.StartYear);
            Assert.AreEqual(2, accolades.Wins);
            Assert.AreEqual(4, accolades.Podiums);
            Assert.AreEqual(1, accolades.PolePositions);
            CollectionAssert.AreEqual(new List<int> { 2021 }, accolades.ChampionshipYears);
        }

        [TestMethod]
        public void GetDriverAccolades_DriverNotInTheRecords_HasNoPast_AndStillFlagsHasBaselineTrue()
        {
            // the records list everyone with a podium or pole, so not being there is a real zero
            var saveGame = CreateTestSaveGame(2024);

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, CreateAllTimeRecords(2023), "D1");

            Assert.IsTrue(accolades.HasBaseline);
            Assert.AreEqual(0, accolades.Wins);
        }

        [TestMethod]
        public void GetDriverAccolades_RecordsStopShortOfTheSave_FlagsHasBaselineFalse()
        {
            var saveGame = CreateTestSaveGame(2024);

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, CreateAllTimeRecords(2022), "D1");

            Assert.IsFalse(accolades.HasBaseline);
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

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, null, "D1", justClinchedChampionshipYear: 2024);

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

            var accolades = AccoladesCalculator.GetDriverAccolades(saveGame, null, "D1", justClinchedChampionshipYear: 2024);

            Assert.AreEqual(1, accolades.Championships);
        }

        #endregion

        #region GetTeamAccolades Tests

        [TestMethod]
        public void GetTeamAccolades_NoAllTimeRecords_CountsOnlySaveHistory_AndFlagsHasBaselineFalse()
        {
            var saveGame = CreateTestSaveGame(2024);
            saveGame.GrandPrixResults = new List<GrandPrixResult>
            {
                BuildResult(("D1", "T1", 1)),
                BuildResult(("D2", "T1", 3)),
            };

            var accolades = AccoladesCalculator.GetTeamAccolades(saveGame, null, "T1");

            Assert.IsFalse(accolades.HasBaseline);
            Assert.AreEqual(1, accolades.Wins);
            Assert.AreEqual(2, accolades.Podiums);
        }

        [TestMethod]
        public void GetTeamAccolades_WithRecord_AddsSeasonsBeforeTheSaveToSaveHistoryCounts()
        {
            var saveGame = CreateTestSaveGame(2024);
            var allTime = CreateAllTimeRecords(2023);
            allTime.Teams.Add(new AllTimeTeamRecord
            {
                TeamId = "T1",
                WinsByYear = new() { [1998] = 2, [2023] = 1, [2024] = 8 },
                Championships = new List<int> { 1998, 2024 }
            });
            saveGame.GrandPrixResults = new List<GrandPrixResult> { BuildResult(("D1", "T1", 1)) };

            var accolades = AccoladesCalculator.GetTeamAccolades(saveGame, allTime, "T1");

            Assert.IsTrue(accolades.HasBaseline);
            Assert.AreEqual(4, accolades.Wins);
            CollectionAssert.AreEqual(new List<int> { 1998 }, accolades.ChampionshipYears);
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

        private static AllTimeRecords CreateAllTimeRecords(int upTo) => new AllTimeRecords { UpTo = upTo };
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
