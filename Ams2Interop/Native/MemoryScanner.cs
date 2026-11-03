using System.Runtime.InteropServices;

namespace Ams2Interop.Native;

/// <summary>
/// Full-process memory sweeps - the shared primitive behind VmResolver and the developer survey
/// (Discovery/CustomRaceSurvey): walk every readable region (for a real process, every committed,
/// private, readable page - see ProcessMemory.ReadableRegions) looking for literal values. Expect
/// a sweep to take on the order of seconds over a multi-GB process.
/// </summary>
public static class MemoryScanner
{
    // 4 MB chunks cut ReadProcessMemory call count substantially - each call has fixed per-call
    // overhead that dominates at small chunk sizes over a multi-GB process.
    private const int ChunkSize = 1 << 22;

    /// <summary>Every address holding the 8-byte <paramref name="value"/>, at any alignment (a type-tag pointer, for VmResolver's use).</summary>
    public static IEnumerable<long> FindOccurrences(IScannableMemory mem, long value) =>
        FindOccurrences(mem, BitConverter.GetBytes(value));

    /// <summary>
    /// Every 8-byte-aligned address holding a qword that is one of <paramref name="targets"/>, with
    /// the value found - i.e. every pointer to one of those addresses. One sweep, whatever the
    /// number of targets.
    /// </summary>
    public static IEnumerable<(long Location, long Value)> FindPointersTo(IScannableMemory mem, IReadOnlySet<long> targets)
    {
        if (targets.Count == 0) yield break;
        long min = targets.Min(), max = targets.Max();

        foreach (var (regionBase, regionSize) in mem.ReadableRegions())
        {
            // regions are page-aligned for a real process; align defensively for anything else
            long start = (regionBase + 7) & ~7L;
            long end = regionBase + regionSize;
            for (long chunk = start; chunk < end; chunk += ChunkSize)
            {
                var toRead = (int)Math.Min(ChunkSize, (end - chunk) & ~7L);
                if (toRead < 8 || !mem.ReadRaw(chunk, toRead, out var buffer)) break;

                foreach (var hit in PointersInChunk(buffer, chunk, targets, min, max))
                    yield return hit;
            }
        }
    }

    // separate from the iterator: a span can't live across a yield
    private static List<(long Location, long Value)> PointersInChunk(byte[] buffer, long chunkBase, IReadOnlySet<long> targets, long min, long max)
    {
        var hits = new List<(long, long)>();
        var qwords = MemoryMarshal.Cast<byte, long>(buffer.AsSpan());
        for (int i = 0; i < qwords.Length; i++)
        {
            var v = qwords[i];
            if (v >= min && v <= max && targets.Contains(v))
                hits.Add((chunkBase + i * 8L, v));
        }
        return hits;
    }

    private static IEnumerable<long> FindOccurrences(IScannableMemory mem, byte[] pattern)
    {
        foreach (var (regionBase, regionSize) in mem.ReadableRegions())
            foreach (var hit in ScanRegion(mem, regionBase, regionSize, pattern))
                yield return hit;
    }

    private static IEnumerable<long> ScanRegion(IMemoryReader mem, long baseAddress, long regionSize, byte[] pattern)
    {
        // The (pattern.Length-1)-byte overlap at each chunk boundary catches a pattern that
        // straddles two reads without double-counting it (a match found in the overlap of read N
        // starts at an index >= ChunkSize, i.e. before read N+1's own base - it's never re-found).
        var overlap = pattern.Length - 1;

        for (long offset = 0; offset < regionSize; offset += ChunkSize)
        {
            var toRead = (int)Math.Min(ChunkSize + overlap, regionSize - offset);
            if (toRead < pattern.Length) yield break;
            if (!mem.ReadRaw(baseAddress + offset, toRead, out var buffer))
                yield break;

            // Span.IndexOf on a byte pattern is hardware-vectorized in modern .NET - dramatically
            // faster than comparing bytes one at a time in a scalar loop, which matters a lot when
            // scanning a multi-GB process.
            var searchStart = 0;
            while (searchStart <= buffer.Length - pattern.Length)
            {
                var idx = buffer.AsSpan(searchStart).IndexOf(pattern);
                if (idx < 0) break;
                var absoluteIdx = searchStart + idx;
                yield return baseAddress + offset + absoluteIdx;
                searchStart = absoluteIdx + 1;
            }
        }
    }
}
