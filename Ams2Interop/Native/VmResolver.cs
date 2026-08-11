namespace Ams2Interop.Native;

/// <summary>
/// Resolves the VM498 (race entrants) and VM550 (session/rules) object pointers inside
/// AMS2AVX.exe. A full process memory sweep is only used as a fallback when there's no cached
/// pointer, or the cached one no longer validates. The sweep walks every committed/private/
/// readable page looking for a specific 8-byte type-tag value and is a genuine
/// multi-hundred-MB-to-GB scan - expect it to take on the order of seconds.
///
/// Both VM objects require the player to have opened AMS2's Custom Race screen at least once
/// in the current session - if they haven't, resolution will legitimately fail every time.
///
/// SAFETY NOTE: a freshly scanned master candidate goes through the same strict
/// identity-tag+sanity-range validation as a cached one before being trusted - calling a remote
/// function (SetCar, or the generic setter) with an unvalidated pointer is a real crash risk, so
/// this class never skips that check for a fresh scan hit.
///
/// Master resolution (ScanForMaster) uses a mixed-base algorithm:
/// <code>
/// hit = <memory location where the container tag value 0x141fd43c8 was found>
/// if (hit - 0x100000008 &gt;= 0x700000000) continue;      // hit plausibility (note the +8 bias)
/// candidate = hit - 8;
/// if (!plausibleAms2Pointer(*candidate)) continue;         // candidate's own first qword
/// // 4-pointer signature uses hit-relative offsets, NOT candidate-relative:
/// if (!plausiblePointer(*(hit+0x18))) continue;
/// if (!plausiblePointer(*(hit+0xd8))) continue;
/// if (!plausiblePointer(*(hit+0x110))) continue;
/// if (!plausiblePointer(*(hit+0x248))) continue;
/// score = CountPopulatedIntProps(candidate);               // candidate-relative, slots 0x18..0x1d8
/// keep highest-scoring candidate; return candidate;         // = hit - 8
/// </code>
///
/// VM550 resolution (ScanForVm550) is simpler and genuinely different, not just a stripped-down
/// version of the master scan:
/// <code>
/// hit = <memory location where the given tag was found>
/// if (hit - 0x100000008 &gt;= 0x700000000) continue;
/// candidate = hit - 8;
/// if (!plausibleAms2Pointer(*candidate)) continue;
/// // NO 4-pointer-signature check at all for this scanner.
/// score = CountPopulatedIntProps(candidate);
/// keep highest-scoring candidate; return candidate;
/// </code>
///
/// Both scanners share the same "count populated int-property slots at candidate+0x18..+0x1d8"
/// scoring logic.
///
/// Implemented as two separate, tailored scan methods rather than one shared generic function -
/// the master and VM550 scanners are similar but genuinely different (mixed hit/candidate bases
/// vs. a uniform candidate base, a signature check that exists for one but not the other).
///
/// VM550 resolution tries three tiers, cheapest/most-reliable first, before falling back to the
/// raw tag scan:
/// <code>
/// // Level 1 - master's own direct fields:
/// for (off = 0x18; off &lt; 0x2000; off += 8) {
///     candidate = *(master + off);
///     if (!plausiblePointer(candidate)) continue;
///     if (!moduleRangePointer(*candidate)) continue;        // candidate's own vtable-slot qword
///     if (*(candidate + 8) == VM550_TAG) return candidate;  // found - done
/// }
/// // Level 2 - one level deeper, through intermediate (non-module-range) pointers:
/// for (off = 0x18; off &lt; 0x2000; off += 8) {
///     mid = *(master + off);
///     if (!plausiblePointer(mid) || moduleRangePointer(mid)) continue;
///     for (off2 = 0x10; off2 &lt; 0x800; off2 += 8) {
///         candidate = *(mid + off2);
///         if (!plausiblePointer(candidate)) continue;
///         if (!moduleRangePointer(*candidate)) continue;
///         if (*(candidate + 8) == VM550_TAG) return candidate;  // found - done
///     }
/// }
/// // Only if this entire walk fails does resolution fall back to the master-context-pair scan,
/// // and after that the throttled global tag scan.
/// </code>
///
/// The global tag scan (tier 3) is a generic "find any instance by tag" primitive reused across
/// many settings-holder objects throughout the game, not something unique to the Custom Race
/// session-rules screen - it can return hundreds of structurally-plausible, score-passing,
/// write-verifiable candidates with no guarantee the highest scorer is the one instance actually
/// wired to the visible UI. Tiers 1/2 sidestep this entirely by only considering objects master
/// itself directly (or one hop indirectly) points to, or objects found via a much more selective
/// search (below). Tier 3 is kept only as a last resort.
/// </summary>
public sealed class VmResolver
{
    private readonly ProcessMemory _mem;
    private readonly long _moduleBase;
    private readonly long _moduleSize;
    private readonly Action<string>? _log;
    private long _cachedMaster;
    private long _cachedVm550;

