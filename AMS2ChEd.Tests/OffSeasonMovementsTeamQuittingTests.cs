using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;
using AMS2ChEd.Business.Services.Contracts;

namespace AMS2ChEd.Tests.Business.Services
{
    [TestClass]
    public class OffSeasonMovementsTeamQuittingTests
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

        #region DropDrivers with TeamQuitting Tests

        [TestMethod]
        public void DropDrivers_TeamQuittingTrue_DropsAllDrivers()
        {
            // Arrange
            var teamSituation = new TeamSituation
            {
                TeamId = "QUITTING_TEAM",
                TeamQuitting = true,
                Reputation = TeamReputation.MIDFIELD,
                Driver1 = new DriverSituation
                {
                    DriverId = "D1",
                    DriverRetiring = false,
                    RacesLeftInContract = 10, // Still has contract
                    Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL // Top driver
                },
                Driver2 = new DriverSituation
                {
                    DriverId = "D2",
                    DriverRetiring = false,
                    RacesLeftInContract = 15, // Still has contract
                    Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL // Top driver
                }
            };

            // Act
            var results = _offSeasonMovements.DropDrivers(new[] { teamSituation }).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            var result = results[0];

            Assert.AreEqual("QUITTING_TEAM", result.TeamId);

            // Both drivers should be dropped because team is quitting, 
            // regardless of contract or reputation
            Assert.IsTrue(result.DropDriver1.IsDropped(),
                "Driver 1 should be dropped when team is quitting, even with valid contract");
            Assert.IsTrue(result.DropDriver2.IsDropped(),
                "Driver 2 should be dropped when team is quitting, even with valid contract");
        }

        [TestMethod]
        public void DropDrivers_TeamQuittingTrue_RetiringDriverShowsRetirementReason()
        {
            // Arrange - Team quitting with a retiring driver
            var teamSituation = new TeamSituation
            {
                TeamId = "QUITTING_TEAM",
                TeamQuitting = true,
                Reputation = TeamReputation.MIDFIELD,
                Driver1 = new DriverSituation
                {
                    DriverId = "D1",
                    DriverRetiring = true, // Retirement age (>= 43)
                    RacesLeftInContract = 10,
                    Reputation = DriverReputation.AGEING_MIDFIELD
                },
                Driver2 = new DriverSituation
                {
                    DriverId = "D2",
                    DriverRetiring = false, // Young driver
                    RacesLeftInContract = 10,
                    Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL
                }
            };

            // Act
            var results = _offSeasonMovements.DropDrivers(new[] { teamSituation }).ToList();

            // Assert
            var result = results[0];

            // CRITICAL: Retirement reason should take priority over team quitting
            Assert.AreEqual(DriverFirerOutcome.DROPPED_RETIRING, result.DropDriver1,
                "Retiring driver (age >= 43) should show DROPPED_RETIRING even when team is quitting");

            // Non-retiring driver on quitting team should show team-related reason
            Assert.IsTrue(result.DropDriver2.IsDropped(),
                "Young driver should be dropped because team is quitting");
            Assert.AreNotEqual(DriverFirerOutcome.DROPPED_RETIRING, result.DropDriver2,
                "Young driver should NOT show retirement reason");
        }

        [TestMethod]
        public void DropDrivers_TeamQuittingTrue_ContractExpiredShowsContractReason()
        {
            // Arrange - Team quitting with driver whose contract expired
            var teamSituation = new TeamSituation
            {
                TeamId = "QUITTING_TEAM",
                TeamQuitting = true,
                Reputation = TeamReputation.MIDFIELD,
                Driver1 = new DriverSituation
                {
                    DriverId = "D1",
                    DriverRetiring = true, // Driver retiring
                    RacesLeftInContract = 0, // Contract expired
                    Reputation = DriverReputation.PRIME_MIDFIELD
                },
                Driver2 = new DriverSituation
                {
                    DriverId = "D2",
                    DriverRetiring = false,
                    RacesLeftInContract = 10, // Valid contract
                    Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL
                }
            };

            // Act
            var results = _offSeasonMovements.DropDrivers(new[] { teamSituation }).ToList();

            // Assert
            var result = results[0];

            // Driver retiring should take priority over team quitting
            Assert.AreEqual(DriverFirerOutcome.DROPPED_RETIRING, result.DropDriver1,
                "Driver with expired contract should show DROPPED_TEAM_QUITTING even when team is quitting");

            // Driver with valid contract on quitting team
            Assert.IsTrue(result.DropDriver2.IsDropped(),
                "Driver with valid contract should still be dropped because team is quitting");
            Assert.AreEqual(DriverFirerOutcome.DROPPED_TEAM_QUITTING, result.DropDriver2,
                "Driver with valid contract should NOT show contract expiry reason");
        }

