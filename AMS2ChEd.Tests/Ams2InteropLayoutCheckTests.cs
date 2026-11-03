using Ams2Interop;
using Ams2Interop.Native;

namespace AMS2ChEd.Tests.Ams2Interop
{
    [TestClass]
    public class Ams2InteropLayoutCheckTests
    {
        [TestMethod]
        public void CheckVm550_AllInRange_Passes()
        {
            var race = new FakeCustomRace();
            var vm = race.NewVm550();

            var result = new Ams2LayoutCheck(race.Mem, FakeCustomRace.Tag).CheckVm550(vm);

            Assert.IsTrue(result.Passed, result.Describe());
            Assert.AreEqual(2026, result.Values[nameof(Ams2Constants.Vm550Slot.Year)]);
        }

        [TestMethod]
        public void CheckVm550_OneOutOfRange_Fails()
        {
            var race = new FakeCustomRace();
            var vm = race.NewVm550(hour: 24);

            var result = new Ams2LayoutCheck(race.Mem, FakeCustomRace.Tag).CheckVm550(vm);

            Assert.IsFalse(result.Passed);
            var failure = result.Failures.Single();
            Assert.AreEqual(Ams2Constants.Vm550Slot.Hour, failure.Slot);
            Assert.AreEqual(24, failure.Value);
        }

        [TestMethod]
        public void CheckVm550_UnreadableNode_Fails()
        {
            var race = new FakeCustomRace();
            var vm = race.NewVm550();
            race.RemoveSlot(vm, Ams2Constants.Vm550Slot.Month);

            var result = new Ams2LayoutCheck(race.Mem, FakeCustomRace.Tag).CheckVm550(vm);

            Assert.IsFalse(result.Passed);
            StringAssert.Contains(result.Failures.Single().Problem, "unreadable");
        }

        [TestMethod]
        public void CheckVm550_WronglyTaggedNode_Fails()
        {
            var race = new FakeCustomRace();
            var vm = race.NewVm550();
            race.SetSlot(vm, Ams2Constants.Vm550Slot.ScheduledFcy, 0, tag: FakeCustomRace.Tag + 0x40);

            var result = new Ams2LayoutCheck(race.Mem, FakeCustomRace.Tag).CheckVm550(vm);

            Assert.IsFalse(result.Passed);
            StringAssert.Contains(result.Failures.Single().Problem, "wrong tag");
        }

        [TestMethod]
        [DataRow(69)]
        [DataRow(121)]
        public void CheckVm498_SkillOutOfRange_Fails(int skill)
        {
            var race = new FakeCustomRace();
            var vm498 = race.NewVm498(skill: skill);

            Assert.IsFalse(new Ams2LayoutCheck(race.Mem, FakeCustomRace.Tag).CheckVm498(vm498).Passed);
        }

        [TestMethod]
        public void CheckVm498_AllInRange_Passes()
        {
            var race = new FakeCustomRace();
            var vm498 = race.NewVm498();

            Assert.IsTrue(new Ams2LayoutCheck(race.Mem, FakeCustomRace.Tag).CheckVm498(vm498).Passed);
        }

        [TestMethod]
        public void CheckMaster_SelectionSlotsMustReadWithAValidTag()
        {
            var race = new FakeCustomRace();
            var master = race.NewMaster();
            var check = new Ams2LayoutCheck(race.Mem, FakeCustomRace.Tag);

            Assert.IsTrue(check.CheckMaster(master).Passed);

            race.SetSlot(master, Ams2Constants.MasterSlot.Livery, 3, tag: 0x1234);
            Assert.IsFalse(check.CheckMaster(master).Passed);
        }

        [TestMethod]
        public void Writer_RaceVmFails_NothingIsWritten()
        {
            var race = new FakeCustomRace();
            var targets = new CustomRaceTargets
            {
                Vm550 = race.NewVm550(hour: 99),
                Vm498 = race.NewVm498(),
                Master = race.NewMaster(),
                Practice1Vm = race.NewVm550(),
                Qualifying1Vm = race.NewVm550(),
                Race2Vm = race.NewVm550(),
            };
            var setter = new FakeSlotSetter();
            var writer = new CustomRaceWriter(race.Mem, race.Mem, setter, FakeCustomRace.Tag);

            var layout = writer.CheckLayout(targets);
            var unverified = new List<(string, int, int?)>();
            writer.ApplyOnce(targets, layout, Opponents(), SessionRules(), Session(), Session(), unverified, unverified);

            Assert.IsTrue(layout.RaceVmFailed);
            Assert.IsTrue(layout.AnyFailed);
            Assert.AreEqual(0, setter.Calls.Count, "no setter call may reach the game");
            Assert.AreEqual(0, race.Mem.RawWrites.Count, "no raw write may reach the game");
            Assert.IsTrue(layout.Failures.Any(f => f.Field.Contains("Race VM550") && f.Field.Contains("Hour")));
        }

