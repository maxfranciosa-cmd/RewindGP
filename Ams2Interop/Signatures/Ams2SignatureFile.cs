using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ams2Interop.Signatures;

/// <summary>
/// The signatures that let a new AMS2 build's offsets be found without re-deriving them by hand.
/// Generated once from a build whose built-in offsets are confirmed working (see
/// Ams2SignatureGenerator / the Ams2Interop.Tool "generate" command), then shipped alongside the
/// app as ams2_signatures.json.
/// </summary>
public sealed class Ams2SignatureFile
{
    public const int CurrentFormatVersion = 1;

    [JsonPropertyName("format_version")]
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    // build id of the AMS2AVX.exe the signatures were generated from (see ModuleImage.BuildId)
    [JsonPropertyName("generated_from_build")]
    public string? GeneratedFromBuild { get; set; }

    [JsonPropertyName("generated_at")]
    public DateTime GeneratedAt { get; set; }

    // offset name (Ams2OffsetNames) -> signature
    [JsonPropertyName("signatures")]
    public Dictionary<string, Ams2Signature> Signatures { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Null when the file doesn't exist; throws on a file that exists but can't be read.</summary>
    public static Ams2SignatureFile? Load(string path)
    {
        if (!File.Exists(path)) return null;
        var file = JsonSerializer.Deserialize<Ams2SignatureFile>(File.ReadAllText(path), JsonOptions);
        if (file == null) throw new InvalidDataException($"'{path}' is empty");
        if (file.FormatVersion != CurrentFormatVersion)
            throw new InvalidDataException($"'{path}' has format version {file.FormatVersion}, expected {CurrentFormatVersion}");
        return file;
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}

public sealed class Ams2Signature
{
    public const string CodeKind = "code";
    public const string DataRefKind = "data_ref";

    // "code": the pattern matches at the start of the function itself.
    // "data_ref": the pattern matches at an instruction that references the address RIP-relatively.
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = CodeKind;

    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = "";

    // data_ref only: where the instruction's disp32 sits within the pattern, and the instruction's length
    [JsonPropertyName("disp_offset")]
    public int? DispOffset { get; set; }

    [JsonPropertyName("instruction_length")]
    public int? InstructionLength { get; set; }

    public static Ams2Signature ForCode(BytePattern pattern) =>
        new() { Kind = CodeKind, Pattern = pattern.ToString() };

    public static Ams2Signature ForDataRef(DataRefSignature signature) =>
        new()
        {
            Kind = DataRefKind,
            Pattern = signature.Pattern.ToString(),
            DispOffset = signature.DispOffset,
            InstructionLength = signature.InstructionLength
        };

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

        return SignatureBuilder.ResolveUnique(image.Text, pattern) is int offset ? image.TextRva + offset : null;
    }

    /// <summary>
    /// Cheap re-check of a previously resolved RVA without scanning: true when a code signature
    /// still matches right at it. Data refs can't be checked this way (the referencing instruction
    /// isn't at the RVA) and always return false - callers re-scan them instead.
    /// </summary>
    public bool StillMatchesAt(ModuleImage image, long rva)
    {
        if (Kind != CodeKind) return false;
        return BytePattern.Parse(Pattern).MatchesAt(image.Text, (int)(rva - image.TextRva));
    }
}
