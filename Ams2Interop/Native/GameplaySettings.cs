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
/// SAFETY: nothing is written unless both fields currently hold a valid index (0-8). A value
/// outside that range means the block's layout isn't what this code expects (most likely an AMS2
/// update moved it), and writing would corrupt whatever actually lives there.
/// </summary>
public sealed class GameplaySettings
{
    private const int MaxIndex = 8;

    private readonly ProcessMemory _mem;
    private readonly long _moduleBase;
    private readonly Action<string>? _log;

    public GameplaySettings(ProcessMemory mem, long moduleBase, Action<string>? log = null)
    {
        _mem = mem;
        _moduleBase = moduleBase;
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

    /// <summary>Current values, or null if the settings block can't be resolved or doesn't look as expected.</summary>
    public WearSettingsSnapshot? Read()
    {
        if (!TryResolveBlock(out var block)) return null;
        if (!_mem.TryReadInt32(block + Ams2Constants.GameplaySettingsOffset.TyreWear, out var tyre) ||
            !_mem.TryReadInt32(block + Ams2Constants.GameplaySettingsOffset.FuelUsage, out var fuel))
        {
            _log?.Invoke("GameplaySettings: tyre wear/fuel usage could not be read");
            return null;
        }

        if (tyre is < 0 or > MaxIndex || fuel is < 0 or > MaxIndex)
        {
            _log?.Invoke($"GameplaySettings: tyre wear={tyre}, fuel usage={fuel} out of range 0-{MaxIndex} - unexpected layout, not touching it");
            return null;
        }

        return new WearSettingsSnapshot(tyre, fuel);
    }

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

        if (Read() is not WearSettingsSnapshot current) return false;
        original = current;

        return Write(new WearSettingsSnapshot(tyreIndex ?? current.TyreWearIndex, fuelIndex ?? current.FuelUsageIndex));
    }

    /// <summary>Writes back values previously handed out by TryApply.</summary>
    public bool TryRestore(WearSettingsSnapshot original)
    {
        if (original.TyreWearIndex is < 0 or > MaxIndex || original.FuelUsageIndex is < 0 or > MaxIndex) return false;
        // Same layout guard as TryApply - never write into a block that doesn't read back sanely.
        return Read() is not null && Write(original);
    }

    private bool Write(WearSettingsSnapshot values)
    {
        if (!TryResolveBlock(out var block)) return false;

        _mem.TryWriteInt32(block + Ams2Constants.GameplaySettingsOffset.TyreWear, values.TyreWearIndex);
        _mem.TryWriteInt32(block + Ams2Constants.GameplaySettingsOffset.FuelUsage, values.FuelUsageIndex);

        var landed = Read() is WearSettingsSnapshot after && after == values;
        if (!landed) _log?.Invoke($"GameplaySettings: write of tyre wear={values.TyreWearIndex}, fuel usage={values.FuelUsageIndex} did not read back");
        return landed;
    }

    private bool TryResolveBlock(out long block)
    {
        // Wider than ProcessMemory.TryReadPointerSafe's heap-shaped range on purpose: this block
        // isn't one of the Custom Race heap objects, so only "a user-mode address" is required.
        if (_mem.TryReadInt64(_moduleBase + Ams2Constants.GameplaySettingsPointerRva, out block) &&
            unchecked((ulong)block) - 0x10000UL < 0x7FFFFFFF0000UL)
        {
            return true;
        }

        _log?.Invoke("GameplaySettings: settings block pointer not resolved");
        block = 0;
        return false;
    }
}
