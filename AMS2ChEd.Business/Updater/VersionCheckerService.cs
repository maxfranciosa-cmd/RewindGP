
using AMS2ChEd.Business.Updater.Models;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace AMS2ChEd.Business.Updater.Services
{
    /// <summary>
    /// Checks whether a newer version of RewindGP is available by reading the
    /// latest GitHub Release for this project's repo (GET .../releases/latest)
    /// and comparing its tag against the running assembly's version.
    ///
    /// Results are cached for 24 hours so the endpoint is not hit on every launch.
    /// </summary>
    public class VersionCheckService
    {
        private const string CacheKeyLastCheck = "UpdateCheck_LastCheck";
        private const string CacheKeyLatestVer = "UpdateCheck_LatestVersion";
        private const string CacheKeyPageUrl = "UpdateCheck_PageUrl";
        private const string CacheKeyDownloadUrl = "UpdateCheck_DownloadUrl";

        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

        private readonly string _releasesApiUrl;
        private readonly ICurrentVersionCheckStore _currentVersionCheckStore;
        private readonly HttpClient _http;
        private readonly Func<string> _getCurrentVersion;
        private readonly bool _forceUpdate;

        public VersionCheckService(
            string releasesApiUrl,
            ICurrentVersionCheckStore currentVersionCheckStore,
            bool forceUpdate,
            HttpClient? http = null,
            Func<string>? getCurrentVersion = null)
        {
            _releasesApiUrl = releasesApiUrl;
            _currentVersionCheckStore = currentVersionCheckStore;
            _http = http ?? new HttpClient();
            _forceUpdate = forceUpdate;

            // GitHub's API rejects anonymous requests with no User-Agent header.
            if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
                _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RewindGP-UpdateChecker", "1.0"));
            if (!_http.DefaultRequestHeaders.Accept.Any(a => a.MediaType == "application/vnd.github+json"))
                _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

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

            if (_forceUpdate || (lastCheck.HasValue
                && cachedVersion != null
                && DateTime.UtcNow - lastCheck.Value < CacheDuration))
            {
                return BuildResult(current, cachedVersion,
                    _currentVersionCheckStore.GetString(CacheKeyPageUrl) ?? "",
                    _currentVersionCheckStore.GetString(CacheKeyDownloadUrl) ?? "",
                    _forceUpdate);
            }

            // Query the GitHub Releases API
            try
            {
                var json = await _http.GetStringAsync(_releasesApiUrl);
                var release = ParseRelease(json);

                if (release == null)
                    return new UpdateCheckResult { CheckFailed = true, CurrentVersion = current };

                _currentVersionCheckStore.SetDateTime(CacheKeyLastCheck, DateTime.UtcNow);
                _currentVersionCheckStore.SetString(CacheKeyLatestVer, release.Value.Version);
                _currentVersionCheckStore.SetString(CacheKeyPageUrl, release.Value.PageUrl);
                _currentVersionCheckStore.SetString(CacheKeyDownloadUrl, release.Value.DownloadUrl);

                return BuildResult(current, release.Value.Version, release.Value.PageUrl, release.Value.DownloadUrl, _forceUpdate);
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

        internal readonly record struct ParsedRelease(string Version, string PageUrl, string DownloadUrl);

        /// <summary>
        /// Parses a GitHub "releases/latest" API response. Returns null if the tag can't be
        /// read as a version, or if the release has no .zip asset attached (nothing to install).
        /// </summary>
        internal static ParsedRelease? ParseRelease(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("tag_name", out var tagEl))
                    return null;

                var tag = tagEl.GetString();
                if (string.IsNullOrWhiteSpace(tag))
                    return null;

                var trimmedTag = tag.Trim();
                if (trimmedTag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    trimmedTag = trimmedTag.Substring(1);

                if (!TryParseVersion(trimmedTag, out var version))
                    return null;

                var pageUrl = root.TryGetProperty("html_url", out var htmlUrlEl) ? htmlUrlEl.GetString() ?? "" : "";

                string downloadUrl = "";
                if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsEl.EnumerateArray())
                    {
                        var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                        if (name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.TryGetProperty("browser_download_url", out var urlEl) ? urlEl.GetString() ?? "" : "";
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                    return null;

                // Normalize to "major.minor" so it matches the exact string comparison
                // AMS2ChEd.Updater does against the extracted exe's FileVersion.
                return new ParsedRelease($"{version.Major}.{version.Minor}", pageUrl, downloadUrl);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // -------------------------------------------------------------------------
        // Version comparison
        // -------------------------------------------------------------------------

        private static UpdateCheckResult BuildResult(
            string current, string latest, string pageUrl, string downloadUrl, bool forceUpdate) =>
            new UpdateCheckResult
            {
                IsUpdateAvailable = forceUpdate || IsNewer(latest, current),
                CurrentVersion = current,
                LatestVersion = latest,
                PageUrl = pageUrl,
                DownloadUrl = downloadUrl
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
