using Ams2Interop;
using Ams2Interop.Discovery;
using Ams2Interop.Native;
using Ams2Interop.Signatures;

namespace AMS2ChEd.Tests.Ams2Interop
{
    /// <summary>
    /// CustomRaceSurvey over a fake "patched" AMS2: new object tags, and VM550's Hour moved from
    /// slot 51 to slot 60. The "developer" is a callback that changes the fake's values the way the
    /// game would when that setting is changed in the UI.
    /// </summary>
    [TestClass]
    public class Ams2InteropSurveyTests
    {
        private const long ModuleBase = 0x140000000;
        private const long ModuleSize = 0x3000000;

        // the old (current constants) tags, and the "patched" build's
        private const long OldVm550Tag = 0x141FDCBE0, OldVm498Tag = 0x141FDCB28, OldContainerTag = 0x141FDD518;
        private const long NewVm550Tag = 0x141FE0100, NewVm498Tag = 0x141FE0048, NewContainerTag = 0x141FE0A38;
        private const int MovedHourSlot = 60;

        private sealed class PatchedGame
        {
            public FakeCustomRace Race { get; } = new();
            public FakeMemory Mem => Race.Mem;
            public long Master, Vm498, Vm550;
            public readonly List<long> SessionVms = new();
            public long? NoiseNode;
            private int _noise;

            public long Node(long obj, int slot) => Mem.TryReadInt64(obj + slot * 8L, out var node) ? node : throw new InvalidOperationException();
            public int Value(long obj, int slot) => Mem.TryReadInt32(Node(obj, slot) + 0x18c, out var v) ? v : throw new InvalidOperationException();
            public void Set(long obj, int slot, int value) => Mem.PutInt32(Node(obj, slot) + 0x18c, value);
            public void Bump(long obj, int slot) => Set(obj, slot, Value(obj, slot) + 1);
            public void Tick() { if (NoiseNode is long n) Mem.PutInt32(n + 0x18c, ++_noise); }

            public void Header(long obj, long tag, long vtable = 0x141000100)
            {
                Mem.PutInt64(obj, vtable);
                Mem.PutInt64(obj + 8, tag);
            }
        }

        private static PatchedGame BuildPatchedGame()
        {
            var g = new PatchedGame();
            var r = g.Race;

            g.Vm550 = r.NewVm550();
            g.Header(g.Vm550, NewVm550Tag);
            r.RemoveSlot(g.Vm550, Ams2Constants.Vm550Slot.Hour);
            r.SetSlot(g.Vm550, MovedHourSlot, 12);

            g.Vm498 = r.NewVm498();
            g.Header(g.Vm498, NewVm498Tag);
            r.SetSlot(g.Vm498, Ams2Constants.Vm498Slot.RivalCountPerClass, 20);
            // eight session wrappers; Race1 (index 6) leads to the race VM550 itself
            for (int i = 0; i < 8; i++)
            {
                long wrapper = r.NewObject();
                long session = i == Ams2Constants.SessionIndex.Race1 ? g.Vm550 : r.NewVm550();
                if (session != g.Vm550) g.Header(session, NewVm550Tag);
                g.SessionVms.Add(session);
                r.Mem.PutInt64(g.Vm498 + 0x18 + i * 8L, wrapper);
                r.Mem.PutInt64(wrapper + 0x18, session);
            }

            g.Master = r.NewMaster();
            g.Header(g.Master, NewContainerTag);
            r.Mem.PutInt64(g.Master + Ams2Constants.Vm498Offset, g.Vm498);
            r.Mem.Put(g.Master + Ams2Constants.MasterActiveFlagsOffset, new byte[] { 1, 1 });
            return g;
        }

        private static CustomRaceSurvey Survey(PatchedGame g) =>
            new(g.Mem, ModuleBase, ModuleSize, FakeCustomRace.Tag);

        private static readonly Dictionary<SurveyObject, long> OldTags = new()
        {
            [SurveyObject.Vm550] = OldVm550Tag, [SurveyObject.Vm498] = OldVm498Tag, [SurveyObject.Master] = OldContainerTag,
        };

