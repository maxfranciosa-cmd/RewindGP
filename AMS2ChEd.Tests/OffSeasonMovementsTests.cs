using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;

using static AMS2ChEd.Business.Services.OffSeasonMovements;

namespace AMS2ChEd.Tests.Business.Services
{
    [TestClass]
    public class OffSeasonMovementsTests
    {
        private OffSeasonMovements _offSeasonMovements;
        private DriverFirer _driverFirer;
        private DriverHirer _driverHirer;

        [TestInitialize]
        public void Setup()
        {
            _driverFirer = new DriverFirer();
            _driverHirer = new DriverHirer();
            _offSeasonMovements = new OffSeasonMovements(_driverFirer, _driverHirer);
        }

        #region DropDrivers Tests

        [TestMethod]
        public void DropDrivers_WithSingleTeam_ReturnsCorrectResult()
        {
            // Arrange
            var teamSituation = new TeamSituation
            {
                TeamId = "T1",
                TeamQuitting = false,
                Reputation = TeamReputation.TOP_TEAM,
                Driver1 = new DriverSituation
                {
                    DriverId = "D1",
                    DriverRetiring = false,
                    RacesLeftInContract = 10,
                    Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL
                },
                Driver2 = new DriverSituation
                {
                    DriverId = "D2",
                    DriverRetiring = false,
                    RacesLeftInContract = 0, // Expired
                    Reputation = DriverReputation.YOUNG_TALENT
                }
            };

            // Act
            var results = _offSeasonMovements.DropDrivers(new[] { teamSituation }).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            var result = results[0];
            Assert.AreEqual("T1", result.TeamId);
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, result.DropDriver1);
            Assert.AreEqual(DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED, result.DropDriver2);
        }

        [TestMethod]
        public void DropDrivers_WithMultipleTeams_ProcessesAllTeams()
        {
            // Arrange
            var teams = new[]
            {
                new TeamSituation
                {
                    TeamId = "T1",
                    TeamQuitting = false,
                    Reputation = TeamReputation.TOP_TEAM,
                    Driver1 = new DriverSituation { DriverId = "D1", DriverRetiring = false, RacesLeftInContract = 10, Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                    Driver2 = new DriverSituation { DriverId = "D2", DriverRetiring = false, RacesLeftInContract = 10, Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL }
                },
                new TeamSituation
                {
                    TeamId = "T2",
                    TeamQuitting = false,
                    Reputation = TeamReputation.MIDFIELD,
                    Driver1 = new DriverSituation { DriverId = "D3", DriverRetiring = false, RacesLeftInContract = 0, Reputation = DriverReputation.PRIME_MIDFIELD },
                    Driver2 = new DriverSituation { DriverId = "D4", DriverRetiring = true, RacesLeftInContract = 5, Reputation = DriverReputation.AGEING_MIDFIELD }
                }
            };

            // Act
            var results = _offSeasonMovements.DropDrivers(teams).ToList();

            // Assert
            Assert.AreEqual(2, results.Count);

            var t1Result = results.First(r => r.TeamId == "T1");
            var t2Result = results.First(r => r.TeamId == "T2");

            // T1: Both championship drivers should be kept
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, t1Result.DropDriver1);
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, t1Result.DropDriver2);

