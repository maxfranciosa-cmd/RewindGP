namespace Ams2Interop.Native;

/// <summary>
/// Reads/writes AMS2's global tyre-wear and fuel-usage gameplay settings (the ones under
/// Options, not part of Custom Race). These are plain int fields in one big settings block
/// reached through a single module-global pointer (see Ams2Constants.GameplaySettingsPointerRva)
/// - no property node, no setter - so this is a raw memory read/write, like Vm498PackedDate.
///
/// Because they're the player's own global settings, a caller that changes them is expected to
/// put them back afterwards: TryApply hands back the values it found, TryRestore writes them back.
///
/// SAFETY: the fields are found relative to the player identity (see SettingsBlockLayout), which
/// can be recognised in the block, rather than trusted at fixed offsets - many neighbouring
/// settings are also 0-8, so a range check alone wouldn't catch a shifted layout. On top of that,
/// nothing is written unless both fields currently hold a valid index (0-8), and TryRestore only
/// writes to the offsets TryApply read from (WearSettingsSnapshot.LayoutAnchor).
/// </summary>
public sealed class GameplaySettings
{
    private const int MaxIndex = SettingsBlockLayout.MaxWearIndex;

    private readonly IMemoryReader _mem;
    private readonly IMemoryWriter _writer;
    private readonly SettingsBlockAccess _block;
    private readonly Action<string>? _log;

    public GameplaySettings(IMemoryReader mem, IMemoryWriter writer, SettingsBlockAccess block, Action<string>? log = null)
    {
        _mem = mem;
        _writer = writer;
        _block = block;
        _log = log;
    }

    /// <summary>Tyre-wear multiplier (1-7) -> the setting's stored index.</summary>
    public static int? TyreWearIndex(int multiplier) => multiplier is >= 1 and <= 7 ? 7 - multiplier : null;

    /// <summary>
    /// Fuel-usage multiplier (2-5) -> the setting's stored index. x1 is deliberately not accepted:
    /// its index isn't established, so "normal fuel usage" is reached by restoring the player's
    /// own value rather than by writing one.
    /// </summary>
    public static int? FuelUsageIndex(int multiplier) => multiplier is >= 2 and <= 5 ? 8 - multiplier : null;

    /// <summary>The wear settings' offsets are resolved (and re-validated) for this build - Ams2RaceConfigurator.CanChangeWearSettings.</summary>
    public bool IsAvailable => Layout() != null;

    /// <summary>Current values, or null if the settings block can't be resolved or doesn't look as expected.</summary>
    public WearSettingsSnapshot? Read() => Layout() is SettingsBlockLayout layout ? Read(layout) : null;

    /// <summary>
    /// Sets tyre wear and/or fuel usage to the given multipliers (null = leave alone). `original`
    /// is what was there before, for a later TryRestore. Returns false - having written nothing -
    /// if the block can't be read or a multiplier is out of range.
    /// </summary>
    public bool TryApply(int? tyreWearMultiplier, int? fuelUsageMultiplier, out WearSettingsSnapshot original)
    {
        original = default;

        int? tyreIndex = null;
        if (tyreWearMultiplier is int tyreMultiplier && (tyreIndex = TyreWearIndex(tyreMultiplier)) is null)
        {
            _log?.Invoke($"GameplaySettings: tyre wear multiplier {tyreMultiplier} out of range 1-7");
            return false;
        }

        int? fuelIndex = null;
        if (fuelUsageMultiplier is int fuelMultiplier && (fuelIndex = FuelUsageIndex(fuelMultiplier)) is null)
        {
            _log?.Invoke($"GameplaySettings: fuel usage multiplier {fuelMultiplier} out of range 2-5");
            return false;
        }

        if (Layout() is not SettingsBlockLayout layout || Read(layout) is not WearSettingsSnapshot current) return false;
        original = current;

        return Write(layout, new WearSettingsSnapshot(tyreIndex ?? current.TyreWearIndex, fuelIndex ?? current.FuelUsageIndex));
    }

    /// <summary>Writes back values previously handed out by TryApply - only to the offsets they were read from.</summary>
    public bool TryRestore(WearSettingsSnapshot original)
    {
        if (original.TyreWearIndex is < 0 or > MaxIndex || original.FuelUsageIndex is < 0 or > MaxIndex) return false;
        if (Layout() is not SettingsBlockLayout layout) return false;
        if (original.LayoutAnchor is int anchor && anchor != layout.CountryOffset)
        {
            _log?.Invoke($"GameplaySettings: the settings were read with the identity at +0x{anchor:X}, it now resolves at +0x{layout.CountryOffset:X} - not restoring into a different layout");
            return false;
        }
        // Same layout guard as TryApply - never write into a block that doesn't read back sanely.
        return Read(layout) is not null && Write(layout, original);
    }

    private SettingsBlockLayout? Layout()
    {
        var layout = _block.Current();
        if (layout is { WearResolved: true }) return layout;
        if (layout != null) _log?.Invoke("GameplaySettings: tyre wear/fuel usage not resolved in the settings block for this AMS2 version - not touching them");
        return null;
    }

    private WearSettingsSnapshot? Read(SettingsBlockLayout layout)
    {
        if (!_mem.TryReadInt32(layout.Block + layout.TyreWearOffset, out var tyre) ||
            !_mem.TryReadInt32(layout.Block + layout.FuelUsageOffset, out var fuel))
        {
            _log?.Invoke("GameplaySettings: tyre wear/fuel usage could not be read");
            return null;
        }

        if (tyre is < 0 or > MaxIndex || fuel is < 0 or > MaxIndex)
        {
            _log?.Invoke($"GameplaySettings: tyre wear={tyre}, fuel usage={fuel} out of range 0-{MaxIndex} - unexpected layout, not touching it");
            return null;
        }

        return new WearSettingsSnapshot(tyre, fuel) { LayoutAnchor = layout.CountryOffset };
    }

    private bool Write(SettingsBlockLayout layout, WearSettingsSnapshot values)
    {
        _writer.TryWriteInt32(layout.Block + layout.TyreWearOffset, values.TyreWearIndex);
        _writer.TryWriteInt32(layout.Block + layout.FuelUsageOffset, values.FuelUsageIndex);

        var landed = Read(layout) is WearSettingsSnapshot after
                     && after.TyreWearIndex == values.TyreWearIndex && after.FuelUsageIndex == values.FuelUsageIndex;
        if (!landed) _log?.Invoke($"GameplaySettings: write of tyre wear={values.TyreWearIndex}, fuel usage={values.FuelUsageIndex} did not read back");
        return landed;
    }
}
