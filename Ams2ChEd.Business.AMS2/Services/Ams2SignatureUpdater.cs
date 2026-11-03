using System.Net.Http;
using Ams2Interop.Signatures;

namespace Ams2ChEd.Business.AMS2.Services
{
    /// <summary>
    /// Keeps Ams2Interop's signature file (ams2_signatures.json) current without an app release.
    ///
    /// The file is pure data - signatures, plus the known_builds values confirmed for specific AMS2
    /// builds - so a broken AMS2 patch can be fixed for every player by publishing a new one at
    /// RemoteUrl. Each race launch starts a download (once per app run) into DownloadedPath; the
    /// offsets are then resolved from whichever of that and the copy shipped with the app is newer
    /// (ChooseSignatureFile).
    ///
    /// Everything here is best-effort: offline, a 404 (nothing published yet), or a file that
    /// doesn't parse leaves the files on disk as they were, and the interop works from those.
    /// Only a GET is made; nothing is sent. A downloaded file can't make the interop write
    /// anywhere new by itself: signatures still have to match the running exe, and known_builds
    /// values are range-checked by Ams2OffsetResolver.
    /// </summary>
    public static class Ams2SignatureUpdater
    {
        public const string RemoteUrl =
            "https://raw.githubusercontent.com/maxfranciosa-cmd/RewindGP/main/Ams2Interop/ams2_signatures.json";

        // a real signature file is a few tens of KB; anything far bigger isn't one
        public const int MaxDownloadBytes = 1024 * 1024;

        public static string DownloadedPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RewindGP", Ams2OffsetOptions.DefaultSignatureFileName);

        public static string ShippedPath { get; } = new Ams2OffsetOptions().SignatureFilePath;

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly object StartLock = new();
        private static Task? _download;

        /// <summary>Starts this run's download, if it hasn't started yet. Returns immediately.</summary>
        public static void StartDownload(Action<string>? log)
        {
            lock (StartLock)
            {
                _download ??= Task.Run(() => DownloadAsync(Http, RemoteUrl, DownloadedPath, log, CancellationToken.None));
            }
        }

        /// <summary>Waits for this run's download to finish, but never longer than <paramref name="maxWait"/>.</summary>
        public static async Task WaitForDownloadAsync(TimeSpan maxWait)
        {
            Task? download;
            lock (StartLock)
            {
                download = _download;
            }
            if (download != null)
            {
                await Task.WhenAny(download, Task.Delay(maxWait)).ConfigureAwait(false);
            }
        }

        /// <summary>Offset options pointing Ams2RaceConfigurator at the newer of the shipped and downloaded signature files.</summary>
        public static Ams2OffsetOptions OffsetOptions(Action<string>? log) =>
            new() { SignatureFilePath = ChooseSignatureFile(ShippedPath, DownloadedPath, log) };

        /// <summary>
        /// Downloads the signature file at <paramref name="url"/> to <paramref name="targetPath"/>.
        /// The file on disk is replaced only by one that parses as a signature file with something in
        /// it; true when it was replaced. Never throws.
        /// </summary>
        public static async Task<bool> DownloadAsync(HttpClient http, string url, string targetPath, Action<string>? log, CancellationToken ct)
        {
            try
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    log?.Invoke($"Ams2SignatureUpdater: no signature file downloaded ({(int)response.StatusCode} {response.ReasonPhrase})");
                    return false;
                }
                if (response.Content.Headers.ContentLength > MaxDownloadBytes)
                {
                    log?.Invoke($"Ams2SignatureUpdater: download refused - {response.Content.Headers.ContentLength} bytes is too big for a signature file");
                    return false;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                if (bytes.Length > MaxDownloadBytes)
                {
                    log?.Invoke($"Ams2SignatureUpdater: download refused - {bytes.Length} bytes is too big for a signature file");
                    return false;
                }

                var json = System.Text.Encoding.UTF8.GetString(bytes);
                var file = Ams2SignatureFile.Parse(json, url);
                if (file.Signatures.Count == 0 && file.KnownBuilds.Count == 0)
                {
                    log?.Invoke("Ams2SignatureUpdater: downloaded file has no signatures and no known builds - not used");
                    return false;
                }

                if (File.Exists(targetPath) && File.ReadAllText(targetPath) == json)
                {
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetPath))!);
                var temp = targetPath + ".download";
                File.WriteAllText(temp, json);
                File.Move(temp, targetPath, overwrite: true);
                log?.Invoke($"Ams2SignatureUpdater: signature file updated (generated {file.GeneratedAt:u} from build {file.GeneratedFromBuild ?? "?"}, "
                            + $"{file.KnownBuilds.Count} known build(s))");
                return true;
            }
            catch (Exception ex)
            {
                // offline, timed out, malformed, disk trouble: keep whatever is on disk
                log?.Invoke($"Ams2SignatureUpdater: signature file not updated ({ex.GetType().Name}: {ex.Message})");
                return false;
            }
        }

        /// <summary>
        /// The signature file to resolve offsets from: the downloaded one when it's at least as new as
        /// the shipped one (by generated_at; a tie goes to the download, which mirrors the published
        /// file), else the shipped one. A file that can't be read is passed over. With neither
        /// readable, the shipped path, so Ams2OffsetResolver reports it as missing as before.
        /// </summary>
        public static string ChooseSignatureFile(string shippedPath, string downloadedPath, Action<string>? log)
        {
            var shipped = TryLoad(shippedPath, log);
            var downloaded = TryLoad(downloadedPath, log);

            if (downloaded == null) return shippedPath;
            if (shipped == null || downloaded.GeneratedAt >= shipped.GeneratedAt) return downloadedPath;

            log?.Invoke($"Ams2SignatureUpdater: shipped signature file ({shipped.GeneratedAt:u}) is newer than the downloaded one ({downloaded.GeneratedAt:u}) - using it");
            return shippedPath;
        }

        private static Ams2SignatureFile? TryLoad(string path, Action<string>? log)
        {
            try
            {
                return Ams2SignatureFile.Load(path);
            }
            catch (Exception ex)
            {
                log?.Invoke($"Ams2SignatureUpdater: '{path}' unreadable ({ex.Message}) - not used");
                return null;
            }
        }
    }
}
