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
/// 1. No signature file (or unreadable module): the built-in values - but only on a build they're
///    known to work on (see BuiltInFallback and Ams2Constants.BuiltIn.ConfirmedBuildIds). While no
///    build has been recorded as confirmed, they're used on any build with a warning, which is the
///    behaviour from before signatures existed.
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
        // Read first, before any early return: the built-in fallback is only trusted on a known build.
        var buildId = ModuleImage.TryReadBuildId(mem, moduleBase, log);

        Ams2SignatureFile? signatures;
        try
        {
            signatures = Ams2SignatureFile.Load(options.SignatureFilePath);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return BuiltInFallback(moduleBase, buildId, $"signature file unreadable ({ex.Message})", log);
        }

        if (signatures == null)
            return BuiltInFallback(moduleBase, buildId, $"no signature file at '{options.SignatureFilePath}'", log);

        var image = ModuleImage.TryRead(mem, moduleBase, log);
        if (image == null)
            return BuiltInFallback(moduleBase, buildId, "module image unreadable", log);

        return Resolve(image, moduleBase, signatures, options.CacheFilePath, log);
    }

    /// <summary>
    /// What to use when there's nothing to find the values with (<paramref name="why"/> says what
    /// was missing): Ams2Constants.BuiltIn, but only on a build it's known to work on.
    ///
    /// - confirmed list empty: the built-in values, with a warning naming the build - nobody has
    ///   recorded the confirmed build yet, so this keeps the behaviour from before the list existed.
    /// - running build in the list: the built-in values.
    /// - otherwise (not in the list, or the build id couldn't be read): nothing at all, so every
    ///   feature is off and IsDegraded is true - a patched build must not be written to through
    ///   values derived for another one.
    /// </summary>
    /// <param name="confirmedBuildIds">Defaults to Ams2Constants.BuiltIn.ConfirmedBuildIds; a parameter so tests needn't touch the constant.</param>
    public static Ams2ResolvedOffsets BuiltInFallback(long moduleBase, string? buildId, string why, Action<string>? log = null,
        IReadOnlyCollection<string>? confirmedBuildIds = null)
    {
        confirmedBuildIds ??= Ams2Constants.BuiltIn.ConfirmedBuildIds;
        var build = buildId ?? "unknown (PE header unreadable)";

        if (confirmedBuildIds.Count == 0)
        {
            log?.Invoke($"Ams2OffsetResolver: WARNING - {why}; using the built-in offsets UNVERIFIED for AMS2 build {build}: " +
                        "no build has been recorded in Ams2Constants.BuiltIn.ConfirmedBuildIds yet");
            return Ams2ResolvedOffsets.BuiltIn(moduleBase, buildId, "unverified - no confirmed build recorded");
        }

        if (buildId != null && confirmedBuildIds.Contains(buildId, StringComparer.OrdinalIgnoreCase))
        {
            log?.Invoke($"Ams2OffsetResolver: {why}; using the built-in offsets - AMS2 build {buildId} is a confirmed build");
            return Ams2ResolvedOffsets.BuiltIn(moduleBase, buildId, "confirmed build");
        }

        var reason = buildId == null
            ? "the running build couldn't be identified, so the built-in offsets can't be checked against the confirmed builds"
            : "the running build isn't one the built-in offsets are confirmed on";
        var rejected = Ams2ResolvedOffsets.Rejected(moduleBase, buildId, reason);
        log?.Invoke($"Ams2OffsetResolver: {why}; {reason} - every patch-sensitive feature is off. {rejected.Describe()}");
        return rejected;
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
