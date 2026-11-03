using Ams2Interop.Native;

namespace Ams2Interop;

/// <summary>The Custom Race objects one ApplyRaceConfigAsync call works on (null = not resolved / not needed).</summary>
public sealed class CustomRaceTargets
{
    public long? Vm498 { get; init; }

    /// <summary>The race session's VM550 (Race1).</summary>
    public long? Vm550 { get; init; }

    public long? Master { get; init; }
    public long? Practice1Vm { get; init; }
    public long? Qualifying1Vm { get; init; }
    public long? Race2Vm { get; init; }
}

/// <summary>
/// What CustomRaceWriter.CheckLayout found: which objects failed Ams2LayoutCheck and must not be
/// written to. An object that wasn't resolved isn't checked, and isn't "failed" either - there's
/// nothing to write to it anyway.
/// </summary>
public sealed class CustomRaceLayout
{
    private readonly HashSet<string> _failed = new();
    private readonly List<(string Field, int Wanted, int? Got)> _failures = new();

    /// <summary>The race VM550 failed: nothing at all is written (see Ams2RaceConfigurator.ApplyRaceConfigAsync).</summary>
    public bool RaceVmFailed => Failed(CustomRaceWriter.RaceVmLabel);
    public bool Vm498Failed => Failed(CustomRaceWriter.Vm498Label);
    public bool MasterFailed => Failed(CustomRaceWriter.MasterLabel);

    public bool AnyFailed => _failed.Count > 0;

    /// <summary>One entry per failing slot, shaped like RaceConfigResult.UnverifiedFields (Wanted is 0; Got is the value read, if any).</summary>
    public IReadOnlyList<(string Field, int Wanted, int? Got)> Failures => _failures;

    /// <summary>The race VM550's Day/Month/Year as read by the check, when it passed - for diagnostics only.</summary>
    public (int Day, int Month, int Year)? RaceVmDate { get; internal set; }

    public bool Failed(string label) => _failed.Contains(label);

    internal void Record(string label, Ams2LayoutCheck.Result result)
    {
        if (result.Passed) return;
        _failed.Add(label);
        foreach (var failure in result.Failures)
            _failures.Add(($"LayoutCheck({label}): {failure}", 0, failure.Value));
    }

    internal void RecordProblem(string label, string field, int wanted, int? got)
    {
        _failed.Add(label);
        _failures.Add((field, wanted, got));
    }
}

/// <summary>
/// The slot-writing half of Ams2RaceConfigurator.ApplyRaceConfigAsync: every write to VM498,
/// VM550 and the per-session VMs, the VM498 packed-date raw write, and SetCar's read-back. The
/// remote calls (SetCar, commit) stay in the configurator.
///
/// Every write is gated on Ams2LayoutCheck: CheckLayout runs before the first write, and an object
/// that fails it is left untouched - and if the race VM550 fails, nothing is written at all.
/// Reads, raw writes and setter calls go through IMemoryReader/IMemoryWriter/ISlotSetter so the
/// gating can be tested against fake memory.
/// </summary>
public sealed class CustomRaceWriter
{
    public const string RaceVmLabel = "Race VM550";
    public const string Vm498Label = "VM498";
    public const string MasterLabel = "master";
    public const string Practice1Label = "Practice1";
    public const string Qualifying1Label = "Qualifying1";
    public const string Race2Label = "Race2";
    public const string Vm498PackedDateLabel = "VM498 packed date";

    /// <summary>
    /// What a VM498 packed date field must decode as before it's overwritten: year 1900-2100,
    /// month 1-12, day 1-31. The field is seeded from the local date when VM498 is built, so a
    /// real one always passes; a value from a moved struct almost never does.
    /// </summary>
    public static bool IsPlausiblePackedDate(Vm498PackedDate.Decoded date) =>
        date.Year is >= 1900 and <= 2100 && date.Month is >= 1 and <= 12 && date.Day is >= 1 and <= 31;

    private readonly IMemoryReader _mem;
    private readonly IMemoryWriter _raw;
    private readonly ISlotSetter _setter;
    private readonly long _intPropertyTag;
    private readonly Ams2LayoutCheck _check;
    private readonly Action<string>? _log;

