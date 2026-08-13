namespace Ams2Interop.Native;

/// <summary>
/// VM498's own directly-addressed packed local-date field - see Ams2Constants.Vm498DatePackedOffset's
/// doc comment for where this was found and what's confirmed/unconfirmed about it. This is a
/// PLAIN STRUCT FIELD, not an int-property wrapper node reached via `vmBase + slot*8` the way
/// VM550's Day/Month/Year slots (53/54/55) are - so it's read/written with a raw memory access
/// (ProcessMemory.TryReadInt32/TryWriteInt32), not the property-node/setter-call path SlotWriter
/// uses for everything else.
///
/// Bit layout, from AMS2AVX.exe's VM498 constructor (FUN_1403eaab0):
/// <code>
/// *(uint*)(vm498 + 0x188) =
///     (((uint)year &lt;&lt; 4 | (uint)month &amp; 0xf) &lt;&lt; 5 | (uint)day &amp; 0x1f)
///     | (existing &amp; 0xfe000000);   // top 7 bits preserved - unknown flags, not touched here
/// </code>
/// i.e. bits 0-4 = day, bits 5-8 = month, bits 9-24 = year, bits 25-31 = untouched/unknown flags.
///
/// CONFIRMED (live-tested): writing ONLY this field does NOT, by itself, change the date AMS2
/// actually uses - so this alone is not "the" commit mechanism. Ams2RaceConfigurator writes this
/// ALONGSIDE the VM550 slots (not instead of) on the theory that AMS2 needs both in agreement -
/// that combined theory is itself confirmed working live (the actual race data lands correctly),
/// though AMS2's own Custom Race menu display can still show a stale value until the submenu is
/// re-visited - a cosmetic-only gap, not a functional one.
/// </summary>
public static class Vm498PackedDate
{
    public readonly record struct Decoded(int Day, int Month, int Year, uint PreservedFlagsBits);

    public static Decoded Decode(uint raw)
    {
        var day = (int)(raw & 0x1Fu);
        var x = (raw >> 5) & 0xFFFFFu; // 20 bits: month(4) | year(16)
        var month = (int)(x & 0xFu);
        var year = (int)(x >> 4);
        var flags = raw & 0xFE000000u;
        return new Decoded(day, month, year, flags);
    }

    public static uint Encode(int year, int month, int day, uint preserveFlagsBits)
    {
        var packed = (((uint)year << 4) | ((uint)month & 0xFu)) << 5 | ((uint)day & 0x1Fu);
        return (packed & 0x01FFFFFFu) | (preserveFlagsBits & 0xFE000000u);
    }
}
