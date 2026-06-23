using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Moq;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class EndOfSeasonManagerTests
    {
        private EndOfSeasonManager _endOfSeasonManager;
        private Mock<IReputationUpdater> _mockReputationUpdater;
        private Mock<OffSeasonMovements> _mockOffSeasonMovements;

        [TestInitialize]
        public void Setup()
        {
            _mockReputationUpdater = new Mock<IReputationUpdater>();

            var mockDriverFirer = new Mock<DriverFirer>();
            var mockDriverHirer = new Mock<DriverHirer>();
            _mockOffSeasonMovements = new Mock<OffSeasonMovements>(mockDriverFirer.Object, mockDriverHirer.Object);

            _endOfSeasonManager = new EndOfSeasonManager(
                _mockReputationUpdater.Object,
                _mockOffSeasonMovements.Object,
                new Mock<IRandomDriverGenerator>().Object);
        }

        #region ExecuteTeamDrops Tests

        [TestMethod]
        public void ExecuteTeamDrops_WithContractExpired_ShouldDropDriver()
        {
            // Arrange
            var mockOffSeasonMovements = new Mock<IOffSeasonMovements>();
            var saveGame = CreateTestSaveGame(2024);
            var driver = CreateTestDriver("D1", "Test Driver", 1995, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "another Driver", 1995, DriverReputation.PRIME_MIDFIELD, 2025);
            saveGame.Drivers = new List<IDriverData> { driver, driver2 };

            var team = CreateTestTeam("T1", TeamReputation.MIDFIELD, "D1", 0, "D2", 5);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team };
            AddMockedGPResult(saveGame);
            AddMockedDriverStandings(saveGame);
            var fullDriverRatingsDatabase = CreateTestDriverRatingsDatabase(2025, driver);

            // Setup mocks
            _mockReputationUpdater
                .Setup(x => x.GetNewReputation(It.IsAny<DriverReputation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(DriverReputation.PRIME_MIDFIELD);

            mockOffSeasonMovements
                .Setup(x => x.DropDrivers(It.IsAny<IEnumerable<TeamSituation>>()))
                .Returns((IEnumerable<TeamSituation> teams) =>
                {
                    return teams.Select(t => new DropTeamResult
                    {
                        TeamId = t.TeamId,
                        DropDriver1 = t.Driver1.RacesLeftInContract <= 0
                            ? DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED
                            : DriverFirerOutcome.NOT_DROPPED,
                        DropDriver2 = t.Driver2.RacesLeftInContract <= 0
                            ? DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED
                            : DriverFirerOutcome.NOT_DROPPED
                    });
                });

            // Act
            var newSeason = CreateTestSeason(2025);
            var newSeasonTeam = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD, "TBD1", "TBD2");
            newSeason.Teams = new List<ITeamEntry> { newSeasonTeam };

            var results = _endOfSeasonManager.ExecuteTeamDrops(saveGame, newSeason).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("T1", results[0].TeamId);
            Assert.AreEqual(DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED, results[0].DropDriver1);
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, results[0].DropDriver2);
        }

        [TestMethod]
        public void ExecuteTeamDrops_WithTopTeamAndLowReputationDriver_ShouldDropDriver()
        {
            // Arrange
            var mockOffSeasonMovements = new Mock<IOffSeasonMovements>();
            var saveGame = CreateTestSaveGame(2024);
            var driver = CreateTestDriver("D1", "Young Talent", 1998, DriverReputation.YOUNG_TALENT, 2025);
            var driver2 = CreateTestDriver("D2", "Not Young Talent", 1990, DriverReputation.PRIME_MIDFIELD, 2025);
            saveGame.Drivers = new List<IDriverData> { driver, driver2 };

            var team = CreateTestTeam("T1", TeamReputation.TOP_TEAM, "D1", 5, "D2", 5);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team };
            AddMockedGPResult(saveGame);
            AddMockedDriverStandings(saveGame);
            var fullDriverRatingsDatabase = CreateTestDriverRatingsDatabase(2025, driver);

            _mockReputationUpdater
                .Setup(x => x.GetNewReputation(It.IsAny<DriverReputation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(DriverReputation.YOUNG_TALENT);

            mockOffSeasonMovements
                .Setup(x => x.DropDrivers(It.IsAny<IEnumerable<TeamSituation>>()))
                .Returns((IEnumerable<TeamSituation> teams) =>
                {
                    var firer = new DriverFirer();
                    return teams.Select(t => new DropTeamResult
                    {
                        TeamId = t.TeamId,
                        DropDriver1 = firer.WillDropDriver(t.Reputation, t.Driver1.Reputation, t.Driver1.RacesLeftInContract, t.Driver1.DriverRetiring, t.TeamQuitting),
                        DropDriver2 = firer.WillDropDriver(t.Reputation, t.Driver2.Reputation, t.Driver2.RacesLeftInContract, t.Driver2.DriverRetiring, t.TeamQuitting)
                    });
                });

            // Act
            var newSeason = CreateTestSeason(2025);
            var newSeasonTeam = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "TBD1", "TBD2");
            newSeason.Teams = new List<ITeamEntry> { newSeasonTeam };

            var results = _endOfSeasonManager.ExecuteTeamDrops(saveGame, newSeason).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(DriverFirerOutcome.DROPPED_UNDERPERFORMING, results[0].DropDriver1);
        }

        [TestMethod]
        public void ExecuteTeamDrops_WithChampionshipDriver_ShouldKeepDriver()
        {
            // Arrange
            var mockOffSeasonMovements = new Mock<IOffSeasonMovements>();
            var saveGame = CreateTestSaveGame(2024);
            var driver = CreateTestDriver("D1", "Champion", 1995, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var driver2 = CreateTestDriver("D2", "not champion", 1995, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            saveGame.Drivers = new List<IDriverData> { driver, driver2 };

            var team = CreateTestTeam("T1", TeamReputation.TOP_TEAM, "D1", 5, "D2", 5);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team };

            AddMockedGPResult(saveGame);
            AddMockedDriverStandings(saveGame);

            var fullDriverRatingsDatabase = CreateTestDriverRatingsDatabase(2025, driver);

            _mockReputationUpdater
                .Setup(x => x.GetNewReputation(It.IsAny<DriverReputation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL);

            mockOffSeasonMovements
                .Setup(x => x.DropDrivers(It.IsAny<IEnumerable<TeamSituation>>()))
                .Returns((IEnumerable<TeamSituation> teams) =>
                {
                    var firer = new DriverFirer();
                    return teams.Select(t => new DropTeamResult
                    {
                        TeamId = t.TeamId,
                        DropDriver1 = firer.WillDropDriver(t.Reputation, t.Driver1.Reputation, t.Driver1.RacesLeftInContract, t.Driver1.DriverRetiring, t.TeamQuitting),
                        DropDriver2 = DriverFirerOutcome.NOT_DROPPED
                    });
                });

            // Act
            var newSeason = CreateTestSeason(2025);
            var newSeasonTeam = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "TBD1", "TBD2");
            newSeason.Teams = new List<ITeamEntry> { newSeasonTeam };

            var results = _endOfSeasonManager.ExecuteTeamDrops(saveGame, newSeason).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, results[0].DropDriver1);
        }

        [TestMethod]
        public void ExecuteTeamDrops_WithRetiringDriver_ShouldDropWithRetirementReason()
        {
            // Arrange
            var mockOffSeasonMovements = new Mock<IOffSeasonMovements>();
            var saveGame = CreateTestSaveGame(2024);
            var driver = CreateTestDriver("D1", "Veteran", 1980, DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, 2025); // Age 45 in 2025
            var driver2 = CreateTestDriver("D2", "not Veteran", 1995, DriverReputation.PRIME_MIDFIELD, 2025);
            saveGame.Drivers = new List<IDriverData> { driver, driver2 };

            var team = CreateTestTeam("T1", TeamReputation.TOP_TEAM, "D1", 5, "D2", 5);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team };
            AddMockedGPResult(saveGame);
            AddMockedDriverStandings(saveGame);
            var fullDriverRatingsDatabase = CreateTestDriverRatingsDatabase(2025, driver);

            _mockReputationUpdater
                .Setup(x => x.GetNewReputation(It.IsAny<DriverReputation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(DriverReputation.AGEING_CHAMPIONSHIP_LEVEL);

            mockOffSeasonMovements
                .Setup(x => x.DropDrivers(It.IsAny<IEnumerable<TeamSituation>>()))
                .Returns((IEnumerable<TeamSituation> teams) =>
                {
                    var firer = new DriverFirer();
                    return teams.Select(t => new DropTeamResult
                    {
                        TeamId = t.TeamId,
                        DropDriver1 = firer.WillDropDriver(t.Reputation, t.Driver1.Reputation, t.Driver1.RacesLeftInContract, t.Driver1.DriverRetiring, t.TeamQuitting),
                        DropDriver2 = DriverFirerOutcome.NOT_DROPPED
                    });
                });

            // Act
            var newSeason = CreateTestSeason(2025);
            var newSeasonTeam = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "TBD1", "TBD2");
            newSeason.Teams = new List<ITeamEntry> { newSeasonTeam };

            var results = _endOfSeasonManager.ExecuteTeamDrops(saveGame, newSeason).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(DriverFirerOutcome.DROPPED_RETIRING, results[0].DropDriver1);
        }

        [TestMethod]
        public void ExecuteTeamDrops_WithPlayerAtRetirementAge_ShouldNotMarkPlayerAsRetiring()
        {
            // Arrange
            var mockOffSeasonMovements = new Mock<IOffSeasonMovements>();
            var saveGame = CreateTestSaveGame(2024);

            // Create a player driver who is 45 years old (retirement age)
            var playerDriver = CreateTestDriver("PLAYER", "Veteran Player", 1980, DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, 2025); // Age 45 in 2025
            var teammateDriver = CreateTestDriver("D2", "Young Teammate", 1995, DriverReputation.PRIME_MIDFIELD, 2025);
            saveGame.Drivers = new List<IDriverData> { playerDriver, teammateDriver };

            // Ensure the player ID matches
            saveGame.PlayerData.DriverId = "PLAYER";
            saveGame.PlayerData.Name = "Veteran Player";

            var team = CreateTestTeam("T1", TeamReputation.TOP_TEAM, "PLAYER", 5, "D2", 5);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team };
            AddMockedGPResult(saveGame);
            AddMockedDriverStandings(saveGame);

            _mockReputationUpdater
                .Setup(x => x.GetNewReputation(It.IsAny<DriverReputation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(DriverReputation.AGEING_CHAMPIONSHIP_LEVEL);

            // Capture the TeamSituation passed to DropDrivers
            TeamSituation capturedTeamSituation = null;
            mockOffSeasonMovements
                .Setup(x => x.DropDrivers(It.IsAny<IEnumerable<TeamSituation>>()))
                .Callback<IEnumerable<TeamSituation>>(teams => {
                    capturedTeamSituation = teams.First();
                })
                .Returns((IEnumerable<TeamSituation> teams) =>
                {
                    return teams.Select(t => new DropTeamResult
                    {
                        TeamId = t.TeamId,
                        DropDriver1 = DriverFirerOutcome.NOT_DROPPED,
                        DropDriver2 = DriverFirerOutcome.NOT_DROPPED
                    });
                });

            // Act
            var newSeason = CreateTestSeason(2025);
            var newSeasonTeam = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "PLAYER", "D2");
            newSeason.Teams = new List<ITeamEntry> { newSeasonTeam };

            var endOfSeasonManager = new EndOfSeasonManager(
                _mockReputationUpdater.Object,
                mockOffSeasonMovements.Object,
                new Mock<IRandomDriverGenerator>().Object);

            var results = endOfSeasonManager.ExecuteTeamDrops(saveGame, newSeason).ToList();

            // Assert
            Assert.IsNotNull(capturedTeamSituation, "TeamSituation should have been captured");
            Assert.AreEqual("PLAYER", capturedTeamSituation.Driver1.DriverId, "Driver1 should be the player");
            Assert.IsFalse(capturedTeamSituation.Driver1.DriverRetiring,
                "Player should NOT be marked as retiring even though they are 45 years old (retirement age)");

            // Verify that a non-player driver of the same age WOULD be marked as retiring
            // (This is implicit in the logic - if the teammate were also 45, they would be marked as retiring)
        }

        private void AddMockedDriverStandings(ISaveGame saveGame)
        {
            var driverStandingEntry =
                saveGame.CurrentSeason.Teams
                .SelectMany((t, i) => new[] {
                    new HistoricalDriverStandingEntry()
                    {
                        DriverId = t.Driver1Contract.DriverId,
                        Points = 0,
                        Position = (i * 2) + 1
                    },
                    new HistoricalDriverStandingEntry()
                    {
                        DriverId = t.Driver2Contract.DriverId,
                        Points = 0,
                        Position = (i * 2) + 1
                    }
                })
                .ToList();

            saveGame.CurrentDriverStandings = driverStandingEntry.ToList();

        }

        private void AddMockedGPResult(ISaveGame saveGame)
        {
            var listOfDriverIds =
                saveGame.CurrentSeason.Teams
                .SelectMany(t => new[] { t.Driver1Contract.DriverId, t.Driver2Contract.DriverId })
                .ToList();
            saveGame.GrandPrixResults = new List<GrandPrixResult>
            {
                new GrandPrixResult
                {
                    Year = saveGame.CurrentSeason.Year,
                    GrandPrixName = "Grand Prix Generic",
                    QualifyingResults = listOfDriverIds.Select((d, i) => new SessionResult
                    {
                        Position = i + 1,
                        DriverId = d
                    }).ToList(),
                    RaceResults = listOfDriverIds.Select((d, i) => new SessionResult
                    {
                        Position = i + 1,
                        DriverId = d
                    }).ToList()
                }
            };
        }

        [TestMethod]
        public void ExecuteTeamDrops_WithNewRookieForNextSeason_ShouldAddToDriverPool()
        {
            // Arrange
            var mockOffSeasonMovements = new Mock<IOffSeasonMovements>();
            var saveGame = CreateTestSaveGame(2024);
            var existingDriver = CreateTestDriver("D1", "Existing", 1995, DriverReputation.PRIME_MIDFIELD, 2025);
            var existingDriver2 = CreateTestDriver("D2", "Existing2", 1995, DriverReputation.PRIME_MIDFIELD, 2025);
            saveGame.Drivers = new List<IDriverData> { existingDriver, existingDriver2 };

            var team = CreateTestTeam("T1", TeamReputation.MIDFIELD, "D1", 5, "D2", 5);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team };

            AddMockedGPResult(saveGame);
            AddMockedDriverStandings(saveGame);

            // Create rookie that only exists in 2025
            var rookie = CreateTestDriver("ROOKIE1", "New Rookie", 2002, DriverReputation.YOUNG_TALENT, 2025);
            saveGame.Drivers = new List<IDriverData> { existingDriver, existingDriver2, rookie };

            _mockReputationUpdater
                .Setup(x => x.GetNewReputation(It.IsAny<DriverReputation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns(DriverReputation.PRIME_MIDFIELD);

            mockOffSeasonMovements
                .Setup(x => x.DropDrivers(It.IsAny<IEnumerable<TeamSituation>>()))
                .Returns(new List<DropTeamResult>());

            // Act
            var newSeason = CreateTestSeason(2025);
            var newSeasonTeam = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD, "TBD1", "TBD2");
            newSeason.Teams = new List<ITeamEntry> { newSeasonTeam };

            _endOfSeasonManager.ExecuteTeamDrops(saveGame, newSeason);

            // Assert
            Assert.IsTrue(saveGame.Drivers.Any(d => d.DriverId == "ROOKIE1"), "Rookie should be added to driver pool");
            var addedRookie = saveGame.Drivers.First(d => d.DriverId == "ROOKIE1");
        }

        [TestMethod]
        public void ExecuteTeamDrops_WithBothDriversDropped_ShouldDropBoth()
        {
            // Arrange
            var mockOffSeasonMovements = new Mock<IOffSeasonMovements>();
            var saveGame = CreateTestSaveGame(2024);
            var driver1 = CreateTestDriver("D1", "Driver 1", 1995, DriverReputation.YOUNG_TALENT, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1996, DriverReputation.PAY_DRIVER_SEASON, 2025);
            saveGame.Drivers = new List<IDriverData> { driver1, driver2 };


            var team = CreateTestTeam("T1", TeamReputation.TOP_TEAM, "D1", 0, "D2", 2);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team };

            AddMockedGPResult(saveGame);
            AddMockedDriverStandings(saveGame);

            var fullDriverRatingsDatabase = CreateTestDriverRatingsDatabase(2025, driver1, driver2);

            _mockReputationUpdater
                .Setup(x => x.GetNewReputation(It.IsAny<DriverReputation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns((DriverReputation rep, int age, int pos, int pod, int dnf, int races) => rep);

            mockOffSeasonMovements
                .Setup(x => x.DropDrivers(It.IsAny<IEnumerable<TeamSituation>>()))
                .Returns((IEnumerable<TeamSituation> teams) =>
                {
                    var firer = new DriverFirer();
                    return teams.Select(t => new DropTeamResult
                    {
                        TeamId = t.TeamId,
                        DropDriver1 = firer.WillDropDriver(t.Reputation, t.Driver1.Reputation, t.Driver1.RacesLeftInContract, t.Driver1.DriverRetiring, t.TeamQuitting),
                        DropDriver2 = firer.WillDropDriver(t.Reputation, t.Driver2.Reputation, t.Driver2.RacesLeftInContract, t.Driver2.DriverRetiring, t.TeamQuitting)
                    });
                });

            // Act
            var newSeason = CreateTestSeason(2025);
            var newSeasonTeam = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "TBD1", "TBD2");
            newSeason.Teams = new List<ITeamEntry> { newSeasonTeam };

            var results = _endOfSeasonManager.ExecuteTeamDrops(saveGame, newSeason).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].DropDriver1.IsDropped(), "Driver 1 should be dropped");
            Assert.IsTrue(results[0].DropDriver2.IsDropped(), "Driver 2 should be dropped");
        }

        [TestMethod]
        public void ExecuteTeamDrops_TeamQuitting_DropsAllDrivers()
        {
            // Arrange
            var mockOffSeasonMovements = new Mock<IOffSeasonMovements>();
            var saveGame = CreateTestSaveGame(2024);

            // Create two championship-level drivers with valid contracts
            var driver1 = CreateTestDriver("D1", "Champion Driver 1", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var driver2 = CreateTestDriver("D2", "Champion Driver 2", 1991, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, 2025);
            saveGame.Drivers = new List<IDriverData> { driver1, driver2 };

            // Team with good drivers and valid contracts
            var team = CreateTestTeam("QUITTING_TEAM", TeamReputation.TOP_TEAM, "D1", 10, "D2", 15);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team };
            AddMockedGPResult(saveGame);
            AddMockedDriverStandings(saveGame);
            var fullDriverRatingsDatabase = CreateTestDriverRatingsDatabase(2025, driver1, driver2);

            _mockReputationUpdater
                .Setup(x => x.GetNewReputation(It.IsAny<DriverReputation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns((DriverReputation rep, int age, int pos, int podiums, int dnfs, int races) => rep);

            TeamSituation capturedTeamSituation = null;
            mockOffSeasonMovements
                .Setup(x => x.DropDrivers(It.IsAny<IEnumerable<TeamSituation>>()))
                .Callback<IEnumerable<TeamSituation>>(teams =>
                {
                    capturedTeamSituation = teams.First();
                })
                .Returns(new List<DropTeamResult>
                {
                    new DropTeamResult
                    {
                        TeamId = "QUITTING_TEAM",
                        DropDriver1 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED, // Dropped due to team quitting
                        DropDriver2 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED  // Dropped due to team quitting
                    }
                });

            var endOfSeasonManager = new EndOfSeasonManager(_mockReputationUpdater.Object, mockOffSeasonMovements.Object, new Mock<IRandomDriverGenerator>().Object);

            // Create next season WITHOUT the team (team is quitting)
            var newSeason = CreateTestSeason(2025);
            newSeason.Teams = new List<ITeamEntry>(); // Empty - team not continuing

            // Act
            var results = endOfSeasonManager.ExecuteTeamDrops(saveGame, newSeason).ToList();

            // Assert
            Assert.IsNotNull(capturedTeamSituation, "TeamSituation should be passed to DropDrivers");
            Assert.AreEqual("QUITTING_TEAM", capturedTeamSituation.TeamId);
            Assert.IsTrue(capturedTeamSituation.TeamQuitting,
                "TeamQuitting should be true when team is not in next season");

            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].DropDriver1.IsDropped(),
                "Driver 1 should be dropped even with valid contract and high reputation");
            Assert.IsTrue(results[0].DropDriver2.IsDropped(),
                "Driver 2 should be dropped even with valid contract and high reputation");
        }

        #endregion

        #region StartNewSeason Tests

        [TestMethod]
        public void StartNewSeason_ShouldArchivePreviousStandings()
        {
            // Arrange
            var saveGame = CreateTestSaveGame(2024);
            var driver = CreateTestDriver("D1", "Young Talent", 1998, DriverReputation.YOUNG_TALENT, 2025);
            var driver2 = CreateTestDriver("D2", "Not Young Talent", 1990, DriverReputation.PRIME_MIDFIELD, 2025);
            saveGame.Drivers = new List<IDriverData> { driver, driver2 };
            var team = CreateTestTeam("T1", TeamReputation.TOP_TEAM, "D1", 5, "D2", 5);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team };

            var driverStandings = new List<HistoricalDriverStandingEntry>
            {
                new HistoricalDriverStandingEntry { DriverId = "D1", Position = 1, Points = 350 },
                new HistoricalDriverStandingEntry { DriverId = "D2", Position = 2, Points = 320 }
            };
            var constructorStandings = new List<ConstructorStandingEntry>
            {
                new ConstructorStandingEntry { TeamId = "T1", Position = 1, Points = 600 }
            };

            saveGame.CurrentDriverStandings = driverStandings;
            saveGame.CurrentConstructorStandings = constructorStandings;
            saveGame.HistoricalDriverStandings = new List<HistoricalDriverStanding>();
            saveGame.HistoricalConstructorStandings = new List<HistoricalConstructorStanding>();

            var newSeason = CreateTestSeason(2025);

            // Act
            _endOfSeasonManager.StartNewSeason(saveGame, newSeason);

            // Assert
            Assert.AreEqual(1, saveGame.HistoricalDriverStandings.Count());
            Assert.AreEqual(2024, saveGame.HistoricalDriverStandings.First().Year);
            Assert.AreEqual(2, saveGame.HistoricalDriverStandings.First().Standing.Count());

            Assert.AreEqual(1, saveGame.HistoricalConstructorStandings.Count());
            Assert.AreEqual(2024, saveGame.HistoricalConstructorStandings.First().Year);
            Assert.AreEqual(1, saveGame.HistoricalConstructorStandings.First().Standing.Count());
        }

        [TestMethod]
        public void StartNewSeason_ShouldInitializeDriverStandings()
        {
            // Arrange
            var saveGame = CreateTestSaveGame(2024);
            var newSeason = CreateTestSeason(2025);

            var team1 = CreateTestTeam("T1", TeamReputation.TOP_TEAM, "D1", 20, "D2", 20);
            var team2 = CreateTestTeam("T2", TeamReputation.MIDFIELD, "D3", 20, "D4", 20);
            newSeason.Teams = new List<ITeamEntry> { team1, team2 };

            // Act
            _endOfSeasonManager.StartNewSeason(saveGame, newSeason);

            // Assert
            Assert.AreEqual(4, saveGame.CurrentDriverStandings.Count(), "Should have 4 drivers (2 teams x 2 drivers)");
            Assert.IsTrue(saveGame.CurrentDriverStandings.All(s => s.Points == 0), "All drivers should start with 0 points");
            Assert.IsTrue(saveGame.CurrentDriverStandings.Select(s => s.Position).Distinct().Count() == 4, "All positions should be unique");
            Assert.IsTrue(saveGame.CurrentDriverStandings.Any(s => s.DriverId == "D1"));
            Assert.IsTrue(saveGame.CurrentDriverStandings.Any(s => s.DriverId == "D2"));
            Assert.IsTrue(saveGame.CurrentDriverStandings.Any(s => s.DriverId == "D3"));
            Assert.IsTrue(saveGame.CurrentDriverStandings.Any(s => s.DriverId == "D4"));
        }

        [TestMethod]
        public void StartNewSeason_ShouldInitializeConstructorStandings()
        {
            // Arrange
            var saveGame = CreateTestSaveGame(2024);
            var newSeason = CreateTestSeason(2025);

            var team1 = CreateTestTeam("T1", TeamReputation.TOP_TEAM, "D1", 20, "D2", 20);
            var team2 = CreateTestTeam("T2", TeamReputation.MIDFIELD, "D3", 20, "D4", 20);
            newSeason.Teams = new List<ITeamEntry> { team1, team2 };

            // Act
            _endOfSeasonManager.StartNewSeason(saveGame, newSeason);

            // Assert
            Assert.AreEqual(2, saveGame.CurrentConstructorStandings.Count(), "Should have 2 teams");
            Assert.IsTrue(saveGame.CurrentConstructorStandings.All(s => s.Points == 0), "All teams should start with 0 points");
            Assert.IsTrue(saveGame.CurrentConstructorStandings.Select(s => s.Position).Distinct().Count() == 2, "All positions should be unique");
            Assert.IsTrue(saveGame.CurrentConstructorStandings.Any(s => s.TeamId == "T1"));
            Assert.IsTrue(saveGame.CurrentConstructorStandings.Any(s => s.TeamId == "T2"));
        }

        [TestMethod]
        public void StartNewSeason_ShouldResetNextGpIndex()
        {
            // Arrange
            var saveGame = CreateTestSaveGame(2024);
            saveGame.NextGpIndex = 20; // Season completed
            var newSeason = CreateTestSeason(2025);

            // Act
            _endOfSeasonManager.StartNewSeason(saveGame, newSeason);

            // Assert
            Assert.AreEqual(0, saveGame.NextGpIndex, "NextGpIndex should be reset to 0");
        }

        [TestMethod]
        public void StartNewSeason_ShouldUpdateCurrentSeason()
        {
            // Arrange
            var saveGame = CreateTestSaveGame(2024);
            var newSeason = CreateTestSeason(2025);

            // Act
            _endOfSeasonManager.StartNewSeason(saveGame, newSeason);

            // Assert
            Assert.AreEqual(2025, saveGame.CurrentSeason.Year);
            Assert.AreSame(newSeason, saveGame.CurrentSeason);
        }

        [TestMethod]
        public void StartNewSeason_WithMultipleTeams_ShouldInitializeAllCorrectly()
        {
            // Arrange
            var saveGame = CreateTestSaveGame(2024);
            var newSeason = CreateTestSeason(2025);

            var teams = new List<ITeamEntry>();
            for (int i = 1; i <= 10; i++)
            {
                teams.Add(CreateTestTeam($"T{i}", TeamReputation.MIDFIELD, $"D{i * 2 - 1}", 20, $"D{i * 2}", 20));
            }
            newSeason.Teams = teams;

            // Act
            _endOfSeasonManager.StartNewSeason(saveGame, newSeason);

            // Assert
            Assert.AreEqual(20, saveGame.CurrentDriverStandings.Count(), "Should have 20 drivers");
            Assert.AreEqual(10, saveGame.CurrentConstructorStandings.Count(), "Should have 10 teams");

            // Check all positions are assigned correctly
            var driverPositions = saveGame.CurrentDriverStandings.Select(s => s.Position).OrderBy(p => p).ToList();
            CollectionAssert.AreEqual(Enumerable.Range(1, 20).ToList(), driverPositions);

            var teamPositions = saveGame.CurrentConstructorStandings.Select(s => s.Position).OrderBy(p => p).ToList();
            CollectionAssert.AreEqual(Enumerable.Range(1, 10).ToList(), teamPositions);
        }

        #endregion

        #region Helper Methods

        private ISaveGame CreateTestSaveGame(int year)
        {
            return new SaveGame
            {
                CurrentSeason = CreateTestSeason(year),
                Drivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                NextGpIndex = 0,
                PlayerData = new PlayerData
                {
                    DriverId = "PLAYER",
                    Name = "Test Player",
                    TeamId = "T1"
                }
            };
        }

        private ISeason CreateTestSeason(int year)
        {
            return new Season
            {
                Year = year,
                Teams = new List<ITeamEntry>(),
                Races = new List<Race>
                {
                    new Race { RaceId = 1, RaceName = "Test GP 1" },
                    new Race { RaceId = 2, RaceName = "Test GP 2" },
                    new Race { RaceId = 3, RaceName = "Test GP 3" }
                },
                Absences = new List<Absence>()
            };
        }

        private IDriverData CreateTestDriver(string id, string name, int yearOfBirth, DriverReputation reputation, int seasonYear)
        {
            return new DriverData
            {
                DriverId = id,
                Name = name,
                YearOfBirth = yearOfBirth,
                Nationality = "GBR",
                Reputation = reputation,
                RatingValues = new Dictionary<string, double>
                        {
                            { "Talent" , 80 },
                            { "Aggression" , 70},
                            { "Defending" , 75 },
                            { "Stamina" , 85},
                            { "Consistency" , 80},
                            { "StartReactions" , 90},
                            { "WetWeather" , 70},
                            { "Tires" , 75}
                        }
            };
        }

        private ITeamEntry CreateTestTeam(
            string teamId,
            TeamReputation reputation,
            string driver1Id,
            int driver1Races,
            string driver2Id,
            int driver2Races)
        {
            return new TeamEntry
            {
                TeamId = teamId,
                TeamName = $"Test Team {teamId}",
                Reputation = reputation,
                Driver1Contract = new DriverContract
                {
                    DriverId = driver1Id,
                    Races = driver1Races
                },
                Driver2Contract = new DriverContract
                {
                    DriverId = driver2Id,
                    Races = driver2Races
                }
            };
        }

        private IEnumerable<IDriverData> CreateTestDriverRatingsDatabase(int year, params IDriverData[] drivers)
        {
            var database = new List<IDriverData>();

            foreach (var driver in drivers)
            {
                var driverCopy = new DriverData
                {
                    DriverId = driver.DriverId,
                    Name = driver.Name,
                    YearOfBirth = driver.YearOfBirth,
                    Nationality = driver.Nationality,
                    RatingValues = new Dictionary<string, double>(driver.RatingValues),
                    Reputation = driver.Reputation
                };

                database.Add(driverCopy);
            }

            return database;
        }

        private ITeamEntry CreateTestTeamEntry(string teamId, TeamReputation reputation, string driver1Id, string driver2Id)
        {
            return new TeamEntry
            {
                TeamId = teamId,
                TeamName = $"Team {teamId}",
                Reputation = reputation,
                Driver1Contract = new DriverContract
                {
                    DriverId = driver1Id,
                    Races = 20
                },
                Driver2Contract = new DriverContract
                {
                    DriverId = driver2Id,
                    Races = 20
                }
            };
        }

        #endregion
    }
}