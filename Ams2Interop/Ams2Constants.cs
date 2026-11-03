namespace Ams2Interop;

/// <summary>
/// Addresses, RVAs, and slot numbers for AMS2AVX.exe's internal Custom Race config objects.
///
/// The type tags and RVAs below are tied to one specific AMS2 build. They're no longer constants:
/// each reads the values resolved for the running build (see Ams2Offsets / Ams2OffsetResolver),
/// which come from the signature file when one is present and fall back to the hand-derived
/// values in <see cref="BuiltIn"/> otherwise. Slot numbers and struct offsets stay constants.
/// </summary>
public static class Ams2Constants
{
    /// <summary>AMS2AVX.exe's preferred image base - the base the absolute tags in BuiltIn assume.</summary>
    public const long DefaultImageBase = 0x140000000;

    /// <summary>
    /// The hand-derived values: what's used when there's no signature file to check against, and
    /// what Ams2SignatureGenerator generates signatures from.
    ///
    /// They target the AMS2 build patched on 2026-09-20, and were LIVE-CONFIRMED on 2026-09-27:
    /// date, car, track, livery, opponent count and opponent class all applied correctly to a
    /// launched race via the real app.
    ///
    /// Patch history, useful when re-deriving: code RVAs moved +0xE80..+0xEB0 on the 2026-09-15
    /// patch and a uniform +0x120 on the 2026-09-20 one; RTTI tags shift together as one data block
    /// (their relative offsets are preserved), except IntPropertyTypeTag, which sits in an
    /// unrelated part of that block and must be confirmed separately.
    /// </summary>
    public static class BuiltIn
    {
        // absolute, at DefaultImageBase
        public const long IntPropertyTypeTag = 0x141FF1658;
        public const long Vm498IdentityTag = 0x141FDCB28;
        public const long Vm550IdentityTag = 0x141FDCBE0;
        public const long Vm498ContainerTag = 0x141FDD518;

        // RVAs
        public const long SetterRva = 0x4ab430;
        public const long SetCarRva = 0x3fcac0;
        public const long CommitRva = 0x3fc830;
        public const long GameplaySettingsPointerRva = 0x29889E8;

        /// <summary>
        /// ModuleImage.BuildId values of the AMS2AVX.exe builds these values are KNOWN to work on
        /// (a real launch applied car, track and date correctly). Ams2OffsetResolver only falls back
        /// to them unchecked on one of these builds: on any other build, once this list isn't empty,
        /// every patch-sensitive feature is switched off instead (Ams2OffsetSource.BuiltInRejected).
        ///
        /// While the list is EMPTY the old behaviour is kept - the values are used on any build, with
        /// a warning in the log - because nobody has recorded the confirmed build's id yet, and an
        /// empty list must not switch the interop off for everyone.
        /// </summary>
        // Add a build here only after a real launch applied car, track and date correctly on it
        // (id from "Ams2Interop.Tool status").
        public static readonly string[] ConfirmedBuildIds =
        {
            "6AAABF2C-02EE1000-00000000", // CONFIRMED 2026-11-03: race config applied in a real launch
        };
    }

    // ---- RTTI / type-tag values ----
    // Literal addresses inside AMS2AVX.exe's own RTTI/vtable data, used to confirm a resolved
    // pointer actually points at the expected object type before trusting it.
    //
    // IntPropertyTypeTag is load-bearing for everything: VmResolver's candidate scoring
    // (CountPopulatedIntProps) and TryReadSlot/SlotWriter's tag check all gate on it, so a wrong
    // value makes every resolve, read-back and write fail. It's the generic setter's dispatch tag:
    // node=*(vm+slot*8); tag=*(node-0x18); if(tag==IntPropertyTypeTag){...}.
    public static long IntPropertyTypeTag => Ams2Offsets.Active.Absolute(Ams2OffsetNames.IntPropertyTypeTag);  // generic "int property" wrapper
    public static long Vm498IdentityTag => Ams2Offsets.Active.Absolute(Ams2OffsetNames.Vm498IdentityTag);      // VM498 (race entrants) object identity
    public static long Vm550IdentityTag => Ams2Offsets.Active.Absolute(Ams2OffsetNames.Vm550IdentityTag);      // VM550 (session/rules) object identity
    public static long Vm498ContainerTag => Ams2Offsets.Active.Absolute(Ams2OffsetNames.Vm498ContainerTag);    // VM498 container class, used by the master scan

