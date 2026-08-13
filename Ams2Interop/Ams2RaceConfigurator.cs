using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Ams2Interop.Native;

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

    private ProcessMemory? _mem;
    private RemoteExecutor? _exec;
    private VmResolver? _vmResolver;
    private SlotWriter? _slotWriter;
    private SessionVmResolver? _sessionVmResolver;
    private long _moduleBase;
    private long _moduleSize;

    public bool IsAttached => _mem != null;

    /// <param name="carHashes">Display name (or slug) -> veh_hash, e.g. from ams2_vehicle_catalog.csv.</param>
    /// <param name="trackHashes">Display name (or slug) -> track_hash, e.g. from circuits_ref.psv.</param>
    /// <param name="log">
    /// Optional diagnostic sink, used for a few attach-time diagnostics (e.g. why the module base
    /// couldn't be resolved) and per-candidate VM498/VM550 scan results during ApplyRaceConfigAsync.
    /// </param>
    public Ams2RaceConfigurator(IReadOnlyDictionary<string, int> carHashes, IReadOnlyDictionary<string, int> trackHashes,
        Action<string>? log = null)
    {
        _carHashes = carHashes;
        _trackHashes = trackHashes;
        _log = log;
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
        _vmResolver = new VmResolver(_mem, _moduleBase, _moduleSize, log: _log);
        _slotWriter = new SlotWriter(_mem, _exec, _moduleBase);
        _sessionVmResolver = new SessionVmResolver(_mem, _exec, _moduleBase, _moduleSize, log: _log);

        return true;
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
    /// way to find a candidate on/off or weather-mode slot beyond what's already confirmed. Costs
    /// real remote-call round-trips on a cold resolve - see SessionVmResolver's doc comment.
    /// </summary>
    public long? ResolvePracticeVmForDiagnostics(long master)
    {
        RequireAttached();
        return _sessionVmResolver!.ResolvePractice1(master);
    }

    /// <summary>DIAGNOSTIC ONLY - see ResolvePracticeVmForDiagnostics's doc comment.</summary>
    public long? ResolveQualifyingVmForDiagnostics(long master)
    {
        RequireAttached();
        return _sessionVmResolver!.ResolveQualifying1(master);
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
        return _sessionVmResolver!.ResolveContainerForDiagnostics(master);
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
    }

    /// <summary>
    /// Applies opponents/session-rules configuration, and car/track/livery, to the currently-open
    /// Custom Race screen - the one function in this library confirmed reliable enough to depend
    /// on (see README.md's Status section). Car/track/livery has no read-back verification (see
    /// the SetCar call below) - a failed lookup or a SetCar call that didn't complete surfaces as
    /// an error, but a call that executed and was silently rejected by AMS2 does not.
    ///
    /// Uses a two-write-then-verify discipline: every field is written, then re-written once more
    /// after a short delay, then read back to confirm.
    /// </summary>
    public async Task<RaceConfigResult> ApplyRaceConfigAsync(
        int livery,
        string car,
        string track,
        OpponentsConfig? opponents = null,
        SessionRulesConfig? sessionRules = null,
        PracticeQualifySessionConfig? practice = null,
        PracticeQualifySessionConfig? qualifying = null,
        CancellationToken ct = default)
    {
        RequireAttached();

        var errors = new List<string>();
        var unverified = new List<(string, int, int?)>();

        var carKnown = _carHashes.TryGetValue(car, out var carHash);
        if (!carKnown) errors.Add($"unknown car '{car}' - not present in the supplied hash dictionary");

        var trackKnown = _trackHashes.TryGetValue(track, out var trackHash);
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

        // Resolved once, up front, rather than inside ApplyOnce below: unlike every other resolve
        // in this method, this one costs real remote-call round-trips per candidate, not just
        // memory reads, so it's deliberately not repeated on the second write pass.
        //
        // Also resolved (even when practice/qualifying configs are null) whenever RaceDate is being
        // applied with DateType=Custom - see SessionVmResolver.ResolveRace2's doc comment:
        // Race2/Practice1/Qualifying1 need that same date written directly, because AMS2 itself only
        // propagates Race1's date into them when the player opens the in-game Race Settings submenu
        // (confirmed) - depending on that is what caused the "date is correct in Race Settings but
        // wrong once the race loads" bug.
        var needsDateSync = sessionRules?.RaceDate != null && sessionRules.DateType == DateType.Custom;
        long? practiceVm = null;
        long? qualifyVm = null;
        long? race2Vm = null;
        if ((practice != null || qualifying != null || needsDateSync) && master is long sessionMaster)
        {
            practiceVm = (practice != null || needsDateSync) ? _sessionVmResolver!.ResolvePractice1(sessionMaster) : null;
            qualifyVm = (qualifying != null || needsDateSync) ? _sessionVmResolver!.ResolveQualifying1(sessionMaster) : null;
            race2Vm = needsDateSync ? _sessionVmResolver!.ResolveRace2(sessionMaster) : null;
        }
        if (practice != null && practiceVm is null)
            errors.Add("practice config supplied but its session VM could not be resolved - see SessionVmResolver's doc comment");
        if (qualifying != null && qualifyVm is null)
            errors.Add("qualifying config supplied but its session VM could not be resolved - see SessionVmResolver's doc comment");

        void ApplyOnce()
        {
            if (opponents != null && vm498 is long v498) ApplyOpponents(v498, opponents, unverified);
            // vm498 is passed through regardless of `opponents` - ApplySessionRules needs it too,
            // for the VM498 packed-date write alongside VM550's slots (see TrySetVm498PackedDate).
            // practiceVm/qualifyVm/race2Vm are passed through regardless of `practice`/`qualifying`
            // too, for RaceDate's Race2/Practice1/Qualifying1 propagation (see needsDateSync above).
            if (sessionRules != null && vm550 is long v550) ApplySessionRules(v550, vm498, sessionRules, unverified, practiceVm, qualifyVm, race2Vm);
            if (practice != null && practiceVm is long pVm) ApplyPracticeOrQualifying(pVm, practice, "Practice", unverified);
            if (qualifying != null && qualifyVm is long qVm) ApplyPracticeOrQualifying(qVm, qualifying, "Qualifying", unverified);
        }

        ApplyOnce();

        // SetCar is a direct function call, not a staged slot write - called once, not as part
        // of the two-write cycle below.
        if (carKnown && trackKnown && master is long m)
        {
            var setCarAddress = _moduleBase + Ams2Constants.SetCarRva;
            var ok = await Task.Run(() => _exec!.CallSetCar(setCarAddress, m, trackHash, carHash, livery), ct)
                .ConfigureAwait(false);
            if (!ok)
                errors.Add("SetCar call did not complete (no read-back verification is available for this call)");
        }

        // SlotWriter's own write already does an immediate read-back per slot; this outer delay
        // only needs to give the game a moment to settle before the SECOND pass, not wait out
        // anything specific - live-testing found 100ms enough.
        await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(false);
        unverified.Clear(); // only the second, later write's verification result is meaningful
        ApplyOnce();

        // See Ams2Constants.CommitRva's doc comment - CONFIRMED to be AMS2's own AI-opponent
        // vehicle-class-count recompute, not a RaceDate/session fix of any kind. Kept here for
        // opponents' sake; don't expect it to matter for sessionRules/RaceDate (the fix that DOES
        // work for RaceDate is the VM498 packed-date write inside ApplySessionRules). Called once,
        // after the final write pass, with `master` as its sole argument - like SetCar, no
        // read-back verification is available for it.
        if (sessionRules != null && master is long commitMaster)
        {
            var commitAddress = _moduleBase + Ams2Constants.CommitRva;
            var committed = await Task.Run(() => _exec!.Call(commitAddress, commitMaster, 0), ct)
                .ConfigureAwait(false);
            if (!committed)
                errors.Add("commit call did not complete (no read-back verification is available for this call)");
        }

        return new RaceConfigResult { Errors = errors, UnverifiedFields = unverified };
    }

    private void ApplyOpponents(long vm498, OpponentsConfig cfg, List<(string, int, int?)> unverified)
    {
        TrySet(vm498, Ams2Constants.Vm498Slot.NumOpponentsType, (int?)cfg.NumOpponentsType, nameof(cfg.NumOpponentsType), unverified);
        TrySet(vm498, Ams2Constants.Vm498Slot.OpponentsTypeKind, (int?)cfg.OpponentsType, nameof(cfg.OpponentsType), unverified);
        TrySet(vm498, Ams2Constants.Vm498Slot.RivalCount, cfg.OpponentCount, nameof(cfg.OpponentCount), unverified);
        // EXPERIMENTAL - see Vm498Slot.RivalCountPerClass's doc comment. Mirrors RivalCount rather
        // than being independently configurable - there's no cfg field of its own for this yet.
        TrySet(vm498, Ams2Constants.Vm498Slot.RivalCountPerClass, cfg.OpponentCount, $"{nameof(cfg.OpponentCount)}(perClass)", unverified);

        if (cfg.Skill is int skill)
        {
            if (skill is < 70 or > 120) unverified.Add(($"{nameof(cfg.Skill)} (out of range 70-120)", skill, null));
            else TrySet(vm498, Ams2Constants.Vm498Slot.Skill, skill, nameof(cfg.Skill), unverified);
        }

        if (cfg.AiWetWeatherSkill is int wetSkill)
        {
            if (wetSkill is < 0 or > 200) unverified.Add(($"{nameof(cfg.AiWetWeatherSkill)} (out of range 0-200)", wetSkill, null));
            else TrySet(vm498, Ams2Constants.Vm498Slot.WetWeatherSkill, wetSkill, nameof(cfg.AiWetWeatherSkill), unverified);
        }

        TrySet(vm498, Ams2Constants.Vm498Slot.MistakeFrequency, (int?)cfg.AiMistakeFrequency, nameof(cfg.AiMistakeFrequency), unverified);
    }

    private void ApplySessionRules(long vm550, long? vm498, SessionRulesConfig cfg, List<(string, int, int?)> unverified,
        long? practice1Vm, long? qualifying1Vm, long? race2Vm)
    {
        TrySet(vm550, Ams2Constants.Vm550Slot.PrivateQuali, (int?)cfg.PrivateQualiSession, nameof(cfg.PrivateQualiSession), unverified);
        TrySet(vm550, Ams2Constants.Vm550Slot.MandatoryPit, (int?)cfg.MandatoryPitStop, nameof(cfg.MandatoryPitStop), unverified);
        TrySet(vm550, Ams2Constants.Vm550Slot.RollingStart, (int?)cfg.RollingStart, nameof(cfg.RollingStart), unverified);
        TrySet(vm550, Ams2Constants.Vm550Slot.Refuelling, (int?)cfg.RefuellingAllowed, nameof(cfg.RefuellingAllowed), unverified);
        TrySet(vm550, Ams2Constants.Vm550Slot.TimeProgression, (int?)cfg.TimeProgression, nameof(cfg.TimeProgression), unverified);
        TrySet(vm550, Ams2Constants.Vm550Slot.DateType, (int?)cfg.DateType, nameof(cfg.DateType), unverified);
        TrySet(vm550, Ams2Constants.Vm550Slot.MinTyres, (int?)cfg.PitMinTyres, nameof(cfg.PitMinTyres), unverified);
        TrySet(vm550, Ams2Constants.Vm550Slot.MinFuel, (int?)cfg.PitMinFuel, nameof(cfg.PitMinFuel), unverified);

        if (cfg.StartHour is int hour)
        {
            if (hour is < 0 or > 23) unverified.Add(($"{nameof(cfg.StartHour)} (out of range 0-23)", hour, null));
            else TrySet(vm550, Ams2Constants.Vm550Slot.Hour, hour, nameof(cfg.StartHour), unverified);
        }

        if (cfg.RaceDate is DateTime raceDate)
        {
            // Only meaningful when DateType is Custom - AMS2 only reads the day/month/year slots
            // for the DateType=Custom case. Writing them with DateType left at Default/Current
            // wouldn't do anything wrong, but it also wouldn't do anything AMS2 pays attention to
            // - so it's reported as unverified instead of silently attempted, the same way an
            // out-of-range Skill/AiWetWeatherSkill value is reported above rather than sent.
            if (cfg.DateType != DateType.Custom)
            {
                unverified.Add((
                    $"{nameof(cfg.RaceDate)} (ignored - DateType is {cfg.DateType?.ToString() ?? "null"}, not Custom)",
                    0, null));
            }
            else
            {
                // Year and month first, day last: AMS2's own date control clamps Day against
                // whatever Month/Year are already set at the moment Day is written. Writing Day
                // first validates it against the OLD month/year (whatever AMS2 still had from
                // before), which silently clamps it whenever the old month has fewer days than the
                // target day needs - and Month/Year changing afterward doesn't retroactively fix
                // it. Setting Month/Year first means Day is finally validated against the correct
                // target date.
                TrySet(vm550, Ams2Constants.Vm550Slot.Year, raceDate.Year, $"{nameof(cfg.RaceDate)}(year)", unverified);
                TrySet(vm550, Ams2Constants.Vm550Slot.Month, raceDate.Month, $"{nameof(cfg.RaceDate)}(month)", unverified);
                TrySet(vm550, Ams2Constants.Vm550Slot.Day, raceDate.Day, $"{nameof(cfg.RaceDate)}(day)", unverified);
                TrySet(vm550, Ams2Constants.Vm550Slot.DateType, (int?)cfg.DateType, nameof(cfg.DateType), unverified);

                // Also raw-write VM498's own packed date field alongside the VM550 slots above
                // (see Vm498PackedDate's doc comment) - writing VM498's field ALONE was live-
                // tested and confirmed NOT sufficient by itself to change the race's actual date;
                // this combined write (both VM550 AND VM498 in agreement) IS confirmed live to
                // work for the actual race data (AMS2's own Custom Race menu display can still lag
                // until the submenu is re-visited - cosmetic only).
                TrySetVm498PackedDate(vm498, raceDate, unverified);

                // CONFIRMED: AMS2's own handler for activating the in-game "Race Settings" submenu
                // (i.e. "CustomEventRaceSettingsDialog") is the ONLY thing that normally propagates
                // Race1's date (what vm550 above already is)
                // into Race2/Practice1/Qualifying1's own Day/Month/Year slots, via the exact same
                // generic setter TrySet/SlotWriter uses - and it only runs when the player manually
                // opens that submenu. Without this, Race2/Practice1/Qualifying1 keep whatever date
                // they already had (real-world "today" by default) even though Race1 - and the
                // Race Settings display, which reads Race1 - are correct. Mirrors that handler
                // exactly: Year/Month/Day only, no DateType, no Hour (each target keeps its own -
                // the native handler re-applies each target's own existing Hour unchanged, it never
                // takes it from Race1).
                PropagateDateToOtherSession(race2Vm, raceDate, "Race2", unverified);
                PropagateDateToOtherSession(practice1Vm, raceDate, "Practice1", unverified);
                PropagateDateToOtherSession(qualifying1Vm, raceDate, "Qualifying1", unverified);
            }
        }

        // Slot 7 is a binary time-vs-laps switch, not a format enum - the two write sites are
        // mutually exclusive.
        if (cfg.DurationType is DurationType durationType && cfg.DurationValue is int durationValue)
        {
            if (durationType == DurationType.LapBased)
            {
                TrySet(vm550, Ams2Constants.Vm550Slot.DurationTypeFlag, 1, nameof(cfg.DurationType), unverified);
                TrySet(vm550, Ams2Constants.Vm550Slot.LapCount, durationValue, $"{nameof(cfg.DurationValue)}(laps)", unverified);
            }
            else
            {
                TrySet(vm550, Ams2Constants.Vm550Slot.DurationTypeFlag, 0, nameof(cfg.DurationType), unverified);
                TrySet(vm550, Ams2Constants.Vm550Slot.DurationMinutes, durationValue, $"{nameof(cfg.DurationValue)}(minutes)", unverified);
            }
        }

        if (cfg.Weather != null) ApplyWeather(vm550, cfg.Weather, "Race", unverified);
    }

    /// <summary>
    /// Applies duration/start-hour/weather to a Practice1/Qualifying1 VM pointer, reusing the
    /// exact same Vm550Slot numbers ApplySessionRules uses against the main VM550, on the theory
    /// that these are structurally-identical VM550 instances. See
    /// PracticeQualifySessionConfig/SessionVmResolver's doc comments for what's confirmed vs. not.
    /// </summary>
    private void ApplyPracticeOrQualifying(long sessionVm, PracticeQualifySessionConfig cfg, string label, List<(string, int, int?)> unverified)
    {
        if (cfg.Enabled is OnOff enabled)
        {
            // Writes the confirmed 3-slot pattern exactly (see Vm550Slot.SessionEnabled's doc
            // comment) rather than writing slot 3 alone - the paired slots' independent meaning
            // isn't decoded, but they're never written independently of SessionEnabled, so
            // neither does this.
            var isOn = enabled == OnOff.On;
            TrySet(sessionVm, Ams2Constants.Vm550Slot.SessionEnabled, isOn ? 1 : 0, $"{label}.{nameof(cfg.Enabled)}", unverified);
            TrySet(sessionVm, Ams2Constants.Vm550Slot.SessionEnabledPaired1, isOn ? 1 : -1, $"{label}.{nameof(cfg.Enabled)}(paired1)", unverified);
            TrySet(sessionVm, Ams2Constants.Vm550Slot.SessionEnabledPaired2, isOn ? -1 : 1, $"{label}.{nameof(cfg.Enabled)}(paired2)", unverified);
        }

        // Always TimeBased(0) - Practice/Qualifying have no lap-based option in AMS2, unlike the
        // race session, so DurationTypeFlag is written automatically rather than exposed.
        if (cfg.DurationValue is int durationValue)
        {
            TrySet(sessionVm, Ams2Constants.Vm550Slot.DurationTypeFlag, 0, $"{label}.{nameof(cfg.DurationValue)}(type)", unverified);
            TrySet(sessionVm, Ams2Constants.Vm550Slot.DurationMinutes, durationValue, $"{label}.{nameof(cfg.DurationValue)}(minutes)", unverified);
        }

        if (cfg.StartHour is int hour)
        {
            if (hour is < 0 or > 23) unverified.Add(($"{label}.{nameof(cfg.StartHour)} (out of range 0-23)", hour, null));
            else TrySet(sessionVm, Ams2Constants.Vm550Slot.Hour, hour, $"{label}.{nameof(cfg.StartHour)}", unverified);
        }

        if (cfg.Weather != null) ApplyWeather(sessionVm, cfg.Weather, label, unverified);
    }

    /// <summary>
    /// Shared by ApplySessionRules (against the main VM550) and ApplyPracticeOrQualifying
    /// (against a resolved session VM) - see SessionWeatherConfig's doc comment for the slot
    /// layout and what's confirmed vs. not. Writes exactly the slots given (1-4) and the matching
    /// count; does NOT pre-fill unset slots itself - AMS2 repeats the last given slot on its own
    /// when count &lt; 4 (see WeatherSlotCount's doc comment), so there's nothing for this method to
    /// fill in.
    /// </summary>
    private void ApplyWeather(long vmBase, SessionWeatherConfig cfg, string label, List<(string, int, int?)> unverified)
    {
        if (cfg.HistoricalWeather is bool historical && historical)
        {
            TrySet(vmBase, Ams2Constants.Vm550Slot.WeatherType, 2, $"{label}.Weather(Historical)", unverified);
            TrySet(vmBase, Ams2Constants.Vm550Slot.WeatherSlotCount, 0, $"{label}.Weather(count)", unverified);
            return;
        }

        if (cfg.Slots is not { Count: > 0 } slots) return;
        if (slots.Count > 4)
        {
            unverified.Add(($"{label}.Weather (too many slots: {slots.Count}, max 4)", slots.Count, null));
            return;
        }

        TrySet(vmBase, Ams2Constants.Vm550Slot.WeatherType, 0, $"{label}.Weather(Historical)", unverified);
        TrySet(vmBase, Ams2Constants.Vm550Slot.WeatherSlotCount, slots.Count, $"{label}.Weather(count)", unverified);

        Span<int> slotOffsets =
        [
            Ams2Constants.Vm550Slot.WeatherSlot1, Ams2Constants.Vm550Slot.WeatherSlot2,
            Ams2Constants.Vm550Slot.WeatherSlot3, Ams2Constants.Vm550Slot.WeatherSlot4,
        ];
        for (var i = 0; i < slots.Count; i++)
            TrySet(vmBase, slotOffsets[i], (int)slots[i], $"{label}.Weather(slot{i + 1})", unverified);
    }

    private void TrySet(long vmBase, int slot, int? value, string fieldName, List<(string, int, int?)> unverified)
    {
        if (value is not int v) return; // null = don't force
        if (!_slotWriter!.TrySetSlot(vmBase, slot, v, out var actual))
            unverified.Add((fieldName, v, actual));
    }

    /// <summary>
    /// Writes RaceDate's year/month/day into another session's VM550-shaped pointer (Race2/
    /// Practice1/Qualifying1) - see SessionVmResolver.ResolveRace2's doc comment for why this is
    /// needed at all. `sessionVm` being null means the resolve itself failed (reported here, same
    /// convention as everything else in this file), not that the write was attempted and rejected.
    /// </summary>
    private void PropagateDateToOtherSession(long? sessionVm, DateTime raceDate, string label, List<(string, int, int?)> unverified)
    {
        if (sessionVm is not long vm)
        {
            unverified.Add(($"RaceDate({label} propagation - session VM not resolved)", 0, null));
            return;
        }
        TrySet(vm, Ams2Constants.Vm550Slot.Year, raceDate.Year, $"RaceDate({label} year)", unverified);
        TrySet(vm, Ams2Constants.Vm550Slot.Month, raceDate.Month, $"RaceDate({label} month)", unverified);
        TrySet(vm, Ams2Constants.Vm550Slot.Day, raceDate.Day, $"RaceDate({label} day)", unverified);
    }

    /// <summary>
    /// Raw-writes VM498's own packed date field (see Vm498PackedDate's doc comment) - a plain
    /// struct field, NOT an int-property slot, so this goes through ProcessMemory directly rather
    /// than SlotWriter/TrySet. Preserves the field's own top 7 bits (unknown flags) rather than
    /// zeroing them. Reports into `unverified` the same way TrySet does: silently succeeds, or
    /// records what was wanted vs. what's actually there afterward.
    /// </summary>
    private void TrySetVm498PackedDate(long? vm498, DateTime raceDate, List<(string, int, int?)> unverified)
    {
        const string field = "RaceDate(vm498 packed)";
        if (vm498 is not long v498)
        {
            unverified.Add((field, 0, null)); // vm498 not resolved - couldn't even attempt this
            return;
        }

        var address = v498 + Ams2Constants.Vm498DatePackedOffset;
        if (!_mem!.TryReadInt32(address, out var beforeRaw))
        {
            unverified.Add((field, 0, null));
            return;
        }

        var newRaw = Vm498PackedDate.Encode(raceDate.Year, raceDate.Month, raceDate.Day, unchecked((uint)beforeRaw));
        _mem.TryWriteInt32(address, unchecked((int)newRaw));

        if (!_mem.TryReadInt32(address, out var afterRaw) || afterRaw != unchecked((int)newRaw))
            unverified.Add((field, unchecked((int)newRaw), afterRaw));
    }

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
