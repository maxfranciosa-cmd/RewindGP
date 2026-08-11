namespace Ams2Interop.Native;

/// <summary>
/// Writes one VM498/VM550 slot by calling AMS2's own setter function. This is deliberately NOT
/// a memory write: it resolves the slot's property-wrapper object, validates its type tag, then
/// calls the game's own setter with that object and the desired value - a raw poke crashes on
/// some fields (RollingStart among them), so this goes through AMS2's own setter for every field
/// instead.
/// </summary>
public sealed class SlotWriter
{
    private readonly ProcessMemory _mem;
    private readonly RemoteExecutor _exec;
    private readonly long _setterAddress;

    public SlotWriter(ProcessMemory mem, RemoteExecutor exec, long moduleBase)
    {
        _mem = mem;
        _exec = exec;
        // See Ams2Constants.SetterRvaDefaultBase's doc comment for why this isn't a plain
        // moduleBase+RVA computation - the real address depends on whether the module loaded at
        // its default preferred base (the observed-in-practice case) or was relocated.
        _setterAddress = Ams2Constants.ResolveSetterAddress(moduleBase);
    }

    /// <summary>
    /// Writes value into vmBase's given slot, then reads it back to confirm it landed. Returns
    /// false if the slot couldn't be resolved/validated OR the write didn't land; actualValue
    /// reports what's actually there afterward either way, for diagnostics.
    /// </summary>
    public bool TrySetSlot(long vmBase, int slot, int value, out int actualValue)
    {
        actualValue = 0;
        if (!_mem.TryReadPointerSafe(vmBase + slot * 8L, out var node)) return false;
        if (!_mem.TryReadInt64(node - 0x18, out var tag) || tag != Ams2Constants.IntPropertyTypeTag) return false;

        var propertyDescriptor = node - 0x18;
        _exec.Call(_setterAddress, propertyDescriptor, value);

        return _mem.TryReadInt32(node + 0x18c, out actualValue) && actualValue == value;
    }
}
