using System.Text.Json;
using System.Text.Json.Serialization;
using Ams2Interop.Native;

namespace Ams2Interop.Signatures;

/// <summary>Where Ams2OffsetResolver finds its signature file and keeps its per-build cache.</summary>
public sealed class Ams2OffsetOptions
{
    public const string DefaultSignatureFileName = "ams2_signatures.json";

    /// <summary>Defaults to ams2_signatures.json next to the running application.</summary>
    public string SignatureFilePath { get; init; } = Path.Combine(AppContext.BaseDirectory, DefaultSignatureFileName);

    /// <summary>Defaults to %LocalAppData%\Ams2Interop\offsets_cache.json.</summary>
    public string CacheFilePath { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ams2Interop", "offsets_cache.json");
}

/// <summary>
/// Works out the patch-sensitive values for the running AMS2 build:
///
/// 1. No signature file (or unreadable module): the built-in values, unverified - exactly the
///    behaviour from before signatures existed, so nothing changes until a file is generated.
/// 2. This build is in the cache: the cached values, after a cheap re-check of the code
///    signatures right at the cached addresses.
/// 3. Otherwise every signature is scanned for. Values that match exactly once are used; the rest
///    are reported as missing and their features switched off - never guessed. A complete result
///    is cached so the next launch on this build skips the scan.
/// </summary>
public static class Ams2OffsetResolver
{
    public static Ams2ResolvedOffsets Resolve(ProcessMemory mem, long moduleBase, Ams2OffsetOptions options, Action<string>? log = null)
    {
        Ams2SignatureFile? signatures;
        try
        {
            signatures = Ams2SignatureFile.Load(options.SignatureFilePath);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            log?.Invoke($"Ams2OffsetResolver: signature file unreadable ({ex.Message}) - using built-in offsets, unverified");
            return Ams2ResolvedOffsets.BuiltIn(moduleBase);
        }

        if (signatures == null)
        {
            log?.Invoke($"Ams2OffsetResolver: no signature file at '{options.SignatureFilePath}' - using built-in offsets, unverified");
            return Ams2ResolvedOffsets.BuiltIn(moduleBase);
        }

        var image = ModuleImage.TryRead(mem, moduleBase, log);
        if (image == null)
        {
            log?.Invoke("Ams2OffsetResolver: module image unreadable - using built-in offsets, unverified");
            return Ams2ResolvedOffsets.BuiltIn(moduleBase);
        }

        return Resolve(image, moduleBase, signatures, options.CacheFilePath, log);
    }

    /// <summary>Steps 2 and 3 above, over an already-read image - separated out so it can be tested without a process.</summary>
    public static Ams2ResolvedOffsets Resolve(ModuleImage image, long moduleBase, Ams2SignatureFile signatures, string? cacheFilePath, Action<string>? log = null)
    {
        var cache = cacheFilePath != null ? OffsetCache.Load(cacheFilePath, log) : new OffsetCache();

        if (cache.Builds.TryGetValue(image.BuildId, out var cached) && CachedStillValid(image, signatures, cached))
        {
            var fromCache = new Ams2ResolvedOffsets(moduleBase, image.BuildId, Ams2OffsetSource.Cached, cached);
            log?.Invoke($"Ams2OffsetResolver: {fromCache.Describe()}");
            return fromCache;
        }

        var found = new Dictionary<string, long>();
        foreach (var name in Ams2OffsetNames.All)
        {
            if (!signatures.Signatures.TryGetValue(name, out var signature))
            {
                log?.Invoke($"Ams2OffsetResolver: no signature for '{name}'");
                continue;
            }

            long? rva;
            try
            {
                rva = signature.Resolve(image);
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException)
            {
                log?.Invoke($"Ams2OffsetResolver: signature for '{name}' is malformed ({ex.Message})");
                continue;
            }

            if (rva is long value)
            {
                found[name] = value;
                if (Ams2OffsetNames.BuiltInRvas.TryGetValue(name, out var builtIn) && builtIn != value)
                    log?.Invoke($"Ams2OffsetResolver: '{name}' moved: built-in 0x{builtIn:X} -> 0x{value:X}");
            }
            else
            {
                log?.Invoke($"Ams2OffsetResolver: '{name}' NOT found (signature matched zero or several places)");
            }
        }

        var scanned = new Ams2ResolvedOffsets(moduleBase, image.BuildId, Ams2OffsetSource.Scanned, found);
        log?.Invoke($"Ams2OffsetResolver: {scanned.Describe()}");

        if (!scanned.IsDegraded && cacheFilePath != null)
        {
            cache.Builds[image.BuildId] = found;
            cache.Save(cacheFilePath, log);
        }

        return scanned;
    }

    private static bool CachedStillValid(ModuleImage image, Ams2SignatureFile signatures, Dictionary<string, long> cached)
    {
        if (Ams2OffsetNames.All.Any(n => !cached.ContainsKey(n))) return false;
        foreach (var name in Ams2OffsetNames.Code)
        {
            if (signatures.Signatures.TryGetValue(name, out var signature) && !signature.StillMatchesAt(image, cached[name]))
                return false;
        }
        return true;
    }

    private sealed class OffsetCache
    {
        // build id -> offset name -> RVA
        [JsonPropertyName("builds")]
        public Dictionary<string, Dictionary<string, long>> Builds { get; set; } = new();

        public static OffsetCache Load(string path, Action<string>? log)
        {
            try
            {
                if (File.Exists(path))
                    return JsonSerializer.Deserialize<OffsetCache>(File.ReadAllText(path)) ?? new OffsetCache();
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                log?.Invoke($"Ams2OffsetResolver: offset cache unreadable ({ex.Message}) - ignoring it");
            }
            return new OffsetCache();
        }

        public void Save(string path, Action<string>? log)
        {
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // the cache only saves a scan next time - failing to write it changes nothing else
                log?.Invoke($"Ams2OffsetResolver: offset cache not saved ({ex.Message})");
            }
        }
    }
}
