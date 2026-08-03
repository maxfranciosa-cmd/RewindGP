using Ams2ChEd.Business.AMS2.PakPatching;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class RepackOffsetPlannerTests
    {
        [TestMethod]
        public void ComputeNewOffsets_AllEntriesUnchanged_PreservesOriginalOffsets()
        {
            var entries = new List<RepackOffsetPlanner.EntryPlan>
            {
                new(0, 0x130, 100),
                new(1, 0x130 + 112, 50), // 100 aligned to 16 = 112
                new(2, 0x130 + 112 + 64, 200), // 50 aligned to 16 = 64
            };

            var result = RepackOffsetPlanner.ComputeNewOffsets(entries);

            Assert.AreEqual(0x130, result.Single(r => r.EntryIndex == 0).NewOffset);
            Assert.AreEqual(0x130 + 112, result.Single(r => r.EntryIndex == 1).NewOffset);
            Assert.AreEqual(0x130 + 112 + 64, result.Single(r => r.EntryIndex == 2).NewOffset);
        }

        [TestMethod]
        public void ComputeNewOffsets_MiddleEntryGrows_ShiftsAllSubsequentEntries()
        {
            var result = RepackOffsetPlanner.ComputeNewOffsets(new List<RepackOffsetPlanner.EntryPlan>
            {
                new(0, 1000, 16),
                new(1, 1016, 100), // grows to 100 -> aligned 112
                new(2, 1032, 16),
            });

            Assert.AreEqual(1000, result.Single(r => r.EntryIndex == 0).NewOffset);
            Assert.AreEqual(1016, result.Single(r => r.EntryIndex == 1).NewOffset); // unchanged start
            Assert.AreEqual(1016 + 112, result.Single(r => r.EntryIndex == 2).NewOffset); // pushed out
        }

        [TestMethod]
        public void ComputeNewOffsets_MiddleEntryShrinks_PullsInSubsequentEntries()
        {
            var entries = new List<RepackOffsetPlanner.EntryPlan>
            {
                new(0, 1000, 16),
                new(1, 1016, 8), // shrinks from original layout gap
                new(2, 1032, 16),
            };

            var result = RepackOffsetPlanner.ComputeNewOffsets(entries);

            Assert.AreEqual(1000, result.Single(r => r.EntryIndex == 0).NewOffset);
            Assert.AreEqual(1016, result.Single(r => r.EntryIndex == 1).NewOffset);
            Assert.AreEqual(1016 + 16, result.Single(r => r.EntryIndex == 2).NewOffset); // 8 aligned to 16
        }

        [TestMethod]
        public void ComputeNewOffsets_EntriesOutOfTocOrderButSequentialByPhysicalOffset_SortsByOffsetNotIndex()
        {
            // TOC index 1 physically comes before TOC index 0 in the file.
            var entries = new List<RepackOffsetPlanner.EntryPlan>
            {
                new(EntryIndex: 0, OriginalOffset: 2000, NewPakSize: 16),
                new(EntryIndex: 1, OriginalOffset: 1000, NewPakSize: 16),
            };

            var result = RepackOffsetPlanner.ComputeNewOffsets(entries);

            // Running cursor starts at the smallest physical offset (1000, belonging to index 1).
            Assert.AreEqual(1000, result.Single(r => r.EntryIndex == 1).NewOffset);
            Assert.AreEqual(1016, result.Single(r => r.EntryIndex == 0).NewOffset);
        }

        [TestMethod]
        public void Align16_RoundsUpToNextSixteenByteBoundary()
        {
            Assert.AreEqual(0, RepackOffsetPlanner.Align16(0));
            Assert.AreEqual(16, RepackOffsetPlanner.Align16(1));
            Assert.AreEqual(16, RepackOffsetPlanner.Align16(16));
            Assert.AreEqual(32, RepackOffsetPlanner.Align16(17));
            Assert.AreEqual(112, RepackOffsetPlanner.Align16(100));
        }

        [TestMethod]
        public void ComputeNewOffsets_EmptyInput_ReturnsEmpty()
        {
            var result = RepackOffsetPlanner.ComputeNewOffsets(Array.Empty<RepackOffsetPlanner.EntryPlan>());

            Assert.AreEqual(0, result.Count);
        }
    }
}