            // T2: D3 expired contract, D4 retiring
            Assert.AreEqual(DriverFirerOutcome.DROPPED_CONTRACT_EXPIRED, t2Result.DropDriver1);
            Assert.AreEqual(DriverFirerOutcome.DROPPED_RETIRING, t2Result.DropDriver2);
        }

        [TestMethod]
        public void DropDrivers_WithEmptyTeams_ReturnsEmptyResults()
        {
            // Arrange
            var teams = new TeamSituation[] { };

            // Act
            var results = _offSeasonMovements.DropDrivers(teams).ToList();

            // Assert
            Assert.AreEqual(0, results.Count);
        }

        #endregion

        #region PickPotentialNewDrivers Tests

        [TestMethod]
        public void PickPotentialNewDrivers_WithOneJobAndOneDriver_ReturnsOneHiring()
        {
            // Arrange
            var jobAds = new[]
            {
                new TeamJobAd
                {
                    TeamId = "T1",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER
                }
            };

            var poolOfDrivers = new[]
            {
                new UnemployedDriver
                {
                    DriverId = "D1",
                    Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL
                }
            };

            // Act
            List<TeamJobAd> adsWithNoCandidates;
            var results = _offSeasonMovements.PickPotentialNewDrivers(jobAds, poolOfDrivers, out adsWithNoCandidates).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            var hiring = results[0];
            Assert.AreEqual("T1", hiring.TeamId);
            Assert.AreEqual("D1", hiring.DriverId);
            Assert.AreEqual(DriverRole.FIRST_DRIVER, hiring.Role);
            Assert.AreEqual(TeamReputation.TOP_TEAM, hiring.TeamReputation);
            Assert.AreEqual(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, hiring.DriverReputation);
        }

        [TestMethod]
        public void PickPotentialNewDrivers_TopTeamPicksBeforeOthers()
        {
            // Arrange
            var jobAds = new[]
            {
                new TeamJobAd { TeamId = "MINNOW", TeamReputation = TeamReputation.MINNOW, Role = DriverRole.FIRST_DRIVER },
                new TeamJobAd { TeamId = "TOP", TeamReputation = TeamReputation.TOP_TEAM, Role = DriverRole.FIRST_DRIVER },
                new TeamJobAd { TeamId = "MIDFIELD", TeamReputation = TeamReputation.MIDFIELD, Role = DriverRole.FIRST_DRIVER }
            };

            var poolOfDrivers = new[]
            {
                new UnemployedDriver { DriverId = "CHAMPION", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new UnemployedDriver { DriverId = "MIDFIELD_DRIVER", Reputation = DriverReputation.PRIME_MIDFIELD },
                new UnemployedDriver { DriverId = "PAY_DRIVER", Reputation = DriverReputation.PAY_DRIVER_SEASON }
            };

            // Act
            List<TeamJobAd> adsWithNoCandidates;
            var results = _offSeasonMovements.PickPotentialNewDrivers(jobAds, poolOfDrivers, out adsWithNoCandidates).ToList();

            // Assert
            Assert.AreEqual(3, results.Count);

            // Top team should get the championship driver
            var topTeamHiring = results.First(r => r.TeamId == "TOP");
            Assert.AreEqual("CHAMPION", topTeamHiring.DriverId);

            // Midfield should get midfield driver
            var midfieldTeamHiring = results.First(r => r.TeamId == "MIDFIELD");
            Assert.AreEqual("MIDFIELD_DRIVER", midfieldTeamHiring.DriverId);

            // Minnow gets remaining driver
            var minnowTeamHiring = results.First(r => r.TeamId == "MINNOW");
            Assert.AreEqual("PAY_DRIVER", minnowTeamHiring.DriverId);
        }

        [TestMethod]
        public void PickPotentialNewDrivers_TwoJobsOneDriver_ShouldMarkOneWithLessTeamReputation_AsWithNoCandidates()
        {
            // Arrange
            var firstDriverJobAd = new TeamJobAd { TeamId = "T1", TeamReputation = TeamReputation.TOP_TEAM, Role = DriverRole.FIRST_DRIVER };
            var secondDriverJobAd = new TeamJobAd { TeamId = "T2", TeamReputation = TeamReputation.MIDFIELD_HIGH, Role = DriverRole.SECOND_DRIVER };


            var jobAds = new[]
            {
                firstDriverJobAd,
                secondDriverJobAd
            };

            var poolOfDrivers = new[]
            {
                new UnemployedDriver { DriverId = "D1", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
            };

            // Act & Assert - should throw exception (not enough drivers)
            List<TeamJobAd> adsWithNoCandidates;
            var result = _offSeasonMovements.PickPotentialNewDrivers(jobAds, poolOfDrivers, out adsWithNoCandidates).ToList();

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, adsWithNoCandidates.Count);

            Assert.AreEqual("D1", result[0].DriverId);
            Assert.AreEqual(secondDriverJobAd, adsWithNoCandidates[0]);
        }

        [TestMethod]
        public void PickPotentialNewDrivers_MultipleSameReputationTeams_AllGetDrivers()
        {
            // Arrange
            var jobAds = new[]
            {
                new TeamJobAd { TeamId = "T1", TeamReputation = TeamReputation.MIDFIELD, Role = DriverRole.FIRST_DRIVER },
                new TeamJobAd { TeamId = "T2", TeamReputation = TeamReputation.MIDFIELD, Role = DriverRole.FIRST_DRIVER },
                new TeamJobAd { TeamId = "T3", TeamReputation = TeamReputation.MIDFIELD, Role = DriverRole.FIRST_DRIVER }
            };

            var poolOfDrivers = new[]
            {
                new UnemployedDriver { DriverId = "D1", Reputation = DriverReputation.PRIME_MIDFIELD },
                new UnemployedDriver { DriverId = "D2", Reputation = DriverReputation.PRIME_MIDFIELD },
                new UnemployedDriver { DriverId = "D3", Reputation = DriverReputation.PRIME_MIDFIELD }
            };

            // Act
            List<TeamJobAd> adsWithNoCandidates;
            var results = _offSeasonMovements.PickPotentialNewDrivers(jobAds, poolOfDrivers, out adsWithNoCandidates).ToList();

            // Assert
            Assert.AreEqual(3, results.Count);

            // All teams should get a driver
            var teamIds = results.Select(r => r.TeamId).OrderBy(id => id).ToList();
            CollectionAssert.AreEqual(new[] { "T1", "T2", "T3" }, teamIds);

            // All drivers should be hired
            var driverIds = results.Select(r => r.DriverId).OrderBy(id => id).ToList();
            CollectionAssert.AreEqual(new[] { "D1", "D2", "D3" }, driverIds);
        }

        [TestMethod]
        public void PickPotentialNewDrivers_EmptyJobAds_ReturnsEmpty()
        {
            // Arrange
            var jobAds = new TeamJobAd[] { };
            var poolOfDrivers = new[]
            {
                new UnemployedDriver { DriverId = "D1", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
            };

            // Act
            List<TeamJobAd> adsWithNoCandidates;
            var results = _offSeasonMovements.PickPotentialNewDrivers(jobAds, poolOfDrivers, out adsWithNoCandidates).ToList();

            // Assert
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void PickPotentialNewDrivers_EmptyDriverPool_MarksJobAdAsWithNoCandidates()
        {
            // Arrange
            var jobAds = new[]
            {
                new TeamJobAd { TeamId = "T1", TeamReputation = TeamReputation.TOP_TEAM, Role = DriverRole.FIRST_DRIVER }
            };
            var poolOfDrivers = new UnemployedDriver[] { };

            // Act & Assert - Should throw since no drivers available
            List<TeamJobAd> adsWithNoCandidates;
            var result = _offSeasonMovements.PickPotentialNewDrivers(jobAds, poolOfDrivers, out adsWithNoCandidates).ToList();

            Assert.AreEqual(0, result.Count);
            Assert.AreEqual(1, adsWithNoCandidates.Count);
            Assert.AreEqual(jobAds[0], adsWithNoCandidates[0]);
        }

        #endregion

        #region DriversProposeToTeams Tests

        [TestMethod]
        public void DriversProposeToTeams_DriverProposesToSuitableTeams()
        {
            // Arrange
            var poolOfDrivers = new[]
            {
                // PRIME_MIDFIELD: MaxReputation = MIDFIELD_HIGH, MinReputation = MINNOW
                new UnemployedDriver { DriverId = "D1", Reputation = DriverReputation.PRIME_MIDFIELD }
            };

            var currentTeamPickings = new[]
            {
                new TeamHiring { TeamId = "TOP", TeamReputation = TeamReputation.TOP_TEAM, DriverId = "TEMP1", Role = DriverRole.FIRST_DRIVER, DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new TeamHiring { TeamId = "MIDFIELD", TeamReputation = TeamReputation.MIDFIELD, DriverId = "TEMP2", Role = DriverRole.FIRST_DRIVER, DriverReputation = DriverReputation.AGEING_MIDFIELD },
                new TeamHiring { TeamId = "MINNOW", TeamReputation = TeamReputation.MINNOW, DriverId = "TEMP3", Role = DriverRole.FIRST_DRIVER, DriverReputation = DriverReputation.PAY_DRIVER_SEASON }
            };

            // Act
            var results = _offSeasonMovements.DriversProposeToTeams(poolOfDrivers, currentTeamPickings).ToList();

            // Assert
            Assert.AreEqual(3, results.Count);

            // TOP_TEAM (too high) - should have NO candidates from D1
            var topBallot = results.First(b => b.OriginalTeamHiring.TeamId == "TOP");
            Assert.AreEqual(0, topBallot.Candidates.Count(), "PRIME_MIDFIELD driver should not propose to TOP_TEAM");

            // MIDFIELD - should have D1 as candidate
            var midfieldBallot = results.First(b => b.OriginalTeamHiring.TeamId == "MIDFIELD");
            Assert.AreEqual(1, midfieldBallot.Candidates.Count());
            Assert.AreEqual("D1", midfieldBallot.Candidates.First().DriverId);

            // MINNOW - should have D1 as candidate
            var minnowBallot = results.First(b => b.OriginalTeamHiring.TeamId == "MINNOW");
            Assert.AreEqual(1, minnowBallot.Candidates.Count());
            Assert.AreEqual("D1", minnowBallot.Candidates.First().DriverId);
        }

        [TestMethod]
        public void DriversProposeToTeams_ChampionDriverOnlyProposesToTopTeams()
        {
            // Arrange
            var poolOfDrivers = new[]
            {
                // PRIME_CHAMPIONSHIP_LEVEL: MaxReputation = TOP_TEAM, MinReputation = TOP_TEAM
                new UnemployedDriver { DriverId = "CHAMP", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
            };

            var currentTeamPickings = new[]
            {
                new TeamHiring { TeamId = "TOP1", TeamReputation = TeamReputation.TOP_TEAM, DriverId = "TEMP1", Role = DriverRole.FIRST_DRIVER, DriverReputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL },
                new TeamHiring { TeamId = "TOP2", TeamReputation = TeamReputation.TOP_TEAM, DriverId = "TEMP2", Role = DriverRole.FIRST_DRIVER, DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new TeamHiring { TeamId = "MIDFIELD", TeamReputation = TeamReputation.MIDFIELD, DriverId = "TEMP3", Role = DriverRole.FIRST_DRIVER, DriverReputation = DriverReputation.PRIME_MIDFIELD }
            };

            // Act
            var results = _offSeasonMovements.DriversProposeToTeams(poolOfDrivers, currentTeamPickings).ToList();

            // Assert
            var top1Ballot = results.First(b => b.OriginalTeamHiring.TeamId == "TOP1");
            var top2Ballot = results.First(b => b.OriginalTeamHiring.TeamId == "TOP2");
            var midfieldBallot = results.First(b => b.OriginalTeamHiring.TeamId == "MIDFIELD");

            // Should propose to both top teams
            Assert.AreEqual(1, top1Ballot.Candidates.Count());
            Assert.AreEqual("CHAMP", top1Ballot.Candidates.First().DriverId);

            Assert.AreEqual(1, top2Ballot.Candidates.Count());
            Assert.AreEqual("CHAMP", top2Ballot.Candidates.First().DriverId);

            // Should NOT propose to midfield
            Assert.AreEqual(0, midfieldBallot.Candidates.Count(),
                "Championship driver should not propose to teams below their ambition");
        }

        [TestMethod]
        public void DriversProposeToTeams_MultipleDriversToSameTeam()
        {
            // Arrange
            var poolOfDrivers = new[]
            {
                new UnemployedDriver { DriverId = "D1", Reputation = DriverReputation.PRIME_MIDFIELD },
                new UnemployedDriver { DriverId = "D2", Reputation = DriverReputation.YOUNG_TALENT },
                new UnemployedDriver { DriverId = "D3", Reputation = DriverReputation.AGEING_MIDFIELD }
            };

            var currentTeamPickings = new[]
            {
                new TeamHiring { TeamId = "MIDFIELD", TeamReputation = TeamReputation.MIDFIELD, DriverId = "TEMP", Role = DriverRole.FIRST_DRIVER, DriverReputation = DriverReputation.PAY_DRIVER_SEASON }
            };

            // Act
            var results = _offSeasonMovements.DriversProposeToTeams(poolOfDrivers, currentTeamPickings).ToList();

            // Assert
            var ballot = results.First();

            // All three drivers should propose to this MIDFIELD team (within their ambition range)
            Assert.AreEqual(3, ballot.Candidates.Count());

            var candidateIds = ballot.Candidates.Select(c => c.DriverId).OrderBy(id => id).ToList();
            CollectionAssert.AreEqual(new[] { "D1", "D2", "D3" }, candidateIds);
        }

        [TestMethod]
        public void DriversProposeToTeams_EmptyDriverPool_ReturnsBallotsWithNoCandidates()
        {
            // Arrange
            var poolOfDrivers = new UnemployedDriver[] { };
            var currentTeamPickings = new[]
            {
                new TeamHiring { TeamId = "T1", TeamReputation = TeamReputation.MIDFIELD, DriverId = "TEMP", Role = DriverRole.FIRST_DRIVER, DriverReputation = DriverReputation.PRIME_MIDFIELD }
            };

            // Act
            var results = _offSeasonMovements.DriversProposeToTeams(poolOfDrivers, currentTeamPickings).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(0, results[0].Candidates.Count());
        }

        [TestMethod]
        public void DriversProposeToTeams_PayDriverProposesToLowerTeams()
        {
            // Arrange
            var poolOfDrivers = new[]
            {
                // PAY_DRIVER_SEASON: MaxReputation = MIDFIELD, MinReputation = SUPER_MINNOW
                new UnemployedDriver { DriverId = "PAY", Reputation = DriverReputation.PAY_DRIVER_SEASON }
            };

            var currentTeamPickings = new[]
            {
                new TeamHiring { TeamId = "TOP", TeamReputation = TeamReputation.TOP_TEAM, DriverId = "T1", Role = DriverRole.FIRST_DRIVER, DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new TeamHiring { TeamId = "MIDFIELD", TeamReputation = TeamReputation.MIDFIELD, DriverId = "T2", Role = DriverRole.FIRST_DRIVER, DriverReputation = DriverReputation.PRIME_MIDFIELD },
                new TeamHiring { TeamId = "SUPER_MINNOW", TeamReputation = TeamReputation.SUPER_MINNOW, DriverId = "T3", Role = DriverRole.FIRST_DRIVER, DriverReputation = DriverReputation.PAY_DRIVER_SEASON }
            };

            // Act
            var results = _offSeasonMovements.DriversProposeToTeams(poolOfDrivers, currentTeamPickings).ToList();

            // Assert
            var topBallot = results.First(b => b.OriginalTeamHiring.TeamId == "TOP");
            var midfieldBallot = results.First(b => b.OriginalTeamHiring.TeamId == "MIDFIELD");
            var superMinnowBallot = results.First(b => b.OriginalTeamHiring.TeamId == "SUPER_MINNOW");

            Assert.AreEqual(0, topBallot.Candidates.Count(), "Pay driver should not propose to TOP_TEAM");
            Assert.AreEqual(1, midfieldBallot.Candidates.Count(), "Pay driver should propose to MIDFIELD");
            Assert.AreEqual(1, superMinnowBallot.Candidates.Count(), "Pay driver should propose to SUPER_MINNOW");
        }

        #endregion

        #region FinalBallotResults - Duplicate Driver Prevention Tests

        [TestMethod]
        public void FinalBallotResults_DriverWinsMultipleBallots_OnlyHiredOnce()
        {
            // Arrange - Same driver is best candidate for multiple teams
            var ballots = new[]
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "TEMP1",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PRIME_MIDFIELD
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate
                        {
                            DriverId = "CHAMPION",
                            DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL
                        },
                        // Backup driver for T2 when CHAMPION is taken
                        new TeamHiringBallotCandidate
                        {
                            DriverId = "BACKUP1",
                            DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD
                        }
                    }
                },
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T2",
                        DriverId = "TEMP2",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PRIME_MIDFIELD
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate
                        {
                            DriverId = "CHAMPION", // Same driver!
                            DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL
                        },
                        // Backup driver for when CHAMPION is taken
                        new TeamHiringBallotCandidate
                        {
                            DriverId = "BACKUP2",
                            DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD
                        }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            var championHirings = results.Where(r => r.DriverId == "CHAMPION").ToList();

            Assert.AreEqual(1, championHirings.Count,
                "CHAMPION driver should only be hired once, not for multiple teams");

            // Verify no duplicate driver IDs in results
            var allDriverIds = results.Select(r => r.DriverId).ToList();
            var uniqueDriverIds = allDriverIds.Distinct().ToList();

            Assert.AreEqual(uniqueDriverIds.Count, allDriverIds.Count,
                "No driver should be hired for multiple positions");
        }

        [TestMethod]
        public void FinalBallotResults_ThreeTeamsOneChampion_OnlyOneTeamGetsHim()
        {
            // Arrange - Three teams want the same championship driver
            var ballots = new[]
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "MERCEDES",
                        DriverId = "TEMP1",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PRIME_MIDFIELD
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "HAMILTON", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "BACKUP1", DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD }
                    }
                },
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "FERRARI",
                        DriverId = "TEMP2",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.AGEING_MIDFIELD
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "HAMILTON", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "BACKUP2", DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD }
                    }
                },
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "REDBULL",
                        DriverId = "TEMP3",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.YOUNG_TALENT
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "HAMILTON", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "BACKUP3", DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            var hamiltonHirings = results.Where(r => r.DriverId == "HAMILTON").ToList();

            Assert.AreEqual(1, hamiltonHirings.Count,
                "Hamilton should only be hired by ONE team, not all three");

            // Other two teams should get their backup drivers
            Assert.AreEqual(3, results.Count, "Should have 3 hirings total");

            // All three teams should have a hiring
            Assert.IsNotNull(results.FirstOrDefault(r => r.TeamId == "MERCEDES"));
            Assert.IsNotNull(results.FirstOrDefault(r => r.TeamId == "FERRARI"));
            Assert.IsNotNull(results.FirstOrDefault(r => r.TeamId == "REDBULL"));
        }

        [TestMethod]
        public void FinalBallotResults_DriverInCandidatesAndOriginal_NoduplicateHiring()
        {
            // Arrange - Driver is BOTH the original hire AND a candidate for another team
            var ballots = new[]
            {
                // Team 1: Has CHAMPION as original hire
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "CHAMPION",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL
                    },
                    Candidates = new List<TeamHiringBallotCandidate>() // No better candidates
                },
                // Team 2: Has CHAMPION as a candidate
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T2",
                        DriverId = "TEMP",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PRIME_MIDFIELD
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate
                        {
                            DriverId = "CHAMPION", // Same as T1's original!
                            DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL
                        }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            var championHirings = results.Where(r => r.DriverId == "CHAMPION").ToList();

            Assert.AreEqual(1, championHirings.Count,
                "CHAMPION should only be hired once, either by T1 (original) or T2 (candidate)");

            // Verify which team got the champion
            var championHiring = championHirings[0];
            Assert.IsTrue(championHiring.TeamId == "T1" || championHiring.TeamId == "T2",
                "CHAMPION should be hired by either T1 or T2, not both");
        }

        [TestMethod]
        public void FinalBallotResults_OriginalWinsButAlsoInResult_OnlyOneHiring()
        {
            // Arrange - Tests the bug where if original wins, BOTH original AND bestCandidate are added
            var ballots = new[]
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "CHAMPION",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL // Better reputation
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate
                        {
                            DriverId = "MIDFIELD_DRIVER",
                            DriverReputation = DriverReputation.PRIME_MIDFIELD // Worse reputation
                        }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(1, results.Count,
                "Should only have ONE hiring result, not two");

            Assert.AreEqual("CHAMPION", results[0].DriverId,
                "Should hire the CHAMPION (original, who won the ballot)");

            Assert.AreEqual("T1", results[0].TeamId);
        }

        [TestMethod]
        public void FinalBallotResults_ProcessedSequentially_FirstBallotGetsDriver()
        {
            // Arrange - Test that once a driver is hired, they're not available for subsequent ballots
            var ballots = new[]
            {
                // First ballot (processed first)
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "FIRST",
                        DriverId = "TEMP1",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.YOUNG_TALENT
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "SUPERSTAR", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "BACKUP_FOR_SECOND", DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD }
                    }
                },
                // Second ballot (processed after)
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "SECOND",
                        DriverId = "TEMP2",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.YOUNG_TALENT
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "SUPERSTAR", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "BACKUP_FOR_SECOND", DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            var superstarHirings = results.Where(r => r.DriverId == "SUPERSTAR").ToList();

            Assert.AreEqual(1, superstarHirings.Count,
                "SUPERSTAR should only be hired once");

            Assert.AreEqual("FIRST", superstarHirings[0].TeamId,
                "First team in ballot order should get the driver");

            // Second team should get backup driver
            var secondTeamHiring = results.FirstOrDefault(r => r.TeamId == "SECOND");
            Assert.IsNotNull(secondTeamHiring, "Second team should still have a hiring");
            Assert.AreNotEqual("SUPERSTAR", secondTeamHiring.DriverId,
                "Second team should NOT get SUPERSTAR (already hired by first team)");
            Assert.IsTrue(secondTeamHiring.DriverId == "BACKUP_FOR_SECOND" || secondTeamHiring.DriverId == "TEMP2",
                "Second team should get backup driver or keep original");
        }

        [TestMethod]
        public void FinalBallotResults_AllUniqueDriverIds_NoDuplicatesInResults()
        {
            // Arrange - Complex scenario with multiple overlapping candidates
            var ballots = new[]
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring { TeamId = "T1", DriverId = "D1", Role = DriverRole.FIRST_DRIVER, TeamReputation = TeamReputation.TOP_TEAM, DriverReputation = DriverReputation.PRIME_MIDFIELD },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "STAR1", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "STAR2", DriverReputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL }
                    }
                },
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring { TeamId = "T2", DriverId = "D2", Role = DriverRole.FIRST_DRIVER, TeamReputation = TeamReputation.TOP_TEAM, DriverReputation = DriverReputation.AGEING_MIDFIELD },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "STAR1", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }, // Same as T1
                        new TeamHiringBallotCandidate { DriverId = "STAR2", DriverReputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL }, // Same as T1
                        new TeamHiringBallotCandidate { DriverId = "BACKUP1", DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD } // Backup
                    }
                },
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring { TeamId = "T3", DriverId = "D3", Role = DriverRole.FIRST_DRIVER, TeamReputation = TeamReputation.MIDFIELD, DriverReputation = DriverReputation.YOUNG_TALENT },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "STAR2", DriverReputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL }, // Same as T1 and T2
                        new TeamHiringBallotCandidate { DriverId = "BACKUP2", DriverReputation = DriverReputation.PRIME_MIDFIELD } // Backup
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(3, results.Count, "Should have exactly 3 hirings (one per team)");

            var driverIds = results.Select(r => r.DriverId).ToList();
            var uniqueDriverIds = driverIds.Distinct().ToList();

            Assert.AreEqual(uniqueDriverIds.Count, driverIds.Count,
                "All driver IDs in results should be unique - no driver hired twice");

            // Verify each team got someone
            var teamIds = results.Select(r => r.TeamId).OrderBy(id => id).ToList();
            CollectionAssert.AreEqual(new[] { "T1", "T2", "T3" }, teamIds,
                "Each team should have exactly one hiring");
        }

        [TestMethod]
        public void FinalBallotResults_DriverAlreadyHired_RemovedFromSubsequentBallots()
        {
            // Arrange
            var ballots = new[]
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "ORIGINAL1",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PAY_DRIVER_SEASON
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "CHAMPION", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
                    }
                },
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T2",
                        DriverId = "ORIGINAL2",
                        Role = DriverRole.SECOND_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PAY_DRIVER_SEASON
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "CHAMPION", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }, // Already hired by T1
                        new TeamHiringBallotCandidate { DriverId = "BACKUP", DriverReputation = DriverReputation.PRIME_MIDFIELD }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            var t1Hiring = results.FirstOrDefault(r => r.TeamId == "T1");
            var t2Hiring = results.FirstOrDefault(r => r.TeamId == "T2");

            Assert.IsNotNull(t1Hiring);
            Assert.IsNotNull(t2Hiring);

            // T1 should get CHAMPION (best candidate, first ballot)
            Assert.AreEqual("CHAMPION", t1Hiring.DriverId);

            // T2 should NOT get CHAMPION (already hired)
            Assert.AreNotEqual("CHAMPION", t2Hiring.DriverId,
                "T2 should not get CHAMPION as they're already hired by T1");

            // T2 should get either BACKUP or keep ORIGINAL2
            Assert.IsTrue(t2Hiring.DriverId == "BACKUP" || t2Hiring.DriverId == "ORIGINAL2",
                "T2 should get BACKUP candidate or keep original");
        }

        [TestMethod]
        public void FinalBallotResults_OriginalDriverAlreadyHiredElsewhere_MustHireCandidate()
        {
            // Arrange - Specific test for when a team's "original" driver is hired by another team first
            // This tests the cross-ballot tracking issue you identified!
            var ballots = new[]
            {
                // T1 hires SUPERSTAR as a candidate
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "TEMP1",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.YOUNG_TALENT
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "SUPERSTAR", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
                    }
                },
                // T2's "original" hire is SUPERSTAR, but SUPERSTAR was just hired by T1!
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T2",
                        DriverId = "SUPERSTAR", // ← This driver was just hired by T1!
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "BACKUP", DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(2, results.Count, "Should have 2 hirings");

            var superstarHirings = results.Where(r => r.DriverId == "SUPERSTAR").ToList();
            Assert.AreEqual(1, superstarHirings.Count,
                "SUPERSTAR should only be hired once");

            var t1Hiring = results.FirstOrDefault(r => r.TeamId == "T1");
            var t2Hiring = results.FirstOrDefault(r => r.TeamId == "T2");

            Assert.IsNotNull(t1Hiring);
            Assert.IsNotNull(t2Hiring);

            // T1 should have SUPERSTAR (hired as candidate in first ballot)
            Assert.AreEqual("SUPERSTAR", t1Hiring.DriverId,
                "T1 should hire SUPERSTAR (first ballot)");

            // T2 CANNOT keep SUPERSTAR as original because already hired by T1
            // T2 must hire BACKUP instead
            Assert.AreEqual("BACKUP", t2Hiring.DriverId,
                "T2 must hire BACKUP because SUPERSTAR (their original) was already hired by T1");
        }

        [TestMethod]
        public void FinalBallotResults_ProcessedByPriority_TopTeamsPickFirst()
        {
            // Arrange - Tests that ballots should be processed in priority order
            // TOP teams should get first pick of drivers over MINNOW teams
            var ballots = new[]
            {
                // MINNOW team (should be processed LAST despite being first in array)
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "MINNOW",
                        DriverId = "PAY_DRIVER",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.MINNOW,
                        DriverReputation = DriverReputation.PAY_DRIVER_SEASON
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "HAMILTON", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "MIDFIELDER", DriverReputation = DriverReputation.PRIME_MIDFIELD },
                        new TeamHiringBallotCandidate { DriverId = "BACKUP_MINNOW", DriverReputation = DriverReputation.YOUNG_TALENT }
                    }
                },
                // MIDFIELD team (should be processed SECOND)
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "MIDFIELD",
                        DriverId = "TEMP_MID",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.MIDFIELD,
                        DriverReputation = DriverReputation.YOUNG_TALENT
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "HAMILTON", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "MIDFIELDER", DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD },
                        new TeamHiringBallotCandidate { DriverId = "BACKUP_MID", DriverReputation = DriverReputation.PRIME_MIDFIELD }
                    }
                },
                // TOP_TEAM (should be processed FIRST despite being last in array)
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "MERCEDES",
                        DriverId = "TEMP_TOP",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.AGEING_MIDFIELD
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "HAMILTON", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(3, results.Count, "Should have 3 hirings");

            var mercedesHiring = results.FirstOrDefault(r => r.TeamId == "MERCEDES");
            var midfieldHiring = results.FirstOrDefault(r => r.TeamId == "MIDFIELD");
            var minnowHiring = results.FirstOrDefault(r => r.TeamId == "MINNOW");

            Assert.IsNotNull(mercedesHiring);
            Assert.IsNotNull(midfieldHiring);
            Assert.IsNotNull(minnowHiring);

            // TOP_TEAM should get HAMILTON (highest priority, processed first)
            Assert.AreEqual("HAMILTON", mercedesHiring.DriverId,
                "TOP_TEAM (Mercedes) should get Hamilton - they have priority over midfield and minnow teams");

            // MIDFIELD should get MIDFIELDER (Hamilton taken by Mercedes)
            Assert.AreEqual("MIDFIELDER", midfieldHiring.DriverId,
                "MIDFIELD team should get MIDFIELDER - Hamilton already hired by Mercedes");

            // MINNOW should get backup (both Hamilton and MIDFIELDER taken)
            Assert.AreNotEqual("HAMILTON", minnowHiring.DriverId,
                "MINNOW team should NOT get Hamilton");
            Assert.AreNotEqual("MIDFIELDER", minnowHiring.DriverId,
                "MINNOW team should NOT get MIDFIELDER");
            Assert.IsTrue(minnowHiring.DriverId == "BACKUP_MINNOW" || minnowHiring.DriverId == "PAY_DRIVER",
                "MINNOW should get backup or keep original");

            // Verify Hamilton only hired once
            var hamiltonHirings = results.Where(r => r.DriverId == "HAMILTON").ToList();
            Assert.AreEqual(1, hamiltonHirings.Count, "Hamilton should only be hired once");
            Assert.AreEqual("MERCEDES", hamiltonHirings[0].TeamId, "Hamilton should be hired by Mercedes (TOP_TEAM)");
        }

        [TestMethod]
        public void FinalBallotResults_PriorityOrder_AllReputationLevels()
        {
            // Arrange - Test all reputation levels in priority order
            var ballots = new[]
            {
                // Lowest priority first in array (to verify sorting)
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "SUPER_MINNOW",
                        DriverId = "TEMP1",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.SUPER_MINNOW,
                        DriverReputation = DriverReputation.PAY_DRIVER_SEASON
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "STAR", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "BACKUP1", DriverReputation = DriverReputation.PAY_DRIVER_SEASON }
                    }
                },
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "MINNOW",
                        DriverId = "TEMP2",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.MINNOW,
                        DriverReputation = DriverReputation.PAY_DRIVER_SEASON
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "STAR", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "BACKUP2", DriverReputation = DriverReputation.YOUNG_TALENT }
                    }
                },
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "MIDFIELD",
                        DriverId = "TEMP3",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.MIDFIELD,
                        DriverReputation = DriverReputation.PRIME_MIDFIELD
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "STAR", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "BACKUP3", DriverReputation = DriverReputation.PRIME_MIDFIELD }
                    }
                },
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "MIDFIELD_HIGH",
                        DriverId = "TEMP4",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.MIDFIELD_HIGH,
                        DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "STAR", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                        new TeamHiringBallotCandidate { DriverId = "BACKUP4", DriverReputation = DriverReputation.PRIME_STRONG_MIDFIELD }
                    }
                },
                // Highest priority last in array (to verify sorting)
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "TOP_TEAM",
                        DriverId = "TEMP5",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "STAR", DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(5, results.Count, "Should have 5 hirings");

            // The TOP_TEAM should get the STAR driver (highest priority)
            var topTeamHiring = results.FirstOrDefault(r => r.TeamId == "TOP_TEAM");
            Assert.IsNotNull(topTeamHiring);
            Assert.AreEqual("STAR", topTeamHiring.DriverId,
                "TOP_TEAM should get STAR driver (highest priority in processing order)");

            // All other teams should NOT get STAR
            var otherTeams = results.Where(r => r.TeamId != "TOP_TEAM").ToList();
            Assert.IsTrue(otherTeams.All(h => h.DriverId != "STAR"),
                "No other team should get STAR driver - already hired by TOP_TEAM");

            // STAR should only be hired once
            var starHirings = results.Where(r => r.DriverId == "STAR").ToList();
            Assert.AreEqual(1, starHirings.Count, "STAR should only be hired once");
            Assert.AreEqual("TOP_TEAM", starHirings[0].TeamId,
                "STAR should be hired by TOP_TEAM (highest priority)");

            // Verify all teams got someone
            var teamIds = results.Select(r => r.TeamId).OrderBy(id => id).ToList();
            CollectionAssert.AreEqual(new[] { "MIDFIELD", "MIDFIELD_HIGH", "MINNOW", "SUPER_MINNOW", "TOP_TEAM" }, teamIds,
                "All teams should have exactly one hiring");
        }

        #endregion

        #region FinalBallotResults Tests

        [TestMethod]
        public void FinalBallotResults_NoCandidates_KeepsOriginalHiring()
        {
            // Arrange
            var ballots = new[]
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "ORIGINAL",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.MIDFIELD,
                        DriverReputation = DriverReputation.PRIME_MIDFIELD
                    },
                    Candidates = new List<TeamHiringBallotCandidate>() // No candidates
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("ORIGINAL", results[0].DriverId);
            Assert.AreEqual("T1", results[0].TeamId);
        }

        [TestMethod]
        public void FinalBallotResults_BetterCandidateAvailable_PicksBetterDriver()
        {
            // Arrange
            var ballots = new[]
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "MIDFIELD_DRIVER",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PRIME_MIDFIELD // Lower reputation
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate
                        {
                            DriverId = "CHAMPION",
                            DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL // Higher reputation
                        }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            // Should pick the champion driver
            var championHiring = results.FirstOrDefault(r => r.DriverId == "CHAMPION");
            Assert.IsNotNull(championHiring, "Champion driver should be hired");
            Assert.AreEqual("T1", championHiring.TeamId);
            Assert.AreEqual(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, championHiring.DriverReputation);
        }

        [TestMethod]
        public void FinalBallotResults_OriginalBetter_KeepsOriginal()
        {
            // Arrange
            var ballots = new[]
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "CHAMPION",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL // Higher
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate
                        {
                            DriverId = "MIDFIELD_DRIVER",
                            DriverReputation = DriverReputation.PRIME_MIDFIELD // Lower
                        }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            // Should keep the original champion driver
            Assert.IsTrue(results.Any(r => r.DriverId == "CHAMPION"),
                "Original champion driver should be kept");
        }

        [TestMethod]
        public void FinalBallotResults_MultipleCandidatesSameReputation_SelectsOne()
        {
            // Arrange
            var ballots = new[]
            {
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "ORIGINAL",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.MIDFIELD,
                        DriverReputation = DriverReputation.PAY_DRIVER_SEASON
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate { DriverId = "D1", DriverReputation = DriverReputation.PRIME_MIDFIELD },
                        new TeamHiringBallotCandidate { DriverId = "D2", DriverReputation = DriverReputation.PRIME_MIDFIELD },
                        new TeamHiringBallotCandidate { DriverId = "D3", DriverReputation = DriverReputation.PRIME_MIDFIELD }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            // Should select one of the PRIME_MIDFIELD drivers (better than PAY_DRIVER)
            var hired = results.Where(r => r.TeamId == "T1").ToList();
            Assert.IsTrue(hired.Count > 0);

            var primeMidfieldHire = hired.FirstOrDefault(h => h.DriverReputation == DriverReputation.PRIME_MIDFIELD);
            Assert.IsNotNull(primeMidfieldHire, "Should hire a PRIME_MIDFIELD driver");
            Assert.IsTrue(new[] { "D1", "D2", "D3" }.Contains(primeMidfieldHire.DriverId));
        }

        [TestMethod]
        public void FinalBallotResults_MultipleBallotsProcessedIndependently()
        {
            // Arrange
            var ballots = new[]
            {
                // Team 1: No candidates, keeps original
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T1",
                        DriverId = "D1",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.MIDFIELD,
                        DriverReputation = DriverReputation.PRIME_MIDFIELD
                    },
                    Candidates = new List<TeamHiringBallotCandidate>()
                },
                // Team 2: Better candidate available
                new TeamHiringBallot
                {
                    OriginalTeamHiring = new TeamHiring
                    {
                        TeamId = "T2",
                        DriverId = "D2",
                        Role = DriverRole.FIRST_DRIVER,
                        TeamReputation = TeamReputation.TOP_TEAM,
                        DriverReputation = DriverReputation.PRIME_MIDFIELD
                    },
                    Candidates = new List<TeamHiringBallotCandidate>
                    {
                        new TeamHiringBallotCandidate
                        {
                            DriverId = "CHAMPION",
                            DriverReputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL
                        }
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            // T1 should keep original
            Assert.IsTrue(results.Any(r => r.TeamId == "T1" && r.DriverId == "D1"));

            // T2 should hire champion
            Assert.IsTrue(results.Any(r => r.TeamId == "T2" && r.DriverId == "CHAMPION"));
        }

        [TestMethod]
        public void FinalBallotResults_EmptyBallots_ReturnsEmpty()
        {
            // Arrange
            var ballots = new TeamHiringBallot[] { };

            // Act
            var results = _offSeasonMovements.FinalBallotResults(ballots).ToList();

            // Assert
            Assert.AreEqual(0, results.Count);
        }

        #endregion
    }
}