    /// <param name="intPropertyTag">Ams2Constants.IntPropertyTypeTag for the running build - a parameter so tests needn't touch the process-wide offsets.</param>
    public CustomRaceWriter(IMemoryReader mem, IMemoryWriter raw, ISlotSetter setter, long intPropertyTag, Action<string>? log = null)
    {
        _mem = mem;
        _raw = raw;
        _setter = setter;
        _intPropertyTag = intPropertyTag;
        _check = new Ams2LayoutCheck(mem, intPropertyTag);
        _log = log;
    }

    /// <summary>
    /// Runs Ams2LayoutCheck on every resolved object that may be written: the race VM550 first (if
    /// it fails, nothing else is checked - nothing will be written), then VM498 and each session VM.
    /// master is checked separately, right before SetCar (CheckMasterLayout).
    /// </summary>
    public CustomRaceLayout CheckLayout(CustomRaceTargets targets)
    {
        var layout = new CustomRaceLayout();

        if (targets.Vm550 is long vm550)
        {
            var race = Check(RaceVmLabel, vm550, _check.CheckVm550, layout);
            if (!race.Passed) return layout;
            layout.RaceVmDate = (race.Values[nameof(Ams2Constants.Vm550Slot.Day)],
                race.Values[nameof(Ams2Constants.Vm550Slot.Month)], race.Values[nameof(Ams2Constants.Vm550Slot.Year)]);
        }

        if (targets.Vm498 is long vm498) Check(Vm498Label, vm498, _check.CheckVm498, layout);
        if (targets.Practice1Vm is long p1) Check(Practice1Label, p1, _check.CheckVm550, layout);
        if (targets.Qualifying1Vm is long q1) Check(Qualifying1Label, q1, _check.CheckVm550, layout);
        if (targets.Race2Vm is long r2) Check(Race2Label, r2, _check.CheckVm550, layout);
        return layout;
    }

    /// <summary>master's selection slots must read cleanly before SetCar runs - they're its only read-back. False (and recorded on <paramref name="layout"/>) when they don't.</summary>
    public bool CheckMasterLayout(long master, CustomRaceLayout layout) =>
        Check(MasterLabel, master, _check.CheckMaster, layout).Passed;

    private Ams2LayoutCheck.Result Check(string label, long address, Func<long, Ams2LayoutCheck.Result> check, CustomRaceLayout layout)
    {
        var result = check(address);
        layout.Record(label, result);
        if (!result.Passed)
            _log?.Invoke($"LayoutCheck: {label} at 0x{address:X} FAILED - nothing will be written to it: {result.Describe()}");
        return result;
    }

    /// <summary>
    /// One pass of slot writes (ApplyRaceConfigAsync runs two, 2s apart). Skips every object the
    /// layout check failed; writes nothing at all when the race VM550 failed.
    /// </summary>
    public void ApplyOnce(CustomRaceTargets t, CustomRaceLayout layout, OpponentsConfig? opponents, SessionRulesConfig? sessionRules,
        PracticeQualifySessionConfig? practice, PracticeQualifySessionConfig? qualifying,
        List<(string, int, int?)> unverified, List<(string, int, int?)> opponentsUnverified)
    {
        if (layout.RaceVmFailed) return;

        if (opponents != null && t.Vm498 is long v498 && !layout.Vm498Failed) ApplyOpponents(v498, opponents, opponentsUnverified);
        // vm498 is passed through regardless of `opponents` - ApplySessionRules needs it too,
        // for the VM498 packed-date write alongside VM550's slots (see TrySetVm498PackedDate).
        // practiceVm/qualifyVm/race2Vm are passed through regardless of `practice`/`qualifying`
        // too, for RaceDate's Race2/Practice1/Qualifying1 propagation.
        if (sessionRules != null && t.Vm550 is long v550) ApplySessionRules(v550, t, layout, sessionRules, unverified);
        if (practice != null && t.Practice1Vm is long pVm && !layout.Failed(Practice1Label)) ApplyPracticeOrQualifying(pVm, practice, "Practice", unverified);
        if (qualifying != null && t.Qualifying1Vm is long qVm && !layout.Failed(Qualifying1Label)) ApplyPracticeOrQualifying(qVm, qualifying, "Qualifying", unverified);
    }

