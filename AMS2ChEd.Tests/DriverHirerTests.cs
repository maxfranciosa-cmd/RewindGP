using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Services;

namespace AMS2ChEd.Tests.Business.Services
{
    [TestClass]
    public class DriverHirerTests
    {
        private DriverHirer _driverHirer;

        [TestInitialize]
        public void Setup()
        {
            _driverHirer = new DriverHirer();
        }

        #region PickBestCandidate Tests

        [TestMethod]
        public void PickBestCandidate_NullDrivers_ReturnsNull()
        {
            var result = _driverHirer.PickBestCandidate(null, DriverRole.FIRST_DRIVER, TeamReputation.TOP_TEAM);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void PickBestCandidate_SingleDriver_ReturnsThatDriver()
        {
            var drivers = new[]
            {
                new DriverResume { Id = "D1", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
            };

            var result = _driverHirer.PickBestCandidate(drivers, DriverRole.FIRST_DRIVER, TeamReputation.TOP_TEAM);

            Assert.AreEqual("D1", result.Id);
        }

        [TestMethod]
        public void PickBestCandidate_OneClearlyBetterFit_PicksBestFit()
        {
            // For a TOP_TEAM first driver, PRIME_CHAMPIONSHIP_LEVEL is a PerfectFit while
            // PRIME_STRONG_MIDFIELD is UnderQualified - no ambiguity, no randomness involved.
            var drivers = new[]
            {
                new DriverResume { Id = "CHAMPION", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL },
                new DriverResume { Id = "MIDFIELDER", Reputation = DriverReputation.PRIME_STRONG_MIDFIELD }
            };

            var result = _driverHirer.PickBestCandidate(drivers, DriverRole.FIRST_DRIVER, TeamReputation.TOP_TEAM);

            Assert.AreEqual("CHAMPION", result.Id);
        }

        [TestMethod]
        public void PickBestCandidate_DifferentReputationsSameFitTier_GivesEachCandidateAFairChance()
        {
            // YOUNG_CHAMPIONSHIP_LEVEL and PRIME_CHAMPIONSHIP_LEVEL are both PerfectFit for a
            // TOP_TEAM first driver (see DriverHirer.teamPolicies) despite being different
            // DriverReputation values. Selection within a fit tier should be fair - not always
            // default to whichever has the (arbitrary, enum-ordinal) higher reputation.
            var wins = new HashSet<string>();

            for (int i = 0; i < 500; i++)
            {
                var drivers = new[]
                {
                    new DriverResume { Id = "A_YOUNG_CHAMP", Reputation = DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL },
                    new DriverResume { Id = "B_PRIME_CHAMP", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL }
                };

                var winner = _driverHirer.PickBestCandidate(drivers, DriverRole.FIRST_DRIVER, TeamReputation.TOP_TEAM);
                wins.Add(winner.Id);
            }

            Assert.AreEqual(2, wins.Count,
                "Both same-tier drivers should win at least once across 500 trials if selection is fair; " +
                "seeing only one winner suggests the tie is being broken deterministically again.");
        }

        [TestMethod]
        public void PickBestCandidate_SameExactReputation_GivesEachDriverAFairChance()
        {
            var wins = new HashSet<string>();

            for (int i = 0; i < 500; i++)
            {
                var drivers = new[]
                {
                    new DriverResume { Id = "D1", Reputation = DriverReputation.PRIME_MIDFIELD },
                    new DriverResume { Id = "D2", Reputation = DriverReputation.PRIME_MIDFIELD }
                };

                var winner = _driverHirer.PickBestCandidate(drivers, DriverRole.FIRST_DRIVER, TeamReputation.MIDFIELD);
                wins.Add(winner.Id);
            }

            Assert.AreEqual(2, wins.Count,
                "Both identically-reputed drivers should win at least once across 500 trials.");
        }

        #endregion
    }
}
