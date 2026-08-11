# Ams2Interop

Talks directly to a running Automobilista 2 (`AMS2AVX.exe`) process: attaches to it, reads/writes
its Custom Race config, and can launch the game if it isn't running yet. Consumer-agnostic by
design - any application that needs this (AMS2ChEd, or others) references this project directly
rather than each reimplementing it.

**For API reference and day-to-day usage patterns, see `USAGE.md`.** This file covers
architecture, implementation status, and known gaps.

## Status: `ApplyRaceConfigAsync` only

`ApplyRaceConfigAsync` - opponents, track, car, livery, and session rules - applies correctly
in-game and is reliable enough to depend on. Any form of persistent hooking is out of scope and
not implemented - see "Architecture" below for why.

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
  **Confirmed live**: opponents, laps, rolling start, refuelling, mandatory pit stops, and start
  hour all apply correctly.
- Car/track/livery selection, wired into `ApplyRaceConfigAsync`, by calling AMS2's `SetCar`
  directly with a `VmResolver`-resolved master pointer as its context argument. **Confirmed
  live**: car, track, and livery selection all apply correctly (still no read-back verification
  available for this call - confirmation here came from direct in-game/in-memory checks, not the
  library's own return value).
- A write-then-verify-twice pattern in `ApplyRaceConfigAsync`, since AMS2's Custom Race screen
  can re-initialize state shortly after opening and silently undo an early write.
- `Ams2Launcher`: detect whether AMS2 is running and launch the game
  through Steam so the resulting process can actually be attached to afterward.

## What's a known simplification

- **`SetCar`'s `flag` argument** — always sent as `0`. Its exact meaning is unconfirmed; `0` is
  a conservative default, not a confirmed-correct value.
- **`SetCar` has no read-back verification** — unlike the VM498/VM550 slot writes, this library
  doesn't know where AMS2 stores the *result* of a car/track/livery change to read it back and
  confirm. `ApplyRaceConfigAsync` only reports whether the remote `SetCar` call executed and
  returned, not whether AMS2 accepted the values.

## Usage

See `USAGE.md` for the full API reference, a complete quick-start example, and a
troubleshooting table.

## Before relying on this in production

1. Config-apply (opponents/track/car/livery/most session rules) is live-confirmed against a real
   AMS2 install - see the Status section above. Still worth re-confirming against whatever exact
   build you target, since offset drift across game patches is a real, observed risk here.
2. Getting to the Custom Race screen is not automated - budget for a manual step, or driving it
   through your own application's UI, before calling into this library.
3. This performs a full-process memory scan (`Native/MemoryScanner.cs`) whenever there's no
   cached pointer - expect a multi-second stall the first time `VmResolver` runs after attaching.
