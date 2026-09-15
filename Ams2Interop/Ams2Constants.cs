namespace Ams2Interop;

/// <summary>
/// Addresses, RVAs, and slot numbers for AMS2AVX.exe's internal Custom Race config objects.
///
/// IMPORTANT: every RVA here assumes AMS2AVX.exe loads at its default (non-relocated) image
/// base. These values are tied to one specific AMS2 build and have NOT been validated against
/// every live game build. Treat a failed write/read as "needs re-deriving for this build," not
/// as a bug in this code.
///
/// Trimmed to just what ApplyRaceConfigAsync needs - RVAs/slots that only backed removed
/// features (persistent hooking) were removed alongside them.
/// </summary>
public static class Ams2Constants
{
    // ---- RTTI / type-tag constants ----
    // These are literal addresses inside AMS2AVX.exe's own RTTI/vtable data, used to confirm
    // a resolved pointer actually points at the object type expected before trusting it.
    //
    // UPDATED 2026-09-14 for the AMS2 build current as of that date (post-patch; the previous
    // values below were observed to no longer resolve anything - see git history for the old
    // values). Vm498IdentityTag/Vm550IdentityTag/Vm498ContainerTag were re-derived via a live,
    // tag-agnostic structural scan of a running AMS2AVX.exe (find objects shaped like
    // vtable-in-module-range + populated int-property slots, without assuming any tag value up
    // front) and then LIVE-CONFIRMED: master/vm498/vm550 all resolved successfully and every
    // read-back slot value (Skill, Aggression, WetWeatherSkill, RollingStart, Hour, Day/Month/
    // Year, etc.) came back sane and in-range against a real, open Custom Race screen. Also
    // confirmed live in that same pass: Vm498Offset and every Vm498Slot/Vm550Slot number below
    // are UNCHANGED from the previous build.
    //
    // IntPropertyTypeTag's ORIGINAL 2026-09-14 candidate (0x141FF1640) was WRONG - off by 0x10 -
    // and its being wrong silently broke everything downstream that depends on it: VmResolver's
    // scoring (CountPopulatedIntProps) gates master/vm550 candidates on score>0 BEFORE the
    // otherwise-correct identity-tag validation ever runs, so a wrong tag here meant
    // ResolveMaster/ResolveVm550 failed outright (not just "less confident") - and TryReadSlot/
    // SlotWriter.TrySetSlot both hard-gate on this tag too, so even a manually-supplied vm498/vm550
    // pointer would have failed every read-back and every write.
    //
    // CORRECTED 2026-09-15 to 0x141FF1650, LIVE-CONFIRMED by bypassing the (buggy) score gate,
    // validating a master candidate purely via the independently-confirmed Vm498IdentityTag, then
    // reading the ACTUAL tag byte at several real property nodes' node-0x18 directly: all 8 probed
    // VM498 slots (NumOpponentsType/RivalCount/RivalCountPerClass/Skill/Aggression/
    // WetWeatherSkill/MistakeFrequency/OpponentsTypeKind), across all 11 structurally-valid master
    // candidates found in the running process, read back exactly 0x141FF1650 with no exceptions.
    // One candidate's values (Skill=108, Aggression=60, WetWeatherSkill=90) also match the
    // 2026-09-14 notes' own live-read example exactly, corroborating this is the same real object
    // shape as before.
    //
    // The exact relative offsets between the four RTTI tags below are preserved identically from
    // the old build (Vm498IdentityTag<->Vm550IdentityTag = 0xB8, Vm498IdentityTag<->
    // Vm498ContainerTag = 0x9F0 in both builds) - this module's static vtable/RTTI data block
    // shifted as a whole rather than being reorganized internally, which is what made re-deriving
    // these tractable. IntPropertyTypeTag sits in a different, unrelated part of that data block
    // (it's why its delta from the anchor region didn't independently confirm it the way the other
    // three tags' shared deltas did) - hence needing this separate live correction.
    public const long IntPropertyTypeTag = 0x141FF1650;  // generic "int property" wrapper - LIVE-CONFIRMED 2026-09-15
    public const long Vm498IdentityTag = 0x141FDCB28;    // VM498 (race entrants) object identity - LIVE-CONFIRMED
    public const long Vm550IdentityTag = 0x141FDCBE0;    // VM550 (session/rules) object identity - LIVE-CONFIRMED
    public const long Vm498ContainerTag = 0x141FDD518;   // VM498 container class, used by the live scan - LIVE-CONFIRMED

