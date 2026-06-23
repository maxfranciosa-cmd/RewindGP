using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AMS2ChEd.Business.Tests.Services
{
    [TestClass]
    public class OffSeasonMovementsUnfilledSeatsTests
    {
        private OffSeasonMovements _sut;
        private DriverHirer _driverHirer;
        private DriverFirer _driverFirer;

        [TestInitialize]
        public void Setup()
        {
            // Use real instances since these classes can't be mocked
            _driverHirer = new DriverHirer();
            _driverFirer = new DriverFirer();
            _sut = new OffSeasonMovements(_driverFirer, _driverHirer);
        }

        #region PickPotentialNewDrivers - Successful Hiring Tests

        [TestMethod]
        public void PickPotentialNewDrivers_WithMatchingDrivers_ReturnsHirings()
        {
            // Arrange
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "team1",
                    TeamReputation = TeamReputation.MIDFIELD,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver
                {
                    DriverId = "driver1",
                    Reputation = DriverReputation.PRIME_MIDFIELD // Min: MINNOW, Max: MIDFIELD_HIGH - matches MIDFIELD
                }
            };

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(1, result.Count());
            Assert.AreEqual(0, adsWithNoCandidates.Count);

            var hire = result.First();
            Assert.AreEqual("driver1", hire.DriverId);
            Assert.AreEqual("team1", hire.TeamId);
            Assert.AreEqual(TeamReputation.MIDFIELD, hire.TeamReputation);
        }

        [TestMethod]
        public void PickPotentialNewDrivers_WithExitingDriverWillingToRenew_IncludesThemInPool()
        {
            // Arrange
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "team1",
                    TeamReputation = TeamReputation.MIDFIELD,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = "driver1",
                    ExitingDriverWillingToRenew = true // Driver CAN be re-signed
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver
                {
                    DriverId = "driver1",
                    Reputation = DriverReputation.PRIME_MIDFIELD
                }
            };

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(1, result.Count());
            Assert.AreEqual(0, adsWithNoCandidates.Count);
            Assert.AreEqual("driver1", result.First().DriverId);
        }

        [TestMethod]
        public void PickPotentialNewDrivers_WithExitingDriverNotWillingToRenew_ExcludesThemFromPool()
        {
            // Arrange
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "team1",
                    TeamReputation = TeamReputation.MIDFIELD,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = "driver1",
                    ExitingDriverWillingToRenew = false // Driver CANNOT be re-signed
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver
                {
                    DriverId = "driver1",
                    Reputation = DriverReputation.PRIME_MIDFIELD
                },
                new UnemployedDriver
                {
                    DriverId = "driver2",
                    Reputation = DriverReputation.PRIME_MIDFIELD
                }
            };

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(1, result.Count());
            Assert.AreEqual(0, adsWithNoCandidates.Count);
            Assert.AreEqual("driver2", result.First().DriverId, "Should hire driver2 since driver1 is excluded");
        }

        #endregion

        #region PickPotentialNewDrivers - No Candidates Tests

        [TestMethod]
        public void PickPotentialNewDrivers_WhenChampionshipDriverWontJoinMinnow_AddsToNoCandidatesList()
        {
            // Arrange
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "minardi",
                    TeamReputation = TeamReputation.SUPER_MINNOW,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver
                {
                    DriverId = "schumacher",
                    Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL // Min: MIDFIELD_HIGH - won't accept SUPER_MINNOW
                }
            };

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(0, result.Count(), "No hiring should happen");
            Assert.AreEqual(1, adsWithNoCandidates.Count, "Job ad should be in the no candidates list");

            var unfilledAd = adsWithNoCandidates.First();
            Assert.AreEqual("minardi", unfilledAd.TeamId);
            Assert.AreEqual(TeamReputation.SUPER_MINNOW, unfilledAd.TeamReputation);
        }

        [TestMethod]
        public void PickPotentialNewDrivers_WhenMultipleAdsHaveNoCandidates_AddsAllToList()
        {
            // Arrange
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "minardi",
                    TeamReputation = TeamReputation.SUPER_MINNOW,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                },
                new TeamJobAd
                {
                    TeamId = "minardi",
                    TeamReputation = TeamReputation.SUPER_MINNOW,
                    Role = DriverRole.SECOND_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver
                {
                    DriverId = "alonso",
                    Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL // Min: MIDFIELD_HIGH - won't accept SUPER_MINNOW
                }
            };

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(0, result.Count());
            Assert.AreEqual(2, adsWithNoCandidates.Count, "Both job ads should be unfilled");
            Assert.IsTrue(adsWithNoCandidates.All(ad => ad.TeamId == "minardi"));
        }

        [TestMethod]
        public void PickPotentialNewDrivers_WithMixedScenario_ReturnsHiringsAndUnfilledAds()
        {
            // Arrange
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "ferrari",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                },
                new TeamJobAd
                {
                    TeamId = "minardi",
                    TeamReputation = TeamReputation.SUPER_MINNOW,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver
                {
                    DriverId = "schumacher",
                    Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL // Min: MIDFIELD_HIGH - Can join TOP_TEAM, won't join SUPER_MINNOW
                }
            };

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(1, result.Count(), "Ferrari should be filled");
            Assert.AreEqual(1, adsWithNoCandidates.Count, "Minardi should be unfilled");

            Assert.AreEqual("ferrari", result.First().TeamId);
            Assert.AreEqual("minardi", adsWithNoCandidates.First().TeamId);
        }

        [TestMethod]
        public void PickPotentialNewDrivers_WhenDriverAlreadyHired_CannotBeHiredAgain()
        {
            // Arrange - Two teams want the same driver
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "ferrari",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                },
                new TeamJobAd
                {
                    TeamId = "mclaren",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver
                {
                    DriverId = "schumacher",
                    Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL
                }
            };

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(1, result.Count(), "Only one team should get the driver");
            Assert.AreEqual(1, adsWithNoCandidates.Count, "Second team should have no candidates");
        }

        [TestMethod]
        public void PickPotentialNewDrivers_MidfieldDriverCanJoinMinnowTeam()
        {
            // Arrange - Midfield driver should be able to join minnow team
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "minardi",
                    TeamReputation = TeamReputation.MINNOW,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver
                {
                    DriverId = "midfielder",
                    Reputation = DriverReputation.PRIME_MIDFIELD // Min: MINNOW, Max: MIDFIELD_HIGH - CAN join MINNOW
                }
            };

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(1, result.Count(), "Midfield driver should join minnow team");
            Assert.AreEqual(0, adsWithNoCandidates.Count);
            Assert.AreEqual("midfielder", result.First().DriverId);
        }

        [TestMethod]
        public void PickPotentialNewDrivers_ChampionshipDriverCanJoinMidfield()
        {
            // Arrange - Championship driver can join midfield team
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "arrows",
                    TeamReputation = TeamReputation.MIDFIELD,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver
                {
                    DriverId = "hill",
                    Reputation = DriverReputation.AGEING_CHAMPIONSHIP_LEVEL // Min: MIDFIELD, Max: TOP_TEAM - CAN join MIDFIELD
                }
            };

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(1, result.Count(), "Championship driver should join midfield team");
            Assert.AreEqual(0, adsWithNoCandidates.Count);
            Assert.AreEqual("hill", result.First().DriverId);
        }

        #endregion

        #region PickPotentialNewDrivers - Edge Cases

        [TestMethod]
        public void PickPotentialNewDrivers_WithEmptyJobAds_ReturnsEmptyResults()
        {
            // Arrange
            var jobAds = new List<TeamJobAd>();
            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver { DriverId = "driver1", Reputation = DriverReputation.PRIME_MIDFIELD }
            };

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(0, result.Count());
            Assert.AreEqual(0, adsWithNoCandidates.Count);
        }

        [TestMethod]
        public void PickPotentialNewDrivers_WithEmptyDriverPool_AllAdsUnfilled()
        {
            // Arrange
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "ferrari",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                }
            };
            var poolOfDrivers = new List<UnemployedDriver>();

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(0, result.Count());
            Assert.AreEqual(1, adsWithNoCandidates.Count);
        }

        [TestMethod]
        public void PickPotentialNewDrivers_ProcessesHigherReputationTeamsFirst()
        {
            // Arrange - Teams should be processed in descending reputation order
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "minardi",
                    TeamReputation = TeamReputation.MINNOW,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                },
                new TeamJobAd
                {
                    TeamId = "ferrari",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver
                {
                    DriverId = "gooddriver",
                    Reputation = DriverReputation.PRIME_STRONG_MIDFIELD // Min: MINNOW, Max: TOP_TEAM - Can join both
                }
            };

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(1, result.Count());
            Assert.AreEqual("ferrari", result.First().TeamId, "Ferrari should get priority");
            Assert.AreEqual(1, adsWithNoCandidates.Count);
            Assert.AreEqual("minardi", adsWithNoCandidates.First().TeamId, "Minardi should be left empty");
        }

        [TestMethod]
        public void PickPotentialNewDrivers_MultipleDriversAvailable_HigherReputationDriversPickedFirst()
        {
            // Arrange
            var jobAds = new List<TeamJobAd>
            {
                new TeamJobAd
                {
                    TeamId = "ferrari",
                    TeamReputation = TeamReputation.TOP_TEAM,
                    Role = DriverRole.FIRST_DRIVER,
                    ExitingDriverId = null,
                    ExitingDriverWillingToRenew = false
                }
            };

            var poolOfDrivers = new List<UnemployedDriver>
            {
                new UnemployedDriver
                {
                    DriverId = "paydriver",
                    Reputation = DriverReputation.PAY_DRIVER_SEASON // Lower reputation
                },
                new UnemployedDriver
                {
                    DriverId = "champion",
                    Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL // Higher reputation
                }
            };

            // Act
            var result = _sut.PickPotentialNewDrivers(jobAds, poolOfDrivers, out var adsWithNoCandidates);

            // Assert
            Assert.AreEqual(1, result.Count());
            // Due to implementation's ordering by reputation, the championship driver should be picked
            // (though pay driver will actually be filtered out due to not meeting min reputation for TOP_TEAM)
            Assert.AreEqual("champion", result.First().DriverId);
        }

        #endregion
    }
}