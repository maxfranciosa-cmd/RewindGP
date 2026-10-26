# Ams2Interop

Talks directly to a running Automobilista 2 (`AMS2AVX.exe`) process: attaches to it, reads/writes
its Custom Race config, and can launch the game if it isn't running yet. Consumer-agnostic by
design - any application that needs this (AMS2ChEd, or others) references this project directly
rather than each reimplementing it.

**For API reference and day-to-day usage patterns, see `USAGE.md`.** This file covers
architecture, implementation status, and known gaps.

## Status: `ApplyRaceConfigAsync` only

`ApplyRaceConfigAsync` - opponents, track, car, livery, and session rules - applies correctly
in-game. **Live-confirmed 2026-09-27** against the AMS2 build patched on 2026-09-20, through the
real app, by checking the launched race (not just the Custom Race screen): date, car, track,
livery, opponent count and opponent class all correct. Any form of persistent hooking is out of
scope and not implemented - see "Architecture" below for why.

Every AMS2 patch so far has moved the addresses in `Ams2Constants.cs`. They're now found again
automatically by signature where possible - see "Surviving AMS2 patches" below.

Two things learned the hard way:

- **A write that reads back correctly can still be ignored by the game.** AMS2 keeps several
  real, correctly-tagged copies of the same config objects, and only one is bound to the live
  screen. Always confirm a change against the actually-launched race.
