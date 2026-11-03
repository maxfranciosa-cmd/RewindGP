namespace Ams2Interop.Native;

/// <summary>
/// Where the player identity and the wear settings actually are inside AMS2's settings block on
/// the running build. The block itself is found by signature (GameplaySettingsPointerRva), but
/// the fields inside it sit at fixed offsets (Ams2Constants.PlayerIdentityOffset /
/// GameplaySettingsOffset) that an update can shift. The identity is the anchor, because it can
/// be recognised: its four name fields are pointers to strings in AMS2's own layout (Ams2String),
/// something a shifted read essentially never imitates.
///
/// Resolve:
/// 1. The constant offsets, if the four name fields there (Forename, Surname, Handle, FullName,
///    +0x8 apart after Country) all point at valid strings.
/// 2. Otherwise a scan of the block's first ScanWindow bytes, 8-byte aligned, for EXACTLY ONE
///    position where four consecutive qwords each point at a valid string; Country is the qword
///    before it. Zero or several such positions leave the identity unresolved - never guessed.
/// 3. The wear settings, derived from the identity with Ams2Constants.SettingsBlockDelta, are
///    accepted only if both read as a valid index (0-8) there. Otherwise only the wear settings
///    are unresolved; the identity stays usable.
/// </summary>
public sealed class SettingsBlockLayout
{
    public const int ScanWindow = 0x80000;
    private const int ScanChunk = 0x10000;
    private const int NameFieldCount = 4;
    public const int MaxWearIndex = 8;

    /// <summary>The settings block's address the layout was resolved in.</summary>
    public long Block { get; }

    /// <summary>Offset of Country in the block; null when the identity couldn't be found (then nothing else is resolved either).</summary>
    public int? CountryOffset { get; }

    /// <summary>Tyre wear and fuel usage read as valid indices at their offsets relative to the identity.</summary>
    public bool WearResolved { get; }

    public bool IdentityResolved => CountryOffset != null;

    public int ForenameOffset => Country + 0x8;
    public int SurnameOffset => Country + 0x10;
    public int HandleOffset => Country + 0x18;
    public int FullNameOffset => Country + 0x20;
    public int TyreWearOffset => Country - Ams2Constants.SettingsBlockDelta.TyreWearBeforeCountry;
    public int FuelUsageOffset => Country - Ams2Constants.SettingsBlockDelta.FuelUsageBeforeCountry;

    private int Country => CountryOffset ?? throw new InvalidOperationException("the player identity wasn't found in the settings block");

    public SettingsBlockLayout(long block, int? countryOffset, bool wearResolved)
    {
        Block = block;
        CountryOffset = countryOffset;
        WearResolved = countryOffset != null && wearResolved;
    }

    public static SettingsBlockLayout Resolve(IMemoryReader mem, long block, Action<string>? log = null)
    {
        int? country = null;
        if (IdentityValidAt(mem, block, Ams2Constants.PlayerIdentityOffset.Country))
        {
            country = Ams2Constants.PlayerIdentityOffset.Country;
        }
        else
        {
            log?.Invoke("SettingsBlockLayout: the player identity isn't at its usual offsets - scanning the settings block for it");
            country = ScanForIdentity(mem, block, log);
            if (country is int moved)
                log?.Invoke($"SettingsBlockLayout: settings block moved: identity {FormatDelta(moved - Ams2Constants.PlayerIdentityOffset.Country)} (Country at +0x{moved:X})");
        }

        if (country is not int c)
        {
            log?.Invoke("SettingsBlockLayout: player identity NOT found in the settings block - identity and wear settings are off");
            return new SettingsBlockLayout(block, null, false);
        }

        var tyre = c - Ams2Constants.SettingsBlockDelta.TyreWearBeforeCountry;
        var fuel = c - Ams2Constants.SettingsBlockDelta.FuelUsageBeforeCountry;
        var wear = tyre >= 0 && mem.TryReadInt32(block + tyre, out var tyreIndex) && tyreIndex is >= 0 and <= MaxWearIndex
                   && mem.TryReadInt32(block + fuel, out var fuelIndex) && fuelIndex is >= 0 and <= MaxWearIndex;
        if (!wear)
            log?.Invoke($"SettingsBlockLayout: tyre wear/fuel usage don't read as valid indices at +0x{tyre:X}/+0x{fuel:X} (relative to the identity) - wear settings are off");

        return new SettingsBlockLayout(block, c, wear);
    }

