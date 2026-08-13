# Usage guide

For architecture, what's implemented vs. simplified vs. a hard gap, and the evidence trail
behind every design decision, see `README.md` first. This document is the API reference and
day-to-day usage patterns.

## Prerequisites

- Windows, x64. AMS2AVX.exe must already be running (see `Ams2Launcher` below if you need to
  launch it yourself first).
- The calling process needs enough privilege to `OpenProcess`/`ReadProcessMemory`/
  `WriteProcessMemory`/`CreateRemoteThread` against AMS2AVX.exe — normally fine if both processes
  run at the same integrity level; if AMS2 was launched elevated (e.g. via some anti-cheat/DRM
  wrapper) and your process wasn't, `AttachAsync` will fail.
- The player must already be on the Custom Race screen before calling `ApplyRaceConfigAsync` —
  getting there is entirely the caller's responsibility (see README.md).

## Lifecycle overview

```
construct  ─▶  AttachAsync  ─▶  (player gets to Custom Race, however you drive that)
                                                                        │
                                                                        ▼
                                                          ApplyRaceConfigAsync ─▶ inspect result
                                                                        │
                                                                        ▼
                                                                    Dispose / Detach
```

## Quick start

```csharp
using Ams2Interop;

// Optional: make sure AMS2 is actually running first.
if (!Ams2Launcher.IsRunning())
{
    Ams2Launcher.Launch();
    await Ams2Launcher.WaitForProcessAsync(TimeSpan.FromMinutes(3));
}

// Build these from your PSV/CSV catalogs (ams2_vehicle_catalog.csv, circuits_ref.psv) - key is
// whatever display name/slug you want to look cars/tracks up by, value is the real veh_hash/track_hash.
IReadOnlyDictionary<string, int> carHashes = LoadCarHashes();
IReadOnlyDictionary<string, int> trackHashes = LoadTrackHashes();

using var configurator = new Ams2RaceConfigurator(carHashes, trackHashes);

if (!await configurator.AttachAsync())
    throw new InvalidOperationException("AMS2AVX.exe not found - is the game running?");

// Caller's responsibility: get the player onto the Custom Race screen before this point.

var result = await configurator.ApplyRaceConfigAsync(
    livery: 54,
    car: "arc_camaro",
    track: "interlagos",
    opponents: new OpponentsConfig
    {
        NumOpponentsType = NumOpponentsType.Custom,
        OpponentCount = 20,
        Skill = 95,
        AiMistakeFrequency = AiMistakeFrequency.Standard,
    },
    sessionRules: new SessionRulesConfig
    {
        DurationType = DurationType.LapBased,
        DurationValue = 25,
        StartHour = 14,
        RollingStart = StartType.Rolling,
    },
    // EXPERIMENTAL - see README.md's status notes before relying on these.
    practice: new PracticeQualifySessionConfig { Enabled = OnOff.On, DurationValue = 20, StartHour = 10 },
    qualifying: new PracticeQualifySessionConfig { Enabled = OnOff.On, DurationValue = 15, StartHour = 12 });

// See "Interpreting RaceConfigResult" below - result.Success covers everything this library can
// verify, but SetCar (car/track/livery) has no read-back check, so it can be true even if the
// car/track selection was silently rejected by AMS2.
foreach (var (field, wanted, got) in result.UnverifiedFields)
    Console.WriteLine($"'{field}' wanted {wanted}, got {got?.ToString() ?? "n/a"}");
```

## `Ams2Launcher`

Optional convenience for getting AMS2 running before you attach - entirely independent of
`Ams2RaceConfigurator`, use it or not.

- `IsRunning() : bool` — whether `AMS2AVX.exe` currently has a running process.
- `Launch()` — launches AMS2 through Steam's `steam://run/<appid>` protocol handler
- `WaitForProcessAsync(TimeSpan timeout) : Task<bool>` — polls `IsRunning()` until it's true or
  the timeout elapses.

## `Ams2RaceConfigurator`

### Constructor

