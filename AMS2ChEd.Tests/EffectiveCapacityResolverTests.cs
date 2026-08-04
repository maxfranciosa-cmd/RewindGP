using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.PakPatching.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AMS2ChEd.Tests
{
    /// <summary>
    /// Covers the decision logic Ams2LiveryService.GenerateLiveryXmlsAMS2 delegates to before
    /// running SlotCapacityAllocator: does a successful/no-op patch raise a model's effective
    /// capacity (so the allocator sees no overflow and never redirects/forces solid colour), and
    /// does a failed/skipped patch leave the declared capacity untouched (so the allocator's
    /// existing redirection remains the fallback)?
    /// </summary>
    [TestClass]
    public class EffectiveCapacityResolverTests
    {
        private static readonly List<(string Model, int Slots)> Declared = new()
        {
            ("m1", 4),
            ("m2", 6),
        };

        [TestMethod]
        public void Resolve_NoModelOverflows_NeverCallsPatcherAndReturnsDeclaredCapacitiesUnchanged()
        {
            var patcher = new Mock<IVehicleLiverySlotPatcher>(MockBehavior.Strict);
            var required = new Dictionary<string, int> { ["m1"] = 3, ["m2"] = 6 };

            var result = EffectiveCapacityResolver.Resolve(required, Declared, patcher.Object, "C:\\AMS2", null);

            patcher.VerifyNoOtherCalls();
            CollectionAssert.AreEqual(Declared, result);
        }

        [TestMethod]
        public void Resolve_ModelOverflowsAndPatchSucceeds_RaisesEffectiveCapacityToRequiredCount()
        {
            var patcher = new Mock<IVehicleLiverySlotPatcher>();
            patcher.Setup(p => p.EnsureSlots("C:\\AMS2", "m1", 7, It.IsAny<IReadOnlyList<string>>()))
                .Returns(new SlotPatchOutcome { Status = SlotPatchStatus.Patched });

            var required = new Dictionary<string, int> { ["m1"] = 7, ["m2"] = 6 };

            var result = EffectiveCapacityResolver.Resolve(required, Declared, patcher.Object, "C:\\AMS2", null);

            Assert.AreEqual(7, result.Single(m => m.Model == "m1").Slots);
            Assert.AreEqual(6, result.Single(m => m.Model == "m2").Slots);
        }

        [TestMethod]
        public void Resolve_ModelOverflowsAndAlreadySufficient_RaisesEffectiveCapacityToRequiredCount()
        {
            // AlreadySufficient means the live .rcf already has enough slots (e.g. patched by an
            // earlier race this season) - the allocator should still see no overflow.
            var patcher = new Mock<IVehicleLiverySlotPatcher>();
            patcher.Setup(p => p.EnsureSlots("C:\\AMS2", "m1", 7, It.IsAny<IReadOnlyList<string>>()))
                .Returns(new SlotPatchOutcome { Status = SlotPatchStatus.AlreadySufficient });

            var required = new Dictionary<string, int> { ["m1"] = 7 };

            var result = EffectiveCapacityResolver.Resolve(required, Declared, patcher.Object, "C:\\AMS2", null);

            Assert.AreEqual(7, result.Single(m => m.Model == "m1").Slots);
        }

        [TestMethod]
        public void Resolve_ModelOverflowsAndPatchFails_LeavesDeclaredCapacityUntouched()
        {
            var patcher = new Mock<IVehicleLiverySlotPatcher>();
            patcher.Setup(p => p.EnsureSlots("C:\\AMS2", "m1", 7, It.IsAny<IReadOnlyList<string>>()))
                .Returns(new SlotPatchOutcome { Status = SlotPatchStatus.Failed, Message = "disk full" });

            var required = new Dictionary<string, int> { ["m1"] = 7 };
            var warnings = new List<string>();

            var result = EffectiveCapacityResolver.Resolve(required, Declared, patcher.Object, "C:\\AMS2", warnings.Add);

            Assert.AreEqual(4, result.Single(m => m.Model == "m1").Slots, "Declared capacity should be unchanged so the allocator still redirects overflow for this model.");
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains(warnings[0], "m1");
        }

        [TestMethod]
        [DataRow(SlotPatchStatus.SkippedInstallNotFound)]
        [DataRow(SlotPatchStatus.SkippedPakNotFound)]
        [DataRow(SlotPatchStatus.SkippedUnrecognizedFormat)]
        public void Resolve_ModelOverflowsAndPatchSkipped_LeavesDeclaredCapacityUntouched(SlotPatchStatus status)
        {
            var patcher = new Mock<IVehicleLiverySlotPatcher>();
            patcher.Setup(p => p.EnsureSlots("C:\\AMS2", "m1", 7, It.IsAny<IReadOnlyList<string>>())).Returns(new SlotPatchOutcome { Status = status });

            var required = new Dictionary<string, int> { ["m1"] = 7 };

            var result = EffectiveCapacityResolver.Resolve(required, Declared, patcher.Object, "C:\\AMS2", null);

            Assert.AreEqual(4, result.Single(m => m.Model == "m1").Slots);
        }

        [TestMethod]
        public void Resolve_NoSlotPatcherProvided_ReturnsDeclaredCapacitiesUnchanged()
        {
            var required = new Dictionary<string, int> { ["m1"] = 7 };

            var result = EffectiveCapacityResolver.Resolve(required, Declared, slotPatcher: null, "C:\\AMS2", null);

            CollectionAssert.AreEqual(Declared, result);
        }

        [TestMethod]
        public void Resolve_NoDeclaredCapacities_ReturnsNullWithoutCallingPatcher()
        {
            var patcher = new Mock<IVehicleLiverySlotPatcher>(MockBehavior.Strict);
            var required = new Dictionary<string, int> { ["m1"] = 7 };

            var result = EffectiveCapacityResolver.Resolve(required, declaredCapacities: null, patcher.Object, "C:\\AMS2", null);

            Assert.IsNull(result);
            patcher.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void Resolve_ModelOverflows_PassesOtherDeclaredModelsAsTextureReuseSiblingsExcludingItself()
        {
            var declared = new List<(string Model, int Slots)> { ("m1", 4), ("m2", 6), ("m3", 5) };
            IReadOnlyList<string>? capturedSiblings = null;

            var patcher = new Mock<IVehicleLiverySlotPatcher>();
            patcher.Setup(p => p.EnsureSlots("C:\\AMS2", "m1", 7, It.IsAny<IReadOnlyList<string>>()))
                .Callback<string, string, int, IReadOnlyList<string>>((_, _, _, siblings) => capturedSiblings = siblings)
                .Returns(new SlotPatchOutcome { Status = SlotPatchStatus.Patched });

            var required = new Dictionary<string, int> { ["m1"] = 7 };

            EffectiveCapacityResolver.Resolve(required, declared, patcher.Object, "C:\\AMS2", null);

            Assert.IsNotNull(capturedSiblings);
            CollectionAssert.AreEqual(new[] { "m2", "m3" }, capturedSiblings!.ToList());
        }

        [TestMethod]
        public void Resolve_UnknownModelNotInDeclaredCapacities_SkipsItWithoutCallingPatcher()
        {
            var patcher = new Mock<IVehicleLiverySlotPatcher>(MockBehavior.Strict);
            var required = new Dictionary<string, int> { ["unknown_model"] = 99 };

            var result = EffectiveCapacityResolver.Resolve(required, Declared, patcher.Object, "C:\\AMS2", null);

            patcher.VerifyNoOtherCalls();
            CollectionAssert.AreEqual(Declared, result);
        }
    }
}
