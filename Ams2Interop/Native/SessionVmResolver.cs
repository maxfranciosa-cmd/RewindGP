namespace Ams2Interop.Native;

/// <summary>
/// Resolves Practice1/Qualifying1's own VM550-shaped pointers via AMS2's own per-session-name
/// getter function (see Ams2Constants.SessionVmGetterRva's doc comment for the full background:
/// where this address came from, what's confirmed vs. not, and why practice/qualifying settings
/// don't show up as VM498/VM550 slots at all).
///
/// The "container" this needs is found by a search rooted at `master`: try `master` itself first,
/// then walk `*(master+offset)` for offset in [0, 0x1800) step 8, calling the getter with
/// (candidate, Practice1) and (candidate, Qualifying1) for each and accepting the first candidate
/// where both come back plausible and distinct. A fuller version of this search also tries a
/// second "canonical" root and cross-validates a THIRD (race) pointer too - this only checks two,
/// since Practice1/Qualifying1 are all ApplySessionRules currently needs.
///
/// COST: unlike every other resolver in this library, each candidate here costs an actual
/// CreateRemoteThread round-trip (not just a memory read) - a cold resolve can be tens to
/// hundreds of remote calls before (if ever) landing on a valid container, so expect this to be
/// noticeably slower than VmResolver's own full scan. The found container is cached exactly like
/// VmResolver caches master/VM550, so this cost is only paid once per attach (until invalidated).
/// </summary>
public sealed class SessionVmResolver
{
    private readonly ProcessMemory _mem;
    private readonly RemoteExecutor _exec;
    private readonly long _getterAddress;
    private readonly long _moduleBase;
    private readonly long _moduleSize;
    private readonly Action<string>? _log;
    private long _cachedContainer;

    public SessionVmResolver(ProcessMemory mem, RemoteExecutor exec, long moduleBase, long moduleSize, Action<string>? log = null)
    {
        _mem = mem;
        _exec = exec;
        _getterAddress = moduleBase + Ams2Constants.SessionVmGetterRva;
        _moduleBase = moduleBase;
        _moduleSize = moduleSize;
        _log = log;
    }

    /// <summary>Resolves Practice1's own VM pointer, or null if the session container couldn't be found/validated or the call itself failed.</summary>
    public long? ResolvePractice1(long master) => ResolveSession(master, Ams2Constants.SessionIndex.Practice1, "Practice1");

    /// <summary>Resolves Qualifying1's own VM pointer, or null - see ResolvePractice1's doc comment.</summary>
    public long? ResolveQualifying1(long master) => ResolveSession(master, Ams2Constants.SessionIndex.Qualifying1, "Qualifying1");

    /// <summary>
    /// Resolves Race2's own VM pointer, or null - see ResolvePractice1's doc comment for the
    /// general resolution mechanism. CONFIRMED NEEDED: AMS2's own handler for activating the
    /// "CustomEventRaceSettingsDialog" - i.e. the in-game Race Settings submenu - is the ONLY thing
    /// that normally propagates Race1's date into Race2/
    /// Practice1/Qualifying1's own Day/Month/Year slots (via the same generic setter SlotWriter
    /// uses), and it only runs when the player manually opens that submenu. Race1 (index 6, what
    /// VmResolver.ResolveVm550() already finds) never gets rewritten by this - it's the SOURCE.
    /// If AMS2 actually plays Race2 rather than Race1 for a given weekend format, Race2 keeps
    /// whatever date it already had until that submenu is visited - this resolver lets
    /// ApplyRaceConfigAsync propagate the date directly instead of depending on that visit.
    /// </summary>
    public long? ResolveRace2(long master) => ResolveSession(master, Ams2Constants.SessionIndex.Race2, "Race2");

