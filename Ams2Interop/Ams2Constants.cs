namespace Ams2Interop;

/// <summary>
/// Addresses, RVAs, and slot numbers for AMS2AVX.exe's internal Custom Race config objects.
///
/// IMPORTANT: every RVA/tag here is tied to one specific AMS2 build and must be re-derived after
/// a game patch. Treat a failed write/read as "needs re-deriving for this build," not as a bug in
/// this code. Every RVA assumes AMS2AVX.exe loads at its default (non-relocated) image base.
///
/// Current values target the AMS2 build patched on 2026-09-20, and were LIVE-CONFIRMED on
/// 2026-09-27: date, car, track, livery, opponent count and opponent class all applied correctly
/// to a launched race via the real app.
///
/// Patch history, useful when re-deriving: code RVAs moved +0xE80..+0xEB0 on the 2026-09-15 patch
/// and a uniform +0x120 on the 2026-09-20 one; RTTI tags shift together as one data block (their
/// relative offsets are preserved), except IntPropertyTypeTag, which sits in an unrelated part of
/// that block and must be confirmed separately.
/// </summary>
public static class Ams2Constants
{
    // ---- RTTI / type-tag constants ----
    // Literal addresses inside AMS2AVX.exe's own RTTI/vtable data, used to confirm a resolved
    // pointer actually points at the expected object type before trusting it.
    //
    // IntPropertyTypeTag is load-bearing for everything: VmResolver's candidate scoring
    // (CountPopulatedIntProps) and TryReadSlot/SlotWriter's tag check all gate on it, so a wrong
    // value makes every resolve, read-back and write fail. It's the generic setter's dispatch tag:
    // node=*(vm+slot*8); tag=*(node-0x18); if(tag==IntPropertyTypeTag){...}.
    public const long IntPropertyTypeTag = 0x141FF1658;  // generic "int property" wrapper
    public const long Vm498IdentityTag = 0x141FDCB28;    // VM498 (race entrants) object identity
    public const long Vm550IdentityTag = 0x141FDCBE0;    // VM550 (session/rules) object identity
    public const long Vm498ContainerTag = 0x141FDD518;   // VM498 container class, used by the master scan

    // ---- Fixed offsets ----
    public const int Vm498Offset = 0x250; // VM498 = *(master + 0x250)

    // ---- RVAs into AMS2AVX.exe ----

    /// <summary>
    /// The generic int-property setter, `setter(longlong node, int value)` - writes at the node's
    /// value offset (0x18c) and recurses over a sibling-node linked list (grouped writes, e.g.
    /// SessionEnabled/Paired1/Paired2). SetCarRva itself writes car/track/livery through this
    /// function. Address resolution:
    /// <code>
    /// HMODULE ResolveSetter(void) {
    ///     HMODULE base = GetModuleHandleA(NULL);
    ///     if (base != (HMODULE)0x140000000) return base + RVA;   // relocated-base case
    ///     return (HMODULE)(0x140000000 + RVA);                   // default-base case (same RVA)
    /// }
    /// </code>
    /// </summary>
    public const long SetterRvaDefaultBase = 0x4ab430;

    /// <summary>Same RVA as the default-base case - see SetterRvaDefaultBase's doc comment; kept as a separate constant only so ResolveSetterAddress can still express the two-branch shape.</summary>
    public const long SetterRva = 0x4ab430;

    /// <summary>Resolves the setter's address given the ACTUAL resolved module base, rather than assuming either branch is right unconditionally.</summary>
    public static long ResolveSetterAddress(long moduleBase) =>
        moduleBase != 0x140000000 ? moduleBase + SetterRva : moduleBase + SetterRvaDefaultBase;

    /// <summary>
    /// `SetCar(master, trackHash, carHash, livery, flag)` - applies the player's chosen
    /// car/track/livery. `master` (RCX) is VmResolver.ResolveMaster()'s result. `flag` (the 5th,
    /// stack-passed argument) - meaning unconfirmed; `0` is a conservative default.
    ///
    /// Opens with a no-op early return if all three values already match (see MasterSlot), then
    /// calls a validation gate (SetCarValidationGateRva) and silently no-ops on rejection: the
    /// gate's code is discarded unless a flag byte at `master+0x259` is already set, which nothing
    /// in this library does. When values change it also calls CommitRva internally, which
    /// overwrites the opponent count - see Ams2RaceConfigurator.ApplyRaceConfigAsync for the
    /// resulting write order.
    /// </summary>
    public const long SetCarRva = 0x3fcac0;

