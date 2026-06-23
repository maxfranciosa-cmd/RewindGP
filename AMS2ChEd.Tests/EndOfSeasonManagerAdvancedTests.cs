using AMS2ChEd.Business.GameLogic.Concrete;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Linq;
using static AMS2ChEd.Business.Services.OffSeasonMovements;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class EndOfSeasonManagerAdvancedTests
    {
        private EndOfSeasonManager _endOfSeasonManager;
        private DriverFirer _driverFirer;
        private DriverHirer _driverHirer;
        private OffSeasonMovements _offSeasonMovements;
        private Mock<IRandomDriverGenerator> _mockRandomDriverGenerator;
        [TestInitialize]
        public void Setup()
        {
            _driverFirer = new DriverFirer();
            _driverHirer = new DriverHirer();
            _offSeasonMovements = new OffSeasonMovements(_driverFirer, _driverHirer);
            _mockRandomDriverGenerator = new Mock<IRandomDriverGenerator>();
            var reputationUpdater = new ReputationUpdater();
            _endOfSeasonManager = new EndOfSeasonManager(reputationUpdater, _offSeasonMovements, _mockRandomDriverGenerator.Object);
        }

        #region TeamPicksPotentialReplacementsDrivers Tests

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_WithOneDroppedDriver_ShouldCreateBallotForThatPosition()
        {
            // Arrange
            var teamOldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "D2");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamOldEntry });
            var driver1 = CreateTestDriver("D1", "Dropped Driver", 1995, DriverReputation.YOUNG_TALENT, 2025);
            var driver2 = CreateTestDriver("D2", "Kept Driver", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var replacementDriver = CreateTestDriver("D3", "Replacement", 1992, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, replacementDriver };

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "D2");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "T1",
                    DropDriver1 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED,
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
            Assert.AreEqual(1, ballots.Count, "Should create one ballot for the dropped driver position");
            Assert.AreEqual("T1", ballots[0].OriginalTeamHiring.TeamId);
            Assert.AreEqual(DriverRole.FIRST_DRIVER, ballots[0].OriginalTeamHiring.Role);
            Assert.IsNotNull(ballots[0].OriginalTeamHiring.DriverId, "Team should have picked a replacement");
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_WithBothDriversDropped_ShouldCreateTwoBallots()
        {
            // Arrange
            var teamOldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "D2");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamOldEntry });
            var driver1 = CreateTestDriver("D1", "Dropped 1", 1995, DriverReputation.YOUNG_TALENT, 2025);
            var driver2 = CreateTestDriver("D2", "Dropped 2", 1996, DriverReputation.PAY_DRIVER_SEASON, 2025);
            var replacement1 = CreateTestDriver("D3", "Replacement 1", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var replacement2 = CreateTestDriver("D4", "Replacement 2", 1991, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, replacement1, replacement2 };

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "D2");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "T1",
                    DropDriver1 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED,
                    DropDriver2 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED
                }
            };

            // Act
            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(
                2025,
                saveGame,
                newSeason.Teams,
                dropResults).ToList();

            // Assert
            Assert.AreEqual(2, ballots.Count, "Should create two ballots for both dropped positions");

            var firstDriverBallot = ballots.FirstOrDefault(b => b.OriginalTeamHiring.Role == DriverRole.FIRST_DRIVER);
            var secondDriverBallot = ballots.FirstOrDefault(b => b.OriginalTeamHiring.Role == DriverRole.SECOND_DRIVER);

            Assert.IsNotNull(firstDriverBallot, "Should have ballot for first driver");
            Assert.IsNotNull(secondDriverBallot, "Should have ballot for second driver");
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_TopTeamPicksFromAvailablePool()
        {
            // Arrange
            var teamOldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "KEEPER");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamOldEntry });
            var droppedDriver = CreateTestDriver("D1", "Dropped", 1995, DriverReputation.YOUNG_TALENT, 2025);
            var bestDriver = CreateTestDriver("D2", "Best Available", 1990, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, 2025);
            var goodDriver = CreateTestDriver("D3", "Good Driver", 1992, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var midfieldDriver = CreateTestDriver("D4", "Midfield", 1993, DriverReputation.PRIME_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { droppedDriver, bestDriver, goodDriver, midfieldDriver };

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "KEEPER");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "T1",
                    DropDriver1 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED,
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
            Assert.AreEqual(1, ballots.Count);
            Assert.AreEqual("D2", ballots[0].OriginalTeamHiring.DriverId,
                "Top team should pick the best championship-level driver");
            Assert.AreEqual(DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, ballots[0].OriginalTeamHiring.DriverReputation);
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_DriversProposeToBallots()
        {
            // Arrange
            var teamEntryOld = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "KEEPER");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamEntryOld });
            var droppedDriver = CreateTestDriver("D1", "Dropped", 1995, DriverReputation.YOUNG_TALENT, 2025);
            var teamPick = CreateTestDriver("D2", "Team Pick", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var ambitiousDriver = CreateTestDriver("D3", "Ambitious", 1992, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED, 2025);

            saveGame.Drivers = new List<IDriverData> { droppedDriver, teamPick, ambitiousDriver };

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "KEEPER");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "T1",
                    DropDriver1 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED,
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
            Assert.AreEqual(1, ballots.Count);
            Assert.IsTrue(ballots[0].Candidates.Any(), "Should have candidates who proposed");

            // Check if the ambitious driver (PRIME_CHAMPIONSHIP_LEVEL_WASHED) proposed to TOP_TEAM
            // According to ambition rules, they can propose to TOP_TEAM (max: TOP_TEAM, min: MIDFIELD)
            var ambitiousProposal = ballots[0].Candidates.FirstOrDefault(c => c.DriverId == "D3");
            Assert.IsNotNull(ambitiousProposal, "Ambitious driver should propose to top team within their ambition range");
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_MultipleTeamsPickInReputationOrder()
        {
            // Arrange
            var topTeamOldEntry = CreateTestTeamEntry("TOP", TeamReputation.TOP_TEAM, "DROP1", "KEEP1");
            var midfieldHighTeamOldEntry = CreateTestTeamEntry("MID", TeamReputation.MIDFIELD_HIGH, "DROP2", "KEEP2");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { topTeamOldEntry, midfieldHighTeamOldEntry });

            // Create one highly desirable driver
            var starDriver = CreateTestDriver("STAR", "Star Driver", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var goodDriver = CreateTestDriver("GOOD", "Good Driver", 1992, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { starDriver, goodDriver };

            // Two teams need drivers: TOP_TEAM and MIDFIELD_HIGH
            var topTeam = CreateTestTeamEntry("TOP", TeamReputation.TOP_TEAM, "DROP1", "KEEP1");
            var midfieldHighTeam = CreateTestTeamEntry("MID", TeamReputation.MIDFIELD_HIGH, "DROP2", "KEEP2");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { topTeam, midfieldHighTeam });

            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "TOP",
                    DropDriver1 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED,
                    DropDriver2 = DriverFirerOutcome.NOT_DROPPED
                },
                new DropTeamResult
                {
                    TeamId = "MID",
                    DropDriver1 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED,
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
            Assert.AreEqual(2, ballots.Count);

            var topTeamBallot = ballots.First(b => b.OriginalTeamHiring.TeamId == "TOP");
            var midTeamBallot = ballots.First(b => b.OriginalTeamHiring.TeamId == "MID");

            // Top team should get the star driver
            Assert.AreEqual("STAR", topTeamBallot.OriginalTeamHiring.DriverId,
                "Top team picks first and should get star driver");

            // Midfield-high team should get the remaining good driver
            Assert.AreEqual("GOOD", midTeamBallot.OriginalTeamHiring.DriverId,
                "Midfield-high team picks second and should get good driver");
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_WithNoDrops_ShouldReturnEmptyList()
        {
            // Arrange
            var teamOldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "D2");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamOldEntry });
            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1991, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2 };

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "D2");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "T1",
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
            Assert.AreEqual(0, ballots.Count, "No ballots should be created when no drivers are dropped");
        }

        [TestMethod]
        public void TeamPicksPotentialReplacementsDrivers_LowerReputationDriversDontProposeToTopTeams()
        {
            // Arrange
            var teamEntryOld = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "KEEPER");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamEntryOld });
            var droppedDriver = CreateTestDriver("D1", "Dropped", 1995, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var teamPick = CreateTestDriver("D2", "Team Pick", 1990, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, 2025);
            var midfieldDriver = CreateTestDriver("D3", "Midfield", 1992, DriverReputation.PRIME_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { droppedDriver, teamPick, midfieldDriver };

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "KEEPER");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "T1",
                    DropDriver1 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED,
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
            Assert.AreEqual(1, ballots.Count);

            // Midfield driver (PRIME_MIDFIELD) has max ambition of MIDFIELD_HIGH
            // Should NOT propose to TOP_TEAM
            var midfieldProposal = ballots[0].Candidates.FirstOrDefault(c => c.DriverId == "D3");
            Assert.IsNull(midfieldProposal,
                "Midfield driver should not propose to top team (outside ambition range)");
        }

        #endregion

        #region GenerateNewSeasonWithNewHirings Tests

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_UpdatesDriverContracts()
        {
            // Arrange
            var teamOldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD", "KEEPER");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamOldEntry });
            var oldDriver = CreateTestDriver("OLD", "Old Driver", 1995, DriverReputation.YOUNG_TALENT, 2025);
            var newDriver = CreateTestDriver("NEW", "New Driver", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);

            saveGame.Drivers = new List<IDriverData> { oldDriver, newDriver };

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD", "KEEPER");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "NEW",
                        Role = DriverRole.FIRST_DRIVER,
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
            var updatedTeam = resultSeason.Teams.First(t => t.TeamId == "T1");
            Assert.AreEqual("NEW", updatedTeam.Driver1Contract.DriverId,
                "First driver contract should be updated to new driver");
            Assert.AreEqual("KEEPER", updatedTeam.Driver2Contract.DriverId,
                "Second driver should remain unchanged");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_ChampionshipDriverGetsMultiYearContract()
        {
            // Arrange
            var teamOldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD", "KEEPER");

            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamOldEntry });
            var championDriver = CreateTestDriver("CHAMP", "Champion", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);

            saveGame.Drivers = new List<IDriverData> { championDriver };

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD", "KEEPER");

            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "CHAMP",
                        Role = DriverRole.FIRST_DRIVER,
                        DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL,
                        TeamReputation = TeamReputation.TOP_TEAM
                    },
                    Candidates = new List<TeamHiringBallotCandidate>()
                }
            };

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            var updatedTeam = resultSeason.Teams.First(t => t.TeamId == "T1");
            int expectedRaces = 3 + 1; // season races + 1 for championship level
            Assert.AreEqual(expectedRaces, updatedTeam.Driver1Contract.Races,
                "Championship-level driver should get multi-year contract (races + 1)");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_RegularDriverGetsSingleYearContract()
        {
            // Arrange
            var teamOldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD, "OLD", "KEEPER");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamOldEntry });
            var regularDriver = CreateTestDriver("REG", "Regular Driver", 1992, DriverReputation.PRIME_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { regularDriver };

            var team = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD, "OLD", "KEEPER");

            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "REG",
                        Role = DriverRole.FIRST_DRIVER,
                        DriverReputation = DriverReputation.PRIME_MIDFIELD,
                        TeamReputation = TeamReputation.MIDFIELD
                    },
                    Candidates = new List<TeamHiringBallotCandidate>()
                }
            };

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            var updatedTeam = resultSeason.Teams.First(t => t.TeamId == "T1");
            int expectedRaces = 3; // just season races
            Assert.AreEqual(expectedRaces, updatedTeam.Driver1Contract.Races,
                "Regular driver should get single-year contract (just season races)");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_BothDriverRolesUpdated()
        {
            // Arrange
            var teamOldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD1", "OLD2");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamOldEntry });
            var newDriver1 = CreateTestDriver("NEW1", "New Driver 1", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var newDriver2 = CreateTestDriver("NEW2", "New Driver 2", 1991, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, 2025);

            saveGame.Drivers = new List<IDriverData> { newDriver1, newDriver2 };

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD1", "OLD2");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "NEW1",
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
                        TeamId = "T1",
                        DriverId = "NEW2",
                        Role = DriverRole.SECOND_DRIVER,
                        DriverReputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL,
                        TeamReputation = TeamReputation.TOP_TEAM
                    },
                    Candidates = new List<TeamHiringBallotCandidate>()
                }
            };

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            var updatedTeam = resultSeason.Teams.First(t => t.TeamId == "T1");
            Assert.AreEqual("NEW1", updatedTeam.Driver1Contract.DriverId, "First driver should be updated");
            Assert.AreEqual("NEW2", updatedTeam.Driver2Contract.DriverId, "Second driver should be updated");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_PreservesRaceCalendar()
        {
            // Arrange
            var teamOldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD", "KEEPER");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamOldEntry });
            var driver = CreateTestDriver("D1", "Driver", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);

            saveGame.Drivers = new List<IDriverData> { driver };

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD", "KEEPER");

            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            var ballots = new List<TeamHiringBallot>();

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(3, resultSeason.Races.Count(), "Race calendar should be preserved");
            Assert.AreEqual("GP 1", resultSeason.Races.ElementAt(0).RaceName);
            Assert.AreEqual("GP 2", resultSeason.Races.ElementAt(1).RaceName);
            Assert.AreEqual("GP 3", resultSeason.Races.ElementAt(2).RaceName);
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_AbsenceWithEmployedDriver_IsKept()
        {
            // Arrange
            // SUB is employed in T2 in the new season.
            // SUB covers TEAM's seat in T1, and the ChainedAbsence covers SUB's own T2 seat
            // with an unemployed driver (UNEMP), which is the correct pattern for an employed DriverIn.
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "TEAM", "KEEPER1");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "SUB", "KEEPER2");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry });

            var teamDriver = CreateTestDriver("TEAM", "Team Driver", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var subDriver = CreateTestDriver("SUB", "Sub Driver", 1991, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, 2025);
            var unempDriver = CreateTestDriver("UNEMP", "Unemployed Driver", 1993, DriverReputation.PRIME_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { teamDriver, subDriver, unempDriver };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "TEAM", "KEEPER1");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "SUB", "KEEPER2");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2 });

            // SUB covers TEAM at T1, with a chained absence covering SUB's own T2 seat with UNEMP
            var absence = new Absence
            {
                RaceId = 2,
                TeamId = "T1",
                DriverOut = "TEAM",
                DriverIn = "SUB",
                ChainedAbsence = new Absence
                {
                    RaceId = 2,
                    TeamId = "T2",
                    DriverOut = "SUB",
                    DriverIn = "UNEMP",
                    ChainedAbsence = null
                }
            };
            newSeason.Absences = new List<Absence> { absence };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(1, resultSeason.Absences.Count(), "Absence with employed driver and valid chain should be kept");
            Assert.AreEqual("SUB", resultSeason.Absences.First().DriverIn);
            Assert.IsNotNull(resultSeason.Absences.First().ChainedAbsence, "ChainedAbsence should be preserved");
            Assert.AreEqual("UNEMP", resultSeason.Absences.First().ChainedAbsence.DriverIn);
        }


        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_AbsenceWithUnemployedDriver_IsKept()
        {
            // Arrange
            var teamOldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "TEAM", "KEEPER");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamOldEntry });
            var teamDriver = CreateTestDriver("TEAM", "Team Driver", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var unemployedDriver = CreateTestDriver("UNEMP", "Unemployed", 1991, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { teamDriver, unemployedDriver };

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "TEAM", "KEEPER");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            // Absence: UNEMP (unemployed) replaces TEAM at race 2
            var absence = new Absence
            {
                RaceId = 2,
                TeamId = "T1",
                DriverOut = "TEAM",
                DriverIn = "UNEMP",
                ChainedAbsence = null
            };
            newSeason.Absences = new List<Absence> { absence };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(1, resultSeason.Absences.Count(), "Absence with unemployed suitable driver should be kept");
            Assert.AreEqual("UNEMP", resultSeason.Absences.First().DriverIn);
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_AbsenceWithUnavailableDriver_IsRemoved()
        {
            // Arrange
            var teamOldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "TEAM", "KEEPER");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamOldEntry });
            var teamDriver = CreateTestDriver("TEAM", "Team Driver", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);

            saveGame.Drivers = new List<IDriverData> { teamDriver };

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "TEAM", "KEEPER");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            // Absence: NONEXISTENT driver replaces TEAM
            var absence = new Absence
            {
                RaceId = 2,
                TeamId = "T1",
                DriverOut = "TEAM",
                DriverIn = "NONEXISTENT",
                ChainedAbsence = null
            };
            newSeason.Absences = new List<Absence> { absence };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(0, resultSeason.Absences.Count(),
                "Absence with non-existent driver should be removed");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_MultipleTeams_AllUpdated()
        {
            // Arrange
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD1", "KEEP1");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "OLD2", "KEEP2");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry });
            var driver1 = CreateTestDriver("NEW1", "New 1", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var driver2 = CreateTestDriver("NEW2", "New 2", 1991, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD1", "KEEP1");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "OLD2", "KEEP2");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2 });

            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "NEW1",
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
                        TeamId = "T2",
                        DriverId = "NEW2",
                        Role = DriverRole.FIRST_DRIVER,
                        DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD,
                        TeamReputation = TeamReputation.MIDFIELD_HIGH
                    },
                    Candidates = new List<TeamHiringBallotCandidate>()
                }
            };

            // Act
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(2, resultSeason.Teams.Count());

            var updatedTeam1 = resultSeason.Teams.First(t => t.TeamId == "T1");
            var updatedTeam2 = resultSeason.Teams.First(t => t.TeamId == "T2");

            Assert.AreEqual("NEW1", updatedTeam1.Driver1Contract.DriverId);
            Assert.AreEqual("NEW2", updatedTeam2.Driver1Contract.DriverId);
        }


        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_UpdatesPlayerTeamId()
        {
            // Player driver starts unemployed
            var playerDriver = CreateTestDriver("PLAYER", "Player Driver", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var otherDriver = CreateTestDriver("OTHER", "Other Driver", 1991, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, 2025);
            var teamEntryOld = CreateTestTeamEntry("NEW_TEAM", TeamReputation.TOP_TEAM, "OLD1", "OLD2");

            // Arrange
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamEntryOld });

            saveGame.Drivers = new List<IDriverData> { playerDriver, otherDriver };
            saveGame.PlayerData.DriverId = "PLAYER";
            saveGame.PlayerData.TeamId = "OLD_TEAM"; // Player was at OLD_TEAM
            var team = CreateTestTeamEntry("NEW_TEAM", TeamReputation.TOP_TEAM, "OLD1", "OLD2");


            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            // Ballot shows player getting hired at NEW_TEAM
            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "NEW_TEAM",
                        DriverId = "PLAYER",
                        Role = DriverRole.FIRST_DRIVER,
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
            Assert.AreEqual("NEW_TEAM", saveGame.PlayerData.TeamId,
                "Player's team ID should be updated to NEW_TEAM");

            var playerTeam = resultSeason.Teams.First(t => t.TeamId == "NEW_TEAM");
            Assert.AreEqual("PLAYER", playerTeam.Driver1Contract.DriverId,
                "Player should be in the team roster");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_PlayerUnemployed_SetsTeamIdToNull()
        {
            // Player driver who won't get hired
            var playerDriver = CreateTestDriver("PLAYER", "Player Driver", 1995, DriverReputation.PAY_DRIVER_SEASON, 2025);
            var goodDriver = CreateTestDriver("GOOD", "Good Driver", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var teamEntryOld = CreateTestTeamEntry("NEW_TEAM", TeamReputation.TOP_TEAM, "OLD1", "OLD2");

            // Arrange
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamEntryOld });

            saveGame.Drivers = new List<IDriverData> { playerDriver, goodDriver };
            saveGame.PlayerData.DriverId = "PLAYER";
            saveGame.PlayerData.TeamId = "OLD_TEAM"; // Player was at OLD_TEAM

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD1", "OLD2");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });

            // Only the good driver gets hired (not the player)
            var ballots = new List<TeamHiringBallot>
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "GOOD",
                        Role = DriverRole.FIRST_DRIVER,
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
            Assert.IsNull(saveGame.PlayerData.TeamId,
                "Player's team ID should be null when unemployed");

            // Verify player is not in any team
            foreach (var teamEntry in resultSeason.Teams)
            {
                Assert.AreNotEqual("PLAYER", teamEntry.Driver1Contract.DriverId,
                    "Player should not be in any team's first driver slot");
                Assert.AreNotEqual("PLAYER", teamEntry.Driver2Contract.DriverId,
                    "Player should not be in any team's second driver slot");
            }
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void Integration_CompleteDriverMarketCycle()
        {
            // Arrange - Simulate complete off-season
            var teamEntryOld = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD", "KEEPER");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamEntryOld });

            // Drivers
            var oldDriver = CreateTestDriver("OLD", "Old Driver", 1995, DriverReputation.YOUNG_TALENT, 2025);
            var starDriver = CreateTestDriver("STAR", "Star", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var goodDriver = CreateTestDriver("GOOD", "Good", 1992, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var keeperDriver = CreateTestDriver("KEEPER", "Keeper", 1992, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { oldDriver, starDriver, goodDriver };

            // Team drops OLD driver
            var teamEntryNew = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "OLD", "KEEPER");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { teamEntryNew });

            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "T1",
                    DropDriver1 = DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED,
                    DropDriver2 = DriverFirerOutcome.NOT_DROPPED
                }
            };

            // Act - Complete flow
            // Step 1: Teams pick potential drivers
            var ballots = _endOfSeasonManager.TeamPicksPotentialReplacementsDrivers(
                2025,
                saveGame,
                newSeason.Teams,
                dropResults).ToList();

            // Step 2: Generate new season with hirings
            var resultSeason = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            var finalTeam = resultSeason.Teams.First(t => t.TeamId == "T1");
            Assert.AreEqual("STAR", finalTeam.Driver1Contract.DriverId,
                "Top team should hire the star driver");
            Assert.AreEqual("KEEPER", finalTeam.Driver2Contract.DriverId,
                "Second driver should remain unchanged");
        }

        [TestMethod]
        public void Integration_PlayerRejectsContract_CreatesOpenPosition()
        {
            // Arrange
            var teamEntryOld = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "PLAYER", "KEEPER");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { teamEntryOld });

            var playerDriver = CreateTestDriver("PLAYER", "Player", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var replacementDriver = CreateTestDriver("REPLACE", "Replacement", 1991, DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, 2025);

            saveGame.Drivers = new List<IDriverData> { playerDriver, replacementDriver };
            saveGame.PlayerData.DriverId = "PLAYER";

            var team = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "PLAYER", "KEEPER");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team });
            saveGame.CurrentSeason.Teams = new List<ITeamEntry> { team };

            // Player rejects contract
            var dropResults = new List<DropTeamResult>
            {
                new DropTeamResult
                {
                    TeamId = "T1",
                    DropDriver1 = DriverFirerOutcome.DROPPED_PLAYER_REJECTING,
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
            Assert.AreEqual(1, ballots.Count, "Should create ballot for player's rejected position");
            Assert.AreNotEqual("PLAYER", ballots[0].OriginalTeamHiring.DriverId,
                "Team should pick a different driver since player rejected");
        }

        #endregion

        #region Absence Validation Tests

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_RemovesAbsence_WhenDriverOutNotInTeam()
        {
            // Arrange
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver4 = CreateTestDriver("D4", "Driver 4", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver5 = CreateTestDriver("D5", "Driver 5", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver6 = CreateTestDriver("D6", "Driver 6", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4, driver5, driver6 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2 });

            // The key here: the absence has DriverOut = "D3" but team "T1" has D1 and D2
            // This mismatch should cause the absence to be removed
            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D3",  // This driver is NOT in T1
                    DriverIn = "D4"
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(0, result.Absences.Count(),
                "Absence should be removed when DriverOut doesn't match any driver in the team");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_KeepsAbsence_WhenDriverOutMatchesDriver1()
        {
            // Arrange
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver4 = CreateTestDriver("D4", "Driver 4", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver5 = CreateTestDriver("D5", "Driver 5", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver6 = CreateTestDriver("D6", "Driver 6", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4, driver5, driver6 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2 });

            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D1",  // Matches Driver1 in T1
                    DriverIn = "D4"    // Unemployed driver
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(1, result.Absences.Count(),
                "Absence should be kept when DriverOut matches Driver1 in the team");
            Assert.AreEqual("D1", result.Absences.First().DriverOut);
            Assert.AreEqual("D4", result.Absences.First().DriverIn);
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_KeepsAbsence_WhenDriverOutMatchesDriver2()
        {
            // Arrange
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver4 = CreateTestDriver("D4", "Driver 4", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver5 = CreateTestDriver("D5", "Driver 5", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver6 = CreateTestDriver("D6", "Driver 6", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4, driver5, driver6 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2 });

            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D2",  // Matches Driver2 in T1
                    DriverIn = "D4"    // Unemployed driver
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(1, result.Absences.Count(),
                "Absence should be kept when DriverOut matches Driver2 in the team");
            Assert.AreEqual("D2", result.Absences.First().DriverOut);
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_RemovesChainedAbsence_WhenAnyLinkHasInvalidDriverOut_AndNoAlternativeExist()
        {
            // Arrange
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver4 = CreateTestDriver("D4", "Driver 4", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver5 = CreateTestDriver("D5", "Driver 5", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver6 = CreateTestDriver("D6", "Driver 6", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4, driver5, driver6 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var team3 = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D4", "D5");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2, team3 });

            // Create a chained absence where the second link has invalid DriverOut
            var chainedAbsence = new Absence
            {
                RaceId = 1,
                TeamId = "T2",
                DriverOut = "D99",  // Invalid - not in T2
                DriverIn = "D5"
            };

            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D1",  // Valid
                    DriverIn = "D3",   // This driver is employed in T2
                    ChainedAbsence = chainedAbsence
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(0, result.Absences.Count(),
                "Entire absence chain should be removed when any link has invalid DriverOut");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_KeepsChainedAbsence_WhenAllLinksHaveValidDriverOut()
        {
            // Arrange
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver4 = CreateTestDriver("D4", "Driver 4", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver5 = CreateTestDriver("D5", "Driver 5", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver6 = CreateTestDriver("D6", "Driver 6", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4, driver5, driver6 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2 });

            // Create a valid chained absence
            var chainedAbsence = new Absence
            {
                RaceId = 1,
                TeamId = "T2",
                DriverOut = "D3",  // Valid - D3 is in T2
                DriverIn = "D5"    // Unemployed
            };

            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D1",  // Valid - D1 is in T1
                    DriverIn = "D3",   // Employed in T2
                    ChainedAbsence = chainedAbsence
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(1, result.Absences.Count(),
                "Chained absence should be kept when all links have valid DriverOut");
            Assert.IsNotNull(result.Absences.First().ChainedAbsence);
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_RemovesMultipleInvalidAbsences_ForSameRace()
        {
            // Arrange
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver4 = CreateTestDriver("D4", "Driver 4", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver5 = CreateTestDriver("D5", "Driver 5", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver6 = CreateTestDriver("D6", "Driver 6", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4, driver5, driver6 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2 });

            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D99",  // Invalid
                    DriverIn = "D4"
                },
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T2",
                    DriverOut = "D88",  // Invalid
                    DriverIn = "D5"
                },
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D1",  // Valid
                    DriverIn = "D4"
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(1, result.Absences.Count(),
                "Only valid absences should remain");
            Assert.AreEqual("D1", result.Absences.First().DriverOut);
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_BreaksChain_WhenChainedDriverInWrongTeam_AndFindsUnemployedReplacement()
        {
            // Arrange
            // Team T1: D1, D2
            // Team T2: D3, D6  
            // Team T3: D7, D8
            // Unemployed: D4, D5
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var team3OldEntry = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D7", "D8");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry, team3OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver4 = CreateTestDriver("D4", "Driver 4", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver5 = CreateTestDriver("D5", "Driver 5", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver6 = CreateTestDriver("D6", "Driver 6", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver7 = CreateTestDriver("D7", "Driver 7", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver8 = CreateTestDriver("D8", "Driver 8", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4, driver5, driver6, driver7, driver8 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var team3 = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D7", "D8");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2, team3 });

            // Original chain intention:
            // D1 out from T1, D3 in (D3 employed in T2) -> D3 out from T3 (WRONG TEAM!), D4 in
            // Expected result:
            // D1 out from T1, D4 in (or D5, whichever unemployed is suitable) - NO CHAIN
            var chainedAbsence = new Absence
            {
                RaceId = 1,
                TeamId = "T3",  // Claims D3 is leaving T3, but D3 is actually in T2!
                DriverOut = "D3",
                DriverIn = "D4"
            };

            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D1",
                    DriverIn = "D3",   // D3 is employed in T2, not T3
                    ChainedAbsence = chainedAbsence
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(1, result.Absences.Count(), "Absence should be kept with replacement driver");
            var absence = result.Absences.First();
            Assert.AreEqual("T1", absence.TeamId);
            Assert.AreEqual("D1", absence.DriverOut);
            // Should be replaced by an unemployed driver (D4 or D5)
            Assert.IsTrue(absence.DriverIn == "D4" || absence.DriverIn == "D5",
                "DriverIn should be an unemployed driver");
            Assert.IsNull(absence.ChainedAbsence, "Chain should be broken - no chained absence");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_UsesUnemployedDriver_EvenIfDoesntHaveEnoughReputation_WhenChainedDriverInWrongTeam_AndNoOtherUnemployedReplacementExist()
        {
            // Arrange
            // Same setup but NO unemployed drivers available for T1
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "D2");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var team3OldEntry = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D7", "D8");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry, team3OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver6 = CreateTestDriver("D6", "Driver 6", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver7 = CreateTestDriver("D7", "Driver 7", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver8 = CreateTestDriver("D8", "Driver 8", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            // D4 has too low reputation for TOP_TEAM
            var driver4 = CreateTestDriver("D4", "Driver 4", 1992, DriverReputation.PAY_DRIVER_SEASON, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4, driver6, driver7, driver8 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "D2");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var team3 = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D7", "D8");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2, team3 });

            var chainedAbsence = new Absence
            {
                RaceId = 1,
                TeamId = "T3",  // Claims D3 is leaving T3, but D3 is actually in T2!
                DriverOut = "D3",
                DriverIn = "D4"
            };

            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D1",
                    DriverIn = "D3",
                    ChainedAbsence = chainedAbsence
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(1, result.Absences.Count(),
                "Absence should use D4 even if doesn't have enough reputation as no other alternatives exist.");
        }


        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_RemovesAbsence_WhenChainedDriverInWrongTeam_AndNoUnemployedReplacement()
        {
            // Arrange
            // Same setup but NO unemployed drivers available for T1
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "D2");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var team3OldEntry = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D7", "D8");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry, team3OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver6 = CreateTestDriver("D6", "Driver 6", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver7 = CreateTestDriver("D7", "Driver 7", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver8 = CreateTestDriver("D8", "Driver 8", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver6, driver7, driver8 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.TOP_TEAM, "D1", "D2");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "D6");
            var team3 = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D7", "D8");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2, team3 });

            var chainedAbsence = new Absence
            {
                RaceId = 1,
                TeamId = "T3",  // Claims D3 is leaving T3, but D3 is actually in T2!
                DriverOut = "D3",
                DriverIn = "D4"
            };

            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D1",
                    DriverIn = "D3",
                    ChainedAbsence = chainedAbsence
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(0, result.Absences.Count(),
                "Absence should be removed when chain is invalid and no unemployed replacement exists");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_BreaksChainAtMiddleLink_WhenDriverInWrongTeam()
        {
            // Arrange
            // 3-link chain: D1->D2->D3->D4, but D3 is in wrong team
            // Expected: D1->D2->D5 (unemployed replacement instead of D3)
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "DX");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D2", "DY");
            var team3OldEntry = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D3", "DZ");
            var team4OldEntry = CreateTestTeamEntry("T4", TeamReputation.MIDFIELD_HIGH, "D6", "D7");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry, team3OldEntry, team4OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver4 = CreateTestDriver("D4", "Driver 4", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver5 = CreateTestDriver("D5", "Driver 5", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver6 = CreateTestDriver("D6", "Driver 6", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver7 = CreateTestDriver("D7", "Driver 7", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4, driver5, driver6, driver7 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "DX");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D2", "DY");
            var team3 = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D3", "DZ");
            var team4 = CreateTestTeamEntry("T4", TeamReputation.MIDFIELD_HIGH, "D6", "D7");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2, team3, team4 });

            // Chain: D1 from T1 -> D2 from T2 -> D3 from T4 (WRONG! D3 is in T3) -> D4
            // Expected: D1 from T1 -> D2 from T2 -> D5 (unemployed)
            var thirdLinkAbsence = new Absence
            {
                RaceId = 1,
                TeamId = "T4",  // Claims D3 is in T4, but D3 is actually in T3!
                DriverOut = "D3",
                DriverIn = "D4"
            };

            var secondLinkAbsence = new Absence
            {
                RaceId = 1,
                TeamId = "T2",
                DriverOut = "D2",
                DriverIn = "D3",  // D3 is employed in T3, not T4
                ChainedAbsence = thirdLinkAbsence
            };

            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D1",
                    DriverIn = "D2",  // D2 is employed in T2
                    ChainedAbsence = secondLinkAbsence
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(1, result.Absences.Count(), "Absence should be kept");
            var absence = result.Absences.First();
            Assert.AreEqual("D1", absence.DriverOut);
            Assert.AreEqual("D2", absence.DriverIn);
            Assert.IsNotNull(absence.ChainedAbsence, "Should have one chained absence");

            var chainedAbsence = absence.ChainedAbsence;
            Assert.AreEqual("T2", chainedAbsence.TeamId);
            Assert.AreEqual("D2", chainedAbsence.DriverOut);
            // D3 was in wrong team, so should be replaced by unemployed (D4 or D5)
            Assert.IsTrue(chainedAbsence.DriverIn == "D4" || chainedAbsence.DriverIn == "D5",
                "Should be replaced by unemployed driver");
            Assert.IsNull(chainedAbsence.ChainedAbsence, "Chain should stop here - third link was invalid");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_KeepsFullChain_WhenAllDriversInCorrectTeams()
        {
            // Arrange
            // Valid 3-link chain where all drivers are in the correct teams
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "DX");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D2", "DY");
            var team3OldEntry = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D3", "DZ");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry, team3OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver4 = CreateTestDriver("D4", "Driver 4", 1992, DriverReputation.PRIME_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "DX");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D2", "DY");
            var team3 = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D3", "DZ");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2, team3 });

            // Valid chain: D1 from T1 -> D2 from T2 -> D3 from T3 -> D4 (unemployed)
            var thirdLinkAbsence = new Absence
            {
                RaceId = 1,
                TeamId = "T3",  // D3 is correctly in T3
                DriverOut = "D3",
                DriverIn = "D4"
            };

            var secondLinkAbsence = new Absence
            {
                RaceId = 1,
                TeamId = "T2",  // D2 is correctly in T2
                DriverOut = "D2",
                DriverIn = "D3",
                ChainedAbsence = thirdLinkAbsence
            };

            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D1",
                    DriverIn = "D2",
                    ChainedAbsence = secondLinkAbsence
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(1, result.Absences.Count(), "Absence should be kept");
            var absence = result.Absences.First();
            Assert.AreEqual("D2", absence.DriverIn);
            Assert.IsNotNull(absence.ChainedAbsence, "Should have chained absence");
            Assert.AreEqual("D3", absence.ChainedAbsence.DriverIn);
            Assert.IsNotNull(absence.ChainedAbsence.ChainedAbsence, "Should have third link");
            Assert.AreEqual("D4", absence.ChainedAbsence.ChainedAbsence.DriverIn);
            Assert.IsNull(absence.ChainedAbsence.ChainedAbsence.ChainedAbsence, "Should end here");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_BreaksChainAtFirstLink_WhenDriverInWrongTeam()
        {
            // Arrange
            // Chain: D1 from T1 -> D2 from T3 (WRONG! D2 is in T2)
            // Expected: D1 from T1 -> D4/D5 (unemployed)
            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "DX");
            var team2OldEntry = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D2", "DY");
            var team3OldEntry = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D3", "DZ");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry, team2OldEntry, team3OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver4 = CreateTestDriver("D4", "Driver 4", 1992, DriverReputation.PRIME_MIDFIELD, 2025);
            var driver5 = CreateTestDriver("D5", "Driver 5", 1992, DriverReputation.PRIME_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4, driver5 };

            var team1 = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "DX");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D2", "DY");
            var team3 = CreateTestTeamEntry("T3", TeamReputation.MIDFIELD_HIGH, "D3", "DZ");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2, team3 });

            // Invalid chain: claims D2 is leaving T3, but D2 is in T2
            var chainedAbsence = new Absence
            {
                RaceId = 1,
                TeamId = "T3",  // WRONG! D2 is in T2, not T3
                DriverOut = "D2",
                DriverIn = "D3"
            };

            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D1",
                    DriverIn = "D2",
                    ChainedAbsence = chainedAbsence
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(1, result.Absences.Count(), "Absence should be kept with replacement");
            var absence = result.Absences.First();
            Assert.AreEqual("D1", absence.DriverOut);
            Assert.IsTrue(absence.DriverIn == "D4" || absence.DriverIn == "D5",
                "Should be replaced by unemployed driver since D2's chain was invalid");
            Assert.IsNull(absence.ChainedAbsence, "Chain should be broken - no chained absence");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_ReplacesDriverIn_WhenDriverInWasUnemployedButIsNowHired()
        {
            // Arrange
            // Scenario: absence in newSeason has DriverIn = D3, with no ChainedAbsence
            // (because when the absence was created, D3 was unemployed — not on any team).
            // In the new season, D3 got picked up by T2, which is a brand-new team
            // (not in the old season), so the ballot loop doesn't overwrite T2's drivers
            // with old-season values and D3 ends up genuinely employed.
            // D4 is still unemployed and should become the new DriverIn.
            // Expected: absence kept, DriverIn = D4, ChainedAbsence = null.

            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            // T2 is intentionally absent from the old season — it's a new entry next year
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3 (now hired)", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver4 = CreateTestDriver("D4", "Driver 4 (unemployed)", 1992, DriverReputation.PRIME_MIDFIELD, 2025);

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3, driver4 };

            // T2 is new — not in old season — so its template drivers (D3, DY) are kept as-is
            // by GenerateNewSeasonWithNewHirings, making D3 genuinely employed
            var team1 = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "DY");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2 });

            // Absence: D1 out from T1, D3 in — no ChainedAbsence because D3 was unemployed when created
            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D1",
                    DriverIn = "D3",
                    ChainedAbsence = null
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(1, result.Absences.Count(), "Absence should be kept with an unemployed replacement");
            var absence = result.Absences.First();
            Assert.AreEqual("D1", absence.DriverOut);
            Assert.AreEqual("D4", absence.DriverIn,
                "DriverIn should be replaced by an unemployed driver since D3 is now employed");
            Assert.IsNull(absence.ChainedAbsence, "ChainedAbsence should remain null");
        }

        [TestMethod]
        public void GenerateNewSeasonWithNewHirings_RemovesAbsence_WhenDriverInWasUnemployedButIsNowHired_AndNoUnemployedReplacementExists()
        {
            // Arrange
            // Same as above but no unemployed driver exists to substitute.
            // Expected: absence removed entirely.

            var team1OldEntry = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var saveGame = CreateTestSaveGame(2024, new List<ITeamEntry> { team1OldEntry });

            var driver1 = CreateTestDriver("D1", "Driver 1", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver2 = CreateTestDriver("D2", "Driver 2", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            var driver3 = CreateTestDriver("D3", "Driver 3 (now hired)", 1990, DriverReputation.PRIME_STRONG_MIDFIELD, 2025);
            // No D4 — all available drivers are employed, leaving no unemployed pool

            saveGame.Drivers = new List<IDriverData> { driver1, driver2, driver3 };

            // T2 is new — D3 ends up employed, no one left in the unemployed pool
            var team1 = CreateTestTeamEntry("T1", TeamReputation.MIDFIELD_HIGH, "D1", "D2");
            var team2 = CreateTestTeamEntry("T2", TeamReputation.MIDFIELD_HIGH, "D3", "DY");
            var newSeason = CreateTestSeason(2025, new List<ITeamEntry> { team1, team2 });

            newSeason.Absences = new List<Absence>
            {
                new Absence
                {
                    RaceId = 1,
                    TeamId = "T1",
                    DriverOut = "D1",
                    DriverIn = "D3",
                    ChainedAbsence = null
                }
            };

            var ballots = new List<TeamHiringBallot>();

            // Act
            var result = _endOfSeasonManager.GenerateNewSeasonWithNewHirings(saveGame, newSeason, ballots);

            // Assert
            Assert.AreEqual(0, result.Absences.Count(),
                "Absence should be removed when DriverIn is now employed and no unemployed replacement exists");
        }

        #endregion

        #region Helper Methods

        private ISaveGame CreateTestSaveGame(int year, List<ITeamEntry> teams)
        {
            return new SaveGame
            {
                CurrentSeason = CreateTestSeason(year, teams),
                Drivers = new List<IDriverData>(),
                CurrentDriverStandings = new List<HistoricalDriverStandingEntry>(),
                CurrentConstructorStandings = new List<ConstructorStandingEntry>(),
                GrandPrixResults = new List<GrandPrixResult>(),
                HistoricalDriverStandings = new List<HistoricalDriverStanding>(),
                HistoricalConstructorStandings = new List<HistoricalConstructorStanding>(),
                PlayerData = new PlayerData { DriverId = "PLAYER", Name = "Test Player", TeamId = "T1" }
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

        private ISeason CreateTestSeason(int year, IEnumerable<ITeamEntry> teams = null)
        {
            return new Season
            {
                Year = year,
                Teams = teams ?? new List<ITeamEntry>(),
                Races = new List<Race>
                {
                    new Race { RaceId = 1, RaceName = "GP 1" },
                    new Race { RaceId = 2, RaceName = "GP 2" },
                    new Race { RaceId = 3, RaceName = "GP 3" }
                },
                Absences = new List<Absence>()
            };
        }

        #endregion
    }
}