    // ---- Fixed offsets ----
    public const int Vm498Offset = 0x250; // VM498 = *(master + 0x250)

    // ---- RVAs into AMS2AVX.exe ----
    //
    // UPDATED 2026-09-15 for the same build the tags above were fixed for. SetterRva/SetCarRva/
    // CommitRva below were RE-DERIVED BY DECOMPILATION (not live-tested yet - see below), after a
    // static delta-prediction from SessionVmGetterRva's confirmed shift was tried and failed (it
    // landed on an unrelated function - code doesn't shift as a uniform block the way the tags'
    // data region did). IntPropertyTypeTag's own vtable was inspected directly per the prior
    // session's recommendation - that was a dead end (none of its vtable slots are the setter; the
    // real setter is a plain non-virtual function, as originally designed) - the actual candidates
    // were found by decompiling functions reachable from the two already-confirmed anchors
    // (FUN_1400c1de0, a session-tab UI dispatcher, and the SessionVmGetterRva region itself).
    //
    // LIVE-VERIFIED 2026-09-15 against the running game (Custom Race screen open):
    // - SetterRva: writing OpponentCount (a VM498 field) and DurationValue/lap count (a VM550
    //   field) both landed correctly and were visually confirmed in AMS2's own UI.
    // - SetCarRva/CommitRva: applying a real car/track/livery selection together with the above
    //   landed correctly and was visually confirmed in AMS2's own UI. One live surprise along the
    //   way: an arbitrary/placeholder livery index (0) consistently failed SetCar's internal
    //   validation gate (FUN_1403fb3e0, called from within SetCarRva - returns a nonzero rejection
    //   code without writing or erroring, which is why a bad livery index looks identical to "the
    //   call executed fine" from ApplyRaceConfigAsync's own result) - this is NOT a library bug,
    //   just confirmation that a real, in-range livery index (resolved via the normal season-pack
    //   data path, as production code already does) is required, exactly as expected. Confirmed by
    //   directly calling FUN_1403fb3e0 and reading its return code across a livery-index range.
    //
    // Evidence for each (see git history / session notes for full decompiles):
    // - SetterRva: FUN_1404ab310(longlong node, int value) - writes at a dynamically-resolved
    //   offset that resolves to the confirmed 0x18c node-value offset, and recurses over a
    //   sibling-node linked list, matching the documented grouped-write behavior (SessionEnabled/
    //   Paired1/Paired2). Has hundreds of callers process-wide (profile of a single shared generic
    //   setter); called directly from both FUN_1400c1de0 and the session-container constructor.
    // - CommitRva: FUN_1403fc710(longlong master) - single-arg; null-checks *(master+0x250)
    //   (Vm498Offset, CONFIRMED), reads a property hash, calls the already-identified
    //   vehiclelist.lst lazy-cache function twice, walks a linked list by that hash, and finishes
    //   by calling a 4-arg function with the VM498 pointer plus 3 counts - matches "counts
    //   vehiclelist.lst entries by class, writes 4 values via the generic setter" exactly. Two real
    //   callers, mirroring the old build's documented two call sites.
    // - SetCarRva: FUN_1403fc9a0(longlong master, int trackHash, uint carHash, int livery, char
    //   flag) - signature matches (master, trackHash, carHash, livery, flag) exactly, flag as a
    //   lone char matching the "always sent as 0" argument. Opens with a no-op early-return guard
    //   comparing all three new values against current property reads, and internally calls
    //   CommitRva (FUN_1403fc710) when values differ - exactly the shape expected of this setter.
    //
    // Corroborating cross-check: new SetCarRva and CommitRva are both exactly +0xE80 from their old
    // addresses; SetterRva is +0xEB0 from ITS old address, in a distant, unrelated code region.
    // Two independent derivations landing on the same small local delta (as opposed to the
    // unrelated -0xA530 delta that applied to the tags' data region) is strong internal-consistency
    // evidence, separate from the call-shape matching above.
    //
    // SessionVmGetterRva lead: the function that builds the exact same 8-entry session name table
    // described below (Practice1/Practice2/Qualifying1/Qualifying2/.../FormationLap/../..) was
    // found and decompile-confirmed at RVA 0x3e8da0 - but it is NOT a drop-in replacement for this
    // constant. In the new build that function is the session CONTAINER's own constructor: it
    // eagerly resolves and caches all 8 session VM pointers as plain fields at fixed offsets
    // (container+0x18, +0x20, +0x28, +0x30, +0x38, +0x40, +0x48, +0x50 for Practice1/Practice2/
    // Qualifying1/Qualifying2/(unnamed)/FormationLap/Race1/Race2 respectively), rather than a
    // lazily-called fn(container, sessionIndex)->vmPointer getter. If re-implementing
    // SessionVmResolver for this build, a direct field read at those offsets (once the container
    // is located) is likely simpler AND cheaper than any remote-call mechanism - not yet
    // implemented or live-verified.

