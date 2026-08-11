namespace Ams2Interop;

public enum NumOpponentsType
{
    MaxAvailable = 0,
    Custom = 1,
    ManualGrid = 2,
}

public enum OpponentsTypeKind
{
    Identical = 0,
    SameClass = 1,
    Multiclass = 2,
}

public enum AiMistakeFrequency
{
    Off = 0,
    Half = 1,        // x0.5
    Standard = 2,
    Double = 3,       // x2
    Triple = 4,       // x3
    Quadruple = 5,    // x4
    Quintuple = 6,    // x5
}

/// <summary>Reused for the several plain on/off VM550 fields.</summary>
public enum OnOff
{
    Off = 0,
    On = 1,
}

public enum StartType
{
    Grid = 0,
    Rolling = 1,
}

public enum TimeProgression
{
    RealTime = 1,
    X2 = 2,
    X5 = 3,
    X10 = 4,
}

public enum DateType
{
    Default = 0,
    Current = 1,
    Custom = 2,
}

/// <summary>Confirmed range from series_config_profiles.psv - only these four values are valid.</summary>
public enum PitTyreCount
{
    Zero = 0,
    Two = 2,
    Four = 4,
}

/// <summary>Confirmed range from series_config_profiles.psv - only these four values are valid.</summary>
public enum PitFuelCount
{
    Zero = 0,
    Two = 2,
    Four = 4,
}

public enum DurationType
{
    LapBased,
    TimeBased,
}
