using Ams2ChEd.Business.AMS2.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    /// <summary>
    /// Tests for SlotCapacityAllocator's 3-pass overflow allocation:
    ///   1. Fit each model's own items up to its declared cap (uncapped if undeclared).
    ///   2. Collect overflow in original global order.
    ///   3. Walk declared models in declared order, filling remaining free slots.
    /// </summary>
    [TestClass]
    public class SlotCapacityAllocatorTests
    {
        private static SlotCapacityAllocator.Item<string> MakeItem(string model, int sequenceIndex, string payload = null) =>
            new SlotCapacityAllocator.Item<string>
            {
                Model = model,
                SequenceIndex = sequenceIndex,
                Payload = payload ?? $"{model}#{sequenceIndex}"
            };

        [TestMethod]
        public void Allocate_OverflowFromOneModel_SpillsIntoSiblingModelsInDeclaredOrder()
        {
            // FE-G1: m1(4 slots), m2(6), m3(6). m1/m3 are filled to 1-short of capacity by
            // "other" teams; m2 is filled to exactly capacity by "other" teams before larrousse's
            // 2 cars (assigned to m2) arrive. larrousse's cars should overflow, one into m1's
            // last free slot and one into m3's last free slot (declared order: m1 then m3).
            var models = new List<(string Model, int Slots)>
            {
                ("m1", 4),
                ("m2", 6),
                ("m3", 6)
            };

            var items = new List<SlotCapacityAllocator.Item<string>>();
            int seq = 0;
            for (int i = 0; i < 3; i++) items.Add(MakeItem("m1", seq++)); // m1: 3 of 4 used
            for (int i = 0; i < 6; i++) items.Add(MakeItem("m2", seq++)); // m2: 6 of 6 used (full)
            var larrousse1 = MakeItem("m2", seq++, "larrousse-car1");
            var larrousse2 = MakeItem("m2", seq++, "larrousse-car2");
            items.Add(larrousse1);
            items.Add(larrousse2);
            for (int i = 0; i < 5; i++) items.Add(MakeItem("m3", seq++)); // m3: 5 of 6 used

            var result = SlotCapacityAllocator.Allocate(items, models);

            Assert.AreEqual(0, result.Unplaceable.Count);
            CollectionAssert.Contains(result.AssignedByModel["m1"].Select(i => i.Payload).ToList(), "larrousse-car1");
            CollectionAssert.Contains(result.AssignedByModel["m3"].Select(i => i.Payload).ToList(), "larrousse-car2");
            Assert.AreEqual(4, result.AssignedByModel["m1"].Count);
            Assert.AreEqual(6, result.AssignedByModel["m2"].Count);
            Assert.AreEqual(6, result.AssignedByModel["m3"].Count);
        }

        [TestMethod]
        public void Allocate_MoreItemsThanTotalCapacity_LeavesExtrasUnplaceable_NoDuplicationOrLoss()
        {
            var models = new List<(string Model, int Slots)> { ("m1", 2), ("m2", 2) };

            var items = new List<SlotCapacityAllocator.Item<string>>();
            for (int i = 0; i < 3; i++) items.Add(MakeItem("m1", i));
            for (int i = 0; i < 3; i++) items.Add(MakeItem("m2", 3 + i));

            var result = SlotCapacityAllocator.Allocate(items, models);

            int assignedCount = result.AssignedByModel.Values.Sum(l => l.Count);
            Assert.AreEqual(items.Count, assignedCount + result.Unplaceable.Count);
            Assert.AreEqual(2, result.Unplaceable.Count);
        }

        [TestMethod]
        public void Allocate_ModelNotDeclaredInClass_IsAlwaysUncappedAndNeverAFillTarget()
        {
            var models = new List<(string Model, int Slots)> { ("m1", 1) };

            var items = new List<SlotCapacityAllocator.Item<string>>
            {
                MakeItem("m1", 0),
                MakeItem("m1", 1), // overflow, but "undeclared" isn't a target so should stay unplaceable
                MakeItem("undeclared_model", 2),
                MakeItem("undeclared_model", 3),
                MakeItem("undeclared_model", 4)
            };

            var result = SlotCapacityAllocator.Allocate(items, models);

            Assert.AreEqual(3, result.AssignedByModel["undeclared_model"].Count);
            Assert.AreEqual(1, result.AssignedByModel["m1"].Count);
            Assert.AreEqual(1, result.Unplaceable.Count);
        }

        [TestMethod]
        public void Allocate_DeclaredModelWithNoDirectItems_IsStillAValidFillTarget()
        {
            var models = new List<(string Model, int Slots)> { ("m1", 1), ("m2", 2) };

            var items = new List<SlotCapacityAllocator.Item<string>>
            {
                MakeItem("m1", 0),
                MakeItem("m1", 1) // overflow, should spill into m2 which had zero direct items
            };

            var result = SlotCapacityAllocator.Allocate(items, models);

            Assert.AreEqual(0, result.Unplaceable.Count);
            Assert.IsTrue(result.AssignedByModel.ContainsKey("m2"));
            Assert.AreEqual(1, result.AssignedByModel["m2"].Count);
        }

        [TestMethod]
        public void Allocate_SameSequenceIndices_ProduceIdenticalResultRegardlessOfInputEnumerationOrder()
        {
            var models = new List<(string Model, int Slots)> { ("m1", 1), ("m2", 1) };

            var itemsA = new List<SlotCapacityAllocator.Item<string>>
            {
                MakeItem("m1", 0, "a"),
                MakeItem("m1", 1, "b"),
                MakeItem("m2", 2, "c")
            };
            var itemsB = new List<SlotCapacityAllocator.Item<string>>(itemsA);
            itemsB.Reverse();

            var resultA = SlotCapacityAllocator.Allocate(itemsA, models);
            var resultB = SlotCapacityAllocator.Allocate(itemsB, models);

            Assert.AreEqual(
                string.Join(",", resultA.AssignedByModel["m1"].Select(i => i.Payload)),
                string.Join(",", resultB.AssignedByModel["m1"].Select(i => i.Payload)));
            Assert.AreEqual(
                string.Join(",", resultA.AssignedByModel["m2"].Select(i => i.Payload)),
                string.Join(",", resultB.AssignedByModel["m2"].Select(i => i.Payload)));
            Assert.AreEqual(resultA.Unplaceable.Count, resultB.Unplaceable.Count);
        }
    }
}
