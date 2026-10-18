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
        public void ReputationScore_FollowsTheStandingLevels()
        {
            var expected = new Dictionary<DriverReputation, int>
            {
                { DriverReputation.PAY_DRIVER_WILD_CARD, 1 },
                { DriverReputation.PAY_DRIVER_SEASON, 2 },
                { DriverReputation.AGEING_MIDFIELD, 3 },
                { DriverReputation.YOUNG_TALENT, 3 },
                { DriverReputation.PRIME_MIDFIELD, 3 },
                { DriverReputation.AGEING_STRONG_MIDFIELD, 4 },
                { DriverReputation.PRIME_STRONG_MIDFIELD, 4 },
                { DriverReputation.JUST_ONE_LAST_DANCE, 4 },
                { DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED, 5 },
                { DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED, 5 },
                { DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN, 5 },
                { DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN, 5 },
                { DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, 6 },
                { DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, 6 },
                { DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL, 6 },
            };

            Assert.AreEqual(Enum.GetValues<DriverReputation>().Length, expected.Count, "every reputation needs a level");
            foreach (var pair in expected)
                Assert.AreEqual(pair.Value, DriverHirer.ReputationScore(pair.Key), pair.Key.ToString());
        }

        [TestMethod]
        public void PickWeightedByReputation_OneLevelApart_StrongerDriverTwiceAsLikely()
        {
            var random = new Random(1234);
            var candidates = new[]
            {
                new DriverResume { Id = "MIDFIELD", Reputation = DriverReputation.PRIME_MIDFIELD },
                new DriverResume { Id = "STRONG", Reputation = DriverReputation.PRIME_STRONG_MIDFIELD }
            };

            int midfieldWins = Enumerable.Range(0, 6000)
                .Count(_ => DriverHirer.PickWeightedByReputation(candidates, d => d.Reputation, random).Id == "MIDFIELD");

            // 2^3 : 2^4 = 1 : 2, so the midfield driver lands the seat about a third of the time
            double share = midfieldWins / 6000.0;
            Assert.IsTrue(share > 0.30 && share < 0.37, $"midfield share {share:0.000}, expected about 0.333");
        }

        [TestMethod]
        public void PickWeightedByReputation_SameLevel_EquallyLikely()
        {
            var random = new Random(99);
            var candidates = new[]
            {
                new DriverResume { Id = "A", Reputation = DriverReputation.AGEING_STRONG_MIDFIELD },
                new DriverResume { Id = "B", Reputation = DriverReputation.PRIME_STRONG_MIDFIELD }
            };

            int aWins = Enumerable.Range(0, 6000)
                .Count(_ => DriverHirer.PickWeightedByReputation(candidates, d => d.Reputation, random).Id == "A");

            double share = aWins / 6000.0;
            Assert.IsTrue(share > 0.46 && share < 0.54, $"share {share:0.000}, expected about 0.5");
        }

        [TestMethod]
        public void PickWeightedByReputation_NoCandidates_ReturnsNull()
        {
            Assert.IsNull(DriverHirer.PickWeightedByReputation(new DriverResume[0], d => d.Reputation, new Random()));
        }

        [TestMethod]
        public void PickBestCandidate_SameFitTierDifferentLevels_WeightsByReputation()
        {
            // For a MIDFIELD second driver both are PerfectFit, but AGEING_STRONG_MIDFIELD is one
            // level above PRIME_MIDFIELD: roughly 2 picks to 1, and the weaker still gets the seat.
            int primeMidfieldWins = 0;
            for (int i = 0; i < 3000; i++)
            {
                var drivers = new[]
                {
                    new DriverResume { Id = "PRIME_MIDFIELD", Reputation = DriverReputation.PRIME_MIDFIELD },
                    new DriverResume { Id = "AGEING_STRONG", Reputation = DriverReputation.AGEING_STRONG_MIDFIELD }
                };
                if (_driverHirer.PickBestCandidate(drivers, DriverRole.SECOND_DRIVER, TeamReputation.MIDFIELD).Id == "PRIME_MIDFIELD")
                    primeMidfieldWins++;
            }

            double share = primeMidfieldWins / 3000.0;
            Assert.IsTrue(share > 0.27 && share < 0.40, $"PRIME_MIDFIELD share {share:0.000}, expected about 0.333");
        }

        [TestMethod]
        public void DoesDriverFitTeamPolicy_AgeingChampionForTopTeamLead_IsPerfectFit()
        {
            // AGEING_CHAMPIONSHIP_LEVEL sits below YOUNG_CHAMPIONSHIP_LEVEL in the enum, so when it
            // was missing from the TOP_TEAM first-driver policy a reigning champion aged 31+ counted
            // as UnderQualified - no better than a midfield driver for the lead seat.
            var fit = _driverHirer.DoesDriverFitTeamPolicy(DriverReputation.AGEING_CHAMPIONSHIP_LEVEL, DriverRole.FIRST_DRIVER, TeamReputation.TOP_TEAM);

            Assert.AreEqual(DriverHirer.DriverPolicyFit.PerfectFit, fit);
        }

        [TestMethod]
        public void PickBestCandidate_AgeingChampionAgainstMidfielders_TopTeamLeadPicksTheChampion()
        {
            for (int i = 0; i < 100; i++)
            {
                var drivers = new[]
                {
                    new DriverResume { Id = "MIDFIELDER", Reputation = DriverReputation.PRIME_STRONG_MIDFIELD },
                    new DriverResume { Id = "AGEING_CHAMPION", Reputation = DriverReputation.AGEING_CHAMPIONSHIP_LEVEL },
                    new DriverResume { Id = "PAY_DRIVER", Reputation = DriverReputation.PAY_DRIVER_WILD_CARD }
                };

                var result = _driverHirer.PickBestCandidate(drivers, DriverRole.FIRST_DRIVER, TeamReputation.TOP_TEAM);

                Assert.AreEqual("AGEING_CHAMPION", result.Id);
            }
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
