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
    // multiclass not implemented yet in this library.
    //Multiclass = 2,
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

/// <summary>
/// UNCONFIRMED against AMS2AVX.exe's own data - a text search of the binary for this catalogue's
/// wording (e.g. "partly cloudy", "thunderstorm", "dense fog") found no match, so despite an
/// earlier version of this comment claiming "exact wording" read from the game, that provenance
/// doesn't hold up and the value->weather mapping below should be treated as unverified (sourced
/// from somewhere other than a direct read of this binary, or simply wrong) until it's re-derived
/// - e.g. from live-testing each value in-game, or from wherever the original wording actually
/// came from. Values 9-11 are absent below; unknown whether that's a real gap in AMS2's own
/// catalogue or just an artifact of this unconfirmed mapping.
///
/// Fog/FogWithRain names here are this library's own naming for what were assumed to be "mist"/
/// "misty with rain" catalogue entries (0x10/0xd) - inferred by elimination, not a direct match on
/// any label text actually found in the binary.
/// </summary>
public enum WeatherType
{
    Clear = 0,
    LightClouds = 1,
    MediumClouds = 2,
    HeavyClouds = 3,
    Overcast = 4,
    LightRain = 5,
    Rain = 6,
    Storm = 7,
    Thunderstorm = 8,
    Hazy = 12,
    FogWithRain = 13,
    HeavyFog = 14,
    HeavyFogWithRain = 15,
    Fog = 16,
    Random = 17,
}