```csharp
Ams2RaceConfigurator(IReadOnlyDictionary<string, int> carHashes, IReadOnlyDictionary<string, int> trackHashes)
```

Cheap, does no I/O. `carHashes`/`trackHashes` map whatever key you want to look vehicles/tracks
up by (display name, slug, internal ID — your choice) to the real `veh_hash`/`track_hash` AMS2
uses internally. Source these from `ams2_vehicle_catalog.csv`/`ams2_vehicle_prices.psv` and
`circuits_ref.psv`. An unrecognized key surfaces as an `Errors` entry from `ApplyRaceConfigAsync`
(see below), and that car/track is then skipped for that call — `livery`/`car`/`track` are only
applied together, as a single `SetCar` call, once both hashes resolve.

The optional `log` parameter (`Action<string>?`) is a diagnostic sink covering both
`AttachAsync` (e.g. why the module base couldn't be resolved) and, once attached, per-candidate
`VM498`/`VM550` scan results during `ApplyRaceConfigAsync`. Pass a logger (even just
`msg => Console.WriteLine(msg)`) any time you're debugging unexpected behavior rather than
trying to infer what happened from a pass/fail result alone.

### `AttachAsync(CancellationToken ct = default) : Task<bool>`

Finds a running `AMS2AVX.exe`, opens a process handle, and resolves its module base (needed for
every subsequent slot write). Returns `false` (not an exception) if the process isn't found or
the module base couldn't be resolved. Safe to call again later — re-attaching first detaches
any existing session. Does **not** resolve `VM498`/`VM550` — that's lazy, because it depends on
the player having opened Custom Race, which may not have happened yet.

Throws `InvalidOperationException` if `OpenProcess` itself fails (e.g. a privilege mismatch) —
this is distinct from "process not found," which just returns `false`.

### `IsAttached : bool`

True after a successful `AttachAsync` until `Detach()`/`Dispose()`.

### `ApplyRaceConfigAsync(...) : Task<RaceConfigResult>`

```csharp
Task<RaceConfigResult> ApplyRaceConfigAsync(
    int livery,
    string car,
    string track,
    OpponentsConfig? opponents = null,
    SessionRulesConfig? sessionRules = null,
    PracticeQualifySessionConfig? practice = null,
    PracticeQualifySessionConfig? qualifying = null,
    CancellationToken ct = default)
```

`practice`/`qualifying` are EXPERIMENTAL and not live-confirmed by this library — see
`PracticeQualifySessionConfig` below and README.md's status notes before relying on them.
Resolving either one costs real remote-call round-trips (not just memory reads like everything
else this library does), so only pass the ones you're actually testing — leave the other `null`
to skip that cost.

Applies `opponents`/`sessionRules` to whatever `VM498`/`VM550` currently resolve to, and
`car`/`track`/`livery` via a single `SetCar` call, all against the currently-open Custom Race
screen. This is the only function in this library confirmed reliable enough to depend on — see
README.md's Status section.

Internally: resolves `VM498`/`VM550`/the master pointer (each of which may trigger a several-
second full-process memory scan the first time — see Troubleshooting); writes every non-null
`opponents`/`sessionRules` field; if both `car` and `track` resolved and the master pointer is
available, issues one `SetCar` call (not repeated — car/track/livery is a direct call, not a
staged value like the slot writes below); waits ~100ms; writes every non-null
`opponents`/`sessionRules` field again; reports the **second** write's verification result (the
first write's outcome is discarded — see "Interpreting RaceConfigResult"). The two-write-then-
verify pattern exists because AMS2's Custom Race screen can re-initialize state shortly after
opening and silently undo an early write.

Throws `InvalidOperationException` if not attached. Does not throw for resolution failures
(unresolvable `VM498`/`VM550`/master, unknown car/track, a failed `SetCar` call) — those surface
in `RaceConfigResult.Errors` instead. If `VM498`/`VM550`/master aren't resolved yet (e.g. the
player just opened Custom Race and no prior call has scanned for them), this call itself performs
the scan — there's no separate "wait until ready" step; just call it once the player is on the
screen.

