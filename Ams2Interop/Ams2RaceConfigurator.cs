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

    public void Detach()
    {
        _exec?.Dispose();
        _mem?.Dispose();
        _mem = null;
        _exec = null;
        _vmResolver = null;
        _slotWriter = null;
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

        void ApplyOnce()
        {
            if (opponents != null && vm498 is long v498) ApplyOpponents(v498, opponents, unverified);
            if (sessionRules != null && vm550 is long v550) ApplySessionRules(v550, sessionRules, unverified);
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

        return new RaceConfigResult { Errors = errors, UnverifiedFields = unverified };
    }

    private void ApplyOpponents(long vm498, OpponentsConfig cfg, List<(string, int, int?)> unverified)
    {
        TrySet(vm498, Ams2Constants.Vm498Slot.NumOpponentsType, (int?)cfg.NumOpponentsType, nameof(cfg.NumOpponentsType), unverified);
        TrySet(vm498, Ams2Constants.Vm498Slot.OpponentsTypeKind, (int?)cfg.OpponentsType, nameof(cfg.OpponentsType), unverified);
        TrySet(vm498, Ams2Constants.Vm498Slot.RivalCount, cfg.OpponentCount, nameof(cfg.OpponentCount), unverified);

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

    private void ApplySessionRules(long vm550, SessionRulesConfig cfg, List<(string, int, int?)> unverified)
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
                TrySet(vm550, Ams2Constants.Vm550Slot.Day, raceDate.Day, $"{nameof(cfg.RaceDate)}(day)", unverified);
                TrySet(vm550, Ams2Constants.Vm550Slot.Month, raceDate.Month, $"{nameof(cfg.RaceDate)}(month)", unverified);
                TrySet(vm550, Ams2Constants.Vm550Slot.Year, raceDate.Year, $"{nameof(cfg.RaceDate)}(year)", unverified);
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
    }

    private void TrySet(long vmBase, int slot, int? value, string fieldName, List<(string, int, int?)> unverified)
    {
        if (value is not int v) return; // null = don't force
        if (!_slotWriter!.TrySetSlot(vmBase, slot, v, out var actual))
            unverified.Add((fieldName, v, actual));
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
