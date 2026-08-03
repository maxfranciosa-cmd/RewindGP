using Ams2ChEd.Business.AMS2.PakPatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class BffPathHashTests
    {
        [TestMethod]
        public void ComputeUid_EmptyString_ReturnsFixedSentinel()
        {
            Assert.AreEqual(0x8DB63936938575BFUL, BffPathHash.ComputeUid(string.Empty));
        }

        [TestMethod]
        public void ComputeUid_IsCaseInsensitive()
        {
            ulong lower = BffPathHash.ComputeUid(@"vehicles\formula_hitech_g1m3\formula_hitech_g1m3.rcf");
            ulong upper = BffPathHash.ComputeUid(@"VEHICLES\FORMULA_HITECH_G1M3\FORMULA_HITECH_G1M3.RCF");

            Assert.AreEqual(lower, upper);
        }

        [TestMethod]
        public void ComputeUid_NormalizesForwardSlashesToBackslashes()
        {
            ulong withSlash = BffPathHash.ComputeUid("vehicles/formula_hitech_g1m3/formula_hitech_g1m3.rcf");
            ulong withBackslash = BffPathHash.ComputeUid(@"vehicles\formula_hitech_g1m3\formula_hitech_g1m3.rcf");

            Assert.AreEqual(withSlash, withBackslash);
        }

        [TestMethod]
        public void ComputeUid_DifferentPaths_ProduceDifferentHashes()
        {
            ulong a = BffPathHash.ComputeUid(@"vehicles\car_a\car_a.rcf");
            ulong b = BffPathHash.ComputeUid(@"vehicles\car_b\car_b.rcf");

            Assert.AreNotEqual(a, b);
        }

        [TestMethod]
        public void ComputeUid_IsDeterministic()
        {
            string path = @"vehicles\formula_hitech_g1m3\formula_hitech_g1m3.rcf_hr";

            ulong first = BffPathHash.ComputeUid(path);
            ulong second = BffPathHash.ComputeUid(path);

            Assert.AreEqual(first, second);
        }

        [TestMethod]
        public void ComputeUid_HandlesLongPathsAcrossTheTwentyFourByteChunkBoundary()
        {
            // Exercises the >=24-byte chunked branch as well as the tail switch, since real pak
            // paths (e.g. vehiclespersistent.bff entries) commonly exceed 24 characters.
            string longPath = @"vehicles\formula_hitech_g1m3\formula_hitech_g1m3_some_very_long_variant_name.rcf_hr";

            ulong result = BffPathHash.ComputeUid(longPath);

            Assert.AreNotEqual(0UL, result);
        }
    }
}