    /// <param name="moduleBase">AMS2AVX.exe's resolved module base - used for the "candidate's own
    /// first qword must land inside the game's module" check both scanners perform. Uses the
    /// ACTUAL resolved base/size rather than assuming a hardcoded default, so it stays correct
    /// even if the module isn't loaded at its preferred address.</param>
    /// <param name="moduleSize">AMS2AVX.exe's resolved module image size, bounding the same check.</param>
    /// <param name="log">Optional diagnostic sink - reports per-stage candidate counts during a
    /// scan, so a resolution failure can be traced to a specific filter rather than just "found
    /// nothing."</param>
    public VmResolver(ProcessMemory mem, long moduleBase, long moduleSize, Action<string>? log = null)
    {
        _mem = mem;
        _moduleBase = moduleBase;
        _moduleSize = moduleSize;
        _log = log;
        _log?.Invoke($"VmResolver: moduleBase=0x{moduleBase:X} moduleSize=0x{moduleSize:X} ({moduleSize / 1048576.0:F1} MB)");
    }

    /// <summary>Master pointer; VM498 = *(master + 0x250).</summary>
    public long? ResolveMaster(bool allowFullScan = true)
    {
        if (_cachedMaster != 0 && ValidateMaster(_cachedMaster))
            return _cachedMaster;

        if (!allowFullScan) return null;

        var tried = 0;
        foreach (var candidate in ScanForMaster())
        {
            tried++;
            if (!ValidateMaster(candidate))
            {
                _log?.Invoke($"master candidate #{tried} @ 0x{candidate:X} failed ValidateMaster (identity tag / skill-range check)");
                continue;
            }
            _log?.Invoke($"master candidate #{tried} @ 0x{candidate:X} PASSED ValidateMaster");
            _cachedMaster = candidate;
            return candidate;
        }
        if (tried == 0) _log?.Invoke("master: no structurally-valid candidates survived the scan filters at all");
        return null;
    }

    public long? ResolveVm498(bool allowFullScan = true)
    {
        var master = ResolveMaster(allowFullScan);
        if (master is not long m) return null;
        return _mem.TryReadPointerSafe(m + Ams2Constants.Vm498Offset, out var vm498) ? vm498 : null;
    }