    /// <summary>
    /// The generic property setter's address depends on whether AMS2AVX.exe is loaded at its
    /// default preferred base:
    /// <code>
    /// HMODULE ResolveSetter(void) {
    ///     HMODULE base = GetModuleHandleA(NULL);
    ///     if (base != (HMODULE)0x140000000) return base + 0x4aa460;   // relocated-base case
    ///     return (HMODULE)0x1404aa460;                                 // default-base case (same RVA)
    /// }
    /// </code>
    /// Both branches use the same RVA.
    ///
    /// UPDATED 2026-09-15 (0x4aa460 -&gt; 0x4ab310) - re-derived by decompilation and LIVE-CONFIRMED
    /// - see the "RVAs into AMS2AVX.exe" section doc comment above.
    /// </summary>
    public const long SetterRvaDefaultBase = 0x4ab310;

    /// <summary>Same RVA as the default-base case - see SetterRvaDefaultBase's doc comment; kept as a separate constant only so ResolveSetterAddress can still express the two-branch shape.</summary>
    public const long SetterRva = 0x4ab310;

    /// <summary>Resolves the setter's address given the ACTUAL resolved module base, rather than assuming either branch is right unconditionally.</summary>
    public static long ResolveSetterAddress(long moduleBase) =>
        moduleBase != 0x140000000 ? moduleBase + SetterRva : moduleBase + SetterRvaDefaultBase;

    /// <summary>
    /// `SetCar`'s RVA - the function that applies the player's chosen car/track/livery.
    /// `context` (RCX) is the master pointer - the same value VmResolver.ResolveMaster()
    /// resolves.
    ///
    /// `flag` (the 5th, stack-passed argument) - meaning unconfirmed; `0` is a conservative
    /// default, not a confirmed-correct value.
    ///
    /// UPDATED 2026-09-15 (0x3fbb20 -&gt; 0x3fc9a0) - re-derived by decompilation and LIVE-CONFIRMED
    /// - see the "RVAs into AMS2AVX.exe" section doc comment above. Internally calls a validation
    /// gate (FUN_1403fb3e0) before writing anything - an invalid car/track/livery combination (per
    /// AMS2's own rules, e.g. an out-of-range livery index) makes this silently no-op (no write, no
    /// error, no crash) rather than failing loudly; that gate is NOT exposed by this library, so an
    /// invalid combination looks identical to success from ApplyRaceConfigAsync's own result.
    /// </summary>
    public const long SetCarRva = 0x3fc9a0;

