namespace Ams2Interop.Native;

/// <summary>
/// Reads/writes the player's identity in AMS2's profile - the forename, surname, nationality and
/// three-letter handle of its "Edit info" screen, plus the full name the game composes from them.
/// They live in the same settings block as the gameplay settings (see
/// Ams2Constants.GameplaySettingsPointerRva), at the offsets SettingsBlockLayout resolves for the
/// running build (normally Ams2Constants.PlayerIdentityOffset).
///
/// The names are pointers to AMS2's own reference-counted strings (Ams2String). A new name is
/// written as new strings in the same layout, in memory allocated in the game, and the profile
/// pointed at them; they're given a use count the game can never release down to zero, so it
/// never tries to free memory it didn't allocate. The game picks the change up on the next screen
/// and session; the full name has to be written too, or the first session loaded still shows the
/// old one.
///
/// Like the gameplay settings, this is the player's own data: a caller that changes it is
/// expected to put it back afterwards (TryApply hands back what it found, TryRestore writes it -
/// only to the offsets TryApply read from, PlayerIdentity.LayoutAnchor).
///
/// SAFETY: nothing is written unless every name field points at a string in the expected layout,
/// and the nationality currently stored is non-zero and (when the caller supplies the set) a
/// known country id. Anything else means the block isn't laid out as this code expects (most
/// likely an AMS2 update moved it), and writing would corrupt whatever actually lives there.
/// </summary>
public sealed class PlayerIdentityWriter
{
    private const int MaxNameLength = 0x80, MaxHandleLength = 3;

    private readonly IMemoryReader _mem;
    private readonly IMemoryWriter _writer;
    private readonly SettingsBlockAccess _block;
    private readonly Func<int, long?> _allocate;
    private readonly Action<string>? _log;

    /// <param name="allocate">Allocates the given number of read/write bytes in the game and returns the address, or null - VirtualAllocEx in Ams2RaceConfigurator, a fake in tests.</param>
    public PlayerIdentityWriter(IMemoryReader mem, IMemoryWriter writer, SettingsBlockAccess block, Func<int, long?> allocate,
        Action<string>? log = null)
    {
        _mem = mem;
        _writer = writer;
        _block = block;
        _allocate = allocate;
        _log = log;
    }

    /// <summary>The identity's offsets are resolved (and re-validated) for this build - Ams2RaceConfigurator.CanChangePlayerIdentity.</summary>
    public bool IsAvailable => Layout() != null;

    /// <summary>The current identity, or null if the profile can't be resolved or doesn't look as expected.</summary>
    public PlayerIdentity? Read() => Layout() is SettingsBlockLayout layout ? Read(layout) : null;

    /// <summary>
    /// Sets the player's identity. `original` is what was there before, for a later TryRestore.
    /// Returns false - having written nothing - if the profile can't be read, a value is out of
    /// range, or the stored nationality isn't a plausible country id.
    /// </summary>
    /// <param name="knownCountryIds">AMS2's country ids, when the caller knows them: the stored and the new nationality must both be one.</param>
    public bool TryApply(PlayerIdentity identity, out PlayerIdentity original, IReadOnlySet<uint>? knownCountryIds = null)
    {
        original = default!;
        if (!IsValid(identity, knownCountryIds)) return false;
        if (Layout() is not SettingsBlockLayout layout || Read(layout) is not PlayerIdentity current) return false;
        if (!StoredCountryPlausible(current, knownCountryIds)) return false;
        original = current;
        return Write(layout, identity);
    }

    /// <summary>Writes back an identity previously handed out by TryApply - only to the offsets it was read from.</summary>
    public bool TryRestore(PlayerIdentity original, IReadOnlySet<uint>? knownCountryIds = null)
    {
        if (!IsValid(original, knownCountryIds)) return false;
        if (Layout() is not SettingsBlockLayout layout) return false;
        if (original.LayoutAnchor is int anchor && anchor != layout.CountryOffset)
        {
            _log?.Invoke($"PlayerIdentity: the identity was read at +0x{anchor:X}, it now resolves at +0x{layout.CountryOffset:X} - not restoring into a different layout");
            return false;
        }
        // Same layout guard as TryApply - never write into a profile that doesn't read back sanely.
        return Read(layout) is PlayerIdentity current && StoredCountryPlausible(current, knownCountryIds) && Write(layout, original);
    }

    private SettingsBlockLayout? Layout()
    {
        var layout = _block.Current();
        if (layout is { IdentityResolved: true }) return layout;
        if (layout != null) _log?.Invoke("PlayerIdentity: the player identity wasn't found in the settings block for this AMS2 version - not touching it");
        return null;
    }