    /// <summary>
    /// Session/rules VM. Tries three tiers, cheapest/most-reliable first (see this class's doc
    /// comment for the full algorithm):
    ///
    /// 1. Structural walk from master. Kept as a free first attempt (a few hundred pointer reads,
    ///    no full scan).
    /// 2. Master-context-pair scan: whatever resolves "the current race context" caches master
    ///    and VM550 together, adjacent, in a small struct:
    ///    <code>{ unknown_ptr @ -0x18, 0 @ -0x8, vm550 @ 0x0, master @ +0x8 }</code>
    ///    - found at locations several hundred MB to GB away from master itself (a sibling cache
    ///    entry some other system owns, not a field of master's own object). This gives a highly
    ///    selective scan: search for occurrences of master's own resolved pointer VALUE (a
    ///    specific ~8-byte needle almost nothing else in the process happens to contain, unlike a
    ///    shared type-tag), then check the qword immediately before each hit for a validated
    ///    VM550 candidate.
    /// 3. Global tag scan (ScanForVm550) - kept as the last resort. Unreliable on its own for
    ///    VM550 specifically: it can return hundreds of raw hits, and picking "highest scoring"
    ///    among them has no guarantee of landing on the UI-connected instance rather than a
    ///    real-but-disconnected object elsewhere in memory. Only reached if both tiers above fail.
    /// </summary>
    public long? ResolveVm550(bool allowFullScan = true)
    {
        if (_cachedVm550 != 0 && ValidateVm550(_cachedVm550))
            return _cachedVm550;

        var master = ResolveMaster(allowFullScan);
        if (master is long m)
        {
            var structural = FindVm550Structural(m);
            if (structural is long sv && ValidateVm550(sv))
            {
                _log?.Invoke($"vm550 structural walk from master @ 0x{m:X} found 0x{sv:X} (PASSED ValidateVm550)");
                _cachedVm550 = sv;
                return sv;
            }
            _log?.Invoke("vm550 structural walk from master found nothing - trying master-context-pair scan");

            if (allowFullScan)
            {
                var viaContext = FindVm550ViaMasterContextPair(m);
                if (viaContext is long cv && ValidateVm550(cv))
                {
                    _log?.Invoke($"vm550 master-context-pair scan found 0x{cv:X} (PASSED ValidateVm550)");
                    _cachedVm550 = cv;
                    return cv;
                }
                _log?.Invoke("vm550 master-context-pair scan found nothing - falling back to global tag scan");
            }
        }

        if (!allowFullScan) return null;

        var tried = 0;
        foreach (var candidate in ScanForVm550())
        {
            tried++;
            if (!ValidateVm550(candidate))
            {
                _log?.Invoke($"vm550 candidate #{tried} @ 0x{candidate:X} failed ValidateVm550 (identity tag check)");
                continue;
            }
            _log?.Invoke($"vm550 candidate #{tried} @ 0x{candidate:X} PASSED ValidateVm550");
            _cachedVm550 = candidate;
            return candidate;
        }
        if (tried == 0) _log?.Invoke("vm550: no structurally-valid candidates survived the scan filters at all");
        return null;
    }

    /// <summary>
    /// Master-context-pair scan - see this class's doc comment for the full algorithm and the
    /// context-struct layout it relies on. Searches for literal occurrences of master's own
    /// resolved pointer VALUE (reusing MemoryScanner.FindOccurrences, which finds arbitrary 8-byte
    /// values, not just type tags), then checks the qword immediately before each hit for a
    /// pointer that validates as VM550 (own qword in module range, own+8 == VM550 identity tag -
    /// the same signature ScanForVm550/ValidateVm550 use).
    /// </summary>
    private long? FindVm550ViaMasterContextPair(long master)
    {
        var results = new List<(long candidate, int score)>();
        foreach (var hit in MemoryScanner.FindOccurrences(_mem, master))
        {
            var candidate = hit - 8;
            if (!_mem.TryReadInt64(candidate, out var vtbl) || !IsModuleRangePointer(vtbl)) continue;
            if (!_mem.TryReadInt64(candidate + 8, out var tag) || tag != Ams2Constants.Vm550IdentityTag) continue;
            var score = CountPopulatedIntProps(candidate);
            if (score == 0) continue;
            results.Add((candidate, score));
        }
        if (results.Count == 0) return null;
        results.Sort((a, b) => b.score.CompareTo(a.score));
        return results[0].candidate;
    }

    private bool ValidateMaster(long master)
    {
        if (!_mem.TryReadPointerSafe(master + Ams2Constants.Vm498Offset, out var vm498)) return false;
        if (!_mem.TryReadInt64(vm498 + 8, out var tag) || tag != Ams2Constants.Vm498IdentityTag) return false;
        // The skill slot, if readable, should be in a plausible range. An unreadable slot just
        // means "not configured yet," not "wrong object."
        return !_mem.TryReadSlot(vm498, Ams2Constants.Vm498Slot.Skill, out var skill) || skill is >= 1 and < 200;
    }