        /// <summary>What changing each setting in the UI does to the fake game.</summary>
        private static Func<SurveyStep, int, SurveyAnswer> Developer(PatchedGame g, Action<SurveyStep>? extra = null) => (step, _) =>
        {
            g.Tick();
            switch (step.Slots[0].Name)
            {
                case nameof(Ams2Constants.Vm550Slot.Year):
                    g.Bump(g.Vm550, Ams2Constants.Vm550Slot.Year);
                    // the game keeps VM498's packed date in step with the race's
                    g.Mem.PutInt32(g.Vm498 + Ams2Constants.Vm498DatePackedOffset, unchecked((int)Vm498PackedDate.Encode(
                        g.Value(g.Vm550, Ams2Constants.Vm550Slot.Year), g.Value(g.Vm550, Ams2Constants.Vm550Slot.Month),
                        g.Value(g.Vm550, Ams2Constants.Vm550Slot.Day), 0x02000000u)));
                    break;
                case nameof(Ams2Constants.Vm550Slot.Hour): g.Bump(g.Vm550, MovedHourSlot); break;
                case nameof(Ams2Constants.Vm498Slot.Skill): g.Bump(g.Vm498, Ams2Constants.Vm498Slot.Skill); break;
                case nameof(Ams2Constants.Vm498Slot.MistakeFrequency): g.Bump(g.Vm498, Ams2Constants.Vm498Slot.MistakeFrequency); break;
                case nameof(Ams2Constants.MasterSlot.Car):
                    g.Bump(g.Master, Ams2Constants.MasterSlot.Car);
                    g.Bump(g.Vm498, Ams2Constants.Vm498Slot.RivalCount); // the game's own recount follows a car change
                    break;
                case nameof(Ams2Constants.MasterSlot.Livery): g.Bump(g.Master, Ams2Constants.MasterSlot.Livery); break;
                default:
                    if (extra == null) return SurveyAnswer.Skip;
                    extra(step);
                    break;
            }
            return SurveyAnswer.Changed;
        };

        [TestMethod]
        public void FindTags_PatchedBuild_FindsTheNewTagsAndTheMovedSlot()
        {
            var g = BuildPatchedGame();

            var report = Survey(g).Run(CustomRaceSurvey.TagSteps, Developer(g), OldTags);

            Assert.IsTrue(report.AllTagsDecided, report.Describe());
            Assert.AreEqual(NewVm550Tag, report.Tags[SurveyObject.Vm550].Tag);
            Assert.AreEqual(NewVm498Tag, report.Tags[SurveyObject.Vm498].Tag);
            Assert.AreEqual(NewContainerTag, report.Tags[SurveyObject.Master].Tag);
            Assert.AreEqual(NewVm498Tag - ModuleBase, report.TagRvas()[Ams2OffsetNames.Vm498IdentityTag]);

            var hour = report.Slots.Single(s => s.Name == nameof(Ams2Constants.Vm550Slot.Hour));
            Assert.AreEqual(MovedHourSlot, hour.NewSlot);
            Assert.IsTrue(hour.Moved);
            Assert.IsTrue(report.Slots.Where(s => s.Name != hour.Name).All(s => s.NewSlot == s.OldSlot), report.Describe());
        }

        [TestMethod]
        public void FindTags_ChecksTheFixedOffsetsAroundTheObjects()
        {
            var g = BuildPatchedGame();

            var report = Survey(g).Run(CustomRaceSurvey.TagSteps, Developer(g), OldTags);

            Assert.AreEqual(g.Master, report.MasterObject);
            Assert.AreEqual(g.Vm498, report.Vm498Object);
            Assert.AreEqual(g.Vm550, report.Vm550Object);
            CollectionAssert.AreEqual(new[] { Ams2Constants.Vm498Offset }, report.Vm498OffsetsInMaster!.ToArray());
            Assert.IsTrue(report.MasterActiveFlagsSet);
            Assert.AreEqual(8, report.SessionWrappersWithVm550Tag);
            CollectionAssert.Contains(report.PackedDateOffsets!.ToList(), Ams2Constants.Vm498DatePackedOffset);
            StringAssert.Contains(report.Describe(), "Vm498DatePackedOffset 0x188: OK");
        }

