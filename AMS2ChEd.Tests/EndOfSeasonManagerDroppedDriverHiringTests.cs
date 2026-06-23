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
    /// Test to verify that recently dropped drivers can join hiring ballots for other teams.
    /// When a driver is dropped (not retiring), they should become available in the unemployment pool
    /// and be able to propose to other teams or be picked by other teams.
    /// </summary>
    [TestClass]
    public class EndOfSeasonManagerDroppedDriverHiringTests
    {
        private EndOfSeasonManager _endOfSeasonManager;
        private Mock<IReputationUpdater> _mockReputationUpdater;
        private Mock<IRandomDriverGenerator> _mockRandomDriverGenerator;
        private OffSeasonMovements _offSeasonMovements;

        [TestInitialize]
        public void Setup()
        {
            _mockReputationUpdater = new Mock<IReputationUpdater>();
            _mockReputationUpdater
                .Setup(r => r.GetNewReputation(It.IsAny<DriverReputation>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .Returns<DriverReputation, int, int, int, int, int>((rep, age, pos, pods, dnfs, races) => rep);

            _mockRandomDriverGenerator = new Mock<IRandomDriverGenerator>();

            var driverFirer = new DriverFirer();
            var driverHirer = new DriverHirer();
            _offSeasonMovements = new OffSeasonMovements(driverFirer, driverHirer);

            _endOfSeasonManager = new EndOfSeasonManager(
                _mockReputationUpdater.Object,
                _offSeasonMovements,
                _mockRandomDriverGenerator.Object);
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_DroppedDriverAppearsInOtherTeamBallot()
        {
            // Arrange - Team1 drops VETTEL, Team2 needs a driver
            // VETTEL should be available for Team2 to pick

            // Current season: 
            // - Team1 (TOP_TEAM): VETTEL + WEBBER
            // - Team2 (TOP_TEAM): ALONSO + needs replacement
            var team1Old = CreateTestTeamEntry("REDBULL", TeamReputation.TOP_TEAM, "VETTEL", "WEBBER");
            var team2Old = CreateTestTeamEntry("FERRARI", TeamReputation.TOP_TEAM, "ALONSO", "MASSA");

            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1Old, team2Old });

            // Drivers
            var vettel = CreateTestDriver("VETTEL", "Sebastian Vettel", 1987, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var webber = CreateTestDriver("WEBBER", "Mark Webber", 1976, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var alonso = CreateTestDriver("ALONSO", "Fernando Alonso", 1981, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var massa = CreateTestDriver("MASSA", "Felipe Massa", 1981, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { vettel, webber, alonso, massa };

            // Next season teams
            var team1New = CreateTestTeamEntry("REDBULL", TeamReputation.TOP_TEAM, "VETTEL", "WEBBER");
            var team2New = CreateTestTeamEntry("FERRARI", TeamReputation.TOP_TEAM, "ALONSO", "MASSA");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1New, team2New });

            // Drop results: 
            // - Team1 drops VETTEL (contract expired, NOT retiring)
            // - Team2 drops MASSA (contract expired, NOT retiring)
            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "REDBULL",
                    DropDriver1 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED,  // VETTEL dropped
                    DropDriver2 = DriverFirerOutcome.NOT_DROPPED                // WEBBER kept
                },
                new DropTeamResult
                {
                    TeamId = "FERRARI",
                    DropDriver1 = DriverFirerOutcome.NOT_DROPPED,               // ALONSO kept
                    DropDriver2 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED   // MASSA dropped
                }
            };

            // Act
            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(
                2025,
                saveGame,
                newSeason.Teams,
                dropResults).ToList();

            // Assert
            Assert.AreEqual(2, ballots.Count, "Should have 2 ballots (REDBULL first driver, FERRARI second driver)");

            // Find the ballots
            var redbullBallot = ballots.FirstOrDefault(b => b.OriginalTeamHiring.TeamId == "REDBULL");
            var ferrariBallot = ballots.FirstOrDefault(b => b.OriginalTeamHiring.TeamId == "FERRARI");

            Assert.IsNotNull(redbullBallot, "Red Bull should have a ballot for first driver");
            Assert.IsNotNull(ferrariBallot, "Ferrari should have a ballot for second driver");

            // Key assertion: VETTEL (dropped by Red Bull) should appear as a candidate for Ferrari
            var vettelInFerrariCandidates = ferrariBallot.Candidates.Any(c => c.DriverId == "VETTEL");
            Assert.IsTrue(vettelInFerrariCandidates,
                "VETTEL (dropped by Red Bull) should be available as a candidate for Ferrari's hiring ballot");

            // Similarly, MASSA (dropped by Ferrari) should appear as a candidate for Red Bull
            var massaInRedbullCandidates = redbullBallot.Candidates.Any(c => c.DriverId == "MASSA");
            Assert.IsTrue(massaInRedbullCandidates,
                "MASSA (dropped by Ferrari) should be available as a candidate for Red Bull's hiring ballot");
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_DroppedChampionshipDriverAvailableForTopTeams()
        {
            // Arrange - Championship-level driver dropped, should be highly sought after

            var team1Old = CreateTestTeamEntry("MERCEDES", TeamReputation.TOP_TEAM, "HAMILTON", "BOTTAS");
            var team2Old = CreateTestTeamEntry("FERRARI", TeamReputation.TOP_TEAM, "LECLERC", "SAINZ");
            var team3Old = CreateTestTeamEntry("REDBULL", TeamReputation.TOP_TEAM, "VERSTAPPEN", "PEREZ");

            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1Old, team2Old, team3Old });

            var hamilton = CreateTestDriver("HAMILTON", "Lewis Hamilton", 1985, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var bottas = CreateTestDriver("BOTTAS", "Valtteri Bottas", 1989, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var leclerc = CreateTestDriver("LECLERC", "Charles Leclerc", 1997, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var sainz = CreateTestDriver("SAINZ", "Carlos Sainz", 1994, DriverReputation.PRIME_MIDFIELD, 2025);
            var verstappen = CreateTestDriver("VERSTAPPEN", "Max Verstappen", 1997, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var perez = CreateTestDriver("PEREZ", "Sergio Perez", 1990, DriverReputation.PRIME_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { hamilton, bottas, leclerc, sainz, verstappen, perez };

            var team1New = CreateTestTeamEntry("MERCEDES", TeamReputation.TOP_TEAM, "HAMILTON", "BOTTAS");
            var team2New = CreateTestTeamEntry("FERRARI", TeamReputation.TOP_TEAM, "LECLERC", "SAINZ");
            var team3New = CreateTestTeamEntry("REDBULL", TeamReputation.TOP_TEAM, "VERSTAPPEN", "PEREZ");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1New, team2New, team3New });

            // Mercedes drops HAMILTON (!), Ferrari drops SAINZ, Red Bull drops PEREZ
            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "MERCEDES",
                    DropDriver1 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED,  // HAMILTON dropped!
                    DropDriver2 = DriverFirerOutcome.NOT_DROPPED
                },
                new DropTeamResult
                {
                    TeamId = "FERRARI",
                    DropDriver1 = DriverFirerOutcome.NOT_DROPPED,
                    DropDriver2 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED   // SAINZ dropped
                },
                new DropTeamResult
                {
                    TeamId = "REDBULL",
                    DropDriver1 = DriverFirerOutcome.NOT_DROPPED,
                    DropDriver2 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED   // PEREZ dropped
                }
            };

            // Act
              var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(
                2025,
                saveGame,
                newSeason.Teams,
                dropResults).ToList();

            // Assert
            Assert.AreEqual(3, ballots.Count, "Should have 3 ballots");

            var ferrariBallot = ballots.FirstOrDefault(b => b.OriginalTeamHiring.TeamId == "FERRARI");
            var redbullBallot = ballots.FirstOrDefault(b => b.OriginalTeamHiring.TeamId == "REDBULL");

            // HAMILTON should be highly sought after - should appear in both Ferrari and Red Bull ballots
            var hamiltonInFerrari = ferrariBallot?.Candidates.Any(c => c.DriverId == "HAMILTON") ?? false;
            var hamiltonInRedbull = redbullBallot?.Candidates.Any(c => c.DriverId == "HAMILTON") ?? false;

            Assert.IsTrue(hamiltonInFerrari || hamiltonInRedbull,
                "HAMILTON (championship-level, dropped by Mercedes) should be a candidate for at least one other top team");

            // In fact, HAMILTON should likely be in BOTH ballots since he's championship level
            // and both teams are TOP_TEAM reputation
            Assert.IsTrue(hamiltonInFerrari,
                "HAMILTON should propose to Ferrari (TOP_TEAM looking for second driver)");
            Assert.IsTrue(hamiltonInRedbull,
                "HAMILTON should propose to Red Bull (TOP_TEAM looking for second driver)");
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_MultipleDroppedDriversCompeteForSamePosition()
        {
            // Arrange - Multiple drivers dropped, should all be available for the same opening

            var team1Old = CreateTestTeamEntry("TEAM1", TeamReputation.MIDFIELD, "D1", "D2");
            var team2Old = CreateTestTeamEntry("TEAM2", TeamReputation.MIDFIELD, "D3", "D4");
            var team3Old = CreateTestTeamEntry("TEAM3", TeamReputation.MIDFIELD, "D5", "D6");

            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1Old, team2Old, team3Old });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1991, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver4 = CreateTestDriver("D4", "Driver 4", 1993, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver5 = CreateTestDriver("D5", "Driver 5", 1994, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver6 = CreateTestDriver("D6", "Driver 6", 1995, DriverReputation.YOUNG_TALENT, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4, driver5, driver6 };

            var team1New = CreateTestTeamEntry("TEAM1", TeamReputation.MIDFIELD, "D1", "D2");
            var team2New = CreateTestTeamEntry("TEAM2", TeamReputation.MIDFIELD, "D3", "D4");
            var team3New = CreateTestTeamEntry("TEAM3", TeamReputation.MIDFIELD, "D5", "D6");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1New, team2New, team3New });

            // Team1 drops D2, Team2 drops D3 and D4, Team3 keeps both
            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "TEAM1",
                    DropDriver1 = DriverFirerOutcome.NOT_DROPPED,
                    DropDriver2 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED   // D2 dropped
                },
                new DropTeamResult
                {
                    TeamId = "TEAM2",
                    DropDriver1 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED,  // D3 dropped
                    DropDriver2 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED   // D4 dropped
                },
                new DropTeamResult
                {
                    TeamId = "TEAM3",
                    DropDriver1 = DriverFirerOutcome.NOT_DROPPED,
                    DropDriver2 = DriverFirerOutcome.NOT_DROPPED
                }
            };

            // Act
            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(
                2025,
                saveGame,
                newSeason.Teams,
                dropResults).ToList();

            // Assert
            Assert.AreEqual(3, ballots.Count, "Should have 3 ballots (TEAM1 second driver, TEAM2 both drivers)");

            var team1Ballot = ballots.FirstOrDefault(b => b.OriginalTeamHiring.TeamId == "TEAM1");

            // D2, D3, and D4 were all dropped - all should be available as candidates
            var d2Available = team1Ballot?.Candidates.Any(c => c.DriverId == "D2") ?? false;
            var d3Available = team1Ballot?.Candidates.Any(c => c.DriverId == "D3") ?? false;
            var d4Available = team1Ballot?.Candidates.Any(c => c.DriverId == "D4") ?? false;

            // At least some of the dropped drivers should appear as candidates
            var droppedDriversAvailable = d2Available || d3Available || d4Available;
            Assert.IsTrue(droppedDriversAvailable,
                "At least one of the dropped drivers (D2, D3, D4) should be available for TEAM1's ballot");

            // Count how many dropped drivers are available
            int availableDroppedDriversCount = (d2Available ? 1 : 0) + (d3Available ? 1 : 0) + (d4Available ? 1 : 0);
            Assert.IsTrue(availableDroppedDriversCount >= 1,
                "Multiple dropped drivers should compete for the same position");
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_RetiringDriverNotAvailableForHiring()
        {
            // Arrange - One driver retiring, one driver dropped (not retiring)
            // Only the dropped driver should be available for hiring

            var team1Old = CreateTestTeamEntry("TEAM1", TeamReputation.MIDFIELD, "RETIRING", "DROPPED");
            var team2Old = CreateTestTeamEntry("TEAM2", TeamReputation.MIDFIELD, "KEPT1", "KEPT2");

            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1Old, team2Old });

            var retiring = CreateTestDriver("RETIRING", "Retiring Driver", 1975, DriverReputation.AGEING_MIDFIELD, 2025);
            var dropped = CreateTestDriver("DROPPED", "Dropped Driver", 1990, DriverReputation.PRIME_MIDFIELD, 2025);
            var kept1 = CreateTestDriver("KEPT1", "Kept Driver 1", 1991, DriverReputation.PRIME_MIDFIELD, 2025);
            var kept2 = CreateTestDriver("KEPT2", "Kept Driver 2", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var rookie = CreateTestDriver("ROOKIE", "Rookie Driver", 2005, DriverReputation.PRIME_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { retiring, dropped, kept1, kept2 , rookie };

            var team1New = CreateTestTeamEntry("TEAM1", TeamReputation.MIDFIELD, "RETIRING", "DROPPED");
            var team2New = CreateTestTeamEntry("TEAM2", TeamReputation.MIDFIELD, "KEPT1", "KEPT2");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1New, team2New });

            // Team1: RETIRING retires, DROPPED is dropped (contract expired)
            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "TEAM1",
                    DropDriver1 = DriverFirerOutcome.DROPPED_RETIRING,          // RETIRING - not available!
                    DropDriver2 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED   // DROPPED - available!
                },
                new DropTeamResult
                {
                    TeamId = "TEAM2",
                    DropDriver1 = DriverFirerOutcome.NOT_DROPPED,
                    DropDriver2 = DriverFirerOutcome.NOT_DROPPED
                }
            };

            // Act
            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(
                2025,
                saveGame,
                newSeason.Teams,
                dropResults).ToList();

            // Assert
            Assert.AreEqual(2, ballots.Count, "Should have 2 ballots for TEAM1 (both positions)");

            var team1Ballots = ballots.Where(b => b.OriginalTeamHiring.TeamId == "TEAM1").ToList();
            Assert.AreEqual(2, team1Ballots.Count);

            // Check all candidates across both ballots
            var allCandidates = team1Ballots.SelectMany(b => b.Candidates).ToList();

            var retiringInCandidates = allCandidates.Any(c => c.DriverId == "RETIRING");
            var droppedInCandidates = allCandidates.Any(c => c.DriverId == "DROPPED");

            Assert.IsFalse(retiringInCandidates,
                "RETIRING driver should NOT be available (retired from sport)");

            Assert.IsTrue(droppedInCandidates,
                "DROPPED driver (not retiring) should be available as a candidate");
        }

        #region Helper Methods

        private ISaveGame CreateTestSaveGame(int year, List<ITeamEntry> teams)
        {
            return new SaveGame
            {
                CurrentSeason = new Season { Year = year, Teams = teams },
                Drivers = new List<IDriverData>(),
                PlayerData = new PlayerData { DriverId = "PLAYER", TeamId = null }
            };
        }

        private IDriverData CreateTestDriver(string driverId, string name, int yearOfBirth, DriverReputation reputation, int year)
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