### `Detach()` / `Dispose()`

Frees the process handle and any allocated remote memory (including the shellcode stubs). Safe
to call when not attached. `Dispose()` just calls `Detach()`.

## Config models

### `OpponentsConfig`

Every property is nullable; **`null` means "don't force this field" — the player's own setting
is left alone**.

| Property | Type | Valid range | Notes |
|---|---|---|---|
| `NumOpponentsType` | `NumOpponentsType?` | `MaxAvailable`, `Custom`, `ManualGrid` | |
| `OpponentsType` | `OpponentsTypeKind?` | `Identical`, `SameClass` | |
| `OpponentCount` | `int?` | unbounded in this library | no numeric ceiling is enforced here — you're relying on AMS2 itself to reject an absurd value, not this library |
| `Skill` | `int?` | **70–120** | out-of-range values are rejected (reported via `UnverifiedFields`, not written) rather than clamped |
| `AiWetWeatherSkill` | `int?` | **0–200** | same rejection behavior |
| `AiMistakeFrequency` | `AiMistakeFrequency?` | `Off`, `Half`, `Standard`, `Double`, `Triple`, `Quadruple`, `Quintuple` | |

### `SessionRulesConfig`

Same nullable = don't-force convention.

| Property | Type | Valid range | Notes |
|---|---|---|---|
| `PrivateQualiSession` | `OnOff?` | `Off`, `On` | |
| `MandatoryPitStop` | `OnOff?` | `Off`, `On` | |
| `RollingStart` | `StartType?` | `Grid`, `Rolling` | |
| `RefuellingAllowed` | `OnOff?` | `Off`, `On` | |
| `TimeProgression` | `TimeProgression?` | `RealTime`, `X2`, `X5`, `X10` | |
| `DateType` | `DateType?` | `Default`, `Current`, `Custom` | |
| `PitMinTyres` | `PitTyreCount?` | `Zero`, `Two`, `Four` | confirmed exact range from series data — no other values are valid |
| `PitMinFuel` | `PitFuelCount?` | `Zero`, `Two`, `Four` | same |
| `StartHour` | `int?` | **0–23** | out-of-range rejected the same way as `Skill` |
| `RaceDate` | `DateTime?` | — | writes day/month/year, but **only takes effect when `DateType` is `Custom`** (matching AMS2's own field grouping); if `DateType` isn't `Custom`, it's ignored and reported via `UnverifiedFields` instead of silently written |
| `DurationType` | `DurationType?` | `LapBased`, `TimeBased` | must be set together with `DurationValue` — either alone is a no-op |
| `DurationValue` | `int?` | — | laps if `DurationType` is `LapBased`, minutes if `TimeBased` |
| `Weather` | `SessionWeatherConfig?` | — | race session's weather — see below |

### `PracticeQualifySessionConfig` — EXPERIMENTAL, not live-confirmed

Used for both the `practice` and `qualifying` parameters — same shape, same nullable = don't-force
convention. Applied against that session's own separately-resolved VM pointer (NOT the main
VM550), reusing the same `Vm550Slot` numbers on the theory that Practice1/Qualifying1 are
structurally-identical VM550 instances. See `Native/SessionVmResolver.cs`'s doc comment for what's
confirmed vs. not before depending on it.

| Property | Type | Notes |
|---|---|---|
| `Enabled` | `OnOff?` | on/off — writes three slots as a group (`Vm550Slot.SessionEnabled`/`SessionEnabledPaired1`/`SessionEnabledPaired2`), not a single flag |
| `DurationValue` | `int?` | minutes — Practice/Qualifying are always time-based in AMS2 (no lap-based option), so unlike `SessionRulesConfig` there's no paired `DurationType`; `DurationTypeFlag` (slot 7) is written as `TimeBased` automatically |
| `StartHour` | `int?` | 0–23 — UNCONFIRMED whether Practice1/Qualifying1 have their own meaningful Hour slot independent of the race session's |
| `Weather` | `SessionWeatherConfig?` | this session's weather — see below |

