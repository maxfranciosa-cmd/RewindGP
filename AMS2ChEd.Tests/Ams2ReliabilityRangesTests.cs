using Ams2ChEd.Business.AMS2.Helpers;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class Ams2ReliabilityRangesTests
    {
        private const double TOLERANCE = 0.001;

        [TestMethod]
        public void TryGetRange_KnownClass_GivesItsGroupsRange()
        {
            Assert.IsTrue(Ams2ReliabilityRanges.TryGetRange("F-Classic_Gen3", out var classic));
            Assert.AreEqual((25.0, 700.0), classic);

            Assert.IsTrue(Ams2ReliabilityRanges.TryGetRange("f-v10_gen2", out var v10));
            Assert.AreEqual((100.0, 1500.0), v10);
        }

        [TestMethod]
        public void TryGetRange_UnknownOrMissingClass_IsFalse()
        {
            Assert.IsFalse(Ams2ReliabilityRanges.TryGetRange("GT3", out _));
            Assert.IsFalse(Ams2ReliabilityRanges.TryGetRange("", out _));
            Assert.IsFalse(Ams2ReliabilityRanges.TryGetRange(null, out _));
        }

        [TestMethod]
        public void ToRatio_WithinTheRange_MatchesReizasExample()
        {
            // the guide's own example: a class of 25-700 minutes, a target of 500 gives 0.839
            Assert.AreEqual(0.839, Ams2ReliabilityRanges.ToRatio((25, 700), 500), TOLERANCE);
            Assert.AreEqual(0.0, Ams2ReliabilityRanges.ToRatio((25, 700), 25), TOLERANCE);
        }

        [TestMethod]
        public void ToRatio_BelowTheMinimum_IsNegative()
        {
            Assert.AreEqual(-0.25, Ams2ReliabilityRanges.ToRatio((25, 700), 20), TOLERANCE);
        }

        [TestMethod]
        public void ToRatio_BeyondTheMaximum_IsCappedAtOne()
        {
            Assert.AreEqual(1.0, Ams2ReliabilityRanges.ToRatio((25, 700), 900), TOLERANCE);
        }

        [TestMethod]
        public void MtbfForFailureChance_GivesThatChanceOverTheRace()
        {
            double mtbf = Ams2ReliabilityRanges.MtbfForFailureChance(0.35, 90);

            Assert.AreEqual(0.35, 1 - Math.Exp(-90 / mtbf), TOLERANCE);
        }
    }
}