    private PlayerIdentity? Read(SettingsBlockLayout layout)
    {
        var block = layout.Block;
        if (!_mem.TryReadInt32(block + layout.CountryOffset!.Value, out var country))
        {
            _log?.Invoke("PlayerIdentity: nationality could not be read");
            return null;
        }

        var forename = Ams2String.TryReadField(_mem, block + layout.ForenameOffset);
        var surname = Ams2String.TryReadField(_mem, block + layout.SurnameOffset);
        var handle = Ams2String.TryReadField(_mem, block + layout.HandleOffset);
        var fullName = Ams2String.TryReadField(_mem, block + layout.FullNameOffset);
        if (forename == null || surname == null || handle == null || fullName == null)
        {
            _log?.Invoke("PlayerIdentity: a name field doesn't point at a string in the expected layout - unexpected layout, not touching it");
            return null;
        }

        return new PlayerIdentity(forename, surname, handle, unchecked((uint)country)) { LayoutAnchor = layout.CountryOffset };
    }

    private bool StoredCountryPlausible(PlayerIdentity current, IReadOnlySet<uint>? knownCountryIds)
    {
        if (current.CountryId != 0 && (knownCountryIds == null || knownCountryIds.Contains(current.CountryId))) return true;
        _log?.Invoke($"PlayerIdentity: the stored nationality 0x{current.CountryId:X8} isn't a known country id - unexpected layout, not touching it");
        return false;
    }

    private bool IsValid(PlayerIdentity identity, IReadOnlySet<uint>? knownCountryIds)
    {
        if (string.IsNullOrWhiteSpace(identity.Forename) || string.IsNullOrWhiteSpace(identity.Surname) ||
            identity.Forename.Length > MaxNameLength || identity.Surname.Length > MaxNameLength ||
            string.IsNullOrWhiteSpace(identity.Handle) || identity.Handle.Length > MaxHandleLength ||
            identity.CountryId == 0 || (knownCountryIds != null && !knownCountryIds.Contains(identity.CountryId)))
        {
            _log?.Invoke($"PlayerIdentity: '{identity.Forename}' '{identity.Surname}' '{identity.Handle}' 0x{identity.CountryId:X8} out of range");
            return false;
        }
        return true;
    }

    private bool Write(SettingsBlockLayout layout, PlayerIdentity identity)
    {
        var block = layout.Block;
        var fields = new (int Offset, string Text)[]
        {
            (layout.ForenameOffset, identity.Forename),
            (layout.SurnameOffset, identity.Surname),
            (layout.HandleOffset, identity.Handle),
            (layout.FullNameOffset, identity.FullName),
        };

        // One allocation for the four strings, each laid out like the game's own and carrying the
        // owner pointer of the string it replaces.
        var blocks = new List<(int Offset, int Start, byte[] Bytes)>();
        int size = 0;
        foreach (var (offset, text) in fields)
        {
            if (!_mem.TryReadInt64(block + offset, out var old) || !_mem.TryReadInt64(old - Ams2String.HeaderSize, out var owner))
            {
                _log?.Invoke($"PlayerIdentity: string at +0x{offset:X} could not be read");
                return false;
            }
            var bytes = Ams2String.Build(owner, text);
            blocks.Add((offset, size, bytes));
            size += (bytes.Length + 7) & ~7;
        }

        if (_allocate(size) is not long baseAddress)
        {
            _log?.Invoke("PlayerIdentity: could not allocate the new names in the game");
            return false;
        }

        foreach (var (_, start, bytes) in blocks)
        {
            if (!_writer.WriteRaw(baseAddress + start, bytes))
            {
                _log?.Invoke("PlayerIdentity: could not write the new names into the game");
                return false;
            }
        }

        // Only now, with every string in place, point the profile at them.
        _writer.TryWriteInt32(block + layout.CountryOffset!.Value, unchecked((int)identity.CountryId));
        foreach (var (offset, start, _) in blocks)
            _writer.WriteRaw(block + offset, BitConverter.GetBytes(baseAddress + start + Ams2String.HeaderSize));

        var landed = Read(layout) is PlayerIdentity after && after.SameValues(identity) &&
                     Ams2String.TryReadField(_mem, block + layout.FullNameOffset) == identity.FullName;
        if (!landed) _log?.Invoke($"PlayerIdentity: write of '{identity.FullName}' ({identity.Handle}, 0x{identity.CountryId:X8}) did not read back");
        return landed;
    }
}
