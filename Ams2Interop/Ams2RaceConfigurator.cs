using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Ams2Interop.Native;
using Ams2Interop.Signatures;

namespace Ams2Interop;

/// <summary>
/// Configures an Automobilista 2 race by talking directly to a running AMS2AVX.exe process.
///
/// Scoped to just what ApplyRaceConfigAsync needs - persistent hooking and standalone
/// escape-hatch methods are out of scope (see README.md for the full implemented/not-implemented
/// breakdown). Everything below exists to get ApplyRaceConfigAsync from
/// "attached to the process" to "a value has genuinely been written and verified": resolving
/// VM498/VM550/master (VmResolver), calling AMS2's own setter/SetCar functions inside the target
/// process rather than poking memory directly (RemoteExecutor, SlotWriter).
/// </summary>
public sealed class Ams2RaceConfigurator : IDisposable
{
    private const string ProcessName = "AMS2AVX";

    private readonly IReadOnlyDictionary<string, int> _carHashes;
    private readonly IReadOnlyDictionary<string, int> _trackHashes;
    private readonly Action<string>? _log;
    private readonly Ams2OffsetOptions _offsetOptions;

    private ProcessMemory? _mem;
    private RemoteExecutor? _exec;
    private VmResolver? _vmResolver;
    private SlotWriter? _slotWriter;
    private SessionVmResolver? _sessionVmResolver;
    private GameplaySettings? _gameplaySettings;
    private PlayerIdentityWriter? _playerIdentity;
    private long _moduleBase;
    private long _moduleSize;

    public bool IsAttached => _mem != null;

    /// <summary>
    /// The offsets resolved for the attached AMS2 build (null before AttachAsync). When some
    /// couldn't be found, the features needing them are skipped and reported as errors - see
    /// Ams2ResolvedOffsets.Can* and Ams2OffsetResolver.
    /// </summary>
    public Ams2ResolvedOffsets? Offsets { get; private set; }

    /// <param name="carHashes">Display name (or slug) -> veh_hash, e.g. from ams2_vehicle_catalog.csv.</param>
    /// <param name="trackHashes">Display name (or slug) -> track_hash, e.g. from circuits_ref.psv.</param>
    /// <param name="log">
    /// Optional diagnostic sink: attach failures, pointer-resolution failures, and a one-line
    /// summary of each ApplyRaceConfigAsync call (errors and unverified fields).
    /// </param>
    /// <param name="offsetOptions">Where the signature file and per-build offset cache live; defaults when null.</param>
    public Ams2RaceConfigurator(IReadOnlyDictionary<string, int> carHashes, IReadOnlyDictionary<string, int> trackHashes,
        Action<string>? log = null, Ams2OffsetOptions? offsetOptions = null)
    {
        _carHashes = carHashes;
        _trackHashes = trackHashes;
        _log = log;
        _offsetOptions = offsetOptions ?? new Ams2OffsetOptions();
    }

    /// <summary>Locates the running AMS2AVX.exe, opens it, and resolves its module base. Does not resolve VM498/VM550 yet - that happens lazily per call, since it depends on the player having opened Custom Race.</summary>
    public async Task<bool> AttachAsync(CancellationToken ct = default)
    {
        var process = Process.GetProcessesByName(ProcessName).FirstOrDefault();
        if (process is null)
        {
            _log?.Invoke($"AttachAsync: no process named '{ProcessName}' found");
            return false;
        }

        Detach(); // in case of re-attach

        _mem = ProcessMemory.Open(process.Id);
        _exec = new RemoteExecutor(_mem);

        // Resolved BEFORE VmResolver is constructed - VmResolver needs the real module base/size
        // to sanity-check scanned candidates against (see VmResolver's SAFETY NOTE).
        if (!await Task.Run(() => TryResolveModuleBase(out _moduleBase, out _moduleSize), ct).ConfigureAwait(false))
        {
            _log?.Invoke("AttachAsync: could not resolve AMS2AVX.exe's module base via EnumProcessModules");
            Detach();
            return false;
        }

        // Every Ams2Constants tag/RVA reads the active offsets, so they're resolved for this build
        // before any resolver or writer touches the process.
        Offsets = await Task.Run(() => ResolveOffsets(_moduleBase), ct).ConfigureAwait(false);
        Ams2Offsets.Activate(Offsets);

        _vmResolver = new VmResolver(_mem, _moduleBase, _moduleSize, log: _log);
        _slotWriter = new SlotWriter(_mem, _exec, _moduleBase);
        _sessionVmResolver = new SessionVmResolver(_mem, _moduleBase, _moduleSize, log: _log);
        _gameplaySettings = new GameplaySettings(_mem, _moduleBase, log: _log);
        _playerIdentity = new PlayerIdentityWriter(_mem, _moduleBase, log: _log);

        return true;
    }