    /// <summary>
    /// DIAGNOSTIC - resolves the "container" itself (the weekend/session-list object Practice1
    /// and Qualifying1 are both looked up FROM), not either session's own VM. Worth dumping this
    /// too, not just Practice1/Qualifying1, when hunting for an on/off slot: it's at least as
    /// plausible that "is this session in the weekend at all" lives one level up on the container
    /// as a single flag/array, rather than as a field on each session's own per-duration/hour VM.
    /// </summary>
    public long? ResolveContainerForDiagnostics(long master) => ResolveContainer(master);

    private long? ResolveSession(long master, int sessionIndex, string label)
    {
        var container = ResolveContainer(master);
        if (container is not long c)
        {
            _log?.Invoke($"SessionVmResolver: couldn't find a session container from master 0x{master:X} - {label} unresolved");
            return null;
        }
        if (!TryGetSessionVm(c, sessionIndex, out var vm) || !IsPlausiblePointer(vm))
        {
            _log?.Invoke($"SessionVmResolver: container 0x{c:X} found, but getter({label}) failed/implausible");
            return null;
        }
        return vm;
    }

    private long? ResolveContainer(long master)
    {
        if (_cachedContainer != 0 && ValidatesAsContainer(_cachedContainer))
            return _cachedContainer;

        if (ValidatesAsContainer(master))
        {
            _log?.Invoke("SessionVmResolver: master itself validates as the session container");
            _cachedContainer = master;
            return master;
        }

        for (long off = 0; off < 0x1800; off += 8)
        {
            if (!_mem.TryReadPointerSafe(master + off, out var candidate)) continue;
            if (!ValidatesAsContainer(candidate)) continue;

            _log?.Invoke($"SessionVmResolver: session container found at master+0x{off:X} -> 0x{candidate:X}");
            _cachedContainer = candidate;
            return candidate;
        }

        return null;
    }

    private bool ValidatesAsContainer(long candidate)
    {
        // SAFETY-CRITICAL: the getter (SessionVmGetterRva) dereferences its argument with no
        // bounds checking of its own, so it must never be called without first confirming the
        // candidate LOOKS like a real object via safe (out-of-process, can't crash anything)
        // memory reads. Calling the getter - an actual in-process function call - on an
        // unvalidated pointer risks crashing AMS2AVX.exe outright: a bad ReadProcessMemory just
        // fails; a bad in-process dereference takes the whole process down. LooksLikeValidTarget
        // MUST run (and pass) before any call to TryGetSessionVm below.
        if (!LooksLikeValidTarget(candidate)) return false;
        if (!TryGetSessionVm(candidate, Ams2Constants.SessionIndex.Practice1, out var p1) || !IsPlausiblePointer(p1)) return false;
        if (!TryGetSessionVm(candidate, Ams2Constants.SessionIndex.Qualifying1, out var q1) || !IsPlausiblePointer(q1)) return false;
        return p1 != q1;
    }

    /// <summary>
    /// Pre-call validation, run before ever calling the getter: candidate itself plausible,
    /// `*(candidate+8)` reads as a pointer that lands inside AMS2AVX.exe's own module range (i.e.
    /// looks like a vtable pointer into code, not arbitrary heap data), and a sanity value at
    /// `vtable+4` is below 0x401. Every check here is a plain out-of-process memory read
    /// (ReadProcessMemory) - safe to fail, never crashes anything - which is the whole point: it's
    /// the gate standing between "unknown heap value" and ever making an actual in-process
    /// function call on it.
    /// </summary>
    private bool LooksLikeValidTarget(long candidate)
    {
        if (!IsPlausiblePointer(candidate)) return false;
        if (!_mem.TryReadInt64(candidate + 8, out var vtbl)) return false;
        if (vtbl < _moduleBase || vtbl >= _moduleBase + _moduleSize) return false;
        return _mem.TryReadInt32(vtbl + 4, out var sanityCheck) && (uint)sanityCheck < 0x401;
    }

    private bool TryGetSessionVm(long container, int sessionIndex, out long vm) =>
        _exec.CallWithReturn(_getterAddress, container, sessionIndex, out vm);

    private static bool IsPlausiblePointer(long pointer) => unchecked((ulong)pointer) - 0x100000000UL < 0x700000000UL;
}