    /// <summary>
    /// Reads back master's own current-selection slots (see Ams2Constants.MasterSlot's doc
    /// comment) after a SetCar call and reports any mismatch the same way TrySet does for every
    /// other field.
    /// </summary>
    public void CheckSetCarReadBack(long master, int trackHash, int carHash, int livery, List<(string, int, int?)> unverified)
    {
        var trackOk = _mem.TryReadSlot(master, Ams2Constants.MasterSlot.Track, _intPropertyTag, out var actualTrack);
        if (!trackOk || actualTrack != trackHash) unverified.Add(("SetCar(track)", trackHash, trackOk ? actualTrack : null));

        var carOk = _mem.TryReadSlot(master, Ams2Constants.MasterSlot.Car, _intPropertyTag, out var actualCar);
        if (!carOk || actualCar != carHash) unverified.Add(("SetCar(car)", carHash, carOk ? actualCar : null));

        var liveryOk = _mem.TryReadSlot(master, Ams2Constants.MasterSlot.Livery, _intPropertyTag, out var actualLivery);
        if (!liveryOk || actualLivery != livery) unverified.Add(("SetCar(livery)", livery, liveryOk ? actualLivery : null));
    }

    /// <summary>Writes the opponents to VM498. Callers must not call it when the VM498 layout check failed.</summary>
    public void ApplyOpponents(long vm498, OpponentsConfig cfg, List<(string, int, int?)> unverified)
    {
        TrySet(vm498, Ams2Constants.Vm498Slot.NumOpponentsType, (int?)cfg.NumOpponentsType, nameof(cfg.NumOpponentsType), unverified);
        TrySet(vm498, Ams2Constants.Vm498Slot.OpponentsTypeKind, (int?)cfg.OpponentsType, nameof(cfg.OpponentsType), unverified);
        TrySet(vm498, Ams2Constants.Vm498Slot.RivalCount, cfg.OpponentCount, nameof(cfg.OpponentCount), unverified);
        // Mirrors RivalCount - see Vm498Slot.RivalCountPerClass's doc comment.
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

    private void ApplySessionRules(long vm550, CustomRaceTargets t, CustomRaceLayout layout, SessionRulesConfig cfg, List<(string, int, int?)> unverified)
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
                // (see Vm498PackedDate's doc comment) - neither write alone changes the race's
                // actual date; both together do (live-confirmed). AMS2's own Custom Race menu
                // display can still lag until the submenu is re-visited - cosmetic only.
                // Skipped (the layout failure is already reported) when VM498 failed its check.
                if (!layout.Vm498Failed) TrySetVm498PackedDate(t.Vm498, layout, raceDate, unverified);

                // AMS2's own handler for activating the in-game "Race Settings" submenu
                // ("CustomEventRaceSettingsDialog") is the ONLY thing that normally propagates
                // Race1's date (what vm550 above already is)
                // into Race2/Practice1/Qualifying1's own Day/Month/Year slots, via the exact same
                // generic setter TrySet/SlotWriter uses - and it only runs when the player manually
                // opens that submenu. Without this, Race2/Practice1/Qualifying1 keep whatever date
                // they already had (real-world "today" by default) even though Race1 - and the
                // Race Settings display, which reads Race1 - are correct. Mirrors that handler
                // exactly: Year/Month/Day only, no DateType, no Hour (each target keeps its own -
                // the native handler re-applies each target's own existing Hour unchanged, it never
                // takes it from Race1).
                PropagateDateToOtherSession(t.Race2Vm, layout, raceDate, Race2Label, unverified);
                PropagateDateToOtherSession(t.Practice1Vm, layout, raceDate, Practice1Label, unverified);
                PropagateDateToOtherSession(t.Qualifying1Vm, layout, raceDate, Qualifying1Label, unverified);
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
        if (!_setter.TrySetSlot(vmBase, slot, v, out var actual))
            unverified.Add((fieldName, v, actual));
    }