    /// <summary>
    /// `gate(longlong master, int trackHash, int carHash, int livery) -&gt; int` - the validation
    /// gate SetCarRva calls internally. Reads `vm498 = *(master + Vm498Offset)`, so a nonzero code
    /// is a genuine rejection of the values.
    ///
    /// Return codes:
    /// - `0` = OK, no rejection.
    /// - `1` = `trackHash` not found in AMS2's own track registry - stale track hash catalog.
    /// - `2` = track found, but an eligibility/ownership check against vm498 fails.
    /// - `3` = `carHash` not found in the vehicle registry - stale car hash catalog.
    /// - `4` = car+track combination ineligible - DLC ownership/compatibility.
    /// - `5` = an additional track/vm498 condition - not fully identified.
    /// - `6` = livery index not found for this car.
    ///
    /// Callable via the 4-argument `RemoteExecutor.CallWithReturn` overload.
    ///
    /// STALE for the current build (this value predates the 2026-09-20 patch). Extrapolating the
    /// +0x120 patch delta predicts `0x3fb500`, but that's unobserved. Ams2RaceConfigurator does NOT
    /// call it - remote-calling an unconfirmed address can crash the game. Confirm the real value
    /// before re-enabling.
    /// </summary>
    public const long SetCarValidationGateRva = 0x3fb3e0; // KNOWN STALE - do not call without re-deriving; predicted (unconfirmed) replacement: 0x3fb500

    /// <summary>
    /// `commit(master)` - AMS2's AI-opponent vehicle-class-count recompute: looks up the selected
    /// car's vehicle class off `*(master + Vm498Offset)`, counts matching `vehiclelist.lst`
    /// entries, and writes the counts into VM498 through the generic setter (overwriting
    /// RivalCount/RivalCountPerClass - see Vm498Slot). Called from AMS2's generic "UI field
    /// changed" handler and from the Custom Race screen's OK-button handler, and internally by
    /// SetCarRva. Unrelated to RaceDate/session fields.
    /// </summary>
    public const long CommitRva = 0x3fc830;

    /// <summary>
    /// A plain struct int field on VM498 itself (NOT an int-property slot - see
    /// Native/Vm498PackedDate.cs for the bit layout and full doc). Seeded from the local date when
    /// VM498 is constructed.
    /// Writing only this field does not change the race's actual date; Ams2RaceConfigurator
    /// writes it alongside the VM550 Day/Month/Year slots, and that combined write is
    /// live-confirmed to work. AMS2's own Custom Race menu display can still lag until the
    /// submenu is re-visited - cosmetic only.
    /// </summary>
    public const int Vm498DatePackedOffset = 0x188;

    /// <summary>
    /// Session indices into VM498's session-wrapper array: VM498 caches 8 wrapper pointers at
    /// `vm498 + 0x18 + index*8`, and each wrapper's own `+0x18` field holds that session's
    /// VM550-shaped pointer - separate, structurally-identical VM550 instances written through the
    /// same Vm550Slot numbers (toggling practice/qualifying settings in AMS2's UI produces zero
    /// diffs on the main VM498/VM550). Names come from AMS2's own session name table. Read by
    /// Native/SessionVmResolver.cs via plain memory reads (never a remote call).
    /// </summary>
    public static class SessionIndex
    {
        public const int Practice1 = 0;
        public const int Practice2 = 1;
        public const int Qualifying1 = 2;
        public const int Qualifying2 = 3;
        // index 4: name unknown
        public const int FormationLap = 5;

        /// <summary>
        /// "Race1" - what VmResolver.ResolveVm550()/ApplyRaceConfigAsync already write to. AMS2
        /// structures every weekend with two race slots (mirroring Practice1/2, Qualifying1/2).
        /// </summary>
        public const int Race1 = 6;

        /// <summary>
        /// "Race2" - see SessionVmResolver.ResolveRace2's doc comment for why RaceDate needs to
        /// reach this VM too, not just Race1's.
        /// </summary>
        public const int Race2 = 7;
    }

    /// <summary>VM498 slots (race entrants / AI).</summary>
    public static class Vm498Slot
    {
        public const int MaxOpponents = 15;
        public const int NumOpponentsType = 16;   // 0=max available, 1=custom, 2=manual grid

        /// <summary>
        /// Opponent count. CommitRva (and SetCarRva, which calls it) overwrite this with AMS2's
        /// own recount for the selected car, so it must be written after SetCar. A count above the
        /// number of same-class cars makes AMS2 pad the grid with other classes.
        /// </summary>
        public const int RivalCount = 17;