### `SessionWeatherConfig` — CONFIRMED slot layout, UNCONFIRMED whether it's sufficient alone

Used by `SessionRulesConfig.Weather` (race) and `PracticeQualifySessionConfig.Weather` (practice/
qualifying) — same shape either way, applied against whichever VM that config targets.

| Property | Type | Notes |
|---|---|---|
| `Slots` | `IReadOnlyList<WeatherType>?` | 1–4 slots, in order. AMS2 itself repeats the last slot when fewer than 4 are given — this library doesn't pre-fill the rest. More than 4 is rejected (`UnverifiedFields`), not truncated. |

`WeatherType` values are AMS2's own catalogue: `Clear`, `LightClouds`, `MediumClouds`,
`HeavyClouds`, `Overcast`, `LightRain`, `Rain`, `Storm`, `Thunderstorm`, `Hazy`, `FogWithRain`,
`HeavyFog`, `HeavyFogWithRain`, `Fog`, `Random`.

**Open question**: whether writing slot values alone actually changes the weather AMS2 uses, or
whether — mirroring `RaceDate` needing `DateType=Custom` first — there's a separate RealHistoric-
vs-Custom mode slot that also needs setting. No such slot was found by static analysis.

## Interpreting `RaceConfigResult`

```csharp
public sealed class RaceConfigResult
{
    public bool Success => UnverifiedFields.Count == 0 && Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; init; }
    public IReadOnlyList<(string Field, int Wanted, int? Got)> UnverifiedFields { get; init; }
}
```

`Success` is a real signal — it's worth understanding exactly what it does and doesn't cover,
since `SetCar` specifically has no read-back check:

- `UnverifiedFields` entries mean one of: the value was out of range and rejected before writing
  (`Got` is `null`), the field is being ignored given current config (`RaceDate` when `DateType`
  isn't `Custom`, `Got` is `null`), or a write was attempted and the read-back didn't match what
  was requested (`Got` is the field's actual current value) — that last case is the same
  situation a live read-back check would also report, and can happen legitimately (e.g. AMS2
  clamped a value you sent, or the write raced against the game's own re-initialization).
- `Errors` entries are conditions checked before/around writing: unresolvable `VM498`/`VM550`/
  master pointer (most likely cause: Custom Race isn't actually open), an unrecognized car/track
  key, or the `SetCar` call itself failing to execute.

**The gap this doesn't cover**: a `SetCar` call that *executes* but that AMS2 silently rejects or
ignores produces neither an `Errors` entry nor an `UnverifiedFields` entry — `Success` can be
`true` while the car/track/livery didn't actually change, because there's no read-back check for
that call. Treat `Success` as "every write I know how to verify landed, and `SetCar` at least
ran" — not as "the race is definitely configured exactly as requested." If you need certainty on
car/track/livery specifically, verify it visually or through whatever telemetry/UI signal your
own application already has for the currently-selected car.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `AttachAsync` returns `false` | AMS2AVX.exe isn't running, or a privilege mismatch between the two processes (see Prerequisites) |
| `AttachAsync` throws `InvalidOperationException` | `OpenProcess` itself failed — almost always a privilege issue |
| `ApplyRaceConfigAsync`'s `Errors` says VM498/VM550/master couldn't be resolved | Player isn't actually on the Custom Race screen — these only exist once it's been opened at least once this session |
| First call after attaching is slow (several seconds) | Expected — the first `VM498`/`VM550` resolution triggers a full process memory scan; subsequent calls use the cached pointer and are fast unless it's invalidated |
| `UnverifiedFields` has entries even though everything looks right | Check `Got` — if it's the value you expect, the *first* of the two writes may have landed and you're just seeing stale state from a race with the game's own init; if `Got` is something else entirely, AMS2 may be clamping/rejecting the value itself |
| `result.Success` is `true` but the car/track/livery didn't actually change in-game | Expected possibility — `SetCar` has no read-back verification (see above); a `flag` value other than the default `0` is one thing worth trying if you fork this |
