using AMS2ChEd.Business.Updater;
using AMS2ChEd.Business.Updater.Services;
using System.Net;
using System.Text;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class VersionCheckerServiceTests
    {
        private const string ApiUrl = "https://api.github.com/repos/maxfranciosa-cmd/RewindGP/releases/latest";
        private const string DefaultHtmlUrl = "https://github.com/maxfranciosa-cmd/RewindGP/releases/tag/test";
        private const string DefaultDownloadUrl = "https://github.com/maxfranciosa-cmd/RewindGP/releases/download/test/RewindGP.zip";

        // -------------------------------------------------------------------------
        // JSON stub helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Produces a minimal GitHub "releases/latest" API response body, matching
        /// what VersionCheckService.ParseRelease parses.
        /// </summary>
        private static string MakeReleaseJson(
            string tagName,
            string htmlUrl = DefaultHtmlUrl,
            string? assetName = "RewindGP.zip",
            string downloadUrl = DefaultDownloadUrl) => $@"{{
  ""tag_name"": ""{tagName}"",
  ""html_url"": ""{htmlUrl}"",
  ""assets"": [
    {(assetName == null ? "" : $@"{{ ""name"": ""{assetName}"", ""browser_download_url"": ""{downloadUrl}"" }}")}
  ]
}}";

        private static HttpClient MakeHttpClient(string responseBody, HttpStatusCode status = HttpStatusCode.OK)
        {
            var handler = new StubHttpMessageHandler(responseBody, status);
            return new HttpClient(handler);
        }

        // -------------------------------------------------------------------------
        // Update available
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task CheckAsync_RemoteVersionHigher_ReturnsUpdateAvailable()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var svc = new VersionCheckService(
                ApiUrl, prefs, false,
                MakeHttpClient(MakeReleaseJson("2.0.0")),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsTrue(result.IsUpdateAvailable);
            Assert.AreEqual("2.0", result.LatestVersion);
            Assert.AreEqual(DefaultDownloadUrl, result.DownloadUrl);
            Assert.IsFalse(result.CheckFailed);
        }

        [TestMethod]
        public async Task CheckAsync_RemoteVersionSame_ReturnsNoUpdate()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var svc = new VersionCheckService(
                ApiUrl, prefs, false,
                MakeHttpClient(MakeReleaseJson("1.0.0")),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsFalse(result.IsUpdateAvailable);
            Assert.IsFalse(result.CheckFailed);
        }

        [TestMethod]
        public async Task CheckAsync_RemoteVersionLower_ReturnsNoUpdate()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var svc = new VersionCheckService(
                ApiUrl, prefs, false,
                MakeHttpClient(MakeReleaseJson("0.9.0")),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsFalse(result.IsUpdateAvailable);
            Assert.IsFalse(result.CheckFailed);
        }

        [TestMethod]
        public async Task CheckAsync_ForceUpdate_ReturnsUpdateAvailableEvenIfSameVersion()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var svc = new VersionCheckService(
                ApiUrl, prefs, forceUpdate: true,
                MakeHttpClient(MakeReleaseJson("1.0.0")),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsTrue(result.IsUpdateAvailable);
            Assert.IsFalse(result.CheckFailed);
        }

        [TestMethod]
        public async Task CheckAsync_VPrefixedTag_NormalizesCorrectly()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var svc = new VersionCheckService(
                ApiUrl, prefs, false,
                MakeHttpClient(MakeReleaseJson("v2.1")),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsTrue(result.IsUpdateAvailable);
            Assert.AreEqual("2.1", result.LatestVersion);
        }

        // -------------------------------------------------------------------------
        // Network / parse failures
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task CheckAsync_NetworkError_ReturnsCheckFailed()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var svc = new VersionCheckService(
                ApiUrl, prefs, false,
                MakeHttpClient("{}", HttpStatusCode.InternalServerError),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsTrue(result.CheckFailed);
            Assert.IsFalse(result.IsUpdateAvailable);
            Assert.AreEqual("1.0.0", result.CurrentVersion);
        }

        [TestMethod]
        public async Task CheckAsync_MalformedJson_ReturnsCheckFailed()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var svc = new VersionCheckService(
                ApiUrl, prefs, false,
                MakeHttpClient("not json"),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsTrue(result.CheckFailed);
            Assert.IsFalse(result.IsUpdateAvailable);
        }

        [TestMethod]
        public async Task CheckAsync_NoZipAsset_ReturnsCheckFailed()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var svc = new VersionCheckService(
                ApiUrl, prefs, false,
                MakeHttpClient(MakeReleaseJson("2.0.0", assetName: null)),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsTrue(result.CheckFailed);
            Assert.IsFalse(result.IsUpdateAvailable);
        }

        // -------------------------------------------------------------------------
        // 24-hour cache
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task CheckAsync_WithinCacheWindow_DoesNotHitNetwork()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var handler = new CountingStubHandler(MakeReleaseJson("2.0.0"));
            var svc = new VersionCheckService(ApiUrl, prefs, false, new HttpClient(handler), () => "1.0.0");

            // First call — hits network
            await svc.CheckAsync();
            Assert.AreEqual(1, handler.CallCount);

            // Second call within 24h — should use cache
            await svc.CheckAsync();
            Assert.AreEqual(1, handler.CallCount);
        }

        [TestMethod]
        public async Task CheckAsync_CacheExpired_HitsNetworkAgain()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            // Seed cache with an expired timestamp and a stale version
            prefs.SetDateTime("UpdateCheck_LastCheck", DateTime.UtcNow.AddHours(-25));
            prefs.SetString("UpdateCheck_LatestVersion", "1.5.0");
            prefs.SetString("UpdateCheck_PageUrl", DefaultHtmlUrl);
            prefs.SetString("UpdateCheck_DownloadUrl", DefaultDownloadUrl);

            var handler = new CountingStubHandler(MakeReleaseJson("2.0.0"));
            var svc = new VersionCheckService(ApiUrl, prefs, false, new HttpClient(handler), () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.AreEqual(1, handler.CallCount);
            Assert.AreEqual("2.0", result.LatestVersion);
        }

        [TestMethod]
        public async Task CheckAsync_CacheHit_ReturnsCachedVersionData()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            prefs.SetDateTime("UpdateCheck_LastCheck", DateTime.UtcNow);
            prefs.SetString("UpdateCheck_LatestVersion", "3.0.0");
            prefs.SetString("UpdateCheck_PageUrl", DefaultHtmlUrl);
            prefs.SetString("UpdateCheck_DownloadUrl", DefaultDownloadUrl);

            // HTTP client that would fail if called
            var svc = new VersionCheckService(
                ApiUrl, prefs, false,
                MakeHttpClient("{}", HttpStatusCode.InternalServerError),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsTrue(result.IsUpdateAvailable);
            Assert.AreEqual("3.0.0", result.LatestVersion);
            Assert.AreEqual(DefaultDownloadUrl, result.DownloadUrl);
            Assert.IsFalse(result.CheckFailed);
        }

        [TestMethod]
        public async Task CheckAsync_CacheHit_PageUrlAndDownloadUrlPreferCached()
        {
            const string cachedPageUrl = "https://github.com/maxfranciosa-cmd/RewindGP/releases/tag/cached";
            const string cachedDownloadUrl = "https://github.com/maxfranciosa-cmd/RewindGP/releases/download/cached/RewindGP.zip";
            var prefs = new InMemoryCurrentVersionCheckStore();
            prefs.SetDateTime("UpdateCheck_LastCheck", DateTime.UtcNow);
            prefs.SetString("UpdateCheck_LatestVersion", "2.0.0");
            prefs.SetString("UpdateCheck_PageUrl", cachedPageUrl);
            prefs.SetString("UpdateCheck_DownloadUrl", cachedDownloadUrl);

            var svc = new VersionCheckService(
                ApiUrl, prefs, false,
                MakeHttpClient("{}", HttpStatusCode.InternalServerError),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.AreEqual(cachedPageUrl, result.PageUrl);
            Assert.AreEqual(cachedDownloadUrl, result.DownloadUrl);
        }

        // -------------------------------------------------------------------------
        // InvalidateCache
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task InvalidateCache_ForcesNetworkCallOnNextCheck()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var handler = new CountingStubHandler(MakeReleaseJson("2.0.0"));
            var svc = new VersionCheckService(ApiUrl, prefs, false, new HttpClient(handler), () => "1.0.0");

            await svc.CheckAsync(); // populates cache
            Assert.AreEqual(1, handler.CallCount);

            svc.InvalidateCache();

            await svc.CheckAsync(); // must hit network again
            Assert.AreEqual(2, handler.CallCount);
        }
    }

    // -------------------------------------------------------------------------
    // HTTP stub helpers
    // -------------------------------------------------------------------------

    internal class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        private readonly HttpStatusCode _statusCode;

        public StubHttpMessageHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseBody = responseBody;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    internal class CountingStubHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        public int CallCount { get; private set; }

        public CountingStubHandler(string responseBody) => _responseBody = responseBody;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
