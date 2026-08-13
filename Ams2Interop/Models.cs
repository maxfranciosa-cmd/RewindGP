namespace Ams2Interop;

/// <summary>
/// AI/opponents configuration. Every property is nullable - null means "don't force," matching
/// the -1 sentinel convention AMS2 itself uses throughout.
/// </summary>
public sealed class OpponentsConfig
{
    public NumOpponentsType? NumOpponentsType { get; init; }
    public OpponentsTypeKind? OpponentsType { get; init; }
    public int? OpponentCount { get; init; }

    /// <summary>70-120. Values outside this range are rejected rather than silently clamped.</summary>
    public int? Skill { get; init; }

    /// <summary>0-200.</summary>
    public int? AiWetWeatherSkill { get; init; }

    public AiMistakeFrequency? AiMistakeFrequency { get; init; }
}

/// <summary>
/// Session/rules configuration. Every property is nullable - null means "don't force." See
/// Ams2Constants.Vm550Slot for the slot mapping.
/// </summary>
public sealed class SessionRulesConfig
{
    public OnOff? PrivateQualiSession { get; init; }
    public OnOff? MandatoryPitStop { get; init; }
    public StartType? RollingStart { get; init; }
    public OnOff? RefuellingAllowed { get; init; }
    public TimeProgression? TimeProgression { get; init; }
    public DateType? DateType { get; init; }
    public PitTyreCount? PitMinTyres { get; init; }
    public PitFuelCount? PitMinFuel { get; init; }

    /// <summary>0-23.</summary>
    public int? StartHour { get; init; }

    /// <summary>Populates day/month/year. Only meaningful when DateType is Custom.</summary>
    public DateTime? RaceDate { get; init; }

    public DurationType? DurationType { get; init; }

    /// <summary>Laps if DurationType is LapBased, minutes if TimeBased.</summary>
    public int? DurationValue { get; init; }

    /// <summary>Race session's weather - see SessionWeatherConfig's doc comment.</summary>
    public SessionWeatherConfig? Weather { get; init; }
}

/// <summary>
/// Up to 4 weather slots for one session - CONFIRMED: count at Vm550Slot.WeatherSlotCount
/// (clamped 0-4), values at Vm550Slot.WeatherSlot1-4 in order. AMS2 itself repeats the last given
/// slot when fewer than 4 are supplied (its own documented behavior) - this library writes
/// exactly the slots you give it and lets AMS2 handle the repeat, rather than pre-filling the
/// remaining slots itself.
///
/// UNCONFIRMED whether writing slot values alone is enough, or whether - mirroring RaceDate
/// needing DateType=Custom first - there's a separate RealHistoric-vs-Custom weather-mode slot
/// that also needs setting. No such slot was identified via static analysis. If slots write and
/// verify but the weather doesn't visibly change in-game, that's the first thing to suspect.
/// </summary>
public sealed class SessionWeatherConfig
{
    /// <summary>1-4 slots, in order. Null/empty = don't force (matches every other config's convention). More than 4 is rejected (reported via UnverifiedFields), not truncated.</summary>
    public IReadOnlyList<WeatherType>? Slots { get; init; }
    /// <summary>if true, slots are ignored and set to zero. otherwise, use the slots above. null = don't force.</summary>
    public bool? HistoricalWeather { get; init; }
}

/// <summary>
/// Practice or Qualifying session configuration - see Native/SessionVmResolver.cs's doc comment
/// for the background. Applied against that session's own separately-resolved VM pointer (NOT the
/// main VM550 SessionRulesConfig targets), reusing the same Vm550Slot numbers on the theory that
/// Practice1/Qualifying1 are structurally-identical VM550 instances.
/// </summary>
public sealed class PracticeQualifySessionConfig
{
    /// <summary>
    /// On/off. CONFIRMED: always writes three slots together as a group
    /// (Vm550Slot.SessionEnabled/SessionEnabledPaired1/2) rather than a single flag - see those
    /// constants' doc comments for the exact pattern this replicates.
    /// </summary>
    public OnOff? Enabled { get; init; }

    /// <summary>
    /// Minutes. Unlike the race session (SessionRulesConfig.DurationType/DurationValue), AMS2's
    /// Practice/Qualifying sessions are always time-based - there's no lap-based option for them -
    /// so there's no DurationType here; DurationTypeFlag (slot 7) is written as TimeBased(0)
    /// automatically whenever DurationValue is supplied.
    /// </summary>
    public int? DurationValue { get; init; }

    /// <summary>0-23. UNCONFIRMED whether this session's own VM has a meaningful Hour slot independent of the main VM550's.</summary>
    public int? StartHour { get; init; }

    /// <summary>This session's weather - see SessionWeatherConfig's doc comment.</summary>
    public SessionWeatherConfig? Weather { get; init; }
}

/// <summary>
/// Result of an ApplyRaceConfigAsync call. A write that was attempted but didn't read back
/// correctly is reported, not silently ignored.
/// </summary>
public sealed class RaceConfigResult
{
    public bool Success => UnverifiedFields.Count == 0 && Errors.Count == 0;

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<(string Field, int Wanted, int? Got)> UnverifiedFields { get; init; } =
        Array.Empty<(string, int, int?)>();
}