    /// <summary>
    /// `commit`'s RVA - a single-argument AMS2 function (`void FUN_1403fb890(long master)` in the
    /// old build, `void FUN_1403fc710(long master)` in the current one - see the UPDATED note
    /// below), confirmed to take the same `master` pointer VmResolver.ResolveMaster()/SetCar
    /// already use.
    /// Its two real call sites: (1) inside a generic "a UI field changed, propagate derived state"
    /// handler, right after re-pushing several values through this same file's SetterRva; (2)
    /// directly inside the Custom Race screen's OK-button click handler ("OKButton" checked by
    /// name) - i.e. this is (at least one of) the function(s) that fires when the player manually
    /// confirms/backs out of a submenu.
    ///
    /// CONFIRMED exactly what its body does: looks up a vehicle-class record by hash off
    /// `*(master + Vm498Offset)` (VM498/opponents), counts matching `vehiclelist.lst` entries for
    /// that class, and writes the resulting counts through four property nodes via the same
    /// generic setter (SetterRva) everything else in this library uses. It is AI-opponent
    /// vehicle-class-count bookkeeping - CONFIRMED NOT related to RaceDate/VM550/session fields in
    /// any way, despite firing on the same OK-button click that also seems like it ought to
    /// "commit" a stale RaceDate. Kept in ApplyRaceConfigAsync for its actual purpose (opponent
    /// class counts); do not expect it to help RaceDate or anything session-related - the RaceDate
    /// staleness fix that DID work is the VM498 packed-date write (see
    /// Native/Vm498PackedDate.cs), applied alongside the normal VM550 slot writes, not this call.
    ///
    /// UPDATED 2026-09-15 (0x3fb890 -&gt; 0x3fc710) - re-derived by decompilation and LIVE-CONFIRMED
    /// (indirectly, as part of a successful full car/track/livery/opponents/laps apply - see the
    /// "RVAs into AMS2AVX.exe" section doc comment above) - not independently isolated the way
    /// SetterRva was, but exercised on every successful SetCarRva call in this session's testing.
    /// </summary>
    public const long CommitRva = 0x3fc710;

    /// <summary>
    /// A plain struct int field on VM498 itself (NOT an int-property slot - see
    /// Native/Vm498PackedDate.cs for the bit layout and full doc). Found via VM498's own
    /// constructor (FUN_1403eaab0), which seeds it from GetLocalTime at construction time.
    /// CONFIRMED live that writing only this field does not by itself change the race's actual
    /// date - Ams2RaceConfigurator writes it alongside (not instead of) the VM550 Day/Month/Year
    /// slots, and that combined write IS confirmed live to work (the race's actual data lands
    /// correctly) - AMS2's own Custom Race menu display can still lag until the submenu is
    /// re-visited, which is a cosmetic gap only.
    /// </summary>
    public const int Vm498DatePackedOffset = 0x188;

    /// <summary>
    /// AMS2's own per-session-name VM pointer getter: `fn(container, sessionIndex) -> vmPointer`,
    /// confirmed at this RVA inside AMS2AVX.exe.
    ///
    /// Internally, does a one-time init building an 8-entry name table (index -&gt; string):
    /// 0="Practice1", 1="Practice2", 2="Qualifying1", 3="Qualifying2", 4=&amp;DAT_141fd390c (name not
    /// yet read), 5="FormationLap", 6=&amp;DAT_141ebc21c (name not yet read - used for "race"),
    /// 7=&amp;DAT_141ebc224 (name not yet read).
    /// Then looks up `container`'s named-child list for that index's name and returns the matched
    /// child's own `+0x18` field, which is then treated as a VM550-shaped object (read/written
    /// through the exact same generic-setter/int-property-slot mechanism SlotWriter already uses
    /// for the "main" VM550) - confirming Practice1/Qualifying1 are separate, structurally-
    /// identical VM550 instances reachable this way - NOT new slot numbers on the main VM550, and
    /// NOT found by a plain slot dump/diff of vm498/vm550 (confirmed live: toggling practice/
    /// qualifying settings on the Custom Race screen produces zero VM498/VM550 slot diffs), since
    /// they live on an entirely separate object.
    /// </summary>
    public const long SessionVmGetterRva = 0x3f2dd0;

