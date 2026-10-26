using System.Text;

namespace Ams2Interop.Native;

/// <summary>
/// Reads/writes the player's identity in AMS2's profile - the forename, surname, nationality and
/// three-letter handle of its "Edit info" screen, plus the full name the game composes from them.
/// They live in the same settings block as the gameplay settings (see
/// Ams2Constants.GameplaySettingsPointerRva), at Ams2Constants.PlayerIdentityOffset.
///
/// The names are pointers to AMS2's own reference-counted strings. A new name is written as new
/// strings in the same layout, in memory allocated in the game, and the profile pointed at them;
/// they're given a use count the game can never release down to zero, so it never tries to free
/// memory it didn't allocate. The game picks the change up on the next screen and session; the
/// full name has to be written too, or the first session loaded still shows the old one.
///
/// Like the gameplay settings, this is the player's own data: a caller that changes it is
/// expected to put it back afterwards (TryApply hands back what it found, TryRestore writes it).
///
/// SAFETY: nothing is written unless every name field points at a string in the expected layout.
/// Anything else means the block isn't laid out as this code expects (most likely an AMS2 update
/// moved it), and writing would corrupt whatever actually lives there.
/// </summary>
public sealed class PlayerIdentityWriter
{
    // [8-byte owner pointer][tag "BLOW" + 4 zero bytes][u16 length][u16 capacity][u32 use count]
    // [UTF-16 text, NUL]; the profile's pointer is to the text, past this header.
    private const int HeaderSize = 24;
    private const int TagOffset = -16, LengthOffset = -8, CapacityOffset = -6;
    private static readonly byte[] Tag = Encoding.ASCII.GetBytes("BLOW");
    private const uint NeverReleased = 0x40000000;
    private const int MaxNameLength = 0x80, MaxHandleLength = 3;

    private readonly ProcessMemory _mem;
    private readonly long _moduleBase;
    private readonly Action<string>? _log;

    public PlayerIdentityWriter(ProcessMemory mem, long moduleBase, Action<string>? log = null)
    {
        _mem = mem;
        _moduleBase = moduleBase;
        _log = log;
    }

    /// <summary>The current identity, or null if the profile can't be resolved or doesn't look as expected.</summary>
    public PlayerIdentity? Read()
    {
        if (!TryResolveBlock(out var block)) return null;
        if (!_mem.TryReadInt32(block + Ams2Constants.PlayerIdentityOffset.Country, out var country))
        {
            _log?.Invoke("PlayerIdentity: nationality could not be read");
            return null;
        }

        var forename = ReadString(block + Ams2Constants.PlayerIdentityOffset.Forename);
        var surname = ReadString(block + Ams2Constants.PlayerIdentityOffset.Surname);
        var handle = ReadString(block + Ams2Constants.PlayerIdentityOffset.Handle);
        var fullName = ReadString(block + Ams2Constants.PlayerIdentityOffset.FullName);
        if (forename == null || surname == null || handle == null || fullName == null)
        {
            _log?.Invoke("PlayerIdentity: a name field doesn't point at a string in the expected layout - unexpected layout, not touching it");
            return null;
        }

        return new PlayerIdentity(forename, surname, handle, unchecked((uint)country));
    }

    /// <summary>
    /// Sets the player's identity. `original` is what was there before, for a later TryRestore.
    /// Returns false - having written nothing - if the profile can't be read or a value is out of range.
    /// </summary>
    public bool TryApply(PlayerIdentity identity, out PlayerIdentity original)
    {
        original = default!;
        if (!IsValid(identity)) return false;
        if (Read() is not PlayerIdentity current) return false;
        original = current;
        return Write(identity);
    }

    /// <summary>Writes back an identity previously handed out by TryApply.</summary>
    public bool TryRestore(PlayerIdentity original) =>
        // Same layout guard as TryApply - never write into a profile that doesn't read back sanely.
        IsValid(original) && Read() is not null && Write(original);