    /// <summary>True when the four name fields after <paramref name="countryOffset"/> all point at valid strings.</summary>
    public static bool IdentityValidAt(IMemoryReader mem, long block, int countryOffset)
    {
        if (countryOffset < 0) return false;
        for (int i = 1; i <= NameFieldCount; i++)
        {
            if (Ams2String.TryReadField(mem, block + countryOffset + i * 8) == null) return false;
        }
        return true;
    }

    private static int? ScanForIdentity(IMemoryReader mem, long block, Action<string>? log)
    {
        // Read the window in chunks into one buffer, so a run spanning two chunks is still found.
        // An unreadable chunk stays zeroed - zero is never a valid string pointer.
        var window = new byte[ScanWindow];
        for (int offset = 0; offset < ScanWindow; offset += ScanChunk)
        {
            if (mem.ReadRaw(block + offset, ScanChunk, out var chunk))
                Buffer.BlockCopy(chunk, 0, window, offset, ScanChunk);
        }

        var valid = new Dictionary<int, bool>();
        bool IsString(int at)
        {
            if (!valid.TryGetValue(at, out var ok))
            {
                long pointer = BitConverter.ToInt64(window, at);
                valid[at] = ok = Ams2String.IsUserModeAddress(pointer) && Ams2String.TryReadAt(mem, pointer) != null;
            }
            return ok;
        }

        var hits = new List<int>();
        for (int at = 8; at + NameFieldCount * 8 <= ScanWindow; at += 8)
        {
            bool run = true;
            for (int i = 0; i < NameFieldCount && run; i++)
                run = IsString(at + i * 8);
            if (!run) continue;
            hits.Add(at - 8);
            if (hits.Count > 1) break;
        }

        if (hits.Count == 1) return hits[0];
        log?.Invoke(hits.Count == 0
            ? "SettingsBlockLayout: no run of four string pointers in the settings block"
            : $"SettingsBlockLayout: several runs of four string pointers in the settings block (Country at +0x{hits[0]:X}, +0x{hits[1]:X}, ...) - ambiguous");
        return null;
    }

    private static string FormatDelta(int delta) => delta < 0 ? $"-0x{-delta:X}" : $"+0x{delta:X}";
}

/// <summary>
/// The settings block for one attach: its layout is resolved once (SettingsBlockLayout.Resolve),
/// then re-validated before every use - the block pointer must be the one it was resolved in, and
/// the identity's four strings must still be there. Anything else and nothing is touched.
/// </summary>
public sealed class SettingsBlockAccess
{
    private readonly IMemoryReader _mem;
    private readonly long? _pointerAddress;
    private readonly Action<string>? _log;
    private SettingsBlockLayout? _layout;

    /// <param name="pointerAddress">moduleBase + GameplaySettingsPointerRva, or null when that pointer wasn't found for this build.</param>
    public SettingsBlockAccess(IMemoryReader mem, long? pointerAddress, Action<string>? log = null)
    {
        _mem = mem;
        _pointerAddress = pointerAddress;
        _log = log;
    }

    /// <summary>The layout to read/write through right now, or null when the block can't be trusted (see the class comment).</summary>
    public SettingsBlockLayout? Current()
    {
        if (!TryReadBlock(out var block)) return null;

        if (_layout == null)
            return _layout = SettingsBlockLayout.Resolve(_mem, block, _log);

        if (block != _layout.Block)
        {
            _log?.Invoke($"SettingsBlock: the settings block pointer changed since it was resolved (0x{_layout.Block:X} -> 0x{block:X}) - not touching it");
            return null;
        }

        if (_layout.CountryOffset is int country && !SettingsBlockLayout.IdentityValidAt(_mem, block, country))
        {
            _log?.Invoke($"SettingsBlock: the player identity at +0x{country:X} no longer validates - not touching the settings block");
            return null;
        }

        return _layout;
    }

    private bool TryReadBlock(out long block)
    {
        block = 0;
        if (_pointerAddress is not long address)
        {
            _log?.Invoke("SettingsBlock: settings block pointer not known for this build");
            return false;
        }

        // Wider than ProcessMemory.TryReadPointerSafe's heap-shaped range on purpose: this block
        // isn't one of the Custom Race heap objects, so only "a user-mode address" is required.
        if (_mem.TryReadInt64(address, out block) && Ams2String.IsUserModeAddress(block)) return true;

        _log?.Invoke("SettingsBlock: settings block pointer not resolved");
        block = 0;
        return false;
    }
}