    /// <summary>
    /// Session name indices for SessionVmGetterRva - see its doc comment. 0/2/6/7 are used by
    /// Ams2RaceConfigurator; the rest are documented for completeness/future use.
    /// </summary>
    public static class SessionIndex
    {
        public const int Practice1 = 0;
        public const int Practice2 = 1;
        public const int Qualifying1 = 2;
        public const int Qualifying2 = 3;
        // index 4: name not yet read (&DAT_141fd390c in AMS2AVX.exe's own data)
        public const int FormationLap = 5;

        /// <summary>
        /// CONFIRMED via direct read of AMS2AVX.exe's own string data at &amp;DAT_141ebc21c: "Race1".
        /// This is what VmResolver.ResolveVm550()/ApplyRaceConfigAsync already write to - AMS2
        /// structures every weekend with two race slots (mirroring Practice1/2, Qualifying1/2), and
        /// this is the first one.
        /// </summary>
        public const int Race1 = 6;

        /// <summary>
        /// CONFIRMED via direct read of AMS2AVX.exe's own string data at &amp;DAT_141ebc224: "Race2".
        /// NOT written by anything in this library by default - see SessionVmResolver.ResolveRace2's
        /// doc comment for why RaceDate needs to reach this VM too, not just Race1's.
        /// </summary>
        public const int Race2 = 7;
    }

    /// <summary>VM498 slots (race entrants / AI).</summary>
    public static class Vm498Slot
    {
        public const int MaxOpponents = 15;
        public const int NumOpponentsType = 16;   // 0=max available, 1=custom, 2=manual grid
        public const int RivalCount = 17;         // opponent count

        /// <summary>
        /// EXPERIMENTAL - written alongside RivalCount (17) in several of AMS2's own call sites
        /// (e.g. "reassert-ais"); independent meaning not confirmed via static analysis. Leading
        /// theory: RivalCount(17) is the field-wide opponent total while this is the per-class
        /// count (AMS2 tracks opponents per vehicle class for multiclass grids) - in a SameClass
        /// race there's only one class, so this should just mirror RivalCount. Untested until now;
        /// Ams2RaceConfigurator writes the same value here as RivalCount whenever RivalCount is set.
        /// </summary>
        public const int RivalCountPerClass = 18;
        public const int Skill = 21;              // 70-120
        public const int Aggression = 22;         // 40/60/80/100 - not exposed in OpponentsConfig
        public const int WetWeatherSkill = 26;    // 0-200
        public const int MistakeFrequency = 28;   // 0=off .. 6=x5
        public const int OpponentsTypeKind = 31;  // 0=identical, 1=same class, 2=multiclass
    }

    /// <summary>VM550 slots (session / rules).</summary>
    public static class Vm550Slot
    {
        /// <summary>
        /// Session on/off - CONFIRMED: plain 0/1 boolean, always written alongside
        /// SessionEnabledPaired1/2 as a group of three (see their doc comments) whenever setting a
        /// session's enabled state. Only meaningful on a per-session VM resolved via
        /// SessionVmResolver (Practice1/Qualifying1 use session indices 0/2 respectively) - NOT a
        /// field on the main VM550.
        /// </summary>
        public const int SessionEnabled = 3;

        /// <summary>Written alongside SessionEnabled as a group: 1 if enabled, else -1. Exact independent meaning beyond "paired with SessionEnabled" not decoded.</summary>
        public const int SessionEnabledPaired1 = 4;

        /// <summary>Written alongside SessionEnabled as a group: the exact inverse of SessionEnabledPaired1 (-1 if enabled, else 1).</summary>
        public const int SessionEnabledPaired2 = 5;

        /// <summary>
        /// Weather slot count (0-4) - CONFIRMED: clamped to max 4 before being written. Present
        /// on the main VM550 (race session) same as any other slot, and independently on
        /// Practice1/Qualifying1's own VMs - the underlying slot is per-session, not shared, even
        /// though all three sessions are typically sent the same count.
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
        // "WEATHER(28/32/33/36/39/42)" group, but it never varies with user input in the traced
        // code path, so its purpose/meaning wasn't decoded and it's not exposed here.

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
}