    /// <summary>
    /// Sets AMS2's global tyre-wear (x1-x7) and/or fuel-usage (x2-x5) multipliers - null leaves
    /// that one alone. These are the player's own game settings, not Custom Race config: they
    /// don't need the Custom Race screen open, and they stay changed until restored, so keep
    /// `original` and pass it to TryRestoreWearSettings when the race is over. Returns false,
    /// having changed nothing, if the settings can't be read or a multiplier is out of range.
    /// See Native/GameplaySettings.cs.
    /// </summary>
    public bool TryApplyWearMultipliers(int? tyreWearMultiplier, int? fuelUsageMultiplier, out WearSettingsSnapshot original)
    {
        RequireAttached();
        original = default;
        if (!GameplaySettingsAvailable()) return false;
        return _gameplaySettings!.TryApply(tyreWearMultiplier, fuelUsageMultiplier, out original);
    }

    /// <summary>Puts back the tyre-wear/fuel-usage settings TryApplyWearMultipliers replaced.</summary>
    public bool TryRestoreWearSettings(WearSettingsSnapshot original)
    {
        RequireAttached();
        if (!GameplaySettingsAvailable()) return false;
        return _gameplaySettings!.TryRestore(original);
    }

    /// <summary>Current tyre-wear/fuel-usage settings, or null if they can't be read.</summary>
    public WearSettingsSnapshot? ReadWearSettings()
    {
        RequireAttached();
        if (!GameplaySettingsAvailable()) return null;
        return _gameplaySettings!.Read();
    }

    /// <summary>
    /// Sets the player's name, handle and nationality in AMS2's profile (its "Edit info" screen),
    /// so the timing screens show the career driver. Like the wear settings, this is the player's
    /// own data and stays changed until restored: keep `original` and pass it to
    /// TryRestorePlayerIdentity when the race is over. Returns false, having changed nothing, if
    /// the profile can't be read or a value is out of range. See Native/PlayerIdentityWriter.cs.
    /// </summary>
    public bool TryApplyPlayerIdentity(PlayerIdentity identity, out PlayerIdentity original)
    {
        RequireAttached();
        original = default!;
        if (!GameplaySettingsAvailable()) return false;
        return _playerIdentity!.TryApply(identity, out original);
    }

    /// <summary>Puts back the identity TryApplyPlayerIdentity replaced.</summary>
    public bool TryRestorePlayerIdentity(PlayerIdentity original)
    {
        RequireAttached();
        if (!GameplaySettingsAvailable()) return false;
        return _playerIdentity!.TryRestore(original);
    }

    /// <summary>The player's current identity in AMS2's profile, or null if it can't be read.</summary>
    public PlayerIdentity? ReadPlayerIdentity()
    {
        RequireAttached();
        if (!GameplaySettingsAvailable()) return null;
        return _playerIdentity!.Read();
    }

    /// <summary>
    /// DEVELOPER TOOL - builds a signature file from the attached build, taking the given known
    /// values (by default Ams2Constants.BuiltIn) as correct. Only run it against a build those
    /// values are confirmed working on: a signature built from a wrong value would lead later
    /// builds to the same wrong place. Writes the file only when every signature was built.
    /// </summary>
    public Ams2SignatureGenerationResult GenerateSignatures(string outputPath, IReadOnlyDictionary<string, long>? knownRvas = null)
    {
        RequireAttached();
        var image = ModuleImage.TryRead(_mem!, _moduleBase, _log)
            ?? throw new InvalidOperationException("could not read AMS2AVX.exe's code section");

        var result = Ams2SignatureGenerator.Generate(image, knownRvas ?? Ams2OffsetNames.BuiltInRvas);
        foreach (var line in result.Report) _log?.Invoke($"GenerateSignatures: {line}");
        if (result.Complete) result.File.Save(outputPath);
        return result;
    }

