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

    /// <summary>VM498 slots (race entrants / AI).</summary>
    public static class Vm498Slot
    {
        public const int MaxOpponents = 15;
        public const int NumOpponentsType = 16;   // 0=max available, 1=custom, 2=manual grid
        public const int RivalCount = 17;         // opponent count
        // slot 18 is written alongside 17 in several call sites (e.g. "reassert-ais"); its
        // independent meaning was never pinned down - not exposed in the typed API.
        public const int Skill = 21;              // 70-120
        public const int Aggression = 22;         // 40/60/80/100 - not exposed in OpponentsConfig
        public const int WetWeatherSkill = 26;    // 0-200
        public const int MistakeFrequency = 28;   // 0=off .. 6=x5
        public const int OpponentsTypeKind = 31;  // 0=identical, 1=same class, 2=multiclass
    }

    /// <summary>VM550 slots (session / rules).</summary>
    public static class Vm550Slot
    {
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
        public const int DateType = 48;
        public const int Hour = 51;               // 0x33
        public const int Day = 53;
        public const int Month = 54;
        public const int Year = 55;
    }
}