    private bool ValidateVm550(long vm550) =>
        _mem.TryReadInt64(vm550 + 8, out var tag) && tag == Ams2Constants.Vm550IdentityTag;

    /// <summary>True if `pointer` is "plausible" - a pointer-shaped value outside a sane heap
    /// range is treated as invalid rather than trusted (see ProcessMemory.TryReadPointerSafe),
    /// used here directly on an already-read value rather than re-reading through the safe-read
    /// wrapper.</summary>
    private static bool IsPlausiblePointer(long pointer) => unchecked((ulong)pointer) - 0x100000000UL < 0x700000000UL;

    /// <summary>
    /// True if `pointer` itself lands inside AMS2AVX.exe's module range - used by the VM550
    /// structural walk to distinguish "this is a class instance whose own first qword is a vtable
    /// pointer into the module" from "this is a plain heap-allocated struct." Falls back to a
    /// hardcoded default-base range (`0x140000000`..+0x3000000) if the real module base/size
    /// weren't resolved.
    /// </summary>
    private bool IsModuleRangePointer(long pointer)
    {
        if (_moduleBase != 0) return pointer >= _moduleBase && pointer < _moduleBase + _moduleSize;
        return unchecked((ulong)pointer) - 0x140000000UL < 0x3000000UL;
    }

    /// <summary>
    /// Structural walk from master looking for a VM550-shaped child pointer - see this class's
    /// doc comment for the full algorithm. Walks master's own direct fields for a child pointer
    /// shaped like a VM550 instance, then one level deeper through non-module-range intermediate
    /// pointers if that fails. Returns null (never throws) if neither level finds anything -
    /// callers fall back to the master-context-pair scan, then the global tag scan.
    /// </summary>
    private long? FindVm550Structural(long master)
    {
        if (!IsPlausiblePointer(master)) return null;

        // Level 1: direct fields of master.
        for (long off = 0x18; off < 0x2001; off += 8)
        {
            if (!_mem.TryReadInt64(master + off, out var candidate) || !IsPlausiblePointer(candidate)) continue;
            if (!_mem.TryReadInt64(candidate, out var vtbl) || !IsModuleRangePointer(vtbl)) continue;
            if (_mem.TryReadInt64(candidate + 8, out var tag) && tag == Ams2Constants.Vm550IdentityTag)
                return candidate;
        }

        // Level 2: one hop deeper, through intermediate pointers that are NOT themselves inside the
        // module (i.e. heap-allocated structs, not code/vtables).
        for (long off = 0x18; off < 0x2001; off += 8)
        {
            if (!_mem.TryReadInt64(master + off, out var mid) || !IsPlausiblePointer(mid)) continue;
            if (IsModuleRangePointer(mid)) continue;

            for (long off2 = 0x10; off2 < 0x801; off2 += 8)
            {
                if (!_mem.TryReadInt64(mid + off2, out var candidate) || !IsPlausiblePointer(candidate)) continue;
                if (!_mem.TryReadInt64(candidate, out var vtbl) || !IsModuleRangePointer(vtbl)) continue;
                if (_mem.TryReadInt64(candidate + 8, out var tag) && tag == Ams2Constants.Vm550IdentityTag)
                    return candidate;
            }
        }

        return null;
    }

