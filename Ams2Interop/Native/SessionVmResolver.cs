namespace Ams2Interop.Native;

/// <summary>
/// Resolves Practice1/Qualifying1/Race2's own VM550-shaped pointers with NO remote calls at all -
/// a pure out-of-process memory scan/validate, the same style VmResolver already uses for
/// master/vm498/vm550.
///
/// Never remote-calls into the game: earlier remote-call-based resolvers crashed AMS2AVX.exe
/// after a game patch moved their target function. Practice/Qualifying/Race are resolved
/// together by memory reads only, requiring all three to be distinct and correctly tagged.
///
/// The "container" is VM498 itself (the object VmResolver.ResolveVm498() finds), which caches 8
/// session-wrapper pointers at `+0x18` through `+0x50` (see Ams2Constants.SessionIndex), each
/// wrapper's own `+0x18` field holding that session's VM550 pointer.
///
/// A validly-tagged VM498 is not enough: its wrapper array can be stale (populated once at
/// construction, not matching the live session). So a candidate is trusted only if its own Race1
/// wrapper entry is pointer-equal to the caller's independently-resolved Race1 VM550
/// (VmResolver.ResolveVm550()) - an exact match that can't happen by coincidence. master's own
/// vm498 is tried first; if it fails, a full-process scan for other Vm498-tagged instances keeps
/// whichever one matches.
/// </summary>
public sealed class SessionVmResolver
{
    private readonly ProcessMemory _mem;
    private readonly long _moduleBase;
    private readonly long _moduleSize;
    private readonly Action<string>? _log;
    private long _cachedContainer;

    public SessionVmResolver(ProcessMemory mem, long moduleBase, long moduleSize, Action<string>? log = null)
    {
        _mem = mem;
        _moduleBase = moduleBase;
        _moduleSize = moduleSize;
        _log = log;
    }

    /// <param name="master">VmResolver.ResolveMaster()'s result.</param>
    /// <param name="knownLiveRace1Vm550">VmResolver.ResolveVm550()'s result - the independently-
    /// resolved, already-trusted Race1 VM550 pointer, used as the freshness anchor (see this
    /// class's doc comment). Required: without it there's no way to tell a live container from a
    /// stale-but-validly-tagged one.</param>
    public long? ResolvePractice1(long master, long knownLiveRace1Vm550) =>
        ResolveSession(master, knownLiveRace1Vm550, Ams2Constants.SessionIndex.Practice1, "Practice1");

    /// <summary>See ResolvePractice1's doc comment.</summary>
    public long? ResolveQualifying1(long master, long knownLiveRace1Vm550) =>
        ResolveSession(master, knownLiveRace1Vm550, Ams2Constants.SessionIndex.Qualifying1, "Qualifying1");

    /// <summary>
    /// See ResolvePractice1's doc comment for the general mechanism. AMS2's own handler for activating the "CustomEventRaceSettingsDialog" - i.e. the in-game Race Settings
    /// submenu - is the ONLY thing that normally propagates Race1's date into Race2/Practice1/
    /// Qualifying1's own Day/Month/Year slots, and it only runs when the player manually opens
    /// that submenu. Race1 itself (what VmResolver.ResolveVm550() already finds) is the SOURCE,
    /// never rewritten by this. If AMS2 actually plays Race2 rather than Race1 for a given weekend
    /// format, Race2 keeps whatever date it already had until that submenu is visited - this
    /// resolver lets ApplyRaceConfigAsync propagate the date directly instead of depending on that
    /// visit.
    /// </summary>
    public long? ResolveRace2(long master, long knownLiveRace1Vm550) =>
        ResolveSession(master, knownLiveRace1Vm550, Ams2Constants.SessionIndex.Race2, "Race2");

    /// <summary>DIAGNOSTIC - resolves the session container itself (VM498, once validated as the live one) rather than any one session's own VM.</summary>
    public long? ResolveContainerForDiagnostics(long master, long knownLiveRace1Vm550) =>
        ResolveContainer(master, knownLiveRace1Vm550);

    private long? ResolveSession(long master, long knownLiveRace1Vm550, int sessionIndex, string label)
    {
        var container = ResolveContainer(master, knownLiveRace1Vm550);
        if (container is not long c)
        {
            _log?.Invoke($"SessionVmResolver: no valid (live-matching) session container found from master 0x{master:X} - {label} unresolved");
            return null;
        }
        if (!TryReadSessionVm(c, sessionIndex, out var vm))
        {
            _log?.Invoke($"SessionVmResolver: container 0x{c:X} validated, but {label}'s own wrapper entry didn't resolve to a valid VM550");
            return null;
        }
        return vm;
    }