    private bool IsValid(PlayerIdentity identity)
    {
        if (string.IsNullOrWhiteSpace(identity.Forename) || string.IsNullOrWhiteSpace(identity.Surname) ||
            identity.Forename.Length > MaxNameLength || identity.Surname.Length > MaxNameLength ||
            string.IsNullOrWhiteSpace(identity.Handle) || identity.Handle.Length > MaxHandleLength ||
            identity.CountryId == 0)
        {
            _log?.Invoke($"PlayerIdentity: '{identity.Forename}' '{identity.Surname}' '{identity.Handle}' 0x{identity.CountryId:X8} out of range");
            return false;
        }
        return true;
    }

    private bool Write(PlayerIdentity identity)
    {
        if (!TryResolveBlock(out var block)) return false;

        var fields = new (int Offset, string Text)[]
        {
            (Ams2Constants.PlayerIdentityOffset.Forename, identity.Forename),
            (Ams2Constants.PlayerIdentityOffset.Surname, identity.Surname),
            (Ams2Constants.PlayerIdentityOffset.Handle, identity.Handle),
            (Ams2Constants.PlayerIdentityOffset.FullName, identity.FullName),
        };

        // One allocation for the four strings, each laid out like the game's own and carrying the
        // owner pointer of the string it replaces.
        var blocks = new List<(int Offset, int Start, byte[] Bytes)>();
        int size = 0;
        foreach (var (offset, text) in fields)
        {
            if (!_mem.TryReadInt64(block + offset, out var old) || !_mem.TryReadInt64(old - HeaderSize, out var owner))
            {
                _log?.Invoke($"PlayerIdentity: string at +0x{offset:X} could not be read");
                return false;
            }
            var bytes = BuildString(owner, text);
            blocks.Add((offset, size, bytes));
            size += (bytes.Length + 7) & ~7;
        }

        var memory = NativeMethods.VirtualAllocEx(_mem.Handle, IntPtr.Zero, (uint)size,
            NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
        if (memory == IntPtr.Zero)
        {
            _log?.Invoke("PlayerIdentity: could not allocate the new names in the game");
            return false;
        }

        long baseAddress = (long)memory;
        foreach (var (_, start, bytes) in blocks)
        {
            if (!_mem.WriteRaw(baseAddress + start, bytes))
            {
                _log?.Invoke("PlayerIdentity: could not write the new names into the game");
                return false;
            }
        }

        // Only now, with every string in place, point the profile at them.
        _mem.TryWriteInt32(block + Ams2Constants.PlayerIdentityOffset.Country, unchecked((int)identity.CountryId));
        foreach (var (offset, start, _) in blocks)
            _mem.WriteRaw(block + offset, BitConverter.GetBytes(baseAddress + start + HeaderSize));

        var landed = Read() is PlayerIdentity after && after == identity &&
                     ReadString(block + Ams2Constants.PlayerIdentityOffset.FullName) == identity.FullName;
        if (!landed) _log?.Invoke($"PlayerIdentity: write of '{identity.FullName}' ({identity.Handle}, 0x{identity.CountryId:X8}) did not read back");
        return landed;
    }

    private static byte[] BuildString(long owner, string text)
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

    // The string a profile field points at, or null if it isn't one in the expected layout.
    private string? ReadString(long field)
    {
        // any user-mode address: the game's strings sit low as well as high, and so do ours
        if (!_mem.TryReadInt64(field, out var text) || unchecked((ulong)text) - 0x10000UL >= 0x7FFFFFFF0000UL)
            return null;
        if (!_mem.ReadRaw(text + TagOffset, 8, out var header) || !header.AsSpan(0, 4).SequenceEqual(Tag)) return null;
        if (!_mem.ReadRaw(text + LengthOffset, 4, out var sizes)) return null;
        int length = BitConverter.ToUInt16(sizes, 0), capacity = BitConverter.ToUInt16(sizes, 2);
        if (length > capacity || length > 0x400) return null;
        if (!_mem.ReadRaw(text, length * 2, out var chars)) return null;
        return Encoding.Unicode.GetString(chars);
    }

    private bool TryResolveBlock(out long block)
    {
        // The same block as GameplaySettings: only "a user-mode address" is required.
        if (_mem.TryReadInt64(_moduleBase + Ams2Constants.GameplaySettingsPointerRva, out block) &&
            unchecked((ulong)block) - 0x10000UL < 0x7FFFFFFF0000UL)
        {
            return true;
        }

        _log?.Invoke("PlayerIdentity: settings block pointer not resolved");
        block = 0;
        return false;
    }
}
