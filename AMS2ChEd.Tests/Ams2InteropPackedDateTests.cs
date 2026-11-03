using Ams2Interop;
using Ams2Interop.Native;

namespace AMS2ChEd.Tests.Ams2Interop
{
    [TestClass]
    public class Ams2InteropPackedDateTests
    {
        [TestMethod]
        public void PlausiblePackedDate_IsWritten_TopSevenBitsPreserved()
        {
            var race = new FakeCustomRace();
            const uint flags = 0xA4000000u;
            var targets = new CustomRaceTargets
            {
                Vm550 = race.NewVm550(),
                Vm498 = race.NewVm498(packedDate: Vm498PackedDate.Encode(2026, 11, 3, flags)),
            };
            var writer = new CustomRaceWriter(race.Mem, race.Mem, new FakeSlotSetter(), FakeCustomRace.Tag);
            var layout = writer.CheckLayout(targets);
            var unverified = new List<(string, int, int?)>();

            writer.ApplyOnce(targets, layout, null, Ams2InteropLayoutCheckTests.SessionRules(), null, null, unverified, unverified);

            var write = race.Mem.RawWrites.Single();
            Assert.AreEqual(targets.Vm498 + Ams2Constants.Vm498DatePackedOffset, write.Address);
            var written = Vm498PackedDate.Decode(BitConverter.ToUInt32(write.Bytes, 0));
            Assert.AreEqual((1996, 3, 10), (written.Year, written.Month, written.Day));
            Assert.AreEqual(flags, written.PreservedFlagsBits);
            Assert.IsFalse(layout.AnyFailed);
        }

        [TestMethod]
        [DataRow(0x00000000u)]   // day 0, month 0, year 0
        [DataRow(0x01FFFFFFu)]   // year 65535
        [DataRow(0x000FC1E1u)]   // 2016, month 15, day 1
        public void ImplausiblePackedDate_IsNotWritten_AndFailsTheLayout(uint raw)
        {
            var race = new FakeCustomRace();
            var targets = new CustomRaceTargets { Vm550 = race.NewVm550(), Vm498 = race.NewVm498(packedDate: raw) };
            var setter = new FakeSlotSetter();
            var writer = new CustomRaceWriter(race.Mem, race.Mem, setter, FakeCustomRace.Tag);
            var layout = writer.CheckLayout(targets);
            var unverified = new List<(string, int, int?)>();

            // both passes, as ApplyRaceConfigAsync runs them
            writer.ApplyOnce(targets, layout, null, Ams2InteropLayoutCheckTests.SessionRules(), null, null, unverified, unverified);
            writer.ApplyOnce(targets, layout, null, Ams2InteropLayoutCheckTests.SessionRules(), null, null, unverified, unverified);

            Assert.AreEqual(0, race.Mem.RawWrites.Count);
            Assert.IsTrue(layout.AnyFailed);
            Assert.IsTrue(layout.Failed(CustomRaceWriter.Vm498PackedDateLabel));
            Assert.AreEqual(1, layout.Failures.Count(f => f.Field.Contains("unexpected layout")), "reported once, not per pass");
            Assert.IsTrue(setter.Calls.Any(c => c.Vm == targets.Vm550), "the VM550 slots still pass their own check");
        }
    }
}
