namespace Ams2Interop;

/// <summary>Names of the patch-sensitive values - the keys of signature files, offset caches and Ams2ResolvedOffsets.</summary>
public static class Ams2OffsetNames
{
    // code (function start RVAs)
    public const string Setter = "Setter";
    public const string SetCar = "SetCar";
    public const string Commit = "Commit";

    // data (RVAs of addresses the code references)
    public const string IntPropertyTypeTag = "IntPropertyTypeTag";
    public const string Vm498IdentityTag = "Vm498IdentityTag";
    public const string Vm550IdentityTag = "Vm550IdentityTag";
    public const string Vm498ContainerTag = "Vm498ContainerTag";
    public const string GameplaySettingsPointer = "GameplaySettingsPointer";

    public static readonly IReadOnlyList<string> Code = new[] { Setter, SetCar, Commit };

    public static readonly IReadOnlyList<string> Data = new[]
    {
        IntPropertyTypeTag, Vm498IdentityTag, Vm550IdentityTag, Vm498ContainerTag, GameplaySettingsPointer
    };

    public static readonly IReadOnlyList<string> All = Code.Concat(Data).ToArray();

    /// <summary>The values of the build Ams2Constants.BuiltIn was derived for, as RVAs.</summary>
    public static IReadOnlyDictionary<string, long> BuiltInRvas { get; } = new Dictionary<string, long>
    {
        [Setter] = Ams2Constants.BuiltIn.SetterRva,
        [SetCar] = Ams2Constants.BuiltIn.SetCarRva,
        [Commit] = Ams2Constants.BuiltIn.CommitRva,
        [IntPropertyTypeTag] = Ams2Constants.BuiltIn.IntPropertyTypeTag - Ams2Constants.DefaultImageBase,
        [Vm498IdentityTag] = Ams2Constants.BuiltIn.Vm498IdentityTag - Ams2Constants.DefaultImageBase,
        [Vm550IdentityTag] = Ams2Constants.BuiltIn.Vm550IdentityTag - Ams2Constants.DefaultImageBase,
        [Vm498ContainerTag] = Ams2Constants.BuiltIn.Vm498ContainerTag - Ams2Constants.DefaultImageBase,
        [GameplaySettingsPointer] = Ams2Constants.BuiltIn.GameplaySettingsPointerRva,
    };
}

public enum Ams2OffsetSource
{
    /// <summary>
    /// The hard-coded Ams2Constants.BuiltIn values, used as-is because there was nothing to check
    /// them against (no signature file yet, or the module couldn't be read). Same behaviour as
    /// before signatures existed.
    /// </summary>
    BuiltInUnverified,

    /// <summary>Values found by signature for this exact build on an earlier run.</summary>
    Cached,

    /// <summary>Values found by signature on this run.</summary>
    Scanned
}

/// <summary>
/// The patch-sensitive values for one running AMS2AVX.exe: RVAs of each named value, and which
/// ones couldn't be found. A feature whose values are missing is switched off (see the Can*
/// properties) instead of being run against addresses that may now point somewhere else.
/// </summary>
public sealed class Ams2ResolvedOffsets
{
    public long ModuleBase { get; }
    public string? BuildId { get; }
    public Ams2OffsetSource Source { get; }
    public IReadOnlyDictionary<string, long> Rvas { get; }
    public IReadOnlyList<string> Missing { get; }

    public Ams2ResolvedOffsets(long moduleBase, string? buildId, Ams2OffsetSource source, IReadOnlyDictionary<string, long> rvas)
    {
        ModuleBase = moduleBase;
        BuildId = buildId;
        Source = source;
        Rvas = rvas;
        Missing = Ams2OffsetNames.All.Where(n => !rvas.ContainsKey(n)).ToArray();
    }

    public static Ams2ResolvedOffsets BuiltIn(long moduleBase, string? buildId = null) =>
        new(moduleBase, buildId, Ams2OffsetSource.BuiltInUnverified, Ams2OffsetNames.BuiltInRvas);

    public bool Has(string name) => Rvas.ContainsKey(name);

    public long Rva(string name) =>
        Rvas.TryGetValue(name, out var rva)
            ? rva
            : throw new InvalidOperationException($"'{name}' isn't known for AMS2 build {BuildId ?? "?"} - check the matching Can* property before using it");

    public long Absolute(string name) => ModuleBase + Rva(name);

    /// <summary>VmResolver/SessionVmResolver can find the Custom Race objects and read their slots.</summary>
    public bool CanResolveCustomRace =>
        Has(Ams2OffsetNames.IntPropertyTypeTag) && Has(Ams2OffsetNames.Vm498IdentityTag)
        && Has(Ams2OffsetNames.Vm550IdentityTag) && Has(Ams2OffsetNames.Vm498ContainerTag);

    /// <summary>Slots can be written through AMS2's own setter.</summary>
    public bool CanWriteSlots => CanResolveCustomRace && Has(Ams2OffsetNames.Setter);

    public bool CanSetCar => CanResolveCustomRace && Has(Ams2OffsetNames.SetCar);

    public bool CanCommit => CanResolveCustomRace && Has(Ams2OffsetNames.Commit);

    public bool CanChangeGameplaySettings => Has(Ams2OffsetNames.GameplaySettingsPointer);

    /// <summary>True when some features are off because their values weren't found for this build.</summary>
    public bool IsDegraded => Missing.Count > 0;

    public string Describe() =>
        $"AMS2 build {BuildId ?? "unknown"}: offsets {Source}"
        + (Missing.Count > 0 ? $", NOT found: {string.Join(", ", Missing)}" : ", all found");
}

/// <summary>
/// The offsets every Ams2Interop class reads (through Ams2Constants) for the AMS2 process that was
/// attached last. Process-wide on purpose: the resolvers, SlotWriter and ProcessMemory read tags
/// and RVAs as plain Ams2Constants members, and there's only ever one AMS2AVX.exe to attach to.
/// Starts as the built-in values at the default image base, i.e. exactly the old constants.
/// </summary>
public static class Ams2Offsets
{
    private static volatile Ams2ResolvedOffsets _active = Ams2ResolvedOffsets.BuiltIn(Ams2Constants.DefaultImageBase);

    public static Ams2ResolvedOffsets Active => _active;

    public static void Activate(Ams2ResolvedOffsets offsets) => _active = offsets;
}
