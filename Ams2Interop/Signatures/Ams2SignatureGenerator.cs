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
/// Several signatures are kept per value (see Ams2SignatureFile): for a function, its start plus
/// up to MaxCodeAlternates patterns from later in the same function; for a data value, up to
/// MaxDataSignatures patterns from distinct instructions that reference it. Each one is checked
/// before it's kept: resolving it against the same build must give back exactly the known value.
/// A value fails only when not even one signature passes that check.
/// </summary>
public static class Ams2SignatureGenerator
{
    public const int MaxCodeAlternates = 2;
    public const int MaxDataSignatures = 3;

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

            var candidates = new List<Ams2Signature>();
            if (Ams2OffsetNames.Code.Contains(name))
            {
                long offset = rva - image.TextRva;
                if (offset >= 0 && offset < image.Text.Length)
                {
                    foreach (var (offsetIntoFunction, pattern) in SignatureBuilder.BuildCodeSignatures(image.Text, (int)offset, MaxCodeAlternates))
                        candidates.Add(Ams2Signature.ForCode(pattern, offsetIntoFunction));
                }
            }
            else
            {
                foreach (var dataRef in SignatureBuilder.BuildDataRefSignatures(image.Text, image.TextRva, rva, MaxDataSignatures))
                    candidates.Add(Ams2Signature.ForDataRef(dataRef));
            }

            // only keep signatures that lead back to the known value on this very build
            var kept = candidates.Where(s => s.Resolve(image) == rva).ToList();
            if (kept.Count > 0)
            {
                file.Signatures[name] = kept;
                foreach (var signature in kept)
                    report.Add($"{name}: 0x{rva:X} -> {signature.Kind}{(signature.Offset is int o ? $" +0x{o:X}" : "")} [{signature.Pattern}]");
                if (kept.Count == 1)
                    report.Add($"{name}: only one signature - a patch that breaks it leaves nothing to fall back on");
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
