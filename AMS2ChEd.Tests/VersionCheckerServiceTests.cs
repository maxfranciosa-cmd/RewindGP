using AMS2ChEd.Business.Updater;
using AMS2ChEd.Business.Updater.Services;
using System.Net;
using System.Text;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class VersionCheckerServiceTests
    {
        private const string PageUrl = "https://www.overtake.gg/downloads/rewind-gp.test/";

        // -------------------------------------------------------------------------
        // HTML stub helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Produces a minimal HTML page containing a JSON-LD block with
        /// mainEntity.version, matching what VersionCheckService.ExtractVersion parses.
        /// </summary>
        private static string MakeHtmlPage(string version) => $@"<!DOCTYPE html>
<html>
<head>
<script type=""application/ld+json"">
{{
  ""@context"": ""https://schema.org"",
  ""@type"": ""WebPage"",
  ""mainEntity"": {{
    ""@type"": ""SoftwareApplication"",
    ""name"": ""Rewind GP"",
    ""version"": ""{version}""
  }}
}}
</script>
</head>
<body><p>Download page</p></body>
</html>";

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
                PageUrl, prefs, false,
                MakeHttpClient(MakeHtmlPage("2.0.0")),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsTrue(result.IsUpdateAvailable);
            Assert.AreEqual("2.0.0", result.LatestVersion);
            Assert.IsFalse(result.CheckFailed);
        }

        [TestMethod]
        public async Task CheckAsync_RemoteVersionSame_ReturnsNoUpdate()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var svc = new VersionCheckService(
                PageUrl, prefs, false,
                MakeHttpClient(MakeHtmlPage("1.0.0")),
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
                PageUrl, prefs, false,
                MakeHttpClient(MakeHtmlPage("0.9.0")),
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
                PageUrl, prefs, forceUpdate: true,
                MakeHttpClient(MakeHtmlPage("1.0.0")),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsTrue(result.IsUpdateAvailable);
            Assert.IsFalse(result.CheckFailed);
        }

        // -------------------------------------------------------------------------
        // Network / parse failures
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task CheckAsync_NetworkError_ReturnsCheckFailed()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var svc = new VersionCheckService(
                PageUrl, prefs, false,
                MakeHttpClient("<html/>", HttpStatusCode.InternalServerError),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsTrue(result.CheckFailed);
            Assert.IsFalse(result.IsUpdateAvailable);
            Assert.AreEqual("1.0.0", result.CurrentVersion);
        }

        [TestMethod]
        public async Task CheckAsync_HtmlWithNoVersionData_ReturnsCheckFailed()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var svc = new VersionCheckService(
                PageUrl, prefs, false,
                MakeHttpClient("<html><body>no json-ld here</body></html>"),
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
            var handler = new CountingStubHandler(MakeHtmlPage("2.0.0"));
            var svc = new VersionCheckService(PageUrl, prefs, false, new HttpClient(handler), () => "1.0.0");

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
            prefs.SetString("UpdateCheck_PageUrl", PageUrl);

            var handler = new CountingStubHandler(MakeHtmlPage("2.0.0"));
            var svc = new VersionCheckService(PageUrl, prefs, false, new HttpClient(handler), () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.AreEqual(1, handler.CallCount);
            Assert.AreEqual("2.0.0", result.LatestVersion);
        }

        [TestMethod]
        public async Task CheckAsync_CacheHit_ReturnsCachedVersionData()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            prefs.SetDateTime("UpdateCheck_LastCheck", DateTime.UtcNow);
            prefs.SetString("UpdateCheck_LatestVersion", "3.0.0");
            prefs.SetString("UpdateCheck_PageUrl", PageUrl);

            // HTTP client that would fail if called
            var svc = new VersionCheckService(
                PageUrl, prefs, false,
                MakeHttpClient("<html/>", HttpStatusCode.InternalServerError),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.IsTrue(result.IsUpdateAvailable);
            Assert.AreEqual("3.0.0", result.LatestVersion);
            Assert.IsFalse(result.CheckFailed);
        }

        [TestMethod]
        public async Task CheckAsync_CacheHit_PageUrlPrefersCachedUrl()
        {
            const string cachedUrl = "https://www.overtake.gg/downloads/rewind-gp.cached/";
            var prefs = new InMemoryCurrentVersionCheckStore();
            prefs.SetDateTime("UpdateCheck_LastCheck", DateTime.UtcNow);
            prefs.SetString("UpdateCheck_LatestVersion", "2.0.0");
            prefs.SetString("UpdateCheck_PageUrl", cachedUrl);

            var svc = new VersionCheckService(
                PageUrl, prefs, false,
                MakeHttpClient("<html/>", HttpStatusCode.InternalServerError),
                () => "1.0.0");

            var result = await svc.CheckAsync();

            Assert.AreEqual(cachedUrl, result.PageUrl);
        }

        // -------------------------------------------------------------------------
        // InvalidateCache
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task InvalidateCache_ForcesNetworkCallOnNextCheck()
        {
            var prefs = new InMemoryCurrentVersionCheckStore();
            var handler = new CountingStubHandler(MakeHtmlPage("2.0.0"));
            var svc = new VersionCheckService(PageUrl, prefs, false, new HttpClient(handler), () => "1.0.0");

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
                Content = new StringContent(_responseBody, Encoding.UTF8, "text/html")
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
                Content = new StringContent(_responseBody, Encoding.UTF8, "text/html")
            };
            return Task.FromResult(response);
        }
    }
}