namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Pure offset math for repacking a .bff pak, per AMS2-livery-modding-knowledge.md's
    /// "Repacking algorithm" general case: entries are laid out back-to-back (16-byte aligned),
    /// so changing one entry's size shifts every subsequent entry's data offset. Sort entries by
    /// their *physical* data offset (not TOC index - they don't necessarily match), then walk in
    /// that order assigning a new offset from a running cursor that starts at the first entry's
    /// original offset.
    /// </summary>
    public static class RepackOffsetPlanner
    {
        public readonly record struct EntryPlan(int EntryIndex, long OriginalOffset, int NewPakSize);
        public readonly record struct PlannedOffset(int EntryIndex, long NewOffset);

        public static IReadOnlyList<PlannedOffset> ComputeNewOffsets(IReadOnlyList<EntryPlan> entries)
        {
            if (entries.Count == 0)
                return Array.Empty<PlannedOffset>();

            var byPhysicalOffset = entries.OrderBy(e => e.OriginalOffset).ToList();

            long running = byPhysicalOffset[0].OriginalOffset;
            var results = new List<PlannedOffset>(entries.Count);

            foreach (var entry in byPhysicalOffset)
            {
                results.Add(new PlannedOffset(entry.EntryIndex, running));
                running += Align16(entry.NewPakSize);
            }

            return results;
        }

        public static long Align16(long size) => (size + 15) & ~15L;
    }
}