    private Ams2ResolvedOffsets ResolveOffsets(long moduleBase)
    {
        try
        {
            return Ams2OffsetResolver.Resolve(_mem!, moduleBase, _offsetOptions, _log);
        }
        catch (Exception ex)
        {
            // the same rule as any other "nothing to resolve with" path: built-in values only on a
            // build they're confirmed on (or, while none is recorded, unverified with a warning)
            string? buildId = null;
            try { buildId = ModuleImage.TryReadBuildId(_mem!, moduleBase); }
            catch (Exception) { /* unknown build - BuiltInFallback treats it as unconfirmed */ }
            return Ams2OffsetResolver.BuiltInFallback(moduleBase, buildId, $"offset resolution failed ({ex.Message})", _log);
        }
    }

    private bool GameplaySettingsAvailable()
    {
        if (Offsets is not { CanChangeGameplaySettings: false } offsets) return true;
        _log?.Invoke($"GameplaySettings: skipped - settings pointer not found for this build ({offsets.Describe()})");
        return false;
    }

    /// <summary>
    /// DIAGNOSTIC ONLY - not used by ApplyRaceConfigAsync. Exposes the resolved VM498/VM550
    /// addresses plus the underlying ProcessMemory so an external tool can read arbitrary slot
    /// indices - including ones with no known meaning yet - to help map new fields. Slot
    /// semantics still have to be worked out empirically (change one setting in AMS2's UI, diff
    /// two dumps, see which slot moved); this just exposes the read primitive the rest of the
    /// library already has, it doesn't do the mapping for you.
    /// </summary>
    public (ProcessMemory? Mem, long? Vm498, long? Vm550, long? Master) ResolveForDiagnostics(bool allowFullScan = true)
    {
        RequireAttached();
        return (_mem, _vmResolver!.ResolveVm498(allowFullScan), _vmResolver!.ResolveVm550(allowFullScan),
            _vmResolver!.ResolveMaster(allowFullScan));
    }

    /// <summary>
    /// DIAGNOSTIC ONLY - not used by ApplyRaceConfigAsync. Resolves Practice1's/Qualifying1's own
    /// session VM pointer (see Native/SessionVmResolver.cs) so an external tool can dump/diff its
    /// int-property slots the same way ResolveForDiagnostics enables for vm498/vm550 - the only
    /// way to find a candidate on/off or weather-mode slot beyond what's already confirmed. Pure
    /// memory reads only (no remote calls) - see SessionVmResolver's doc comment - but requires
    /// Race1's own VM550 to already be resolved, since it's used as the freshness anchor; returns
    /// null if that isn't available yet.
    /// </summary>
    public long? ResolvePracticeVmForDiagnostics(long master)
    {
        RequireAttached();
        return _vmResolver!.ResolveVm550() is long vm550 ? _sessionVmResolver!.ResolvePractice1(master, vm550) : null;
    }

    /// <summary>DIAGNOSTIC ONLY - see ResolvePracticeVmForDiagnostics's doc comment.</summary>
    public long? ResolveQualifyingVmForDiagnostics(long master)
    {
        RequireAttached();
        return _vmResolver!.ResolveVm550() is long vm550 ? _sessionVmResolver!.ResolveQualifying1(master, vm550) : null;
    }

    /// <summary>
    /// DIAGNOSTIC ONLY - resolves the session CONTAINER itself, one level up from Practice1/
    /// Qualifying1 (see SessionVmResolver.ResolveContainerForDiagnostics's doc comment). Worth
    /// dumping when hunting for an on/off or weather-mode slot - at least as plausible a home for
    /// "is this session in the weekend at all" as either session's own per-duration/hour VM.
    /// </summary>
    public long? ResolveSessionContainerForDiagnostics(long master)
    {
        RequireAttached();
        return _vmResolver!.ResolveVm550() is long vm550 ? _sessionVmResolver!.ResolveContainerForDiagnostics(master, vm550) : null;
    }

    public void Detach()
    {
        _exec?.Dispose();
        _mem?.Dispose();
        _mem = null;
        _exec = null;
        _vmResolver = null;
        _slotWriter = null;
        _sessionVmResolver = null;
        _gameplaySettings = null;
        _playerIdentity = null;
        Offsets = null;
    }

