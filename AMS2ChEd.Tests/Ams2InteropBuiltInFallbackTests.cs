using Ams2Interop;
using Ams2Interop.Signatures;

namespace AMS2ChEd.Tests.Ams2Interop
{
    [TestClass]
    public class Ams2InteropBuiltInFallbackTests
    {
        private const long Base = Ams2Constants.DefaultImageBase;

        [TestMethod]
        public void BuiltInFallback_NoConfirmedBuildRecorded_UsesBuiltInAndWarns()
        {
            var log = new List<string>();

            var offsets = Ams2OffsetResolver.BuiltInFallback(Base, "AAAA-BBBB-CCCC", "no signature file", log.Add, Array.Empty<string>());

            Assert.AreEqual(Ams2OffsetSource.BuiltInUnverified, offsets.Source);
            Assert.IsFalse(offsets.IsDegraded);
            Assert.IsTrue(offsets.CanWriteSlots);
            Assert.IsTrue(log.Any(l => l.Contains("WARNING") && l.Contains("AAAA-BBBB-CCCC")), string.Join("\n", log));
        }

        [TestMethod]
        public void BuiltInFallback_RunningBuildIsConfirmed_UsesBuiltIn()
        {
            var offsets = Ams2OffsetResolver.BuiltInFallback(Base, "AAAA-BBBB-CCCC", "no signature file", null, new[] { "AAAA-BBBB-CCCC" });

            Assert.AreEqual(Ams2OffsetSource.BuiltInUnverified, offsets.Source);
            Assert.IsFalse(offsets.IsDegraded);
            foreach (var name in Ams2OffsetNames.All)
                Assert.AreEqual(Ams2OffsetNames.BuiltInRvas[name], offsets.Rva(name), name);
        }

        [TestMethod]
        public void BuiltInFallback_RunningBuildNotConfirmed_NothingResolved()
        {
            var offsets = Ams2OffsetResolver.BuiltInFallback(Base, "NEW-PATCHED-BUILD", "no signature file", null, new[] { "AAAA-BBBB-CCCC" });

            AssertRejected(offsets);
            StringAssert.Contains(offsets.Describe(), "isn't one the built-in offsets are confirmed on");
        }

        [TestMethod]
        public void BuiltInFallback_BuildIdUnreadable_NothingResolved()
        {
            var offsets = Ams2OffsetResolver.BuiltInFallback(Base, null, "module image unreadable", null, new[] { "AAAA-BBBB-CCCC" });

            AssertRejected(offsets);
            StringAssert.Contains(offsets.Describe(), "couldn't be identified");
        }

        [TestMethod]
        public void BuildIdFromHeaders_ReadsTimestampSizeAndChecksum()
        {
            var headers = new byte[0x1000];
            headers[0] = (byte)'M';
            headers[1] = (byte)'Z';
            const int nt = 0x80;
            BitConverter.GetBytes(nt).CopyTo(headers, 0x3C);
            BitConverter.GetBytes(0x00004550u).CopyTo(headers, nt);
            BitConverter.GetBytes(0x12345678u).CopyTo(headers, nt + 4 + 4);       // TimeDateStamp
            BitConverter.GetBytes(0x02A00000u).CopyTo(headers, nt + 4 + 20 + 56); // SizeOfImage
            BitConverter.GetBytes(0x0000BEEFu).CopyTo(headers, nt + 4 + 20 + 64); // CheckSum

            Assert.AreEqual("12345678-02A00000-0000BEEF", ModuleImage.BuildIdFromHeaders(headers));
            Assert.IsNull(ModuleImage.BuildIdFromHeaders(new byte[0x1000]));
        }

        private static void AssertRejected(Ams2ResolvedOffsets offsets)
        {
            Assert.AreEqual(Ams2OffsetSource.BuiltInRejected, offsets.Source);
            Assert.IsTrue(offsets.IsDegraded);
            Assert.AreEqual(0, offsets.Rvas.Count);
            Assert.IsFalse(offsets.CanResolveCustomRace);
            Assert.IsFalse(offsets.CanWriteSlots);
            Assert.IsFalse(offsets.CanSetCar);
            Assert.IsFalse(offsets.CanCommit);
            Assert.IsFalse(offsets.CanChangeGameplaySettings);
        }
    }
}
