using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AMS2ChEd.Business.Tests.GameLogic
{
    [TestClass]
    public class EndOfSeasonManagerUpdateDriversPoolTests
    {
        private Mock<IReputationUpdater> _mockReputationUpdater;
        private Mock<IOffSeasonMovements> _mockOffSeasonMovements;
        private Mock<IRandomDriverGenerator> _mockRandomDriverGenerator;
        private EndOfSeasonManager _sut;

        [TestInitialize]
        public void Setup()
        {
            _mockReputationUpdater = new Mock<IReputationUpdater>();
            _mockOffSeasonMovements = new Mock<IOffSeasonMovements>();
            _mockRandomDriverGenerator = new Mock<IRandomDriverGenerator>();
            _sut = new EndOfSeasonManager(
                _mockReputationUpdater.Object,
                _mockOffSeasonMovements.Object,
                _mockRandomDriverGenerator.Object);
        }

        #region UpdateDriversPoolForNextSeason - Basic Functionality

        [TestMethod]
        public void UpdateDriversPoolForNextSeason_ExistingDriverWithoutNewSeasonData_AddsNewRatingsForNextSeason()
        {
            // Arrange
            var currentSeasonYear = 1996;
            var nextSeasonYear = 1997;
            var saveGame = CreateSaveGame(currentSeasonYear);
            var driversNewSeasonDictionary = new Dictionary<string, IDriverData>(); // Empty - no new season data

            SetupReputationUpdater(DriverReputation.PRIME_MIDFIELD, DriverReputation.PRIME_STRONG_MIDFIELD);

            // Act
            _sut.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, driversNewSeasonDictionary);

            // Assert
            var driver = saveGame.Drivers.First(d => d.DriverId == "driver1");
            Assert.AreEqual(DriverReputation.PRIME_STRONG_MIDFIELD, driver.Reputation, "New rating should be updated");
        }

        [TestMethod]
        public void UpdateDriversPoolForNextSeason_ExistingDriverWithNewSeasonData_ReplacesWithNewSeasonVersion()
        {
            // Arrange
            var currentSeasonYear = 1996;
            var nextSeasonYear = 1997;
            var saveGame = CreateSaveGame(currentSeasonYear);

            // Driver has evolved in the new season with new name
            var updatedDriver = CreateDriver("driver1", DriverReputation.AGEING_MIDFIELD, nextSeasonYear);
            updatedDriver.Name = "Evolved Driver Name"; // Name might change



            var driversNewSeasonDictionary = new Dictionary<string, IDriverData>
            {
                { "driver1", updatedDriver }
            };

            // Act
            _sut.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, driversNewSeasonDictionary);

            // Assert
            var driver = saveGame.Drivers.First(d => d.DriverId == "driver1");
            Assert.AreEqual("Evolved Driver Name", driver.Name, "Driver name should be updated");
        }

        [TestMethod]
        public void UpdateDriversPoolForNextSeason_NewRookieDriver_AddsToDriverPool()
        {
            // Arrange
            var currentSeasonYear = 1996;
            var nextSeasonYear = 1997;
            var saveGame = CreateSaveGame(currentSeasonYear);
            var originalDriverCount = saveGame.Drivers.Count();

            var rookie = CreateDriver("rookie1", DriverReputation.YOUNG_TALENT, nextSeasonYear);

            var driversNewSeasonDictionary = new Dictionary<string, IDriverData>
            {
                { "rookie1", rookie }
            };

            // Act
            _sut.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, driversNewSeasonDictionary);

            // Assert
            Assert.AreEqual(originalDriverCount + 1, saveGame.Drivers.Count(), "Should have one more driver");
            Assert.IsTrue(saveGame.Drivers.Any(d => d.DriverId == "rookie1"), "Rookie should be in driver pool");

            var rookieInPool = saveGame.Drivers.First(d => d.DriverId == "rookie1");
        }

        [TestMethod]
        public void UpdateDriversPoolForNextSeason_MultipleRookies_AddsAllToDriverPool()
        {
            // Arrange
            var currentSeasonYear = 1996;
            var nextSeasonYear = 1997;
            var saveGame = CreateSaveGame(currentSeasonYear);
            var originalDriverCount = saveGame.Drivers.Count();

            var rookie1 = CreateDriver("rookie1", DriverReputation.YOUNG_TALENT, nextSeasonYear);
            var rookie2 = CreateDriver("rookie2", DriverReputation.PAY_DRIVER_SEASON, nextSeasonYear);
            var rookie3 = CreateDriver("rookie3", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, nextSeasonYear);

            var driversNewSeasonDictionary = new Dictionary<string, IDriverData>
            {
                { "rookie1", rookie1 },
                { "rookie2", rookie2 },
                { "rookie3", rookie3 }
            };

            // Act
            _sut.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, driversNewSeasonDictionary);

            // Assert
            Assert.AreEqual(originalDriverCount + 3, saveGame.Drivers.Count(), "Should have three more drivers");
            Assert.IsTrue(saveGame.Drivers.Any(d => d.DriverId == "rookie1"), "Rookie1 should be in pool");
            Assert.IsTrue(saveGame.Drivers.Any(d => d.DriverId == "rookie2"), "Rookie2 should be in pool");
            Assert.IsTrue(saveGame.Drivers.Any(d => d.DriverId == "rookie3"), "Rookie3 should be in pool");
        }

        #endregion

        #region UpdateDriversPoolForNextSeason - Reputation Updates

        [TestMethod]
        public void UpdateDriversPoolForNextSeason_ActiveDriver_UsesReputationUpdaterWithSeasonRecap()
        {
            // Arrange
            var currentSeasonYear = 1996;
            var nextSeasonYear = 1997;
            var saveGame = CreateSaveGameWithRaceResults(currentSeasonYear);
            var driversNewSeasonDictionary = new Dictionary<string, IDriverData>();

            // Driver finished 3rd with 2 podiums and 1 DNF
            _mockReputationUpdater
                .Setup(x => x.GetNewReputation(
                    DriverReputation.PRIME_MIDFIELD,
                    It.IsAny<int>(), // age
                    3, // position
                    2, // podiums
                    1,
                    3)) // DNFs
                .Returns(DriverReputation.PRIME_STRONG_MIDFIELD);

            // Act
            _sut.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, driversNewSeasonDictionary);

            // Assert
            _mockReputationUpdater.Verify(
                x => x.GetNewReputation(
                    DriverReputation.PRIME_MIDFIELD,
                    It.IsAny<int>(),
                    3,
                    2,
                    1,
                    3),
                Times.Once,
                "Should call GetNewReputation with season recap data");

            var driver = saveGame.Drivers.First(d => d.DriverId == "driver1");
            Assert.AreEqual(DriverReputation.PRIME_STRONG_MIDFIELD, driver.Reputation);
        }

        [TestMethod]
        public void UpdateDriversPoolForNextSeason_InactiveDriver_UsesReputationUpdaterForInactive()
        {
            // Arrange
            var currentSeasonYear = 1996;
            var nextSeasonYear = 1997;
            var saveGame = CreateSaveGame(currentSeasonYear); // No race results
            var driversNewSeasonDictionary = new Dictionary<string, IDriverData>();

            _mockReputationUpdater
                .Setup(x => x.GetNewReputationForInactiveDriver(
                    DriverReputation.PRIME_MIDFIELD,
                    It.IsAny<int>())) // age
                .Returns(DriverReputation.AGEING_MIDFIELD);

            // Act
            _sut.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, driversNewSeasonDictionary);

            // Assert
            _mockReputationUpdater.Verify(
                x => x.GetNewReputationForInactiveDriver(
                    DriverReputation.PRIME_MIDFIELD,
                    It.IsAny<int>()),
                Times.Once,
                "Should call GetNewReputationForInactiveDriver for driver without season recap");

            var driver = saveGame.Drivers.First(d => d.DriverId == "driver1");
            Assert.AreEqual(DriverReputation.AGEING_MIDFIELD, driver.Reputation);
        }

        [TestMethod]
        public void UpdateDriversPoolForNextSeason_ChampionDriver_GetsImprovedReputation()
        {
            // Arrange
            var currentSeasonYear = 1996;
            var nextSeasonYear = 1997;
            var saveGame = CreateSaveGameWithChampionResults(currentSeasonYear);
            var driversNewSeasonDictionary = new Dictionary<string, IDriverData>();

            // Champion: 1st place, 10 podiums, 0 DNFs
            _mockReputationUpdater
                .Setup(x => x.GetNewReputation(
                    DriverReputation.PRIME_MIDFIELD,
                    It.IsAny<int>(),
                    1, // champion
                    10, // lots of podiums
                    0,
                    10)) // no DNFs
                .Returns(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL);

            // Act
            _sut.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, driversNewSeasonDictionary);

            // Assert
            var driver = saveGame.Drivers.First(d => d.DriverId == "driver1");
            Assert.AreEqual(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, driver.Reputation,
                "Champion should be promoted to championship level");
        }

        #endregion

        #region UpdateDriversPoolForNextSeason - Player Data Updates

        [TestMethod]
        public void UpdateDriversPoolForNextSeason_PlayerDriverInNewSeason_UpdatesPlayerData()
        {
            // Arrange
            var currentSeasonYear = 1996;
            var nextSeasonYear = 1997;
            var saveGame = CreateSaveGame(currentSeasonYear);
            saveGame.PlayerData.DriverId = "driver1"; // Player is driver1

            var updatedPlayerDriver = CreateDriver("driver1", DriverReputation.PRIME_STRONG_MIDFIELD, nextSeasonYear);
            updatedPlayerDriver.Name = "Player Updated Name";
            updatedPlayerDriver.Nationality = "Updated Nationality";

            var driversNewSeasonDictionary = new Dictionary<string, IDriverData>
            {
                { "driver1", updatedPlayerDriver }
            };

            // Act
            _sut.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, driversNewSeasonDictionary);

            // Assert
            Assert.AreEqual("Player Updated Name", saveGame.PlayerData.Name, "Player name should be updated");
            Assert.AreEqual("Updated Nationality", saveGame.PlayerData.Nationality, "Player nationality should be updated");
        }

        [TestMethod]
        public void UpdateDriversPoolForNextSeason_NonPlayerDriver_DoesNotUpdatePlayerData()
        {
            // Arrange
            var currentSeasonYear = 1996;
            var nextSeasonYear = 1997;
            var saveGame = CreateSaveGame(currentSeasonYear);
            saveGame.PlayerData.DriverId = "player_id"; // Player is NOT driver1
            saveGame.PlayerData.Name = "Original Player Name";

            var updatedDriver = CreateDriver("driver1", DriverReputation.PRIME_STRONG_MIDFIELD, nextSeasonYear);
            updatedDriver.Name = "Updated Driver Name";

            var driversNewSeasonDictionary = new Dictionary<string, IDriverData>
            {
                { "driver1", updatedDriver }
            };

            // Act
            _sut.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, driversNewSeasonDictionary);

            // Assert
            Assert.AreEqual("Original Player Name", saveGame.PlayerData.Name,
                "Player name should NOT change when non-player driver is updated");
        }

        #endregion

        #region UpdateDriversPoolForNextSeason - Retired Drivers

        [TestMethod]
        public void UpdateDriversPoolForNextSeason_RetiredDriverInNewSeasonDictionary_ExcludedFromRookies()
        {
            // Arrange
            var currentSeasonYear = 1996;
            var nextSeasonYear = 1997;
            var saveGame = CreateSaveGame(currentSeasonYear);
            var originalDriverCount = saveGame.Drivers.Count();

            var retiredDriver = CreateDriver("retired1", DriverReputation.AGEING_MIDFIELD, nextSeasonYear);
            saveGame.RetiredDrivers = new List<IDriverData> { retiredDriver };

            // Retired driver appears in new season dictionary but should be excluded
            var driversNewSeasonDictionary = new Dictionary<string, IDriverData>
            {
                { "retired1", retiredDriver }
            };

            // Act
            _sut.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, driversNewSeasonDictionary);

            // Assert
            Assert.AreEqual(originalDriverCount, saveGame.Drivers.Count(),
                "Retired driver should not be added back to driver pool");
            Assert.IsFalse(saveGame.Drivers.Any(d => d.DriverId == "retired1"),
                "Retired driver should not be in active driver pool");
        }

        #endregion

        #region UpdateDriversPoolForNextSeason - Edge Cases

        [TestMethod]
        public void UpdateDriversPoolForNextSeason_EmptyNewSeasonDictionary_AllDriversGetUpdatedRatings()
        {
            // Arrange
            var currentSeasonYear = 1996;
            var nextSeasonYear = 1997;
            var saveGame = CreateSaveGame(currentSeasonYear);
            var driversNewSeasonDictionary = new Dictionary<string, IDriverData>();

            SetupReputationUpdater(DriverReputation.PRIME_MIDFIELD, DriverReputation.PRIME_STRONG_MIDFIELD);
            SetupReputationUpdater(DriverReputation.YOUNG_TALENT, DriverReputation.PRIME_MIDFIELD);

            // Act
            _sut.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, driversNewSeasonDictionary);

            // Assert
            foreach (var driver in saveGame.Drivers)
            {
                Assert.IsTrue(driver.Reputation == DriverReputation.PRIME_STRONG_MIDFIELD || driver.Reputation == DriverReputation.PRIME_MIDFIELD,
                    $"Driver {driver.DriverId} should have ratings for next season");
            }
        }

        [TestMethod]
        public void UpdateDriversPoolForNextSeason_MixedScenario_HandlesAllCasesCorrectly()
        {
            // Arrange
            var currentSeasonYear = 1996;
            var nextSeasonYear = 1997;
            var saveGame = CreateSaveGame(currentSeasonYear);
            var originalDriverCount = saveGame.Drivers.Count();

            // Driver1: Exists and has new season data (replaced)
            var updatedDriver1 = CreateDriver("driver1", DriverReputation.AGEING_MIDFIELD, nextSeasonYear);
            updatedDriver1.Name = "othername";

            // Driver2: Exists but no new season data (gets updated ratings)
            SetupReputationUpdater(DriverReputation.YOUNG_TALENT, DriverReputation.PRIME_MIDFIELD);

            // Rookie: New driver for next season
            var rookie = CreateDriver("rookie1", DriverReputation.YOUNG_TALENT, nextSeasonYear);

            var driversNewSeasonDictionary = new Dictionary<string, IDriverData>
            {
                { "driver1", updatedDriver1 },
                { "rookie1", rookie }
            };

            // Act
            _sut.UpdateDriversPoolForNextSeason(nextSeasonYear, saveGame, driversNewSeasonDictionary);

            // Assert
            Assert.AreEqual(originalDriverCount + 1, saveGame.Drivers.Count(), "Should have one more driver (rookie)");

            // Driver1 replaced
            var driver1 = saveGame.Drivers.First(d => d.DriverId == "driver1");
            Assert.IsTrue(driver1.Name == "othername");

            // Driver2 updated
            var driver2 = saveGame.Drivers.First(d => d.DriverId == "driver2");
            Assert.IsTrue(driver2.Reputation == DriverReputation.PRIME_MIDFIELD);

            // Rookie added
            Assert.IsTrue(saveGame.Drivers.Any(d => d.DriverId == "rookie1"));
        }

        #endregion

        #region Helper Methods

        private SaveGame CreateSaveGame(int year)
        {
            var season = new Season
            {
                Year = year,
                Teams = new List<ITeamEntry>
                {
                    CreateTeamEntry("ferrari", "driver1", "driver2")
                }
            };

            return new SaveGame
            {
                Drivers = new List<IDriverData>
                {
                    CreateDriver("driver1", DriverReputation.PRIME_MIDFIELD, year),
                    CreateDriver("driver2", DriverReputation.YOUNG_TALENT, year)
                },
                CurrentSeason = season,
                PlayerData = new PlayerData { DriverId = "player_id", Name = "Player Name" },
                RetiredDrivers = new List<IDriverData>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>()
            };
        }

        private SaveGame CreateSaveGameWithRaceResults(int year)
        {
            var saveGame = CreateSaveGame(year);

            // Add race results for driver1: 3rd place, 2 podiums, 1 DNF
            saveGame.GrandPrixResults = new List<GrandPrixResult>
            {
                new GrandPrixResult
                {
                    Year = year,
                    RaceResults = new List<SessionResult>
                    {
                        new SessionResult { DriverId = "driver1", Position = 2, DNF = false },
                        new SessionResult { DriverId = "driver1", Position = 3, DNF = false },
                        new SessionResult { DriverId = "driver1", Position = 5, DNF = true }
                    }
                }
            };

            saveGame.CurrentDriverStandings = new List<HistoricalDriverStandingEntry>
            {
                new HistoricalDriverStandingEntry
                {
                    DriverId = "driver1",
                    Position = 3,
                    Points = 50,
                    TeamId = "ferrari",
                    PositionsTally = new PositionsTally()
                }
            };

            return saveGame;
        }

        private SaveGame CreateSaveGameWithChampionResults(int year)
        {
            var saveGame = CreateSaveGame(year);

            // Add race results for driver1: Champion with 10 podiums, 0 DNFs
            var raceResults = new List<SessionResult>();
            for (int i = 0; i < 10; i++) 
            {
                raceResults.Add(new SessionResult { DNF = false, DriverId = "driver1", Position = 1});
            }

            saveGame.GrandPrixResults = new List<GrandPrixResult>
            {
                new GrandPrixResult
                {
                    Year = year,
                    RaceResults = raceResults
                }
            };

            saveGame.CurrentDriverStandings = new List<HistoricalDriverStandingEntry>
            {
                new HistoricalDriverStandingEntry
                {
                    DriverId = "driver1",
                    Position = 1, // Champion
                    Points = 100,
                    TeamId = "ferrari",
                    PositionsTally = new PositionsTally()
                }
            };

            return saveGame;
        }

        private TeamEntry CreateTeamEntry(string teamId, string driver1Id, string driver2Id)
        {
            return new TeamEntry
            {
                TeamId = teamId,
                Reputation = TeamReputation.TOP_TEAM,
                Driver1Contract = new DriverContract { DriverId = driver1Id, Races = 10 },
                Driver2Contract = new DriverContract { DriverId = driver2Id, Races = 10 }
            };
        }

        private DriverData CreateDriver(string driverId, DriverReputation reputation, int year)
        {
            return new DriverData
            {
                DriverId = driverId,
                Name = $"Driver {driverId}",
                Nationality = "Test Nation",
                YearOfBirth = 1970,
                Reputation = reputation
            };
        }

        private void SetupReputationUpdater(DriverReputation oldRep, DriverReputation newRep)
        {
            _mockReputationUpdater
                .Setup(x => x.GetNewReputationForInactiveDriver(oldRep, It.IsAny<int>()))
                .Returns(newRep);
        }

        #endregion
    }
}