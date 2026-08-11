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
