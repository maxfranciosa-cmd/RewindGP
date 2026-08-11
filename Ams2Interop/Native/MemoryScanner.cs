using System.Runtime.InteropServices;

namespace Ams2Interop.Native;

/// <summary>
/// Full-process memory sweep for a literal 8-byte value - the shared primitive behind VmResolver:
/// walk every committed, private, readable page looking for literal occurrences of a known
/// constant (a vtable/type-tag pointer - the tag genuinely occupies a full pointer-sized slot for
/// VmResolver's use case). Expect this to take on the order of seconds over a multi-GB process.
/// </summary>
public static class MemoryScanner
{
    public static IEnumerable<long> FindOccurrences(ProcessMemory mem, long value) =>
        FindOccurrences(mem, BitConverter.GetBytes(value));

    private static IEnumerable<long> FindOccurrences(ProcessMemory mem, byte[] pattern)
    {
        var address = IntPtr.Zero;
        var mbiSize = (uint)Marshal.SizeOf<NativeMethods.MEMORY_BASIC_INFORMATION>();

        while (NativeMethods.VirtualQueryEx(mem.Handle, address, out var mbi, mbiSize) != IntPtr.Zero)
        {
            var regionSize = (long)mbi.RegionSize;
            if (regionSize <= 0) break;
            var nextAddress = (long)mbi.BaseAddress + regionSize;

            var qualifies = mbi.State == NativeMethods.MEM_COMMIT
                             && mbi.Type == NativeMethods.MEM_PRIVATE
                             && (mbi.Protect & NativeMethods.PAGE_NOACCESS) == 0
                             && (mbi.Protect & NativeMethods.PAGE_GUARD) == 0;

            if (qualifies)
            {
                foreach (var hit in ScanRegion(mem, (long)mbi.BaseAddress, regionSize, pattern))
                    yield return hit;
            }

            if (nextAddress <= (long)address) break; // guard against a non-advancing region
            address = (IntPtr)nextAddress;
        }
    }

    private static IEnumerable<long> ScanRegion(ProcessMemory mem, long baseAddress, long regionSize, byte[] pattern)
    {
        // 4 MB chunks cut ReadProcessMemory call count substantially - each call has fixed
        // per-call overhead that dominates at small chunk sizes over a multi-GB process. The
        // (pattern.Length-1)-byte overlap at each chunk boundary catches a pattern that straddles
        // two reads without double-counting it (a match found in the overlap of read N starts at
        // an index >= chunkSize, i.e. before read N+1's own base - it's never re-found).
        const int chunkSize = 1 << 22; // 4 MB
        var overlap = pattern.Length - 1;

        for (long offset = 0; offset < regionSize; offset += chunkSize)
        {
            var toRead = (int)Math.Min(chunkSize + overlap, regionSize - offset);
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