        [TestMethod]
        public void DropDrivers_TeamQuittingFalse_UsesNormalRules()
        {
            // Arrange
            var teamSituation = new TeamSituation
            {
                TeamId = "CONTINUING_TEAM",
                TeamQuitting = false,
                Reputation = TeamReputation.TOP_TEAM,
                Driver1 = new DriverSituation
                {
                    DriverId = "D1",
                    DriverRetiring = false,
                    RacesLeftInContract = 10, // Has contract
                    Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL // Fits top team
                },
                Driver2 = new DriverSituation
                {
                    DriverId = "D2",
                    DriverRetiring = false,
                    RacesLeftInContract = 5,
                    Reputation = DriverReputation.YOUNG_TALENT // Does not fit top team
                }
            };

            // Act
            var results = _offSeasonMovements.DropDrivers(new[] { teamSituation }).ToList();

            // Assert
            Assert.AreEqual(1, results.Count);
            var result = results[0];

            // Normal rules apply when team is not quitting
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, result.DropDriver1,
                "Championship driver with contract should be kept at top team");
            Assert.AreEqual(DriverFirerOutcome.DROPPED_UNDERPERFORMING, result.DropDriver2,
                "Young talent doesn't fit top team reputation and should be dropped");
        }

        [TestMethod]
        public void DropDrivers_TeamQuittingTrue_MultipleDropReasonsShowsHighestPriority()
        {
            // Arrange - Driver who is BOTH retiring AND on expired contract AND team quitting
            var teamSituation = new TeamSituation
            {
                TeamId = "QUITTING_TEAM",
                TeamQuitting = true,
                Reputation = TeamReputation.MIDFIELD,
                Driver1 = new DriverSituation
                {
                    DriverId = "D1",
                    DriverRetiring = true, // Retiring (>= 43)
                    RacesLeftInContract = 0, // Contract expired
                    Reputation = DriverReputation.AGEING_MIDFIELD
                },
                Driver2 = new DriverSituation
                {
                    DriverId = "D2",
                    DriverRetiring = false,
                    RacesLeftInContract = 10,
                    Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL
                }
            };

            // Act
            var results = _offSeasonMovements.DropDrivers(new[] { teamSituation }).ToList();

            // Assert
            var result = results[0];

            // Priority order should be: Retirement > Contract Expired > Team Quitting
            Assert.AreEqual(DriverFirerOutcome.DROPPED_RETIRING, result.DropDriver1,
                "When multiple reasons exist, retirement should take priority");

            Assert.IsTrue(result.DropDriver2.IsDropped(),
                "Driver 2 should be dropped due to team quitting");
        }

        [TestMethod]
        public void DropDrivers_MixedTeamsQuittingAndContinuing()
        {
            // Arrange
            var teamSituations = new[]
            {
                // Team quitting with retiring driver
                new TeamSituation
                {
                    TeamId = "QUITTING",
                    TeamQuitting = true,
                    Reputation = TeamReputation.MIDFIELD,
                    Driver1 = new DriverSituation
                    {
                        DriverId = "D1",
                        DriverRetiring = true, // Retiring
                        RacesLeftInContract = 10,
                        Reputation = DriverReputation.AGEING_MIDFIELD
                    },
                    Driver2 = new DriverSituation
                    {
                        DriverId = "D2",
                        DriverRetiring = false,
                        RacesLeftInContract = 10,
                        Reputation = DriverReputation.PRIME_MIDFIELD
                    }
                },
                // Team continuing - good drivers
                new TeamSituation
                {
                    TeamId = "CONTINUING",
                    TeamQuitting = false,
                    Reputation = TeamReputation.TOP_TEAM,
                    Driver1 = new DriverSituation
                    {
                        DriverId = "D3",
                        DriverRetiring = false,
                        RacesLeftInContract = 10,
                        Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL
                    },
                    Driver2 = new DriverSituation
                    {
                        DriverId = "D4",
                        DriverRetiring = false,
                        RacesLeftInContract = 10,
                        Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL
                    }
                }
            };

            // Act
            var results = _offSeasonMovements.DropDrivers(teamSituations).ToList();

            // Assert
            Assert.AreEqual(2, results.Count);

            var quittingResult = results.First(r => r.TeamId == "QUITTING");
            var continuingResult = results.First(r => r.TeamId == "CONTINUING");

            // Quitting team: retiring driver shows retirement reason
            Assert.AreEqual(DriverFirerOutcome.DROPPED_RETIRING, quittingResult.DropDriver1,
                "Retiring driver should show DROPPED_RETIRING reason");
            Assert.IsTrue(quittingResult.DropDriver2.IsDropped(),
                "Non-retiring driver should still be dropped (team quitting)");

            // Continuing team: drivers kept (they fit the reputation)
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, continuingResult.DropDriver1,
                "Championship driver should be kept at continuing top team");
            Assert.AreEqual(DriverFirerOutcome.NOT_DROPPED, continuingResult.DropDriver2,
                "Championship driver should be kept at continuing top team");
        }

        #endregion
    }
}