    // ---- Fixed offsets ----
    public const int Vm498Offset = 0x250; // VM498 = *(master + 0x250)

    /// <summary>
    /// Two flag bytes on master, both 1 on the master the Custom Race screen is bound to and 0 on
    /// the other, correctly-tagged masters AMS2 keeps (eleven of them, live-checked 2026-10-26).
    /// Their "populated slots" score doesn't tell them apart: the live one can score lower.
    /// </summary>
    public const int MasterActiveFlagsOffset = 0x240;

    // ---- RVAs into AMS2AVX.exe ----

    /// <summary>
    /// The generic int-property setter, `setter(longlong node, int value)` - writes at the node's
    /// value offset (0x18c) and recurses over a sibling-node linked list (grouped writes, e.g.
    /// SessionEnabled/Paired1/Paired2). SetCarRva itself writes car/track/livery through this
    /// function. Its address is always moduleBase + RVA, relocated or not.
    /// </summary>
    public static long SetterRva => Ams2Offsets.Active.Rva(Ams2OffsetNames.Setter);

    /// <summary>The setter's address given the ACTUAL resolved module base.</summary>
    public static long ResolveSetterAddress(long moduleBase) => moduleBase + SetterRva;

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
    public static long SetCarRva => Ams2Offsets.Active.Rva(Ams2OffsetNames.SetCar);

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
    /// before re-enabling. Deliberately kept out of Ams2OffsetNames (no signature is generated or
    /// resolved for it): nothing may call it until it's re-derived and live-confirmed.
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
    public static long CommitRva => Ams2Offsets.Active.Rva(Ams2OffsetNames.Commit);

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
    /// A module-global pointer (`block = *(moduleBase + this)`) to AMS2's big settings block,
    /// which holds the global gameplay options - see GameplaySettingsOffset. Unlike the Custom
    /// Race objects it exists whenever the game is running. A DATA RVA, so it doesn't follow the
    /// code-RVA patch deltas noted on BuiltIn - its signature finds it through the code that reads
    /// it instead. NOT YET LIVE-CONFIRMED.
    /// </summary>
    public static long GameplaySettingsPointerRva => Ams2Offsets.Active.Rva(Ams2OffsetNames.GameplaySettingsPointer);

    /// <summary>
    /// Plain int fields inside the settings block (see GameplaySettingsPointerRva), each an index
    /// 0-8 into that option's own list rather than the multiplier itself: tyre wear x1..x7 is
    /// index 6..0 (`7 - multiplier`), fuel usage x2..x5 is index 6..3 (`8 - multiplier`). The
    /// remaining indices (tyre 7/8, fuel 0/1/2/7/8 - off, x1 and the like) aren't established.
    /// </summary>
    public static class GameplaySettingsOffset
    {
        public const int TyreWear = 0x3D3C0;
        public const int FuelUsage = 0x3D3C4;
    }

    /// <summary>
    /// The player's profile identity inside the settings block (see GameplaySettingsPointerRva):
    /// the nationality as the text id of the country's name (as in the game's text\general.tdb),
    /// then pointers to the forename, surname, three-letter handle and composed full name strings
    /// (see Native/PlayerIdentityWriter.cs). Live-confirmed 2026-10-26: a change shows in the
    /// "Edit info" screen and in the next session.
    /// </summary>
    public static class PlayerIdentityOffset
    {
        public const int Country = 0x3D480;
        public const int Forename = 0x3D488;
        public const int Surname = 0x3D490;
        public const int Handle = 0x3D498;
        public const int FullName = 0x3D4A0;
    }

