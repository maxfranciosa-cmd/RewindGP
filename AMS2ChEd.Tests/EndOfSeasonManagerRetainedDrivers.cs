using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    /// <summary>
    /// Tests for retained driver overwrite behavior in GenerateNewSeasonWithNewHirings.
    /// When drivers are retained (not replaced) from current season, they should OVERWRITE
    /// the placeholder drivers in the next season's team entry.
    /// 
    /// Example:
    /// Current Season - Team1: driver1 + driver2
    /// Hiring: driver3 replaces driver2 (only driver2 has ballot)
    /// Next Season Definition: Team1: driver4 + driver5 (placeholders)
    /// 
    /// Expected Result: Team1: driver1 + driver3
    /// - driver1 (retained) should OVERWRITE driver4
    /// - driver3 (hired) should OVERWRITE driver5
    /// </summary>
    [TestClass]
    public class EndOfSeasonManagerRetainedDriverTests
    {
        private EndOfSeasonManager _endOfSeasonManager;
        private Mock<IReputationUpdater> _mockReputationUpdater;
        private OffSeasonMovements _offSeasonMovements;

        [TestInitialize]
        public void Setup()
        {
            _mockReputationUpdater = new Mock<IReputationUpdater>();
            _mockReputationUpdater
                .Setup(r => r.GetNewReputation(It.IsAny<DriverReputation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<DriverReputation, int, int, int, int, int>((rep, age, pos, pods, dnfs, races) => rep);

            var driverFirer = new DriverFirer();
            var driverHirer = new DriverHirer();
            _offSeasonMovements = new OffSeasonMovements(driverFirer, driverHirer);

            _endOfSeasonManager = new EndOfSeasonManager(
                _mockReputationUpdater.Object,
                _offSeasonMovements,
                new Mock<IRandomDriverGenerator>().Object);
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_RetainedDriverOverwritesNewSeasonPlaceholder()
        {
            // Arrange
            // Current Season: Team1 has driver1 (FIRST) + driver2 (SECOND)
            var currentTeam = CreateTestTeamEntry("TEAM1", TeamReputation.TOP_TEAM, "driver1", "driver2");
            var saveGame = CreateTestSaveGame(2024);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { currentTeam };

            var driver1 = CreateTestDriver("driver1", "Driver One", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL);
            var driver2 = CreateTestDriver("driver2", "Driver Two", 1992, DriverReputation.PRIME_MIDFIELD);
            var driver3 = CreateTestDriver("driver3", "Driver Three", 1995, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL);
            var driver4 = CreateTestDriver("driver4", "Placeholder Four", 1985, DriverReputation.PAY_DRIVER_SEASON);
            var driver5 = CreateTestDriver("driver5", "Placeholder Five", 1986, DriverReputation.PAY_DRIVER_SEASON);
            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4, driver5 };

            // Next Season Definition: Team1 has driver4 + driver5 (placeholders)
            var newSeasonTeam = CreateTestTeamEntry("TEAM1", TeamReputation.TOP_TEAM, "driver4", "driver5");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { newSeasonTeam });

            // Hiring ballot: Only driver2 is replaced by driver3 (SECOND_DRIVER role)
            // driver1 should be retained (no ballot for FIRST_DRIVER)
            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "TEAM1",
                        DriverId = "driver3",
                        Role = DriverRole.SECOND_DRIVER, // Only replacing second driver
                        DriverReputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL,
                        TeamReputation = TeamReputation.TOP_TEAM
                    },
                    Candidates = new List<TeamHiringBallotCandidate>()
                }
            };

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(
                saveGame,
                newSeason,
                ballots);

            // Assert
            var updatedTeam = resultSeason.Teams.First(t => t.TeamId == "TEAM1");

            Assert.AreEqual("driver1", updatedTeam.Driver1Contract.DriverId,
                "driver1 (retained from current season) should OVERWRITE driver4 placeholder");

            Assert.AreEqual("driver3", updatedTeam.Driver2Contract.DriverId,
                "driver3 (hired) should OVERWRITE driver5 placeholder");

            Assert.AreNotEqual("driver4", updatedTeam.Driver1Contract.DriverId,
                "driver4 placeholder should NOT remain in first driver position");

            Assert.AreNotEqual("driver5", updatedTeam.Driver2Contract.DriverId,
                "driver5 placeholder should NOT remain in second driver position");

            Assert.AreNotEqual("driver2", updatedTeam.Driver2Contract.DriverId,
                "driver2 should be replaced (not retained)");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_BothDriversRetained_BothOverwritePlaceholders()
        {
            // Arrange - Both drivers retained, no hiring
            // Current Season: Team1 has HAMILTON + RUSSELL
            var currentTeam = CreateTestTeamEntry("MERCEDES", TeamReputation.TOP_TEAM, "HAMILTON", "RUSSELL");
            var saveGame = CreateTestSaveGame(2024);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { currentTeam };

            var hamilton = CreateTestDriver("HAMILTON", "Lewis Hamilton", 1985, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL);
            var russell = CreateTestDriver("RUSSELL", "George Russell", 1998, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL);
            var tbd1 = CreateTestDriver("TBD1", "TBD One", 1990, DriverReputation.PAY_DRIVER_SEASON);
            var tbd2 = CreateTestDriver("TBD2", "TBD Two", 1992, DriverReputation.PAY_DRIVER_SEASON);
            saveGame.Drivers = new List<IDriverData> { hamilton, russell, tbd1, tbd2 };

            // Next Season Definition: Team1 has TBD1 + TBD2 (placeholders)
            var newSeasonTeam = CreateTestTeamEntry("MERCEDES", TeamReputation.TOP_TEAM, "TBD1", "TBD2");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { newSeasonTeam });

            // No hiring ballots - both drivers retained
            var ballots = new List<TeamHiringBallot>();

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(
                saveGame,
                newSeason,
                ballots);

            // Assert
            var updatedTeam = resultSeason.Teams.First(t => t.TeamId == "MERCEDES");

            Assert.AreEqual("HAMILTON", updatedTeam.Driver1Contract.DriverId,
                "HAMILTON (retained) should OVERWRITE TBD1");

            Assert.AreEqual("RUSSELL", updatedTeam.Driver2Contract.DriverId,
                "RUSSELL (retained) should OVERWRITE TBD2");

            Assert.AreNotEqual("TBD1", updatedTeam.Driver1Contract.DriverId,
                "TBD1 should be overwritten");

            Assert.AreNotEqual("TBD2", updatedTeam.Driver2Contract.DriverId,
                "TBD2 should be overwritten");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_FirstDriverRetained_SecondDriverHired()
        {
            // Arrange - First driver retained, second driver replaced
            // Current Season: Team1 has VERSTAPPEN + PEREZ
            var currentTeam = CreateTestTeamEntry("REDBULL", TeamReputation.TOP_TEAM, "VERSTAPPEN", "PEREZ");
            var saveGame = CreateTestSaveGame(2024);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { currentTeam };

            var verstappen = CreateTestDriver("VERSTAPPEN", "Max Verstappen", 1997, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL);
            var perez = CreateTestDriver("PEREZ", "Sergio Perez", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL);
            var lawson = CreateTestDriver("LAWSON", "Liam Lawson", 2002, DriverReputation.YOUNG_TALENT);
            var placeholder1 = CreateTestDriver("PLACEHOLDER1", "Placeholder", 1985, DriverReputation.PAY_DRIVER_SEASON);
            var placeholder2 = CreateTestDriver("PLACEHOLDER2", "Placeholder", 1986, DriverReputation.PAY_DRIVER_SEASON);
            saveGame.Drivers = new List<IDriverData> { verstappen, perez, lawson, placeholder1, placeholder2 };

            // Next Season Definition: Team1 has PLACEHOLDER1 + PLACEHOLDER2
            var newSeasonTeam = CreateTestTeamEntry("REDBULL", TeamReputation.TOP_TEAM, "PLACEHOLDER1", "PLACEHOLDER2");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { newSeasonTeam });

            // Only SECOND_DRIVER ballot (PEREZ replaced by LAWSON)
            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "REDBULL",
                        DriverId = "LAWSON",
                        Role = DriverRole.SECOND_DRIVER,
                        DriverReputation = DriverReputation.YOUNG_TALENT,
                        TeamReputation = TeamReputation.TOP_TEAM
                    },
                    Candidates = new List<TeamHiringBallotCandidate>()
                }
            };

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(
                saveGame,
                newSeason,
                ballots);

            // Assert
            var updatedTeam = resultSeason.Teams.First(t => t.TeamId == "REDBULL");

            Assert.AreEqual("VERSTAPPEN", updatedTeam.Driver1Contract.DriverId,
                "VERSTAPPEN (retained) should OVERWRITE PLACEHOLDER1");

            Assert.AreEqual("LAWSON", updatedTeam.Driver2Contract.DriverId,
                "LAWSON (hired) should OVERWRITE PLACEHOLDER2");

            Assert.AreNotEqual("PEREZ", updatedTeam.Driver2Contract.DriverId,
                "PEREZ should be replaced");

            Assert.AreNotEqual("PLACEHOLDER1", updatedTeam.Driver1Contract.DriverId,
                "PLACEHOLDER1 should be overwritten by retained driver");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_BothDriversReplaced_NoneRetained()
        {
            // Arrange - Both drivers replaced, none retained
            // Current Season: Team1 has OLD1 + OLD2
            var currentTeam = CreateTestTeamEntry("FERRARI", TeamReputation.TOP_TEAM, "OLD1", "OLD2");
            var saveGame = CreateTestSaveGame(2024);
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { currentTeam };

            var old1 = CreateTestDriver("OLD1", "Old Driver 1", 1985, DriverReputation.AGEING_CHAMPIONSHIP_LEVEL);
            var old2 = CreateTestDriver("OLD2", "Old Driver 2", 1986, DriverReputation.AGEING_MIDFIELD);
            var leclerc = CreateTestDriver("LECLERC", "Charles Leclerc", 1997, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL);
            var hamilton = CreateTestDriver("HAMILTON", "Lewis Hamilton", 1985, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL);
            var tbd1 = CreateTestDriver("TBD1", "TBD", 1990, DriverReputation.PAY_DRIVER_SEASON);
            var tbd2 = CreateTestDriver("TBD2", "TBD", 1992, DriverReputation.PAY_DRIVER_SEASON);
            saveGame.Drivers = new List<IDriverData> { old1, old2, leclerc, hamilton, tbd1, tbd2 };

            // Next Season Definition: Team1 has TBD1 + TBD2
            var newSeasonTeam = CreateTestTeamEntry("FERRARI", TeamReputation.TOP_TEAM, "TBD1", "TBD2");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { newSeasonTeam });

            // Both drivers replaced
            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "FERRARI",
                        DriverId = "LECLERC",
                        Role = DriverRole.FIRST_DRIVER,
                        DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL,
                        TeamReputation = TeamReputation.TOP_TEAM
                    },
                    Candidates = new List<TeamHiringBallotCandidate>()
                },
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "FERRARI",
                        DriverId = "HAMILTON",
                        Role = DriverRole.SECOND_DRIVER,
                        DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL,
                        TeamReputation = TeamReputation.TOP_TEAM
                    },
                    Candidates = new List<TeamHiringBallotCandidate>()
                }
            };

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(
                saveGame,
                newSeason,
                ballots);

            // Assert
            var updatedTeam = resultSeason.Teams.First(t => t.TeamId == "FERRARI");

            Assert.AreEqual("LECLERC", updatedTeam.Driver1Contract.DriverId,
                "LECLERC (hired) should be first driver");

            Assert.AreEqual("HAMILTON", updatedTeam.Driver2Contract.DriverId,
                "HAMILTON (hired) should be second driver");

            Assert.AreNotEqual("OLD1", updatedTeam.Driver1Contract.DriverId,
                "OLD1 should not be retained");

            Assert.AreNotEqual("OLD2", updatedTeam.Driver2Contract.DriverId,
                "OLD2 should not be retained");

            Assert.AreNotEqual("TBD1", updatedTeam.Driver1Contract.DriverId,
                "TBD1 should be overwritten");

            Assert.AreNotEqual("TBD2", updatedTeam.Driver2Contract.DriverId,
                "TBD2 should be overwritten");
        }

        #region Helper Methods

        private ISaveGame CreateTestSaveGame(int year)
        {
            return new SaveGame
            {
                CurrentSeason = new Season { Year = year, Teams = new List<ITeamEntry>(), Races = new List<Race>() },
                Drivers = new List<IDriverData>(),
                PlayerData = new PlayerData { DriverId = "PLAYER", TeamId = null }
            };
        }

        private IDriverData CreateTestDriver(string driverId, string name, int yearOfBirth, DriverReputation reputation)
        {
            return new DriverData
            {
                DriverId = driverId,
                Name = name,
                YearOfBirth = yearOfBirth,
                Reputation = reputation
            };
        }

        private ITeamEntry CreateTestTeamEntry(string teamId, TeamReputation reputation, string driver1Id, string driver2Id)
        {
            return new TeamEntry
            {
                TeamId = teamId,
                Reputation = reputation,
                Driver1Contract = new DriverContract { DriverId = driver1Id, Races = 20 },
                Driver2Contract = new DriverContract { DriverId = driver2Id, Races = 20 }
            };
        }

        private ISeason CreateTestSeason(int year, List<ITeamEntry> teams)
        {
            return new Season
            {
                Year = year,
                Teams = teams,
                Races = new List<Race>
                {
                    new Race { RaceId = 1, RaceName = "Race 1" }
                },
                Absences = new List<Absence>()
            };
        }

        #endregion
    }
}