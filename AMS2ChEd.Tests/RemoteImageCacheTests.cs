using AMS2ChEd.Business.Storage.Concrete;
using System.Net;
using System.Net.Http.Headers;

namespace AMS2ChEd.Tests.Business.Storage
{
    [TestClass]
    public class RemoteImageCacheTests
    {
        private const string Url = "https://example.com/photos/Senna.PNG";

        private string _folder;

        [TestInitialize]
        public void Setup()
        {
            _folder = Path.Combine(Path.GetTempPath(), "rewindgp_imagecache_" + Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, recursive: true);
        }

        [TestMethod]
        public void CacheFileName_IsStablePerUrl_KeepsAPlausibleExtension()
        {
            var name = RemoteImageCache.CacheFileName(Url);

            Assert.AreEqual(name, RemoteImageCache.CacheFileName(Url));
            Assert.AreNotEqual(name, RemoteImageCache.CacheFileName(Url + "?v=2"));
            StringAssert.EndsWith(name, ".png");
            Assert.AreEqual(64 + 4, name.Length);
        }

        [TestMethod]
        [DataRow("https://example.com/picture")]
        [DataRow("https://example.com/odd.p%20ng")]
        [DataRow("https://example.com/")]
        public void CacheFileName_NoUsableExtension_FallsBackToImg(string url)
        {
            StringAssert.EndsWith(RemoteImageCache.CacheFileName(url), ".img");
        }

        [TestMethod]
        public void CacheFileName_QueryStringIsNotPartOfTheExtension()
        {
            StringAssert.EndsWith(RemoteImageCache.CacheFileName("https://example.com/cover.jpg?w=800"), ".jpg");
        }

        [TestMethod]
        [DataRow("https://example.com/a.png", true)]
        [DataRow("http://example.com/a.png", true)]
        [DataRow("file:///C:/photos/a.png", false)]
        [DataRow("photos/a.png", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        public void IsRemoteUrl_OnlyHttpAndHttps(string url, bool expected)
        {
            Assert.AreEqual(expected, RemoteImageCache.IsRemoteUrl(url));
        }

        [TestMethod]
        public async Task DownloadAsync_StoresThePicture_AndLaterLookupsFindItWithoutDownloading()
        {
            var handler = new FakeHandler(_ => Picture(new byte[] { 1, 2, 3 }));
            var cache = new RemoteImageCache(_folder, new HttpClient(handler));

            Assert.IsNull(cache.TryGetCachedPath(Url));

            var path = await cache.DownloadAsync(Url);

            Assert.IsNotNull(path);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(path));
            Assert.AreEqual(path, cache.TryGetCachedPath(Url));

            var again = await cache.DownloadAsync(Url);
            Assert.AreEqual(path, again);
            Assert.AreEqual(1, handler.Requests);
        }

        [TestMethod]
        public async Task DownloadAsync_ConcurrentRequestsForOneUrl_DownloadOnce()
        {
            var release = new TaskCompletionSource();
            var handler = new FakeHandler(async _ =>
            {
                await release.Task;
                return Picture(new byte[] { 9 });
            });
            var cache = new RemoteImageCache(_folder, new HttpClient(handler));

            var first = cache.DownloadAsync(Url);
            var second = cache.DownloadAsync(Url);
            release.SetResult();

            Assert.AreEqual(await first, await second);
            Assert.AreEqual(1, handler.Requests);
        }

        [TestMethod]
        public async Task DownloadAsync_HttpError_CachesNothing()
        {
            var cache = new RemoteImageCache(_folder, new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))));

            Assert.IsNull(await cache.DownloadAsync(Url));
            Assert.IsNull(cache.TryGetCachedPath(Url));
            AssertNoTempFiles();
        }

        [TestMethod]
        public async Task DownloadAsync_HtmlServedInsteadOfAPicture_CachesNothing()
        {
            var cache = new RemoteImageCache(_folder, new HttpClient(new FakeHandler(_ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>moved</html>") };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
                return response;
            })));

            Assert.IsNull(await cache.DownloadAsync(Url));
            Assert.IsNull(cache.TryGetCachedPath(Url));
        }

        [TestMethod]
        public async Task DownloadAsync_EmptyBody_CachesNothing()
        {
            var cache = new RemoteImageCache(_folder, new HttpClient(new FakeHandler(_ => Picture(Array.Empty<byte>()))));

            Assert.IsNull(await cache.DownloadAsync(Url));
            Assert.IsNull(cache.TryGetCachedPath(Url));
            AssertNoTempFiles();
        }

        [TestMethod]
        public async Task DownloadAsync_NetworkFailure_ReturnsNullAndCanRetryLater()
        {
            var fail = true;
            var handler = new FakeHandler(_ => fail ? throw new HttpRequestException("offline") : Picture(new byte[] { 5 }));
            var cache = new RemoteImageCache(_folder, new HttpClient(handler));

            Assert.IsNull(await cache.DownloadAsync(Url));

            fail = false;
            Assert.IsNotNull(await cache.DownloadAsync(Url));
        }

        [TestMethod]
        public async Task DownloadAsync_NotARemoteUrl_DoesNothing()
        {
            var handler = new FakeHandler(_ => Picture(new byte[] { 1 }));
            var cache = new RemoteImageCache(_folder, new HttpClient(handler));

            Assert.IsNull(await cache.DownloadAsync("photos/local.png"));
            Assert.AreEqual(0, handler.Requests);
        }

        [TestMethod]
        public async Task Clear_RemovesEveryCachedPicture()
        {
            var cache = new RemoteImageCache(_folder, new HttpClient(new FakeHandler(_ => Picture(new byte[] { 1 }))));
            await cache.DownloadAsync(Url);
            await cache.DownloadAsync("https://example.com/other.jpg");

            Assert.AreEqual(2, cache.Clear());
            Assert.IsNull(cache.TryGetCachedPath(Url));
            Assert.AreEqual(0, new RemoteImageCache(Path.Combine(_folder, "missing"), new HttpClient()).Clear());
        }

        private void AssertNoTempFiles()
        {
            if (Directory.Exists(_folder))
                Assert.AreEqual(0, Directory.GetFiles(_folder, "*.tmp").Length);
        }

        private static HttpResponseMessage Picture(byte[] bytes)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return response;
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond;
            private int _requests;

            public int Requests => _requests;

            public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
                : this(request => Task.FromResult(respond(request)))
            {
            }

            public FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond)
            {
                _respond = respond;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _requests);
                return _respond(request);
            }
        }
    }
}
