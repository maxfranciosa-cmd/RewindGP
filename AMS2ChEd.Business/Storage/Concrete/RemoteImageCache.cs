using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AMS2ChEd.Business.Storage.Concrete
{
    /// <summary>
    /// A disk cache for pictures linked by URL (driver photos, race covers): each URL is downloaded
    /// once and read from disk from then on. One file per URL, named after a hash of it, in a folder
    /// of its own (AppPaths.ImageCacheFolder). A cached picture is never checked against the site
    /// again: delete it (or Clear the cache) to fetch a picture that changed at the same URL.
    /// Thread-safe; every failure is swallowed, as the caller can always fall back to the URL.
    /// </summary>
    public class RemoteImageCache
    {
        // past this, a picture isn't worth keeping (and is unlikely to be one)
        private const long MaxFileBytes = 20L * 1024 * 1024;

        private const string DefaultExtension = ".img";

        private readonly string _folder;
        private readonly HttpClient _http;

        // one download per URL at a time: a second request for the same URL shares the first
        // (Lazy, so concurrent callers can't both start one)
        private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _inFlight = new();

        public RemoteImageCache(string folder, HttpClient http)
        {
            _folder = folder;
            _http = http;
        }

        public static bool IsRemoteUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        /// <summary>
        /// The cache file name for a URL: the SHA-256 of the URL, plus the URL's own extension when
        /// it looks like one (the content decides how the picture is decoded, not the name).
        /// </summary>
        public static string CacheFileName(string url)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
            return hash + ExtensionOf(url);
        }

        public string CachePath(string url) => Path.Combine(_folder, CacheFileName(url));

        /// <summary>The cached file for this URL, or null when it hasn't been downloaded yet.</summary>
        public string TryGetCachedPath(string url)
        {
            if (!IsRemoteUrl(url))
                return null;

            try
            {
                var path = CachePath(url);
                return File.Exists(path) ? path : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Downloads the URL into the cache, unless it's there already. Returns the cached file, or
        /// null when the download failed. A file only ever appears complete: it's written under a
        /// temporary name and renamed once whole.
        /// </summary>
        public Task<string> DownloadAsync(string url, CancellationToken ct = default)
        {
            if (!IsRemoteUrl(url))
                return Task.FromResult<string>(null);

            var cached = TryGetCachedPath(url);
            if (cached != null)
                return Task.FromResult(cached);

            var entry = _inFlight.GetOrAdd(url, u => new Lazy<Task<string>>(() => DownloadCoreAsync(u, ct)));
            var task = entry.Value;

            // forget the download once it's over, success or not, so a failed one can be retried.
            // Removed by a continuation rather than inside the download itself: a download that
            // fails at once would otherwise finish before it's in the list, and stay there for good.
            task.ContinueWith(_ => _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<string>>>(url, entry)),
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            return task;
        }

        /// <summary>Deletes every cached picture; returns how many were removed.</summary>
        public int Clear()
        {
            var removed = 0;
            try
            {
                if (!Directory.Exists(_folder))
                    return 0;

                foreach (var file in Directory.GetFiles(_folder))
                {
                    try
                    {
                        File.Delete(file);
                        removed++;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // in use or locked: left for next time
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
            return removed;
        }

        private async Task<string> DownloadCoreAsync(string url, CancellationToken ct)
        {
            string tempPath = null;
            try
            {
                // another request may have finished this download a moment ago
                var existing = TryGetCachedPath(url);
                if (existing != null)
                    return existing;

                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return null;

                // an error page served with 200 isn't a picture
                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType != null && !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && mediaType != "application/octet-stream")
                    return null;

                if (response.Content.Headers.ContentLength > MaxFileBytes)
                    return null;

                Directory.CreateDirectory(_folder);
                var finalPath = CachePath(url);
                tempPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

                long written;
                await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                await using (var target = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    written = await CopyLimitedAsync(source, target, ct).ConfigureAwait(false);
                }

                if (written <= 0)
                    return null;

                File.Move(tempPath, finalPath, overwrite: true);
                tempPath = null;
                return finalPath;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or TaskCanceledException or OperationCanceledException or InvalidDataException)
            {
                return null;
            }
            finally
            {
                if (tempPath != null)
                {
                    try { File.Delete(tempPath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
            }
        }

        // copies at most MaxFileBytes; anything bigger is rejected
        private static async Task<long> CopyLimitedAsync(Stream source, Stream target, CancellationToken ct)
        {
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > MaxFileBytes)
                    throw new InvalidDataException("picture too large");
                await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            }
            return total;
        }

        private static string ExtensionOf(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return DefaultExtension;

            var extension = Path.GetExtension(uri.AbsolutePath);
            if (string.IsNullOrEmpty(extension) || extension.Length > 6 || !extension.Skip(1).All(char.IsAsciiLetterOrDigit))
                return DefaultExtension;

            return extension.ToLowerInvariant();
        }
    }
}
