using System.Text;

namespace Ams2Interop.Native;

/// <summary>
/// AMS2's own reference-counted string layout, as the profile's name fields point at it:
/// <code>
/// [8-byte owner pointer][tag "BLOW" + 4 zero bytes][u16 length][u16 capacity][u32 use count]
/// [UTF-16 text, NUL]
/// </code>
/// A field's pointer is to the text, past this header. Shared by PlayerIdentityWriter (reading
/// and building names) and SettingsBlockLayout (finding the identity fields by their strings).
/// </summary>
public static class Ams2String
{
    public const int HeaderSize = 24;
    public const int TagOffset = -16, LengthOffset = -8;
    public const int MaxLength = 0x400;
    private const uint NeverReleased = 0x40000000;
    private static readonly byte[] Tag = Encoding.ASCII.GetBytes("BLOW");

    /// <summary>Any user-mode address: the game's strings sit low as well as high, and so do ours.</summary>
    public static bool IsUserModeAddress(long address) => unchecked((ulong)address) - 0x10000UL < 0x7FFFFFFF0000UL;

    /// <summary>The string a field (a pointer to the text) points at, or null if it isn't one in the expected layout.</summary>
    public static string? TryReadField(IMemoryReader mem, long field) =>
        mem.TryReadInt64(field, out var text) ? TryReadAt(mem, text) : null;

    /// <summary>The string whose text starts at <paramref name="text"/>, or null if there's no string in the expected layout there.</summary>
    public static string? TryReadAt(IMemoryReader mem, long text)
    {
        if (!IsUserModeAddress(text)) return null;
        if (!mem.ReadRaw(text + TagOffset, 8, out var header) || !header.AsSpan(0, 4).SequenceEqual(Tag)) return null;
        if (!mem.ReadRaw(text + LengthOffset, 4, out var sizes)) return null;
        int length = BitConverter.ToUInt16(sizes, 0), capacity = BitConverter.ToUInt16(sizes, 2);
        if (length > capacity || length > MaxLength) return null;
        if (length == 0) return "";
        if (!mem.ReadRaw(text, length * 2, out var chars)) return null;
        return Encoding.Unicode.GetString(chars);
    }

    /// <summary>
    /// A new string in the game's layout, carrying <paramref name="owner"/> (the owner pointer of
    /// the string it replaces) and a use count the game can never release down to zero, so it
    /// never tries to free memory it didn't allocate.
    /// </summary>
    public static byte[] Build(long owner, string text)
    {
        var chars = Encoding.Unicode.GetBytes(text + "\0");
        var bytes = new byte[HeaderSize + chars.Length];
        BitConverter.GetBytes(owner).CopyTo(bytes, 0);
        Tag.CopyTo(bytes, 8);
        BitConverter.GetBytes((ushort)text.Length).CopyTo(bytes, 16);
        BitConverter.GetBytes((ushort)text.Length).CopyTo(bytes, 18);
        BitConverter.GetBytes(NeverReleased).CopyTo(bytes, 20);
        chars.CopyTo(bytes, HeaderSize);
        return bytes;
    }
}