- **Write order matters.** `SetCar` recomputes the opponent count for the newly-selected car,
  so opponents are written again after it (see `ApplyRaceConfigAsync`'s doc comment).

## Surviving AMS2 patches

The patch-sensitive values (the setter/SetCar/commit RVAs, the four type tags and the gameplay
settings pointer) are resolved per AMS2 build on `AttachAsync`, by `Signatures/Ams2OffsetResolver`:

1. **No `ams2_signatures.json` yet** - the hand-derived `Ams2Constants.BuiltIn` values are used
   unverified. This is exactly the behaviour from before signatures existed.
2. **Build already in the cache** (`%LocalAppData%\Ams2Interop\offsets_cache.json`, keyed by the
   exe's link timestamp, image size and checksum) - the cached values, after a cheap re-check.
3. **Unknown build** - each signature is scanned for in the code section. A code signature is the
   start of the function with every call target and RIP-relative displacement wildcarded; a data
   signature is an instruction that references the tag/pointer, and the new address is read back
   out of that instruction. A value is only used when its signature matches exactly once; a
   complete result is cached.

Anything not found switches off only the features that need it (`Ams2ResolvedOffsets.Can*`):
`ApplyRaceConfigAsync` reports it in `Errors` and applies everything else, and the app tells the
player the AMS2 version isn't recognised. Nothing is ever called or written through a value that
wasn't found for the running build - except in case 1, as before.

**One-time setup** - generate the signature file on a build where the built-in values work (the
current one), then commit it in this folder; `Ams2Interop.csproj` copies it next to the app:

```
dotnet run --project Ams2Interop.Tool -- generate Ams2Interop/ams2_signatures.json
```

**After an AMS2 update** - run `dotnet run --project Ams2Interop.Tool -- status`. If everything is
found, there's nothing to do. If something isn't (a function was actually rewritten), re-derive
just that value by hand as before, update `Ams2Constants.BuiltIn`, confirm it live, and run
`generate` again on the new build.

`X64LengthDecoder` is a length decoder only - it covers the legacy opcode maps and VEX, and stops
a pattern at anything it doesn't recognise (e.g. EVEX) rather than guessing.

## Architecture

Two pieces:

- **Config-apply** (`Ams2RaceConfigurator`, `Native/*`). Sets opponents/session-rules/car/track/
  livery by resolving AMS2's own `VM498`/`VM550` config objects and the `master` pointer, then
  calling AMS2's own internal setter/`SetCar` functions - not by poking memory. This needs code
  actually executing inside `AMS2AVX.exe`, which `RemoteExecutor` provides via two tiny
  hand-written x64 stubs (one for the 2-argument generic setter, one for `SetCar`'s
  4-register-plus-stack-argument shape) run once per call via `CreateRemoteThread`. Each call is
  self-contained (allocate a small parameter block, run the stub, wait, free) - no persistent
  hook, no injected DLL.
- **Launch** (`Ams2Launcher`). A convenience for getting AMS2 running in the first place - finds
  the Steam install and launches it through Steam's own protocol handler (see `Ams2Launcher.cs`'s
  `Launch` doc comment for why a direct exe launch doesn't work). Entirely independent of
  config-apply; use it or not.

Getting a race onto the Custom Race screen in the first place is entirely the caller's
responsibility - callers get there themselves (manually, or via whatever UI the consuming
application already drives) before calling `ApplyRaceConfigAsync`.

## What's implemented

- Attach to a running `AMS2AVX.exe`, resolve its module base (`Ams2RaceConfigurator.AttachAsync`).
- Resolve `VM498`/`VM550` via a cached-pointer-then-full-scan strategy (`Native/VmResolver.cs`).
- Write any VM498/VM550 "int property" slot via the real setter-call mechanism, with read-back
  verification (`Native/SlotWriter.cs`).
- `OpponentsConfig` / `SessionRulesConfig` typed properties covering everything specified.
  **Confirmed live**: opponent count and class, laps, rolling start, refuelling, mandatory pit
  stops, and start hour all apply correctly.
- Car/track/livery selection, wired into `ApplyRaceConfigAsync`, by calling AMS2's `SetCar`
  directly with a `VmResolver`-resolved master pointer as its context argument, then reading the
  selection back from `master`'s own slots (`Ams2Constants.MasterSlot`). **Confirmed live.**
- A write-twice-then-verify pattern in `ApplyRaceConfigAsync` with a 2-second gap, since AMS2's
  Custom Race screen can re-initialize state shortly after opening and silently undo an early
  write. `SetCar` runs after that gap, and opponents are written once more after `SetCar`.
- `Ams2Launcher`: detect whether AMS2 is running and launch the game
  through Steam so the resulting process can actually be attached to afterward.
- **RaceDate now applies reliably** — writing VM550's day/month/year slots alone left the field
  visibly stale (updated internally, didn't reach the actual race). Confirmed fix: also raw-write
  VM498's own packed date field (`Native/Vm498PackedDate.cs`) in the same call. **Confirmed live**:
  the actual race data lands correctly with both writes together; AMS2's own Custom Race menu
  display can still lag until the submenu is re-visited, which is cosmetic only.
- **EXPERIMENTAL, not individually live-verified**: Practice/Qualifying session on/off, duration,
  start hour, and per-session weather (`practice`/`qualifying` parameters,
  `PracticeQualifySessionConfig`, `SessionWeatherConfig`), plus RaceDate propagation to
  Race2/Practice1/Qualifying1. The resolution runs cleanly on every real race launch, but these
  specific fields haven't been checked in-game. They aren't VM498/VM550 slots at all (toggling
  them in AMS2's UI produces zero VM498/VM550 diffs) — they're separate, structurally-identical
  VM550 instances reached through VM498's own session-wrapper array (see
  `Native/SessionVmResolver.cs` and `Ams2Constants.SessionIndex`). **No remote calls are
  involved** — earlier remote-call-based approaches crashed `AMS2AVX.exe` after a game patch (see
  git history); resolution is plain memory reads, validated by requiring the candidate
  container's own Race1 entry to match the independently-resolved Race1 VM550.

- **Player identity** (`TryApplyPlayerIdentity`/`TryRestorePlayerIdentity`,
  `Native/PlayerIdentityWriter.cs`): forename, surname, three-letter handle and nationality in the
  player's profile, the "Edit info" screen - in the same settings block as the wear multipliers
  below. Names longer than the stored ones are written as new strings in AMS2's own layout,
  allocated in the game. **Confirmed live** with the raw writes (2026-10-26): the change shows in
  Edit info on the next screen and in the first session loaded; the wrapper itself is not yet
  live-tested.
- **NOT YET LIVE-TESTED**: tyre-wear and fuel-usage multipliers
  (`TryApplyWearMultipliers`/`TryRestoreWearSettings`, `Native/GameplaySettings.cs`). These are
  AMS2's global gameplay settings, not Custom Race config: plain int fields in a settings block
  reached through one module-global pointer, written directly. The caller is responsible for
  restoring the player's own values afterwards. Nothing is written unless both fields currently
  read as a valid index, so a moved layout after a game update fails safe.

## What's a known simplification

- **`SetCar`'s `flag` argument** — always sent as `0`. Its exact meaning is unconfirmed; `0` is
  a conservative default, not a confirmed-correct value.
- **`SetCar` rejections carry no reason** — `SetCar` silently no-ops if AMS2's own validation
  rejects the car/track/livery combination. The `master`-slot read-back reports that as an
  unverified field, but not *why* (unknown hash, DLC, bad livery index). The validation gate that
  would say why (`Ams2Constants.SetCarValidationGateRva`) hasn't been re-derived for the current
  build, so it isn't called.
- **`CommitRva` (the "commit" call) is unrelated to RaceDate/session fields** — see its doc
  comment in `Ams2Constants.cs`. It's AI-opponent vehicle-class-count bookkeeping, and it
  overwrites the opponent count, which is why opponents are written again after it.
- **Practice/Qualifying `Enabled`/weather are confirmed by analysis, not live-tested by this
  library** — see the EXPERIMENTAL bullet above. `Enabled` replicates a 3-slot write pattern
  (`Vm550Slot.SessionEnabled`/`SessionEnabledPaired1`/`SessionEnabledPaired2`). Weather has one
  known open question: whether writing slot values alone is sufficient, or whether — mirroring
  `RaceDate` needing `DateType=Custom` — there's a separate RealHistoric-vs-Custom mode slot that
  also needs setting first. No such slot has been identified.

## Usage

See `USAGE.md` for the full API reference, a complete quick-start example, and a
troubleshooting table.

## Before relying on this in production

1. Config-apply (opponents/track/car/livery/most session rules) is live-confirmed against a real
   AMS2 install - see the Status section above. Re-confirm after every AMS2 update: every patch
   so far has moved these addresses.
2. Getting to the Custom Race screen is not automated - budget for a manual step, or driving it
   through your own application's UI, before calling into this library.
3. This performs a full-process memory scan (`Native/MemoryScanner.cs`) whenever there's no
   cached pointer - expect a multi-second stall the first time `VmResolver` runs after attaching.
