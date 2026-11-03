using Ams2Interop;

namespace AMS2ChEd.Tests.Ams2Interop
{
    [TestClass]
    public class Ams2InteropCommitCheckTests
    {
        [TestMethod]
        public void CheckCommitResult_PlausibleClassCounts_NothingReported()
        {
            var (writer, layout, vm498, _) = Setup(26, 26);

            Assert.AreEqual(0, writer.CheckCommitResult(vm498, layout).Count);
        }

        [TestMethod]
        [DataRow(0, 26)]
        [DataRow(26, 65)]
        [DataRow(-1, -1)]
        public void CheckCommitResult_ImplausibleClassCount_UnverifiedEntryAdded(int a, int b)
        {
            var (writer, layout, vm498, _) = Setup(a, b);

            var problems = writer.CheckCommitResult(vm498, layout);

            Assert.IsTrue(problems.Count > 0);
            Assert.IsTrue(problems.All(p => p.Field.StartsWith(CustomRaceWriter.CommitResultField)));
        }

        [TestMethod]
        public void CheckCommitResult_WronglyTaggedSlot_UnverifiedEntryAdded()
        {
            var (writer, layout, vm498, race) = Setup(26, 26);
            race.SetSlot(vm498, Ams2Constants.Vm498Slot.CommitClassCountB, 26, tag: 0x99);

            var problems = writer.CheckCommitResult(vm498, layout);

            Assert.AreEqual(1, problems.Count);
            Assert.IsNull(problems[0].Got);
        }

        [TestMethod]
        public void RaceConfigResult_OnlyAdvisoryEntries_StillSuccessful()
        {
            var (writer, layout, vm498, _) = Setup(0, 0);
            var problems = writer.CheckCommitResult(vm498, layout);

            var advisoryOnly = new RaceConfigResult
            {
                UnverifiedFields = problems.Select(p => (p.Field, p.Wanted, p.Got)).ToList(),
                AdvisoryFields = problems.Select(p => p.Field).ToArray(),
            };
            var withARealFailure = new RaceConfigResult
            {
                UnverifiedFields = advisoryOnly.UnverifiedFields.Append(("OpponentCount", 21, 20)).ToList(),
                AdvisoryFields = advisoryOnly.AdvisoryFields,
            };

            Assert.IsTrue(advisoryOnly.Success);
            Assert.IsFalse(withARealFailure.Success);
        }

        private static (CustomRaceWriter Writer, CustomRaceLayout Layout, long Vm498, FakeCustomRace Race) Setup(int countA, int countB)
        {
            var race = new FakeCustomRace();
            var vm498 = race.NewVm498();
            race.SetSlot(vm498, Ams2Constants.Vm498Slot.CommitClassCountA, countA);
            race.SetSlot(vm498, Ams2Constants.Vm498Slot.CommitClassCountB, countB);
            var writer = new CustomRaceWriter(race.Mem, race.Mem, new FakeSlotSetter(), FakeCustomRace.Tag);
            var layout = writer.CheckLayout(new CustomRaceTargets { Vm550 = race.NewVm550(), Vm498 = vm498 });
            return (writer, layout, vm498, race);
        }
    }
}