    /// <summary>
    /// Finds the VM498-shaped container whose wrapper array is actually live - see this class's
    /// doc comment. Unlike a plain identity-tag check, this validates the object BY WHETHER ITS
    /// RACE1 WRAPPER ENTRY MATCHES THE INDEPENDENTLY-KNOWN-LIVE RACE1 VM550, which is what a stale
    /// (but validly-tagged) VM498 fails.
    /// </summary>
    private long? ResolveContainer(long master, long knownLiveRace1Vm550)
    {
        if (_cachedContainer != 0 && ValidatesAsContainer(_cachedContainer, knownLiveRace1Vm550))
            return _cachedContainer;

        // Tier 1: master's own vm498 field - cheapest, and correct whenever it isn't stale.
        if (_mem.TryReadPointerSafe(master + Ams2Constants.Vm498Offset, out var vm498) &&
            ValidatesAsContainer(vm498, knownLiveRace1Vm550))
        {
            _cachedContainer = vm498;
            return vm498;
        }

        // Tier 2: master's own vm498 is stale or absent - fall back to a full-process scan for
        // OTHER Vm498-tagged instances (a real, validly-tagged, but disconnected VM498 instance
        // can coexist with the correct one elsewhere in memory). Scored by populated-int-property count only to break ties between multiple
        // matches, which shouldn't normally happen - the knownLiveRace1Vm550 equality check inside
        // ValidatesAsContainer is what actually discriminates the live instance.
        var candidates = new List<(long candidate, int score)>();
        var rawHits = 0;
        foreach (var hit in MemoryScanner.FindOccurrences(_mem, Ams2Constants.Vm498IdentityTag))
        {
            rawHits++;
            var candidate = hit - 8;
            if (!_mem.TryReadInt64(candidate, out var selfPtr) || !IsModuleRangePointer(selfPtr)) continue;
            if (!ValidatesAsContainer(candidate, knownLiveRace1Vm550)) continue;
            candidates.Add((candidate, CountPopulatedIntProps(candidate)));
        }

        if (candidates.Count == 0)
        {
            _log?.Invoke($"SessionVmResolver: no session container matches the known-live Race1 VM550 0x{knownLiveRace1Vm550:X} (scanned {rawHits} vm498-tagged candidate(s))");
            return null;
        }

        candidates.Sort((a, b) => b.score.CompareTo(a.score));
        var best = candidates[0].candidate;
        _cachedContainer = best;
        return best;
    }

    /// <summary>
    /// A candidate is trusted as the live session container only if it's a correctly-tagged VM498
    /// AND its own Race1 wrapper entry equals the caller-supplied, already-confirmed-live Race1
    /// VM550 - see this class's doc comment. Practice1/Qualifying1 are additionally required to be
    /// distinct from Race1 and from each other before either is trusted.
    /// </summary>
    private bool ValidatesAsContainer(long candidate, long knownLiveRace1Vm550)
    {
        if (!IsPlausiblePointer(candidate)) return false;
        if (!_mem.TryReadInt64(candidate + 8, out var tag) || tag != Ams2Constants.Vm498IdentityTag) return false;

        if (!TryReadSessionVm(candidate, Ams2Constants.SessionIndex.Race1, out var r1) || r1 != knownLiveRace1Vm550)
            return false;
        if (!TryReadSessionVm(candidate, Ams2Constants.SessionIndex.Practice1, out var p1)) return false;
        if (!TryReadSessionVm(candidate, Ams2Constants.SessionIndex.Qualifying1, out var q1)) return false;
        return p1 != q1 && p1 != r1 && q1 != r1;
    }

    /// <summary>
    /// Reads one session's VM550 pointer off the container's own wrapper array: container +
    /// (0x18 + sessionIndex*8) is a wrapper pointer, whose own +0x18 field holds the actual
    /// VM550-shaped pointer (see Ams2Constants.SessionIndex). Validated by identity tag only - liveness itself is
    /// established by ValidatesAsContainer's known-Race1-pointer match, not by anything checked
    /// here in isolation.
    /// </summary>
    private bool TryReadSessionVm(long container, int sessionIndex, out long vm)
    {
        vm = 0;
        var wrapperOffset = 0x18 + sessionIndex * 8;
        if (!_mem.TryReadPointerSafe(container + wrapperOffset, out var wrapper)) return false;
        if (!_mem.TryReadPointerSafe(wrapper + 0x18, out var candidate)) return false;
        if (!_mem.TryReadInt64(candidate + 8, out var tag) || tag != Ams2Constants.Vm550IdentityTag) return false;
        vm = candidate;
        return true;
    }

    private static bool IsPlausiblePointer(long pointer) => unchecked((ulong)pointer) - 0x100000000UL < 0x700000000UL;

    /// <summary>See VmResolver.IsModuleRangePointer's doc comment - same check, duplicated rather than shared (see this file's own scan/validate style, mirroring VmResolver's).</summary>
    private bool IsModuleRangePointer(long pointer)
    {
        if (_moduleBase != 0) return pointer >= _moduleBase && pointer < _moduleBase + _moduleSize;
        return unchecked((ulong)pointer) - 0x140000000UL < 0x3000000UL;
    }

    /// <summary>See VmResolver.CountPopulatedIntProps's doc comment - same scoring logic, used only to break ties between multiple live-matching candidates (which shouldn't normally happen).</summary>
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
