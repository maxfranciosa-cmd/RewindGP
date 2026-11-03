namespace Ams2Interop.Native;

/// <summary>
/// The out-of-process reads the layout checks need - implemented by ProcessMemory, and by fakes
/// in tests, so every check can run against synthetic memory without a game process.
/// </summary>
public interface IMemoryReader
{
    bool TryReadInt64(long address, out long value);
    bool TryReadInt32(long address, out int value);
    bool ReadRaw(long address, int length, out byte[] buffer);
}

/// <summary>
/// An IMemoryReader that can also list its readable memory - what MemoryScanner sweeps. ProcessMemory
/// lists the target's committed, private, readable pages; fakes list their mapped regions.
/// </summary>
public interface IScannableMemory : IMemoryReader
{
    IEnumerable<(long Base, long Size)> ReadableRegions();
}

/// <summary>Raw out-of-process writes (ProcessMemory) - see ProcessMemory.TryWriteInt32's doc comment for the caveats.</summary>
public interface IMemoryWriter
{
    bool TryWriteInt32(long address, int value);
    bool WriteRaw(long address, byte[] buffer);
}

/// <summary>Writes one int-property slot through AMS2's own setter (SlotWriter), behind an interface so tests can record writes instead.</summary>
public interface ISlotSetter
{
    bool TrySetSlot(long vmBase, int slot, int value, out int actualValue);
}

/// <summary>Why a slot read didn't produce a value - see MemoryReads.ReadSlot.</summary>
public enum SlotReadStatus
{
    Ok,
    /// <summary>`*(vmBase + slot*8)` couldn't be read or isn't a plausible heap pointer.</summary>
    NodeUnreadable,
    /// <summary>The node's tag at node-0x18 isn't IntPropertyTypeTag - not an int property, or not this layout.</summary>
    WrongTag,
    /// <summary>The value at node+0x18c couldn't be read.</summary>
    ValueUnreadable
}

/// <summary>The read conventions ProcessMemory uses, over any IMemoryReader.</summary>
public static class MemoryReads
{
    /// <summary>Offset of an int property's value inside its node. Same for every slot of VM498/VM550/master.</summary>
    public const int SlotValueOffset = 0x18c;

    /// <summary>Offset back from the node to its type tag (the property descriptor's first qword).</summary>
    public const int SlotTagOffset = -0x18;

    /// <summary>See ProcessMemory.TryReadPointerSafe.</summary>
    public static bool TryReadPointerSafe(this IMemoryReader mem, long address, out long pointer)
    {
        if (!mem.TryReadInt64(address, out pointer)) return false;
        var biased = unchecked((ulong)pointer) - 0x100000000UL;
        return biased < 0x700000000UL;
    }

    /// <summary>
    /// An int-property slot's value, exactly as ProcessMemory.TryReadSlot reads it (node =
    /// *(vmBase + slot*8), tag at node-0x18 must equal <paramref name="intPropertyTag"/>, value at
    /// node+0x18c), but saying which step failed.
    /// </summary>
    public static SlotReadStatus ReadSlot(this IMemoryReader mem, long vmBase, int slot, long intPropertyTag, out int value)
    {
        value = 0;
        if (!mem.TryReadPointerSafe(vmBase + slot * 8L, out var node)) return SlotReadStatus.NodeUnreadable;
        if (!mem.TryReadInt64(node + SlotTagOffset, out var tag) || tag != intPropertyTag) return SlotReadStatus.WrongTag;
        return mem.TryReadInt32(node + SlotValueOffset, out value) ? SlotReadStatus.Ok : SlotReadStatus.ValueUnreadable;
    }

    public static bool TryReadSlot(this IMemoryReader mem, long vmBase, int slot, long intPropertyTag, out int value) =>
        mem.ReadSlot(vmBase, slot, intPropertyTag, out value) == SlotReadStatus.Ok;
}
