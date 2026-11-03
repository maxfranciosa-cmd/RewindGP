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

**Goal:** after any AMS2 patch, every value the interop writes into the game is either found
again automatically, or its feature switches itself off before writing. Nothing is ever written
to an address or field that can't be confirmed for the running build.

### Patch-sensitive values (RVAs, type tags, the settings pointer)

Resolved per AMS2 build on `AttachAsync`, by `Signatures/Ams2OffsetResolver`:

1. **No `ams2_signatures.json` (or the module can't be read)** - the hand-derived
   `Ams2Constants.BuiltIn` values, but only on a build listed in
   `Ams2Constants.BuiltIn.ConfirmedBuildIds` (`Ams2OffsetResolver.BuiltInFallback`). On any other
   build, or when the build id can't be read, nothing is resolved (`Ams2OffsetSource.BuiltInRejected`):
   every `Can*` is off and the app says the AMS2 version isn't recognised. **While the list is
   empty** (it is until someone records the confirmed build - see the workflow below) the
   built-in values are used on any build, with a warning naming the build in the log.
2. **Build listed in the file's `known_builds`** - the values recorded there for that exact build
   (`Ams2OffsetSource.KnownBuild`). These are values confirmed by hand on that build, so a build
   whose signatures broke can be supported by editing the file, with no rebuild. An entry missing
   a value, or pointing a function outside the code section (or data outside the image), is
   ignored with a log line, and the build is resolved as below.
3. **Build already in the cache** (`%LocalAppData%\Ams2Interop\offsets_cache.json`, keyed by the
   exe's link timestamp, image size and checksum) - the cached values, after a cheap re-check that
   at least one of each function's code signatures still matches where it should.
4. **Unknown build** - every signature is scanned for in the code section. Each value has
   several (format version 2 of the file; version 1 files still load as one per value):
   - a function: its start, plus up to two alternates from later instruction boundaries of the
     same function (stored with their offset from the start), each with every call target and
     RIP-relative displacement wildcarded;
   - a data value (tag, pointer): up to three patterns, each from a different instruction that
     references it; the new address is read back out of the instruction.

   A value is used when at least one of its signatures matches exactly once **and** every one
   that matched agrees. Signatures that disagree make the value missing (both candidates are
   logged). A complete result is cached.

Anything not found switches off only the features that need it (`Ams2ResolvedOffsets.Can*`):
`ApplyRaceConfigAsync` reports it in `Errors` and applies everything else, and the app tells the
player the AMS2 version isn't recognised.

**Updating the file without an app release.** The signature file is data, so the app also
fetches it online (`Ams2ChEd.Business.AMS2/Services/Ams2SignatureUpdater.cs`): each race launch
starts a download (once per app run) of

    https://raw.githubusercontent.com/maxfranciosa-cmd/RewindGP/main/Ams2Interop/ams2_signatures.json

into `%LocalAppData%\RewindGP\ams2_signatures.json`, and waits at most 5 s for it before
attaching. The newer of that and the copy shipped with the app (by `generated_at`; a tie goes to
the download) is used. A download replaces the local copy only if it parses as a signature file
with something in it; offline, a 404 or a bad file just leave things as they were. So pushing a
new `ams2_signatures.json` to `main` updates every player on their next launch. Bump
`generated_at` when editing the file by hand, so it wins over an older copy.

### Layouts (slot numbers and struct offsets)

Slot numbers and field offsets are constants and can't be found by signature, so they're checked
by reading before anything is written:

- **Custom Race objects** (`Native/Ams2LayoutCheck.cs`) - before the first write, the race VM550,
  each session VM (Practice1, Qualifying1, Race2) and VM498 have a conservative set of slots read
  through the same node/tag/`+0x18c` path the writes use; master's selection slots are read
  before `SetCar`. One slot unreadable, wrongly tagged or out of range fails the whole object, and
  nothing is written to it. A failing race VM550 skips the whole race config; a failing master
  skips `SetCar` and commit. `RaceConfigResult.LayoutCheckFailed` is set, and the app shows the
  "game version not recognised" message, the same as for missing offsets.
- **VM498's packed date** (`vm498 + 0x188`, a raw write) - only overwritten if its current value
  decodes as a plausible date.
- **The settings block** (`Native/SettingsBlockLayout.cs`) - the player identity is found by its
  four AMS2 string pointers, at the usual offsets or by a bounded scan for exactly one such run;
  tyre wear and fuel usage are derived from it and must read as valid indices. Wear and identity
  have their own gates (`CanChangeWearSettings`, `CanChangePlayerIdentity`); restores only write
  to the offsets the values were read from.
- **After commit** - VM498's class-count slots are read back as an advisory check (not fatal).

`X64LengthDecoder` is a length decoder only - it covers the legacy opcode maps and VEX, and stops
a pattern at anything it doesn't recognise (e.g. EVEX) rather than guessing.

### Coverage map: everything the interop writes

| # | What is written | How | Address/layout dependency | Protection |
|---|---|---|---|---|
| 1 | **Race session rules** (VM550): duration, laps, start hour, date Y/M/D, date type, time progression, rolling start, pit rules, refuelling, private quali, weather mode/count/slots | `SlotWriter` -> AMS2 setter | setter RVA, `IntPropertyTypeTag`, VM550 tag, `Vm550Slot` numbers, value offset `node + 0x18c` | confirmed-build gate or signatures (several per value, must agree); tag check; **race VM550 layout check before any write - failure skips the whole race config**; read-back |
| 2 | **Practice1 / Qualifying1 / Race2 sessions**: enable trio, duration, start hour, weather, date propagation | `SlotWriter` | as #1, plus the session-wrapper array (`vm498 + 0x18 + i*8`, wrapper `+0x18`) | as #1; Race1-anchored resolver; **per-session layout check - a failing session is skipped** |
| 3 | **Opponents** (VM498): count type, kind, rival count (+ per class), skill, wet skill, mistakes | `SlotWriter` | as #1, plus VM498 tag, `Vm498Offset = 0x250`, `Vm498Slot` numbers | as #1; **VM498 layout check - failure skips opponents and the packed date** |
| 4 | **VM498 packed date** at `vm498 + 0x188` | **raw** write (preserves top 7 bits) | struct offset `0x188` | VM498 layout check; **current value must decode as a plausible date**; read-back |
| 5 | **Car / track / livery** | remote call `SetCar` | `SetCar` RVA; `MasterSlot` numbers for the read-back | signatures; **master layout check before the call**; `CheckSetCarReadBack` |
| 6 | **Opponent class recount** | remote call `Commit` | `Commit` RVA | signatures; master layout check; **advisory read-back of VM498 slots 37/39** (UNCONFIRMED, not fatal) |
| 7 | **Tyre wear / fuel usage** | **raw** writes | settings-block pointer (signature); offsets derived from the identity (`SettingsBlockDelta`) | **identity-anchored layout**, values must read 0-8 at the derived offsets; re-validated before each write; restore only at the apply-time offsets |
| 8 | **Player identity**: country, forename, surname, handle, full name | `VirtualAllocEx` + **raw** pointer writes | settings-block pointer; identity found by its strings (normally `0x3D480-0x3D4A0`); AMS2 string header (`BLOW`) | identity located by four valid strings (constants, else unique scan hit); **stored nationality non-zero and a known country id**; re-validated before each write; restore only at the apply-time offsets; read-back |

Not a patch risk: the car/track hash catalogs (`IAms2HashCatalogProvider`,
`Ams2InteropHashCatalogLoader`) are data files. New AMS2 content is missing from them until it's
added, and that already falls back to the manual-instructions flow.

### Developer workflow

Do this with AMS2 running, on Windows, ideally before the next AMS2 patch:

1. `dotnet run --project Ams2Interop.Tool -- status`. This shows the build id (boxed at the top)
   and whether the built-in values resolve.
2. If the build is the one `BuiltIn` was confirmed on (car, track and date apply correctly in a
   real launch), copy the build id into `Ams2Constants.BuiltIn.ConfirmedBuildIds`.
3. `dotnet run --project Ams2Interop.Tool -- generate Ams2Interop/ams2_signatures.json`. Only
   commit the file if the tool reports every signature as built (it lists each one, and warns
   about values that only got a single signature). It also records the build in `known_builds`,
   keeping the builds already in the file.
4. Rebuild. The `.csproj` copies the file next to the app automatically. Push it to `main` too,
   so installed apps download it.
5. After a future AMS2 patch, run `status` again. If everything is found by scan and the layout
   checks pass in a test launch, nothing needs doing: each player's app finds the values itself.
   If something is missing, no app release is needed:
   1. re-derive only that value;
   2. add the new build to `known_builds` in `Ams2Interop/ams2_signatures.json`, with every
      value (RVAs, as in the existing entries), and bump `generated_at`;
   3. run `generate` on the new build: it takes that entry as correct and rebuilds the
      signatures from it, so the following patches can be found by scan again;
   4. push the file to `main`.

   `BuiltIn` and `ConfirmedBuildIds` only need changing for the no-signature-file fallback.
6. **In-game verifications still outstanding:**
   - slot 28 / weather mode: `Vm550Slot.WeatherType` vs. the older "always 12" note (both
     flagged UNCONFIRMED / CONFLICTING in `Ams2Constants.cs`)
   - the `WeatherType` enum's value-to-weather mapping (`Enums.cs`)
   - what VM498 slots 37-40 mean (the post-commit check reads 37/39 as class counts)
   - the identity -> tyre/fuel deltas (`Ams2Constants.SettingsBlockDelta`) after the next patch

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
  restoring the player's own values afterwards. Both fields are located relative to the player
  identity (`Native/SettingsBlockLayout.cs`), and nothing is written unless both currently read
  as a valid index there, so a moved layout after a game update fails safe.

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
  (`Vm550Slot.SessionEnabled`/`SessionEnabledPaired1`/`SessionEnabledPaired2`). Weather's
  historic-vs-custom mode has its own slot, `Vm550Slot.WeatherType` (28): `ApplyWeather` writes 0
  (custom) or 2 (historic) before the count. **UNCONFIRMED / CONFLICTING**: an older note in
  `Ams2Constants.cs` says slot 28 (0x1c) is always sent as the constant 12 and never varies with
  user input. Both readings are kept, flagged, and need checking in game.
- **The car/track hash catalogs are data, not a patch risk** — `IAms2HashCatalogProvider` /
  `Ams2InteropHashCatalogLoader` (in `Ams2ChEd.Business.AMS2`) map car and track names to AMS2's
  hashes from data files. Content AMS2 adds after those files were last updated simply isn't in
  them; that already surfaces as an unknown car/track and falls back to the manual-instructions
  flow, so nothing is written for it.

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

## Future work

### RTTI-based tag lookup (investigated, not implemented)

The four type tags are the values most likely to need re-deriving by hand when a signature
breaks. Could they be found through MSVC RTTI instead (a `.?AV...@@` TypeDescriptor name ->
its CompleteObjectLocator -> the vtable that points at it), from the module image alone?

What the code establishes:

- **The identity/container tags** (`Vm498IdentityTag`, `Vm550IdentityTag`, `Vm498ContainerTag`)
  are compared by value at `object + 8`. The qword at `object + 0` is only required to lie in the
  module's range (`VmResolver.IsModuleRangePointer`, "candidate's own vtable-slot qword") - it's
  never compared to anything, so the primary vtable isn't what identifies these objects.
- **`IntPropertyTypeTag`** is read at `node - 0x18`, which is the first qword of the property
  descriptor the generic setter is called with (`SlotWriter`: `propertyDescriptor = node - 0x18`).
  The first qword of a C++ object is normally its vtable pointer, so this tag is plausibly the
  int-property descriptor's vtable.
- `Ams2Constants.BuiltIn`'s patch history says the identity/container tags move together as one
  data block while `IntPropertyTypeTag` sits elsewhere, which fits vtables (or RTTI data) laid
  out per class, but doesn't prove it.

What is NOT established: whether the `+8` tags are vtables at all. An object with a second base
class would carry that base's vtable pointer at `+8`, which would make them secondary vtables;
but they could just as well be pointers to some static per-class descriptor that isn't RTTI.
Nothing in the code or comments says which, and the class names they'd be looked up by are
unknown. So it isn't implemented.

How to settle it, with the game running (reads only):

1. Read the qword just before each tag (`tag - 8`). For a vtable, that's its
   CompleteObjectLocator: in x64 images its `signature` field is 1, and its `offset` field would
   be 8 for the `+8` tags (a secondary base) or 0 for `IntPropertyTypeTag`.
2. Follow the COL's TypeDescriptor RVA and read the decorated name (`.?AV<Class>@@`).
3. If all four check out, a resolver could look each class name up in the image's `.data`
   (TypeDescriptors), find the COLs that reference it, and the vtables that reference those -
   independent of any code pattern. It would need the image's data sections, not just `.text`,
   which `ModuleImage` doesn't read today.