        [TestMethod]
        public void Survey_PropertyChangingByItself_IsIgnored()
        {
            var g = BuildPatchedGame();
            // a ticking counter on some other object, changing in the baseline read and in every step
            long clock = g.Race.NewObject();
            g.Header(clock, 0x141FE7000);
            g.NoiseNode = g.Race.SetSlot(clock, 5, 0);

            var report = Survey(g).Run(CustomRaceSurvey.TagSteps, Developer(g), OldTags, baselinePause: g.Tick);

            Assert.IsTrue(report.AllTagsDecided, report.Describe());
            Assert.IsFalse(report.Observations.SelectMany(o => o.Owners).Any(o => o.Tag == 0x141FE7000));
        }

        [TestMethod]
        public void Survey_AnObjectSharingEverySetting_LosesToTheRealOnes()
        {
            var g = BuildPatchedGame();
            // a UI-side object that references the same properties of every Custom Race object
            long mirror = g.Race.NewObject();
            g.Header(mirror, 0x141FE8000);
            g.Mem.PutInt64(mirror + 0x10, g.Node(g.Vm550, Ams2Constants.Vm550Slot.Year));
            g.Mem.PutInt64(mirror + 0x18, g.Node(g.Vm550, MovedHourSlot));
            g.Mem.PutInt64(mirror + 0x20, g.Node(g.Vm498, Ams2Constants.Vm498Slot.Skill));
            g.Mem.PutInt64(mirror + 0x28, g.Node(g.Vm498, Ams2Constants.Vm498Slot.MistakeFrequency));
            g.Mem.PutInt64(mirror + 0x30, g.Node(g.Master, Ams2Constants.MasterSlot.Car));
            g.Mem.PutInt64(mirror + 0x38, g.Node(g.Master, Ams2Constants.MasterSlot.Livery));

            var report = Survey(g).Run(CustomRaceSurvey.TagSteps, Developer(g), OldTags);

            Assert.AreEqual(NewVm550Tag, report.Tags[SurveyObject.Vm550].Tag, report.Describe());
            Assert.AreEqual(NewVm498Tag, report.Tags[SurveyObject.Vm498].Tag, report.Describe());
            Assert.AreEqual(NewContainerTag, report.Tags[SurveyObject.Master].Tag, report.Describe());
        }

        [TestMethod]
        public void Survey_TwoTagsTiedForVm498_SettledByWhichOneMasterHolds()
        {
            var g = BuildPatchedGame();
            // a look-alike holding the same two AI properties, with a tag of its own
            long lookAlike = g.Race.NewObject();
            g.Header(lookAlike, 0x141FE9000);
            g.Mem.PutInt64(lookAlike + Ams2Constants.Vm498Slot.Skill * 8L, g.Node(g.Vm498, Ams2Constants.Vm498Slot.Skill));
            g.Mem.PutInt64(lookAlike + Ams2Constants.Vm498Slot.MistakeFrequency * 8L, g.Node(g.Vm498, Ams2Constants.Vm498Slot.MistakeFrequency));
            var steps = CustomRaceSurvey.TagSteps.Where(s => s.Slots[0].Name != nameof(Ams2Constants.MasterSlot.Car)).ToList();

            var report = Survey(g).Run(steps, Developer(g), currentTags: null);

            Assert.AreEqual(NewVm498Tag, report.Tags[SurveyObject.Vm498].Tag, report.Describe());
            StringAssert.Contains(report.Tags[SurveyObject.Vm498].Note, "master");
        }

