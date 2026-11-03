using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ams2Interop.Signatures;

/// <summary>
/// The signatures that let a new AMS2 build's offsets be found without re-deriving them by hand.
/// Generated once from a build whose built-in offsets are confirmed working (see
/// Ams2SignatureGenerator / the Ams2Interop.Tool "generate" command), then shipped alongside the
/// app as ams2_signatures.json.
///
/// Format version 2 keeps several signatures per value (see Ams2OffsetResolver for how they're
/// combined), so a patch that breaks one pattern can still be resolved through another. Version 1
/// files, with one signature per value, still load: each becomes a list of one.
/// </summary>
public sealed class Ams2SignatureFile
{
    public const int CurrentFormatVersion = 2;
    public const int LegacyFormatVersion = 1;

    [JsonPropertyName("format_version")]
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    // build id of the AMS2AVX.exe the signatures were generated from (see ModuleImage.BuildId)
    [JsonPropertyName("generated_from_build")]
    public string? GeneratedFromBuild { get; set; }

    [JsonPropertyName("generated_at")]
    public DateTime GeneratedAt { get; set; }

    // offset name (Ams2OffsetNames) -> its signatures, the primary first
    [JsonPropertyName("signatures")]
    public Dictionary<string, List<Ams2Signature>> Signatures { get; set; } = new();

    private Dictionary<string, Dictionary<string, long>> _knownBuilds = new();

    /// <summary>
    /// Build id -> offset name -> RVA, for builds these exact values are CONFIRMED working on (a real
    /// launch applied car, track and date). Used as-is on that build, ahead of the cache and the
    /// scan, so a build whose signatures broke can be supported by editing this file alone - no
    /// rebuild. "generate" adds the build it ran on and keeps the entries already in the file.
    /// Optional: absent in version 1 files and in version 2 files written before it existed (older
    /// readers ignore it).
    /// </summary>
    [JsonPropertyName("known_builds")]
    public Dictionary<string, Dictionary<string, long>> KnownBuilds
    {
        get => _knownBuilds;
        set => _knownBuilds = value ?? new();
    }

    /// <summary>The known_builds entry for a build id, ignoring case; null when there's none.</summary>
    public Dictionary<string, long>? KnownBuild(string? buildId) =>
        buildId == null
            ? null
            : KnownBuilds.FirstOrDefault(kv => string.Equals(kv.Key, buildId, StringComparison.OrdinalIgnoreCase)).Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Null when the file doesn't exist; throws on a file that exists but can't be read.</summary>
    public static Ams2SignatureFile? Load(string path)
    {
        if (!File.Exists(path)) return null;
        return Parse(File.ReadAllText(path), path);
    }

    /// <summary>Reads either format version from JSON text; <paramref name="source"/> only names it in errors.</summary>
    public static Ams2SignatureFile Parse(string json, string source = "signature file")
    {
        int version;
        using (var document = JsonDocument.Parse(json))
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"'{source}' is empty");
            version = document.RootElement.TryGetProperty("format_version", out var v) && v.TryGetInt32(out var n) ? n : 0;
        }

        switch (version)
        {
            case CurrentFormatVersion:
                return JsonSerializer.Deserialize<Ams2SignatureFile>(json, JsonOptions)
                       ?? throw new InvalidDataException($"'{source}' is empty");

            case LegacyFormatVersion:
                var legacy = JsonSerializer.Deserialize<V1File>(json, JsonOptions)
                             ?? throw new InvalidDataException($"'{source}' is empty");
                return new Ams2SignatureFile
                {
                    // migrated in memory only; Save writes the current version
                    FormatVersion = CurrentFormatVersion,
                    GeneratedFromBuild = legacy.GeneratedFromBuild,
                    GeneratedAt = legacy.GeneratedAt,
                    Signatures = legacy.Signatures.ToDictionary(kv => kv.Key, kv => new List<Ams2Signature> { kv.Value })
                };

            default:
                throw new InvalidDataException(
                    $"'{source}' has format version {version}, expected {LegacyFormatVersion} or {CurrentFormatVersion}");
        }
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>The format-version-1 shape: exactly one signature per name.</summary>
    private sealed class V1File
    {
        [JsonPropertyName("generated_from_build")]
        public string? GeneratedFromBuild { get; set; }

        [JsonPropertyName("generated_at")]
        public DateTime GeneratedAt { get; set; }

        [JsonPropertyName("signatures")]
        public Dictionary<string, Ams2Signature> Signatures { get; set; } = new();
    }
}

public sealed class Ams2Signature
{
    public const string CodeKind = "code";
    public const string DataRefKind = "data_ref";

    // "code": the pattern matches inside the function itself, `offset` bytes after its start.
    // "data_ref": the pattern matches at an instruction that references the address RIP-relatively.
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = CodeKind;

    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    // code only: how far into the function the pattern starts (0 = the function start itself;
    // alternates start at a later instruction boundary), so resolved RVA = hit - offset.
    // Absent in format version 1 files, which only had function-start patterns.
    [JsonPropertyName("offset")]
    public int? Offset { get; set; }

    // data_ref only: where the instruction's disp32 sits within the pattern, and the instruction's length
    [JsonPropertyName("disp_offset")]
    public int? DispOffset { get; set; }

    [JsonPropertyName("instruction_length")]
    public int? InstructionLength { get; set; }

    public static Ams2Signature ForCode(BytePattern pattern, int offsetIntoFunction = 0) =>
        new() { Kind = CodeKind, Pattern = pattern.ToString(), Offset = offsetIntoFunction == 0 ? null : offsetIntoFunction };

    public static Ams2Signature ForDataRef(DataRefSignature signature) =>
        new()
        {
            Kind = DataRefKind,
            Pattern = signature.Pattern.ToString(),
            DispOffset = signature.DispOffset,
            InstructionLength = signature.InstructionLength
        };

    private int CodeOffset => Offset ?? 0;

    /// <summary>Resolves this signature to an RVA on the given build, or null when it doesn't match exactly once.</summary>
    public long? Resolve(ModuleImage image)
    {
        var pattern = BytePattern.Parse(Pattern);
        if (Kind == DataRefKind)
        {
            if (DispOffset is not int dispOffset || InstructionLength is not int length) return null;
            var rva = SignatureBuilder.ResolveDataRef(image.Text, image.TextRva, new DataRefSignature(pattern, dispOffset, length));
            return rva is long r && r >= 0 && r < image.SizeOfImage ? r : null;
        }

        if (CodeOffset < 0) return null;
        return SignatureBuilder.ResolveUnique(image.Text, pattern) is int hit && hit - CodeOffset >= 0
            ? image.TextRva + hit - CodeOffset
            : null;
    }

    /// <summary>
    /// Cheap re-check of a previously resolved RVA without scanning: true when a code signature
    /// still matches where it should relative to it. Data refs can't be checked this way (the
    /// referencing instruction isn't at the RVA) and always return false - callers re-scan them instead.
    /// </summary>
    public bool StillMatchesAt(ModuleImage image, long rva)
    {
        if (Kind != CodeKind) return false;
        long at = rva - image.TextRva + CodeOffset;
        return at is >= 0 and <= int.MaxValue && BytePattern.Parse(Pattern).MatchesAt(image.Text, (int)at);
    }
}
