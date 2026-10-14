namespace Ams2Interop.Signatures;

/// <summary>What Ams2SignatureGenerator produced, and anything it couldn't build a signature for.</summary>
public sealed class Ams2SignatureGenerationResult
{
    public Ams2SignatureFile File { get; init; } = new();
    public IReadOnlyList<string> Failed { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Report { get; init; } = Array.Empty<string>();
    public bool Complete => Failed.Count == 0;
}

/// <summary>
/// Builds a signature file from a build whose offsets are KNOWN to be right - run it on the AMS2
/// build Ams2Constants.BuiltIn was confirmed against (or after re-deriving them by hand for a new
/// one). The signatures it writes are what lets later builds be resolved automatically.
///
/// Each generated signature is checked before it's accepted: resolving it against the same build
/// must give back exactly the known value.
/// </summary>
public static class Ams2SignatureGenerator
{
    public static Ams2SignatureGenerationResult Generate(ModuleImage image, IReadOnlyDictionary<string, long> knownRvas)
    {
        var file = new Ams2SignatureFile { GeneratedFromBuild = image.BuildId, GeneratedAt = DateTime.UtcNow };
        var failed = new List<string>();
        var report = new List<string> { $"build {image.BuildId}, code section at RVA 0x{image.TextRva:X}, {image.Text.Length:N0} bytes" };

        foreach (var name in Ams2OffsetNames.All)
        {
            if (!knownRvas.TryGetValue(name, out var rva))
            {
                failed.Add(name);
                report.Add($"{name}: no known value to generate from");
                continue;
            }

            Ams2Signature? signature = null;
            if (Ams2OffsetNames.Code.Contains(name))
            {
                long offset = rva - image.TextRva;
                if (offset >= 0 && offset < image.Text.Length && SignatureBuilder.BuildCodeSignature(image.Text, (int)offset) is BytePattern pattern)
                    signature = Ams2Signature.ForCode(pattern);
            }
            else if (SignatureBuilder.BuildDataRefSignature(image.Text, image.TextRva, rva) is DataRefSignature dataRef)
            {
                signature = Ams2Signature.ForDataRef(dataRef);
            }

            // only keep a signature that leads back to the known value on this very build
            if (signature != null && signature.Resolve(image) == rva)
            {
                file.Signatures[name] = signature;
                report.Add($"{name}: 0x{rva:X} -> {signature.Kind} [{signature.Pattern}]");
            }
            else
            {
                failed.Add(name);
                report.Add($"{name}: 0x{rva:X} -> NO unique signature found");
            }
        }

        return new Ams2SignatureGenerationResult { File = file, Failed = failed, Report = report };
    }
}