        [TestMethod]
        public void Survey_TwoUnexpectedSlotsChange_LeftUndecidedWithBoth()
        {
            var g = BuildPatchedGame();
            g.Race.SetSlot(g.Vm550, 61, 0);
            g.Race.SetSlot(g.Vm550, 62, 0);
            var step = new SurveyStep("Change the race START TIME (the hour)", SurveyObject.Vm550, "Hour", Ams2Constants.Vm550Slot.Hour);
            var steps = new List<SurveyStep>(CustomRaceSurvey.TagSteps.Where(s => s.Slots[0].Name != "Hour")) { step };

            var report = Survey(g).Run(steps, (s, i) =>
            {
                if (s != step) return Developer(g)(s, i);
                g.Bump(g.Vm550, 61);
                g.Bump(g.Vm550, 62);
                return SurveyAnswer.Changed;
            }, OldTags);

            var hour = report.Slots.Single(s => s.Name == "Hour");
            Assert.IsNull(hour.NewSlot);
            CollectionAssert.AreEqual(new[] { 61, 62 }, hour.Candidates.ToArray());
        }

        [TestMethod]
        public void Survey_GroupStep_MatchedByOrder()
        {
            var g = BuildPatchedGame();
            var group = CustomRaceSurvey.SlotSteps.Single(s => s.Slots.Count == 2);
            var steps = CustomRaceSurvey.TagSteps.Append(group).ToList();

            var report = Survey(g).Run(steps, Developer(g, s =>
            {
                g.Bump(g.Vm498, Ams2Constants.Vm498Slot.RivalCount);
                g.Bump(g.Vm498, Ams2Constants.Vm498Slot.RivalCountPerClass);
            }), OldTags);

            Assert.AreEqual(Ams2Constants.Vm498Slot.RivalCount, report.Slots.Single(s => s.Name == "RivalCount").NewSlot);
            Assert.AreEqual(Ams2Constants.Vm498Slot.RivalCountPerClass, report.Slots.Single(s => s.Name == "RivalCountPerClass").NewSlot);
        }

        [TestMethod]
        public void Survey_SkippedStep_ReportedAsSkipped()
        {
            var g = BuildPatchedGame();
            var steps = CustomRaceSurvey.TagSteps.Append(CustomRaceSurvey.SlotSteps.Single(s => s.Slots[0].Name == "Fcy")).ToList();

            var report = Survey(g).Run(steps, Developer(g), OldTags);

            var fcy = report.Slots.Single(s => s.Name == "Fcy");
            Assert.IsNull(fcy.NewSlot);
            Assert.AreEqual("skipped", fcy.Note);
        }

        [TestMethod]
        public void FindObjectStart_WalksBackToTheHeader()
        {
            var g = BuildPatchedGame();
            var survey = Survey(g);

            Assert.AreEqual(g.Vm550, survey.FindObjectStart(g.Vm550 + MovedHourSlot * 8L));
            Assert.AreEqual(g.Master, survey.FindObjectStart(g.Master + Ams2Constants.MasterSlot.Livery * 8L));
            long headerless = g.Race.NewObject();
            Assert.IsNull(survey.FindObjectStart(headerless + 0x100));
        }

        [TestMethod]
        public void TrustedValuesFor_NewBuildWithoutAnEntry_IsNull()
        {
            var file = new Ams2SignatureFile();
            file.KnownBuilds["KNOWN"] = new Dictionary<string, long> { [Ams2OffsetNames.Setter] = 0x1234 };

            Assert.IsNull(Ams2SignatureGenerator.TrustedValuesFor("NEW", file, new[] { "CONFIRMED" }));
            Assert.IsNull(Ams2SignatureGenerator.TrustedValuesFor("NEW", null, Array.Empty<string>()));
            Assert.AreEqual(0x1234, Ams2SignatureGenerator.TrustedValuesFor("known", file, new[] { "CONFIRMED" })![Ams2OffsetNames.Setter]);
            Assert.AreSame(Ams2OffsetNames.BuiltInRvas, Ams2SignatureGenerator.TrustedValuesFor("confirmed", file, new[] { "CONFIRMED" }));
        }
    }
}