    /// <summary>Master scan - see this class's doc comment for the full byte-level algorithm this implements, including the mixed hit/candidate bases.</summary>
    private IEnumerable<long> ScanForMaster()
    {
        var rawHits = 0;
        var hitImplausible = 0;
        var selfPtrUnreadable = 0;
        var failedModuleRange = 0;
        var failedSignature = 0;
        var failedMinScore = 0;
        var results = new List<(long candidate, int score)>();

        foreach (var hit in MemoryScanner.FindOccurrences(_mem, Ams2Constants.Vm498ContainerTag))
        {
            rawHits++;

            // Hit-plausibility check uses a +8-biased range (`hit - 0x100000008 < 0x700000000`)
            // rather than the usual `TryReadPointerSafe` bias.
            if (unchecked((ulong)hit) - 0x100000008UL >= 0x700000000UL) { hitImplausible++; continue; }

            var candidate = hit - 8;

            if (!_mem.TryReadInt64(candidate, out var selfPtr)) { selfPtrUnreadable++; continue; }
            if (_moduleBase != 0 && (selfPtr < _moduleBase || selfPtr >= _moduleBase + _moduleSize))
            {
                failedModuleRange++;
                continue;
            }

            // 4-pointer signature - HIT-relative offsets, not candidate-relative.
            if (!_mem.TryReadInt64(hit + 0x18, out var p1) || !IsPlausiblePointer(p1) ||
                !_mem.TryReadInt64(hit + 0xd8, out var p2) || !IsPlausiblePointer(p2) ||
                !_mem.TryReadInt64(hit + 0x110, out var p3) || !IsPlausiblePointer(p3) ||
                !_mem.TryReadInt64(hit + 0x248, out var p4) || !IsPlausiblePointer(p4))
            {
                failedSignature++;
                continue;
            }

            var score = CountPopulatedIntProps(candidate);
            if (score == 0) { failedMinScore++; continue; }

            results.Add((candidate, score));
        }

        _log?.Invoke($"master scan: rawHits={rawHits} hitImplausible={hitImplausible} selfPtrUnreadable={selfPtrUnreadable} " +
                     $"failedModuleRange={failedModuleRange} failedSignature={failedSignature} " +
                     $"failedMinScore={failedMinScore} survived={results.Count}");

        results.Sort((a, b) => b.score.CompareTo(a.score));
        foreach (var (candidate, _) in results)
            yield return candidate;
    }

    /// <summary>
    /// VM550/generic-tag scanner - see this class's doc comment. Deliberately simpler than
    /// ScanForMaster: uniform candidate-relative addressing throughout, and no 4-pointer-signature
    /// check.
    /// </summary>
    private IEnumerable<long> ScanForVm550()
    {
        var rawHits = 0;
        var hitImplausible = 0;
        var selfPtrUnreadable = 0;
        var failedModuleRange = 0;
        var failedMinScore = 0;
        var results = new List<(long candidate, int score)>();

        foreach (var hit in MemoryScanner.FindOccurrences(_mem, Ams2Constants.Vm550IdentityTag))
        {
            rawHits++;

            if (unchecked((ulong)hit) - 0x100000008UL >= 0x700000000UL) { hitImplausible++; continue; }

            var candidate = hit - 8;

            if (!_mem.TryReadInt64(candidate, out var selfPtr)) { selfPtrUnreadable++; continue; }
            if (_moduleBase != 0 && (selfPtr < _moduleBase || selfPtr >= _moduleBase + _moduleSize))
            {
                failedModuleRange++;
                continue;
            }

            var score = CountPopulatedIntProps(candidate);
            if (score == 0) { failedMinScore++; continue; }

            results.Add((candidate, score));
        }

        _log?.Invoke($"vm550 scan: rawHits={rawHits} hitImplausible={hitImplausible} selfPtrUnreadable={selfPtrUnreadable} " +
                     $"failedModuleRange={failedModuleRange} failedMinScore={failedMinScore} survived={results.Count}");

        results.Sort((a, b) => b.score.CompareTo(a.score));
        foreach (var (candidate, _) in results)
            yield return candidate;
    }

    /// <summary>
    /// Counts populated int-property slots at candidate+0x18..+0x1d8 (stride 8). Shared scoring
    /// logic used by both scanners to rank candidates when a tag scan returns more than one hit.
    /// </summary>
    private int CountPopulatedIntProps(long candidate)
    {
        var score = 0;
        for (var off = 0x18; off < 0x1e0; off += 8)
        {
            if (_mem.TryReadInt64(candidate + off, out var slotPtr) && IsPlausiblePointer(slotPtr) &&
                _mem.TryReadInt64(slotPtr - 0x18, out var slotTag) &&
                slotTag == Ams2Constants.IntPropertyTypeTag &&
                _mem.TryReadInt32(slotPtr + 0x18c, out var slotVal) &&
                slotVal != 0)
            {
                score++;
            }
        }
        return score;
    }
}
