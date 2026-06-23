
using AMS2ChEd.Business.Updater.Models;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AMS2ChEd.Business.Updater.Services
{
    /// <summary>
    /// Checks whether a newer version of RewindGP is available by scraping the
    /// overtake.gg download page and reading the version from the embedded
    /// JSON-LD script tag (application/ld+json → mainEntity.version).
    ///
    /// Results are cached for 24 hours so the page is not fetched on every launch.
    /// </summary>
    public class VersionCheckService
    {
        private const string CacheKeyLastCheck = "UpdateCheck_LastCheck";
        private const string CacheKeyLatestVer = "UpdateCheck_LatestVersion";
        private const string CacheKeyPageUrl = "UpdateCheck_PageUrl";

        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

        // Regex to extract the JSON-LD block from the page HTML
        private static readonly Regex JsonLdRegex = new(
            @"<script\s+type=""application/ld\+json""[^>]*>(.*?)</script>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private readonly string _pageUrl;
        private readonly ICurrentVersionCheckStore _currentVersionCheckStore;
        private readonly HttpClient _http;
        private readonly Func<string> _getCurrentVersion;
        private readonly bool _forceUpdate;

        public VersionCheckService(
            string pageUrl,
            ICurrentVersionCheckStore currentVersionCheckStore,
            bool forceUpdate,
            HttpClient? http = null,
            Func<string>? getCurrentVersion = null)
        {
            _pageUrl = pageUrl;
            _currentVersionCheckStore = currentVersionCheckStore;
            _http = http ?? new HttpClient();
            _forceUpdate = forceUpdate;
            
            // Default: read AssemblyInformationalVersion so "0.83" style versions
            // work without needing a full four-part assembly version string.
            _getCurrentVersion = getCurrentVersion
                ?? (() =>  $"{ Assembly.GetEntryAssembly()?.GetName().Version?.Major}.{Assembly.GetEntryAssembly()?.GetName().Version?.Minor}"
                    ?? "0.0");
        }

        /// <summary>
        /// Returns the update check result, using the 24-hour cache where possible.
        /// Never throws — returns CheckFailed = true on any network or parse error.
        /// </summary>
        public async Task<UpdateCheckResult> CheckAsync()
        {
            var current = _getCurrentVersion();

            // Try cache first
            var lastCheck = _currentVersionCheckStore.GetDateTime(CacheKeyLastCheck);
            var cachedVersion = _currentVersionCheckStore.GetString(CacheKeyLatestVer);

            if (lastCheck.HasValue
                && cachedVersion != null
                && DateTime.UtcNow - lastCheck.Value < CacheDuration)
            {
                return BuildResult(current, cachedVersion,
                    _currentVersionCheckStore.GetString(CacheKeyPageUrl) ?? _pageUrl, _forceUpdate);
            }

            // Scrape overtake.gg page
            try
            {
                var html = await _http.GetStringAsync(_pageUrl);
                var version = ExtractVersion(html);

                if (version == null)
                    return new UpdateCheckResult { CheckFailed = true, CurrentVersion = current };

                _currentVersionCheckStore.SetDateTime(CacheKeyLastCheck, DateTime.UtcNow);
                _currentVersionCheckStore.SetString(CacheKeyLatestVer, version);
                _currentVersionCheckStore.SetString(CacheKeyPageUrl, _pageUrl);

                return BuildResult(current, version, _pageUrl, _forceUpdate);
            }
            catch
            {
                return new UpdateCheckResult { CheckFailed = true, CurrentVersion = current };
            }
        }

        /// <summary>
        /// Forces the next CheckAsync() to hit the remote page, ignoring the cache.
        /// </summary>
        public void InvalidateCache() =>
            _currentVersionCheckStore.SetDateTime(CacheKeyLastCheck, DateTime.MinValue);

        // -------------------------------------------------------------------------
        // Parsing
        // -------------------------------------------------------------------------

        internal static string? ExtractVersion(string html)
        {
            foreach (Match match in JsonLdRegex.Matches(html))
            {
                var json = match.Groups[1].Value.Trim();
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("mainEntity", out var mainEntity)
                        && mainEntity.TryGetProperty("version", out var versionEl))
                    {
                        var version = versionEl.GetString();
                        if (!string.IsNullOrWhiteSpace(version))
                            return version.Trim();
                    }
                }
                catch (JsonException) { /* malformed block — try next */ }
            }

            return null;
        }

        // -------------------------------------------------------------------------
        // Version comparison
        // -------------------------------------------------------------------------

        private static UpdateCheckResult BuildResult(
            string current, string latest, string pageUrl, bool forceUpdate) =>
            new UpdateCheckResult
            {
                IsUpdateAvailable = forceUpdate || IsNewer(latest, current),
                CurrentVersion = current,
                LatestVersion = latest,
                PageUrl = pageUrl
            };

        /// <summary>
        /// Returns true if <paramref name="latest"/> is strictly newer than
        /// <paramref name="current"/>. Handles both "0.83" and "1.0.0" style strings.
        /// </summary>
        internal static bool IsNewer(string latest, string current)
        {
            if (!TryParseVersion(latest, out var latestVer)) return false;
            if (!TryParseVersion(current, out var currentVer)) return false;
            return latestVer > currentVer;
        }

        private static bool TryParseVersion(string raw, out Version result)
        {
            // Pad short versions (e.g. "0.83" → "0.83.0.0") so Version.Parse accepts them
            var parts = raw.Split('.');
            while (parts.Length < 4)
                Array.Resize(ref parts, parts.Length + 1);
            for (int i = 0; i < parts.Length; i++)
                if (string.IsNullOrEmpty(parts[i])) parts[i] = "0";

            return Version.TryParse(string.Join(".", parts), out result!);
        }
    }
}