        [TestMethod]
        public void Writer_Vm498Fails_SessionWrittenButNotOpponentsNorPackedDate()
        {
            var race = new FakeCustomRace();
            var targets = new CustomRaceTargets
            {
                Vm550 = race.NewVm550(),
                Vm498 = race.NewVm498(rivalCount: 900),
                Practice1Vm = race.NewVm550(),
                Qualifying1Vm = race.NewVm550(),
                Race2Vm = race.NewVm550(),
            };
            var setter = new FakeSlotSetter();
            var writer = new CustomRaceWriter(race.Mem, race.Mem, setter, FakeCustomRace.Tag);

            var layout = writer.CheckLayout(targets);
            var unverified = new List<(string, int, int?)>();
            writer.ApplyOnce(targets, layout, Opponents(), SessionRules(), null, null, unverified, unverified);

            Assert.IsFalse(layout.RaceVmFailed);
            Assert.IsTrue(layout.Vm498Failed);
            Assert.IsFalse(setter.Calls.Any(c => c.Vm == targets.Vm498), "VM498 must not be written");
            Assert.AreEqual(0, race.Mem.RawWrites.Count, "the VM498 packed date must not be written");
            Assert.IsTrue(setter.Calls.Any(c => c.Vm == targets.Vm550 && c.Slot == Ams2Constants.Vm550Slot.Hour));
        }

        [TestMethod]
        public void Writer_SessionVmFails_OnlyThatSessionIsSkipped()
        {
            var race = new FakeCustomRace();
            var targets = new CustomRaceTargets
            {
                Vm550 = race.NewVm550(),
                Vm498 = race.NewVm498(),
                Practice1Vm = race.NewVm550(month: 13),
                Qualifying1Vm = race.NewVm550(),
                Race2Vm = race.NewVm550(),
            };
            var setter = new FakeSlotSetter();
            var writer = new CustomRaceWriter(race.Mem, race.Mem, setter, FakeCustomRace.Tag);

            var layout = writer.CheckLayout(targets);
            var unverified = new List<(string, int, int?)>();
            writer.ApplyOnce(targets, layout, Opponents(), SessionRules(), Session(), Session(), unverified, unverified);

            Assert.IsTrue(layout.Failed(CustomRaceWriter.Practice1Label));
            Assert.IsFalse(setter.Calls.Any(c => c.Vm == targets.Practice1Vm));
            Assert.IsTrue(setter.Calls.Any(c => c.Vm == targets.Qualifying1Vm));
            Assert.IsTrue(setter.Calls.Any(c => c.Vm == targets.Race2Vm));
            Assert.IsTrue(setter.Calls.Any(c => c.Vm == targets.Vm498));
        }

        [TestMethod]
        public void Writer_AllPass_EveryObjectWritten()
        {
            var race = new FakeCustomRace();
            var targets = new CustomRaceTargets
            {
                Vm550 = race.NewVm550(),
                Vm498 = race.NewVm498(),
                Practice1Vm = race.NewVm550(),
                Qualifying1Vm = race.NewVm550(),
                Race2Vm = race.NewVm550(),
            };
            var setter = new FakeSlotSetter();
            var writer = new CustomRaceWriter(race.Mem, race.Mem, setter, FakeCustomRace.Tag);

            var layout = writer.CheckLayout(targets);
            var unverified = new List<(string, int, int?)>();
            writer.ApplyOnce(targets, layout, Opponents(), SessionRules(), Session(), Session(), unverified, unverified);

            Assert.IsFalse(layout.AnyFailed, string.Join("; ", layout.Failures));
            Assert.AreEqual((15, 6, 2026), layout.RaceVmDate);
            foreach (var vm in new[] { targets.Vm550, targets.Vm498, targets.Practice1Vm, targets.Qualifying1Vm, targets.Race2Vm })
                Assert.IsTrue(setter.Calls.Any(c => c.Vm == vm));
            Assert.AreEqual(1, race.Mem.RawWrites.Count, "the VM498 packed date");
            Assert.AreEqual(0, unverified.Count, string.Join("; ", unverified));
        }

        internal static OpponentsConfig Opponents() => new()
        {
            NumOpponentsType = NumOpponentsType.Custom,
            OpponentsType = OpponentsTypeKind.SameClass,
            OpponentCount = 21,
            Skill = 95,
        };

        internal static SessionRulesConfig SessionRules() => new()
        {
            StartHour = 14,
            DateType = DateType.Custom,
            RaceDate = new DateTime(1996, 3, 10),
            RollingStart = StartType.Grid,
            Weather = new SessionWeatherConfig { Slots = new[] { WeatherType.Clear } },
        };

        internal static PracticeQualifySessionConfig Session() => new() { DurationValue = 30, StartHour = 13 };
    }
}