    /// <summary>
    /// Applies opponents/session-rules configuration, and car/track/livery, to the currently-open
    /// Custom Race screen (see README.md's Status section for what's live-confirmed).
    ///
    /// Order matters:
    /// 1. Every slot field is written, then written again after a 2s settle delay (AMS2's Custom
    ///    Race screen re-initializes some state shortly after opening and can clobber an early write).
    /// 2. SetCar (car/track/livery) + commit run after that, once the screen has settled.
    /// 3. Opponents are written one final time, because SetCar/commit recompute AMS2's own
    ///    opponent/class counts for the new car and overwrite them.
    /// Every field's read-back reflects its final write.
    ///
    /// Before anything is written, each object's layout is checked (Native/Ams2LayoutCheck.cs):
    /// an object that fails is left untouched, and a failing race VM550 stops the whole call -
    /// opponents or SetCar into a half-trusted layout would be pointless. Either way the result's
    /// LayoutCheckFailed is set and the failing slots are listed in UnverifiedFields.
    ///
    /// A null <paramref name="track"/> keeps the track already selected on the screen.
    /// </summary>
    public async Task<RaceConfigResult> ApplyRaceConfigAsync(
        int livery,
        string car,
        string? track,
        OpponentsConfig? opponents = null,
        SessionRulesConfig? sessionRules = null,
        PracticeQualifySessionConfig? practice = null,
        PracticeQualifySessionConfig? qualifying = null,
        CancellationToken ct = default)
    {
        RequireAttached();

        var errors = new List<string>();
        var unverified = new List<(string, int, int?)>();

        // Nothing below can work without the Custom Race objects and the setter: say why and stop,
        // rather than scanning for and writing through values that may belong to a different build.
        if (Offsets is { CanWriteSlots: false } degraded)
        {
            errors.Add($"race settings can't be applied: this AMS2 version isn't recognised ({degraded.Describe()})");
            _log?.Invoke($"ApplyRaceConfigAsync: skipped - {errors[0]}");
            return new RaceConfigResult { Errors = errors, UnverifiedFields = unverified };
        }
        // Kept separate so the final post-SetCar opponents write can replace these results
        // wholesale; merged into `unverified` before reporting.
        var opponentsUnverified = new List<(string, int, int?)>();

        var carKnown = _carHashes.TryGetValue(car, out var carHash);
        if (!carKnown) errors.Add($"unknown car '{car}' - not present in the supplied hash dictionary");

        var trackHash = 0;
        var trackKnown = track == null || _trackHashes.TryGetValue(track, out trackHash);
        if (!trackKnown) errors.Add($"unknown track '{track}' - not present in the supplied hash dictionary");

        var vm498 = _vmResolver!.ResolveVm498();
        var vm550 = _vmResolver!.ResolveVm550();
        var master = _vmResolver!.ResolveMaster();

        if (opponents != null && vm498 is null)
            errors.Add("opponents config supplied but VM498 could not be resolved - is Custom Race open?");
        if (sessionRules != null && vm550 is null)
            errors.Add("session rules config supplied but VM550 could not be resolved - is Custom Race open?");
        if (master is null)
            errors.Add("car/track/livery could not be applied - the master pointer could not be resolved - is Custom Race open?");

        // Resolved once, up front, rather than inside ApplyOnce below - a full-process fallback scan
        // is possible when master's own vm498 is stale, so this isn't repeated on the second pass.
        //
        // Also resolved (even when practice/qualifying configs are null) whenever RaceDate is being
        // applied with DateType=Custom - see SessionVmResolver.ResolveRace2's doc comment:
        // Race2/Practice1/Qualifying1 need that same date written directly, because AMS2 itself only
        // propagates Race1's date into them when the player opens the in-game Race Settings submenu.
        var needsDateSync = sessionRules?.RaceDate != null && sessionRules.DateType == DateType.Custom;
        long? practiceVm = null;
        long? qualifyVm = null;
        long? race2Vm = null;
        // Race1's own vm550 (already resolved above) is required as the freshness anchor
        // SessionVmResolver validates candidate containers against - see its doc comment.
        if ((practice != null || qualifying != null || needsDateSync) && master is long sessionMaster && vm550 is long knownVm550)
        {
            practiceVm = (practice != null || needsDateSync) ? _sessionVmResolver!.ResolvePractice1(sessionMaster, knownVm550) : null;
            qualifyVm = (qualifying != null || needsDateSync) ? _sessionVmResolver!.ResolveQualifying1(sessionMaster, knownVm550) : null;
            race2Vm = needsDateSync ? _sessionVmResolver!.ResolveRace2(sessionMaster, knownVm550) : null;
        }
        if (practice != null && practiceVm is null)
            errors.Add("practice config supplied but its session VM could not be resolved - see SessionVmResolver's doc comment");
        if (qualifying != null && qualifyVm is null)
            errors.Add("qualifying config supplied but its session VM could not be resolved - see SessionVmResolver's doc comment");

        var targets = new CustomRaceTargets
        {
            Vm498 = vm498, Vm550 = vm550, Master = master,
            Practice1Vm = practiceVm, Qualifying1Vm = qualifyVm, Race2Vm = race2Vm,
        };
        var writer = new CustomRaceWriter(_mem!, _mem!, _slotWriter!, Ams2Constants.IntPropertyTypeTag, _log);

        // Read-only, before the first write: an object whose slots don't hold what this build's
        // layout says they should is not written to at all.
        var layout = writer.CheckLayout(targets);
        if (layout.RaceVmFailed)
        {
            errors.Add("race settings not applied: the Custom Race session layout doesn't match what this AMS2 version is expected to have");
            unverified.AddRange(layout.Failures);
            _log?.Invoke($"ApplyRaceConfigAsync: skipped - {errors[^1]}");
            return new RaceConfigResult { Errors = errors, UnverifiedFields = unverified, LayoutCheckFailed = true };
        }
        if (opponents != null && layout.Vm498Failed)
            errors.Add("opponents not applied: VM498's layout doesn't match what this AMS2 version is expected to have");

        writer.ApplyOnce(targets, layout, opponents, sessionRules, practice, qualifying, unverified, opponentsUnverified);

        // SlotWriter already reads back each write immediately, so this delay isn't about our write
        // landing - it's about outlasting AMS2's own Custom Race screen re-initializing some of its
        // state shortly after opening, which can silently clobber a config written too early.
        // Shorter delays were live-confirmed to lose writes.
        await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
        unverified.Clear(); // only the second, later write's verification result is meaningful
        opponentsUnverified.Clear();
        writer.ApplyOnce(targets, layout, opponents, sessionRules, practice, qualifying, unverified, opponentsUnverified);

        // SetCar is a direct function call, not a staged slot write, so it isn't part of the
        // two-write cycle above - but it runs AFTER that cycle settles, for the same reason the
        // cycle exists: the screen's re-initialization can clobber an early SetCar too.
        var canSetCar = Offsets?.CanSetCar != false;
        if (!canSetCar)
            errors.Add($"car/track/livery skipped: SetCar not found for this AMS2 version ({Offsets!.Describe()})");

        // master's selection slots are SetCar's only read-back: if they don't read cleanly,
        // SetCar's effect couldn't be verified, so it (and commit, which also takes master) isn't run.
        var masterTrusted = master is long checkedMaster && writer.CheckMasterLayout(checkedMaster, layout);
        if (master is not null && !masterTrusted)
            errors.Add("car/track/livery not applied: master's selection slots don't read as this AMS2 version is expected to have them");

        // SetCar can't leave the track out: to keep it, it's handed back the one already selected.
        if (track == null && masterTrusted && master is long selectionMaster && !_mem!.TryReadSlot(selectionMaster, Ams2Constants.MasterSlot.Track, out trackHash))
        {
            trackKnown = false;
            errors.Add("car/livery could not be applied - the currently selected track could not be read");
        }

        if (canSetCar && carKnown && trackKnown && masterTrusted && master is long m)
        {
            // SetCarRva silently no-ops when its internal validation gate rejects the combination
            // (see Ams2Constants.SetCarValidationGateRva). Calling the gate directly would say why,
            // but its RVA hasn't been re-derived for the current AMS2 build, and remote-calling an
            // unconfirmed address can crash the game - so it's not called. The MasterSlot
            // read-back below still catches a rejection, just without the reason.
            var setCarAddress = _moduleBase + Ams2Constants.SetCarRva;
            var ok = await Task.Run(() => _exec!.CallSetCar(setCarAddress, m, trackHash, carHash, livery), ct)
                .ConfigureAwait(false);
            if (!ok)
                errors.Add("SetCar call did not complete");

            // The Custom Race screen's livery box may keep showing the previous livery: the session
            // still starts with this one (live-confirmed 2026-10-26).
            writer.CheckSetCarReadBack(m, trackHash, carHash, livery, unverified);
            _log?.Invoke($"ApplyRaceConfigAsync: master=0x{m:X}, vm498={(vm498 is long l498 ? $"0x{l498:X}" : "null")}, vm550={(vm550 is long l550 ? $"0x{l550:X}" : "null")}, SetCar(track={track ?? "(kept)"}/{trackHash}, car={car}/{carHash}, livery={livery}) -> {(ok ? "completed" : "NOT completed")}");
        }

        // AMS2's own AI-opponent vehicle-class-count recompute (see Ams2Constants.CommitRva's doc
        // comment) - mirrors what SetCarRva itself calls internally when values change. Unrelated
        // to RaceDate/session fields. Its only read-back is CheckCommitResult's advisory sanity check.
        var canCommit = Offsets?.CanCommit != false;
        List<(string Field, int Wanted, int? Got)> commitProblems = new();
        if (sessionRules != null && !canCommit)
            errors.Add($"opponent class recount skipped: commit not found for this AMS2 version ({Offsets!.Describe()})");

        if (canCommit && sessionRules != null && masterTrusted && master is long commitMaster)
        {
            var commitAddress = _moduleBase + Ams2Constants.CommitRva;
            var committed = await Task.Run(() => _exec!.Call(commitAddress, commitMaster, 0), ct)
                .ConfigureAwait(false);
            if (!committed)
                errors.Add("commit call did not complete");
            else
                commitProblems = writer.CheckCommitResult(vm498, layout);
        }

        // SetCar/commit recompute AMS2's own opponent counts for the newly-selected car, overwriting
        // what the two ApplyOnce passes wrote (live-confirmed: RivalCount reset to the class maximum,
        // and a count above the number of same-class cars makes AMS2 pad the grid with other
        // classes). So opponents are written once more, last, and verified from this final write.
        if (opponents != null && vm498 is long finalVm498 && !layout.Vm498Failed)
        {
            opponentsUnverified.Clear();
            writer.ApplyOpponents(finalVm498, opponents, opponentsUnverified);
        }
        unverified.AddRange(opponentsUnverified);
        unverified.AddRange(layout.Failures);
        unverified.AddRange(commitProblems);

        _log?.Invoke($"ApplyRaceConfigAsync: done - {errors.Count} error(s), {unverified.Count} unverified field(s)"
            + (layout.AnyFailed ? " | LAYOUT CHECK FAILED" : "")
            + (errors.Count > 0 ? $" | errors: {string.Join("; ", errors)}" : "")
            + (unverified.Count > 0 ? $" | unverified: {string.Join("; ", unverified.Select(u => $"{u.Item1}(wanted={u.Item2}, actual={u.Item3?.ToString() ?? "null"})"))}" : ""));

        return new RaceConfigResult
        {
            Errors = errors,
            UnverifiedFields = unverified,
            AdvisoryFields = commitProblems.Select(p => p.Field).ToArray(),
            LayoutCheckFailed = layout.AnyFailed
        };
    }

