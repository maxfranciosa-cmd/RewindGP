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
    public const long IntPropertyTypeTag = 0x141fe8508;  // generic "int property" wrapper
    public const long Vm498IdentityTag = 0x141fd39d8;    // VM498 (race entrants) object identity
    public const long Vm550IdentityTag = 0x141fd3a90;    // VM550 (session/rules) object identity
    public const long Vm498ContainerTag = 0x141fd43c8;   // VM498 container class, used by the live scan

    // ---- Fixed offsets ----
    public const int Vm498Offset = 0x250; // VM498 = *(master + 0x250)

    // ---- RVAs into AMS2AVX.exe ----

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
    /// Both branches use the same RVA, 0x4aa460.
    /// </summary>
    public const long SetterRvaDefaultBase = 0x4aa460;

    /// <summary>Same RVA as the default-base case - see SetterRvaDefaultBase's doc comment; kept as a separate constant only so ResolveSetterAddress can still express the two-branch shape.</summary>
    public const long SetterRva = 0x4aa460;

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
    /// </summary>
    public const long SetCarRva = 0x3fbb20;

    /// <summary>
    /// `commit`'s RVA - a single-argument AMS2 function, `void FUN_1403fb890(long master)`,
    /// confirmed to take the same `master` pointer VmResolver.ResolveMaster()/SetCar already use.
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
    /// </summary>
    public const long CommitRva = 0x3fb890;

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