        /// <summary>
        /// AMS2 writes this alongside RivalCount, and CommitRva overwrites both together. Likely the per-class count for multiclass
        /// grids; for a same-class race it mirrors RivalCount, which is what Ams2RaceConfigurator
        /// writes (live-confirmed working together with RivalCount).
        /// </summary>
        public const int RivalCountPerClass = 18;
        public const int Skill = 21;              // 70-120
        public const int Aggression = 22;         // 40/60/80/100 - not exposed in OpponentsConfig
        public const int WetWeatherSkill = 26;    // 0-200
        public const int MistakeFrequency = 28;   // 0=off .. 6=x5
        public const int OpponentsTypeKind = 31;  // 0=identical, 1=same class, 2=multiclass
        // Slots 37-40 are also written by CommitRva. 37/39 looked like the number of cars
        // available in the selected class (26 in the one live read); 38/40 are undecoded.
    }

    /// <summary>VM550 slots (session / rules).</summary>
    public static class Vm550Slot
    {
        /// <summary>
        /// Session on/off - plain 0/1 boolean, always written alongside SessionEnabledPaired1/2 as
        /// a group of three (see their doc comments) whenever setting a session's enabled state.
        /// Only meaningful on a per-session VM resolved via SessionVmResolver (Practice1/Qualifying1
        /// use session indices 0/2 respectively) - NOT a field on the main VM550.
        /// </summary>
        public const int SessionEnabled = 3;

        /// <summary>Written alongside SessionEnabled as a group: 1 if enabled, else -1. Exact independent meaning beyond "paired with SessionEnabled" not decoded.</summary>
        public const int SessionEnabledPaired1 = 4;

        /// <summary>Written alongside SessionEnabled as a group: the exact inverse of SessionEnabledPaired1 (-1 if enabled, else 1).</summary>
        public const int SessionEnabledPaired2 = 5;

        /// <summary>
        /// Weather slot count (0-4) - clamped to max 4 before being written. Present on the main
        /// VM550 (race session) same as any other slot, and independently on Practice1/Qualifying1's
        /// own VMs - the underlying slot is per-session, not shared, even though all three sessions
        /// are typically sent the same count.
        /// UNCONFIRMED whether a separate RealHistoric-vs-Custom mode slot also needs setting
        /// first (mirrors the RaceDate/DateType relationship) - none was identified.
        /// </summary>
        public const int WeatherSlotCount = 0x20;

        /// <summary>1st of 4 weather slot values, in order - see WeatherSlotCount's doc comment. AMS2 itself repeats the last given slot when count &lt; 4 (its own stated behavior, not enforced by this library).</summary>
        public const int WeatherSlot1 = 0x21;
        public const int WeatherSlot2 = 0x24;
        public const int WeatherSlot3 = 0x27;
        public const int WeatherSlot4 = 0x2a;
        // Slot 0x1c (28) is always sent as the constant 12, part of the same
        // "WEATHER(28/32/33/36/39/42)" group, but it never varies with user input, so its
        // purpose/meaning wasn't decoded and it's not exposed here.

        public const int DurationTypeFlag = 7;    // 0 = time mode, 1 = laps mode - a binary switch, NOT a format enum
        public const int DurationMinutes = 8;     // used only when DurationTypeFlag == 0
        public const int LapCount = 11;           // 0xb - used only when DurationTypeFlag == 1
        public const int PrivateQuali = 13;
        public const int RollingStart = 18;       // 0 = grid, 1 = rolling
        public const int MandatoryPit = 20;
        public const int MinTyres = 21;           // valid: {-1,0,2,4}
        public const int MinFuel = 22;            // valid: {-1,0,2,4}
        public const int Fcy = 23;                // 0-5, 0 = off; meaning of 1-5 not decoded
        public const int Refuelling = 24;
        public const int TimeProgression = 27;    // 0x1b - 1=real,2=x2,3=x5,4=x10
        public const int WeatherType = 28;          // 0=custom,2=historic
        public const int DateType = 48;
        public const int Hour = 51;               // 0x33
        public const int Day = 53;
        public const int Month = 54;
        public const int Year = 55;
    }

    /// <summary>
    /// Plain int-property slots on `master` ITSELF (not VM498/VM550) holding the currently-
    /// selected car/track/livery - found inside SetCarRva's own "already selected, nothing to do"
    /// early-return check. Same node shape as every other slot here (`*(master + slot*8)` -&gt;
    /// node; value at `node+0x18c`), read via `ProcessMemory.TryReadSlot`. Used as SetCar's
    /// read-back check.
    /// </summary>
    public static class MasterSlot
    {
        public const int Track = 4;    // master + 0x20
        public const int Car = 28;     // master + 0xE0
        public const int Livery = 35;  // master + 0x118
    }
}