    /// <summary>
    /// Where the wear settings sit RELATIVE TO the player identity in the settings block, so they
    /// can follow it when an AMS2 update moves the block's fields (see
    /// Native/SettingsBlockLayout.cs): TyreWear = Country - TyreWearBeforeCountry, FuelUsage =
    /// Country - FuelUsageBeforeCountry. Derived from the confirmed offsets above
    /// (0x3D480 - 0x3D3C0, 0x3D480 - 0x3D3C4) - ASSUMED, UNCONFIRMED across patches: an update
    /// that moves the identity and the wear settings by different amounts breaks it, which is why
    /// the derived fields must still read as valid indices before they're used.
    /// </summary>
    public static class SettingsBlockDelta
    {
        public const int TyreWearBeforeCountry = PlayerIdentityOffset.Country - GameplaySettingsOffset.TyreWear;   // 0xC0
        public const int FuelUsageBeforeCountry = PlayerIdentityOffset.Country - GameplaySettingsOffset.FuelUsage; // 0xBC
    }

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

        /// <summary>
        /// Written by CommitRva; UNCONFIRMED meaning - looked like the number of cars available in
        /// the selected class (26 in the one live read). Only read, as a sanity check after commit
        /// (see CustomRaceWriter.CheckCommitResult).
        /// </summary>
        public const int CommitClassCountA = 37;

        /// <summary>See CommitClassCountA - same reading, same caveat.</summary>
        public const int CommitClassCountB = 39;
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
        /// The historic-vs-custom weather mode has its own slot, WeatherType (28): ApplyWeather
        /// writes it (0 = custom, 2 = historic) before this count - see WeatherType's
        /// UNCONFIRMED / CONFLICTING note.
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
        // UNCONFIRMED / CONFLICTING: the note above predates WeatherType below, which IS slot 28
        // (0x1c) and IS written by ApplyWeather as 0 (custom) or 2 (historic). The two readings
        // contradict each other - which one holds needs in-game verification (set custom and
        // historic weather in AMS2's UI and read slot 28 each time). Behaviour is unchanged.

        public const int DurationTypeFlag = 7;    // 0 = time mode, 1 = laps mode - a binary switch, NOT a format enum
        public const int DurationMinutes = 8;     // used only when DurationTypeFlag == 0
        public const int LapCount = 11;           // 0xb - used only when DurationTypeFlag == 1
        public const int PrivateQuali = 13;
        public const int RollingStart = 18;       // 0 = grid, 1 = rolling
        public const int MandatoryPit = 20;
        public const int MinTyres = 21;           // valid: {-1,0,2,4}
        public const int MinFuel = 22;            // valid: {-1,0,2,4}
        /// <summary>
        /// "Scheduled Full Course Yellow", from the race's session settings - NOT the "Full Course
        /// Yellow enabled" switch, which lives on a different settings page (with the pit speed
        /// limit) and isn't a VM550 slot here. Identified from which page the setting sits on
        /// (2026-11-03), not from a live read. 0 = off; values 1-5 not decoded, and whether the
        /// setting has more options than that is UNCONFIRMED. Not written by the interop - only
        /// range-checked by Ams2LayoutCheck.
        /// </summary>
        public const int ScheduledFcy = 23;
        public const int Refuelling = 24;
        public const int TimeProgression = 27;    // 0x1b - 1=real,2=x2,3=x5,4=x10
        /// <summary>
        /// Weather mode: 0 = custom (the WeatherSlot values apply), 2 = historic - written by
        /// ApplyWeather ahead of WeatherSlotCount.
        /// UNCONFIRMED / CONFLICTING: this is the same slot (28 = 0x1c) that the note above
        /// WeatherSlot4 describes as "always sent as the constant 12" and never varying with user
        /// input. Both can't be right; needs in-game verification. Not part of Ams2LayoutCheck
        /// for that reason.
        /// </summary>
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
