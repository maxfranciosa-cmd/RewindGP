using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AMS2ChEd.Business.Tests.GameLogic
{
    [TestClass]
    public class EndOfSeasonManagerGeneratedDriversTests
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

        #region TeamPicksPotentialReplacementsDrivers - Basic Functionality

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_WithUnfilledSeats_GeneratesNewDrivers()
        {
            // Arrange
            var newSeasonYear = 1996;
            var saveGame = CreateSaveGame(newSeasonYear);
            var originalDriverCount = saveGame.Drivers.Count();
            var newSeasonTeamEntries = CreateTeamEntries();
            var dropResults = CreateDropResults("ferrari", dropsDriver1: true, dropsDriver2: false);

            var unfilledJobAd = new TeamJobAd
            {
                TeamId = "ferrari",
                TeamReputation = TeamReputation.TOP_TEAM,
                Role = DriverRole.FIRST_DRIVER,
                ExitingDriverId = "driver1",
                ExitingDriverWillingToRenew = false
            };

            var generatedDriver = CreateDriver("newdriver", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, newSeasonYear);

            // Setup: PickPotentialNewDrivers returns empty list with one unfilled ad
            _mockOffSeasonMovements
                .Setup(x => x.PickPotentialNewDrivers(
                    It.IsAny<IEnumerable<TeamJobAd>>(),
                    It.IsAny<IEnumerable<UnemployedDriver>>(),
                    out It.Ref<List<TeamJobAd>>.IsAny))
                .Returns((IEnumerable<TeamJobAd> ads, IEnumerable<UnemployedDriver> drivers, out List<TeamJobAd> unfilled) =>
                {
                    unfilled = new List<TeamJobAd> { unfilledJobAd };
                    return new List<TeamHiring>();
                });

            // Setup: DriversProposeToTeams returns ballots
            _mockOffSeasonMovements
                .Setup(x => x.DriversProposeToTeams(
                    It.IsAny<IEnumerable<UnemployedDriver>>(),
                    It.IsAny<IEnumerable<TeamHiring>>()))
                .Returns((IEnumerable<UnemployedDriver> drivers, IEnumerable<TeamHiring> hirings) =>
                {
                    return hirings.Select(h => new TeamHiringBallot
                    {
                        OriginalTeamHiring = h,
                        Candidates = new List<TeamHiringBallotCandidate>()
                    });
                });

            // Setup: GenerateDriver returns the new driver
            _mockRandomDriverGenerator
                .Setup(x => x.GenerateDriver(It.IsAny<IEnumerable<IDriverData>>(), It.IsAny<int>()))
                .Returns(generatedDriver);

            // Act
            var result = _sut.TeamPicksPotentialReplacementsDrivers(newSeasonYear, saveGame, newSeasonTeamEntries, dropResults);

            // Assert
            var ballots = result.ToList();
            Assert.AreEqual(1, ballots.Count, "Should have one ballot for the generated driver");

            var hiring = ballots.First().OriginalTeamHiring;
            Assert.AreEqual("newdriver", hiring.DriverId, "Should hire the generated driver");
            Assert.AreEqual("ferrari", hiring.TeamId, "Should be for Ferrari");
            Assert.AreEqual(DriverRole.FIRST_DRIVER, hiring.Role, "Should be first driver role");

            // Verify the generated driver was added to the save game
            Assert.AreEqual(originalDriverCount + 1, saveGame.Drivers.Count(), "Should have one more driver");
            Assert.IsTrue(saveGame.Drivers.Any(d => d.DriverId == "newdriver"), "New driver should be in save game");
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_WithMultipleUnfilledSeats_GeneratesMultipleDrivers()
        {
            // Arrange
            var newSeasonYear = 1996;
            var saveGame = CreateSaveGame(newSeasonYear);
            var originalDriverCount = saveGame.Drivers.Count();
            var newSeasonTeamEntries = CreateTeamEntries();
            var dropResults = CreateDropResults("ferrari", dropsDriver1: true, dropsDriver2: true);

            var unfilledJobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "ferrari",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = "driver1",
                    ExitingDriverWillingToRenew = false
                },
                new TeamJobAd
                {
                    TeamId = "ferrari",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.SECOND_DRIVER,
                    ExitingDriverId = "driver2",
                    ExitingDriverWillingToRenew = false
                }
            };

            var generatedDriver1 = CreateDriver("rookie1", DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, newSeasonYear);
            var generatedDriver2 = CreateDriver("rookie2", DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, newSeasonYear);

            // Setup mocks
            _mockOffSeasonMovements
                .Setup(x => x.PickPotentialNewDrivers(
                    It.IsAny<IEnumerable<TeamJobAd>>(),
                    It.IsAny<IEnumerable<UnemployedDriver>>(),
                    out It.Ref<List<TeamJobAd>>.IsAny))
                .Returns((IEnumerable<TeamJobAd> ads, IEnumerable<UnemployedDriver> drivers, out List<TeamJobAd> unfilled) =>
                {
                    unfilled = unfilledJobAds;
                    return new List<TeamHiring>();
                });

            _mockOffSeasonMovements
                .Setup(x => x.DriversProposeToTeams(
                    It.IsAny<IEnumerable<UnemployedDriver>>(),
                    It.IsAny<IEnumerable<TeamHiring>>()))
                .Returns((IEnumerable<UnemployedDriver> drivers, IEnumerable<TeamHiring> hirings) =>
                {
                    return hirings.Select(h => new TeamHiringBallot
                    {
                        OriginalTeamHiring = h,
                        Candidates = new List<TeamHiringBallotCandidate>()
                    });
                });

            _mockRandomDriverGenerator
                .SetupSequence(x => x.GenerateDriver(It.IsAny<IEnumerable<IDriverData>>(), It.IsAny<int>()))
                .Returns(generatedDriver1)
                .Returns(generatedDriver2);

            // Act
            var result = _sut.TeamPicksPotentialReplacementsDrivers(newSeasonYear, saveGame, newSeasonTeamEntries, dropResults);

            // Assert
            var ballots = result.ToList();
            Assert.AreEqual(2, ballots.Count, "Should have two ballots");

            var hirings = ballots.Select(b => b.OriginalTeamHiring).ToList();
            Assert.IsTrue(hirings.Any(h => h.DriverId == "rookie1"), "Should hire rookie1");
            Assert.IsTrue(hirings.Any(h => h.DriverId == "rookie2"), "Should hire rookie2");

            // Verify both generated drivers were added to the save game
            Assert.AreEqual(originalDriverCount + 2, saveGame.Drivers.Count(), "Should have two more drivers");
            Assert.IsTrue(saveGame.Drivers.Any(d => d.DriverId == "rookie1"), "Rookie1 should be in save game");
            Assert.IsTrue(saveGame.Drivers.Any(d => d.DriverId == "rookie2"), "Rookie2 should be in save game");
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_WithNoUnfilledSeats_GeneratesNoDrivers()
        {
            // Arrange
            var newSeasonYear = 1996;
            var saveGame = CreateSaveGame(newSeasonYear);
            var originalDriverCount = saveGame.Drivers.Count();
            var newSeasonTeamEntries = CreateTeamEntries();
            var dropResults = CreateDropResults("ferrari", dropsDriver1: false, dropsDriver2: false);

            var normalHiring = new TeamHiring
            {
                TeamId = "ferrari",
                DriverId = "driver1",
                Role = DriverRole.FIRST_DRIVER,
                TeamReputation = TeamReputation.TOP_TEAM,
                DriverReputation = DriverReputation.PRIME_MIDFIELD
            };

            // Setup: No unfilled ads
            _mockOffSeasonMovements
                .Setup(x => x.PickPotentialNewDrivers(
                    It.IsAny<IEnumerable<TeamJobAd>>(),
                    It.IsAny<IEnumerable<UnemployedDriver>>(),
                    out It.Ref<List<TeamJobAd>>.IsAny))
                .Returns((IEnumerable<TeamJobAd> ads, IEnumerable<UnemployedDriver> drivers, out List<TeamJobAd> unfilled) =>
                {
                    unfilled = new List<TeamJobAd>(); // No unfilled ads
                    return new List<TeamHiring> { normalHiring };
                });

            _mockOffSeasonMovements
                .Setup(x => x.DriversProposeToTeams(
                    It.IsAny<IEnumerable<UnemployedDriver>>(),
                    It.IsAny<IEnumerable<TeamHiring>>()))
                .Returns((IEnumerable<UnemployedDriver> drivers, IEnumerable<TeamHiring> hirings) =>
                {
                    return hirings.Select(h => new TeamHiringBallot
                    {
                        OriginalTeamHiring = h,
                        Candidates = new List<TeamHiringBallotCandidate>()
                    });
                });

            // Act
            var result = _sut.TeamPicksPotentialReplacementsDrivers(newSeasonYear, saveGame, newSeasonTeamEntries, dropResults);

            // Assert
            var ballots = result.ToList();
            CollectionAssert.AllItemsAreNotNull(ballots);

            // Driver count should not change (no generated drivers)
            Assert.AreEqual(originalDriverCount, saveGame.Drivers.Count(), "Should not generate any new drivers");
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_GeneratedDriver_HasCorrectReputation()
        {
            // Arrange
            var newSeasonYear = 1996;
            var saveGame = CreateSaveGame(newSeasonYear);
            var newSeasonTeamEntries = CreateTeamEntries();
            var dropResults = CreateDropResults("ferrari", dropsDriver1: true, dropsDriver2: false);

            var unfilledJobAd = new TeamJobAd
            {
                TeamId = "ferrari",
                TeamReputation = TeamReputation.TOP_TEAM,
                Role = DriverRole.FIRST_DRIVER,
                ExitingDriverId = "driver1",
                ExitingDriverWillingToRenew = false
            };

            var generatedDriver = CreateDriver("paydriver", DriverReputation.PAY_DRIVER_SEASON, newSeasonYear);

            // Setup mocks
            _mockOffSeasonMovements
                .Setup(x => x.PickPotentialNewDrivers(
                    It.IsAny<IEnumerable<TeamJobAd>>(),
                    It.IsAny<IEnumerable<UnemployedDriver>>(),
                    out It.Ref<List<TeamJobAd>>.IsAny))
                .Returns((IEnumerable<TeamJobAd> ads, IEnumerable<UnemployedDriver> drivers, out List<TeamJobAd> unfilled) =>
                {
                    unfilled = new List<TeamJobAd> { unfilledJobAd };
                    return new List<TeamHiring>();
                });

            _mockOffSeasonMovements
                .Setup(x => x.DriversProposeToTeams(
                    It.IsAny<IEnumerable<UnemployedDriver>>(),
                    It.IsAny<IEnumerable<TeamHiring>>()))
                .Returns((IEnumerable<UnemployedDriver> drivers, IEnumerable<TeamHiring> hirings) =>
                {
                    return hirings.Select(h => new TeamHiringBallot
                    {
                        OriginalTeamHiring = h,
                        Candidates = new List<TeamHiringBallotCandidate>()
                    });
                });

            _mockRandomDriverGenerator
                .Setup(x => x.GenerateDriver(It.IsAny<IEnumerable<IDriverData>>(), It.IsAny<int>()))
                .Returns(generatedDriver);

            // Act
            var result = _sut.TeamPicksPotentialReplacementsDrivers(newSeasonYear, saveGame, newSeasonTeamEntries, dropResults);

            // Assert
            var hiring = result.First().OriginalTeamHiring;
            Assert.AreEqual(DriverReputation.PAY_DRIVER_SEASON, hiring.DriverReputation, "Hiring should have correct reputation");

            // Verify driver in save game has matching reputation
            var savedDriver = saveGame.Drivers.First(d => d.DriverId == "paydriver");
            Assert.AreEqual(DriverReputation.PAY_DRIVER_SEASON, savedDriver.Reputation, "Saved driver should have correct reputation");
        }

        #endregion

        #region Helper Methods

        private SaveGame CreateSaveGame(int year = 1996)
        {
            var saveGame = new SaveGame
            {
                Drivers = new List<IDriverData>
                {
                    CreateDriver("driver1", DriverReputation.PRIME_MIDFIELD, year),
                    CreateDriver("driver2", DriverReputation.YOUNG_TALENT, year)
                },
                CurrentSeason = CreateSeason(year),
                PlayerData = new PlayerData { DriverId = "player_id" },
                RetiredDrivers = new List<IDriverData>()
            };

            return saveGame;
        }

        private Season CreateSeason(int year)
        {
            return new Season
            {
                Year = year,
                Teams = new List<ITeamEntry>
                {
                    CreateTeamEntry("ferrari", "driver1", "driver2")
                }
            };
        }

        private IEnumerable<ITeamEntry> CreateTeamEntries()
        {
            return new List<ITeamEntry>
            {
                CreateTeamEntry("ferrari", "driver1", "driver2", TeamReputation.TOP_TEAM)
            };
        }

        private TeamEntry CreateTeamEntry(string teamId, string driver1Id, string driver2Id, TeamReputation reputation = TeamReputation.TOP_TEAM)
        {
            return new TeamEntry
            {
                TeamId = teamId,
                Reputation = reputation,
                Driver1Contract = new DriverContract { DriverId = driver1Id, Races = 10 },
                Driver2Contract = new DriverContract { DriverId = driver2Id, Races = 10 }
            };
        }

        private DriverData CreateDriver(string driverId, DriverReputation reputation, int year = 1996)
        {
            return new DriverData
            {
                DriverId = driverId,
                Name = $"Driver {driverId}",
                YearOfBirth = 1970,
                Reputation = reputation
            };
        }

        private IEnumerable<DropTeamResult> CreateDropResults(string teamId, bool dropsDriver1, bool dropsDriver2)
        {
            return new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = teamId,
                    DropDriver1 = dropsDriver1 ? DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED : DriverFirerOutcome.NOT_DROPPED,
                    DropDriver2 = dropsDriver2 ? DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED : DriverFirerOutcome.NOT_DROPPED
                }
            };
        }

        #endregion
    }
}