    /// <summary>
    /// Writes RaceDate's year/month/day into another session's VM550-shaped pointer (Race2/
    /// Practice1/Qualifying1) - see SessionVmResolver.ResolveRace2's doc comment for why this is
    /// needed at all. `sessionVm` being null means the resolve itself failed (reported here, same
    /// convention as everything else in this file), not that the write was attempted and rejected.
    /// A session VM that failed its layout check is skipped (its failure is already reported).
    /// </summary>
    private void PropagateDateToOtherSession(long? sessionVm, CustomRaceLayout layout, DateTime raceDate, string label, List<(string, int, int?)> unverified)
    {
        if (sessionVm is not long vm)
        {
            unverified.Add(($"RaceDate({label} propagation - session VM not resolved)", 0, null));
            return;
        }
        if (layout.Failed(label)) return;
        TrySet(vm, Ams2Constants.Vm550Slot.Year, raceDate.Year, $"RaceDate({label} year)", unverified);
        TrySet(vm, Ams2Constants.Vm550Slot.Month, raceDate.Month, $"RaceDate({label} month)", unverified);
        TrySet(vm, Ams2Constants.Vm550Slot.Day, raceDate.Day, $"RaceDate({label} day)", unverified);
    }

    /// <summary>
    /// Raw-writes VM498's own packed date field (see Vm498PackedDate's doc comment) - a plain
    /// struct field, NOT an int-property slot, so this goes through the raw writer directly rather
    /// than the setter. Preserves the field's own top 7 bits (unknown flags) rather than zeroing
    /// them. Reports into `unverified` the same way TrySet does: silently succeeds, or records
    /// what was wanted vs. what's actually there afterward.
    ///
    /// The slot check (Ams2LayoutCheck) can't cover this field - it isn't a slot - and a read-back
    /// only proves the write landed, not that it landed in the date. So the field's CURRENT value
    /// must already decode as a plausible date (IsPlausiblePackedDate); if it doesn't, the struct
    /// has most likely moved, nothing is written, and it's reported as a layout failure.
    /// </summary>
    private void TrySetVm498PackedDate(long? vm498, CustomRaceLayout layout, DateTime raceDate, List<(string, int, int?)> unverified)
    {
        const string field = "RaceDate(vm498 packed)";
        if (vm498 is not long v498)
        {
            unverified.Add((field, 0, null)); // vm498 not resolved - couldn't even attempt this
            return;
        }

        var address = v498 + Ams2Constants.Vm498DatePackedOffset;
        if (!_mem.TryReadInt32(address, out var beforeRaw))
        {
            unverified.Add((field, 0, null));
            return;
        }

        if (layout.Failed(Vm498PackedDateLabel)) return; // already refused (and reported) on an earlier pass

        var before = Vm498PackedDate.Decode(unchecked((uint)beforeRaw));
        if (!IsPlausiblePackedDate(before))
        {
            layout.RecordProblem(Vm498PackedDateLabel,
                $"{field} (unexpected layout: +0x{Ams2Constants.Vm498DatePackedOffset:X} decodes as {before.Year}-{before.Month}-{before.Day}, not a date - not written)",
                0, beforeRaw);
            _log?.Invoke($"LayoutCheck: VM498 packed date at 0x{address:X} = 0x{beforeRaw:X8} decodes as {before.Year}-{before.Month}-{before.Day} - unexpected layout, NOT written");
            return;
        }

        // Diagnostics only: the game syncs the two lazily, so a difference is normal.
        if (layout.RaceVmDate is { } vm550Date && vm550Date != (before.Day, before.Month, before.Year))
            _log?.Invoke($"CustomRaceWriter: VM498 packed date {before.Year}-{before.Month:D2}-{before.Day:D2} differs from the race VM550's {vm550Date.Year}-{vm550Date.Month:D2}-{vm550Date.Day:D2} (normal - AMS2 syncs them lazily)");

        var newRaw = Vm498PackedDate.Encode(raceDate.Year, raceDate.Month, raceDate.Day, unchecked((uint)beforeRaw));
        _raw.TryWriteInt32(address, unchecked((int)newRaw));

        if (!_mem.TryReadInt32(address, out var afterRaw) || afterRaw != unchecked((int)newRaw))
            unverified.Add((field, unchecked((int)newRaw), afterRaw));
    }
}
