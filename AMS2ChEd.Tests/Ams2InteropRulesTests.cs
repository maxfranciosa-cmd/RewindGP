using Ams2Interop;
using Ams2Interop.Native;

namespace AMS2ChEd.Tests.Ams2Interop
{
    /// <summary>The Rules &amp; Regulations object: finding it (RulesVmResolver) and writing its settings (CustomRaceWriter.ApplyRules).</summary>
    [TestClass]
    public class Ams2InteropRulesTests
    {
        private const long ModuleBase = 0x140000000;
        private const long ModuleSize = 0x3000000;
        private const long RulesTag = 0x141FDC720;
        private const long Vtable = 0x141000200;

        private static long NewRules(FakeCustomRace race, int pitKph = 60, int fcy = 1, long tag = RulesTag, long vtable = Vtable)
        {
            long rules = race.NewObject();
            race.Mem.PutInt64(rules, vtable);
            race.Mem.PutInt64(rules + 8, tag);
            race.SetSlot(rules, Ams2Constants.RulesSlot.PitSpeedLimit, pitKph);
            race.SetSlot(rules, Ams2Constants.RulesSlot.FullCourseYellow, fcy);
            return rules;
        }

        // master and vm498 as the interop resolves them: only their addresses matter here
        private const long Master = 0x2_1000_0000, Vm498 = 0x2_2000_0000;

        private static RulesVmResolver Resolver(FakeCustomRace race) =>
            new(race.Mem, ModuleBase, ModuleSize, RulesTag, FakeCustomRace.Tag);

        // a context object holding [master][vm498][rules] consecutively, as the game does
        private static void Context(FakeCustomRace race, long rules, long master = Master, long vm498 = Vm498)
        {
            long context = race.NewObject();
            race.Mem.PutInt64(context + 0x30, master);
            race.Mem.PutInt64(context + 0x38, vm498);
            race.Mem.PutInt64(context + 0x40, rules);
        }

        // the custom event object: master +0xF0, vm498 +0x100, rules +0x110
        private static void Event(FakeCustomRace race, long rules)
        {
            long ev = race.NewObject();
            race.Mem.PutInt64(ev + 0xF0, Master);
            race.Mem.PutInt64(ev + 0x100, Vm498);
            race.Mem.PutInt64(ev + 0x110, rules);
        }

        [TestMethod]
        public void Resolve_TheRulesObjectBesideMasterAndVm498_IsFound_OtherRulesObjectsIgnored()
        {
            var race = new FakeCustomRace();
            NewRules(race, pitKph: 95); // another game mode's, or the Rules page's copy
            long rules = NewRules(race, pitKph: 95);
            NewRules(race);
            Context(race, rules);
            Event(race, rules);

            Assert.AreEqual(rules, Resolver(race).Resolve(Master, Vm498));
        }

        [TestMethod]
        public void Resolve_OnlyTheEventObject_IsEnough()
        {
            var race = new FakeCustomRace();
            long rules = NewRules(race);
            Event(race, rules);

            Assert.AreEqual(rules, Resolver(race).Resolve(Master, Vm498));
        }

        [TestMethod]
        public void Resolve_BesideAnotherMaster_IsNotTaken()
        {
            var race = new FakeCustomRace();
            Context(race, NewRules(race), master: 0x2_3000_0000);

            Assert.IsNull(Resolver(race).Resolve(Master, Vm498));
        }

        [TestMethod]
        public void Resolve_TwoDifferentRulesObjectsBesideMaster_TakesNeither()
        {
            var race = new FakeCustomRace();
            Context(race, NewRules(race));
            Event(race, NewRules(race, pitKph: 80));

            Assert.IsNull(Resolver(race).Resolve(Master, Vm498));
        }

        [TestMethod]
        public void Resolve_SomethingElseBesideMaster_IsNotTaken()
        {
            var race = new FakeCustomRace();
            Context(race, NewRules(race, tag: 0x141FDC999));

            Assert.IsNull(Resolver(race).Resolve(Master, Vm498));
        }

        [TestMethod]
        [DataRow(30, 1)]   // pit limit below what the page allows
        [DataRow(60, 2)]   // full course yellow not a 0/1
        public void Resolve_SlotsOutOfRange_RefusesTheObject(int pitKph, int fcy)
        {
            var race = new FakeCustomRace();
            Context(race, NewRules(race, pitKph, fcy));

            Assert.IsNull(Resolver(race).Resolve(Master, Vm498));
        }

        [TestMethod]
        public void ApplyRules_WritesThePitLimitAndFullCourseYellow()
        {
            var race = new FakeCustomRace();
            long rules = NewRules(race);
            var setter = new FakeSlotSetter();
            var writer = new CustomRaceWriter(race.Mem, race.Mem, setter, FakeCustomRace.Tag);
            var unverified = new List<(string, int, int?)>();

            writer.ApplyRules(rules, new SessionRulesConfig { PitSpeedLimitKph = 80, FullCourseYellow = OnOff.Off }, unverified);

            CollectionAssert.AreEquivalent(new[]
            {
                (rules, Ams2Constants.RulesSlot.PitSpeedLimit, 80),
                (rules, Ams2Constants.RulesSlot.FullCourseYellow, 0),
            }, setter.Calls);
            Assert.AreEqual(0, unverified.Count);
        }

        [TestMethod]
        public void ApplyRules_PitLimitOutOfRange_IsReportedNotSent()
        {
            var race = new FakeCustomRace();
            long rules = NewRules(race);
            var setter = new FakeSlotSetter();
            var writer = new CustomRaceWriter(race.Mem, race.Mem, setter, FakeCustomRace.Tag);
            var unverified = new List<(string, int, int?)>();

            writer.ApplyRules(rules, new SessionRulesConfig { PitSpeedLimitKph = 300 }, unverified);

            Assert.AreEqual(0, setter.Calls.Count);
            Assert.AreEqual(1, unverified.Count);
        }
    }
}