    /// <summary>See Ams2Constants.SetCarValidationGateRva's doc comment for the full evidence behind each code.</summary>
    private static string DescribeSetCarGateCode(long code) => code switch
    {
        1 => "track hash not found in AMS2's own track registry",
        2 => "track failed an eligibility/ownership check",
        3 => "car hash not found in AMS2's own vehicle registry",
        4 => "car/track combination ineligible (DLC ownership/compatibility)",
        5 => "unidentified track/vm498 condition",
        6 => "livery index not found for this car",
        _ => "unknown code",
    };

    private bool TryResolveModuleBase(out long baseAddress, out long moduleSize)
    {
        baseAddress = 0;
        moduleSize = 0;
        var modules = new IntPtr[1024];
        if (!NativeMethods.EnumProcessModules(_mem!.Handle, modules, (uint)(IntPtr.Size * modules.Length), out var bytesNeeded))
            return false;

        var count = (int)(bytesNeeded / (uint)IntPtr.Size);
        var nameBuffer = new StringBuilder(260);
        for (var i = 0; i < count; i++)
        {
            nameBuffer.Clear();
            NativeMethods.GetModuleFileNameExW(_mem.Handle, modules[i], nameBuffer, (uint)nameBuffer.Capacity);
            if (!nameBuffer.ToString().EndsWith("AMS2AVX.exe", StringComparison.OrdinalIgnoreCase)) continue;

            if (NativeMethods.GetModuleInformation(_mem.Handle, modules[i], out var info,
                    (uint)Marshal.SizeOf<NativeMethods.MODULEINFO>()))
            {
                baseAddress = (long)info.lpBaseOfDll;
                moduleSize = info.SizeOfImage;
                return true;
            }
        }
        return false;
    }

    private void RequireAttached()
    {
        if (_mem is null) throw new InvalidOperationException("Not attached - call AttachAsync() first.");
    }

    public void Dispose() => Detach();
}
