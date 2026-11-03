using System.Net;
using System.Net.Http;
using Ams2ChEd.Business.AMS2.Services;
using Ams2Interop.Signatures;

namespace AMS2ChEd.Tests.Ams2Interop
{
    [TestClass]
    public class Ams2SignatureUpdaterTests
    {
        private string _dir = null!;

        [TestInitialize]
        public void Init()
        {
            _dir = Path.Combine(Path.GetTempPath(), $"ams2_sig_updater_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void Cleanup() => Directory.Delete(_dir, recursive: true);

        private string PathOf(string name) => Path.Combine(_dir, name);

        private static string FileJson(DateTime generatedAt, bool withKnownBuild = true)
        {
            var file = new Ams2SignatureFile { GeneratedFromBuild = "BUILD", GeneratedAt = generatedAt };
            if (withKnownBuild) file.KnownBuilds["BUILD"] = new Dictionary<string, long> { ["Setter"] = 0x1000 };
            var path = Path.GetTempFileName();
            try
            {
                file.Save(path);
                return File.ReadAllText(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static HttpClient Serving(HttpStatusCode status, string body) => new(new StubHandler(status, body));

        [TestMethod]
        public async Task Download_ValidFile_Written()
        {
            var json = FileJson(new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc));
            var target = PathOf("dl.json");

            var updated = await Ams2SignatureUpdater.DownloadAsync(Serving(HttpStatusCode.OK, json), "https://x/y.json", target, null, CancellationToken.None);

            Assert.IsTrue(updated);
            Assert.AreEqual(json, File.ReadAllText(target));
            Assert.IsFalse(File.Exists(target + ".download"));
        }

        [TestMethod]
        public async Task Download_SameContentAgain_NotRewritten()
        {
            var json = FileJson(new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc));
            var target = PathOf("dl.json");
            File.WriteAllText(target, json);

            var updated = await Ams2SignatureUpdater.DownloadAsync(Serving(HttpStatusCode.OK, json), "https://x/y.json", target, null, CancellationToken.None);

            Assert.IsFalse(updated);
        }

        [TestMethod]
        [DataRow(HttpStatusCode.NotFound, "404: Not Found")]
        [DataRow(HttpStatusCode.OK, "<html>not json</html>")]
        [DataRow(HttpStatusCode.OK, "{\"format_version\": 9, \"signatures\": {}}")]
        [DataRow(HttpStatusCode.OK, "{\"format_version\": 2, \"signatures\": {}}")] // nothing in it
        public async Task Download_BadResponse_KeepsTheFileOnDisk(HttpStatusCode status, string body)
        {
            var target = PathOf("dl.json");
            var existing = FileJson(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
            File.WriteAllText(target, existing);
            var log = new List<string>();

            var updated = await Ams2SignatureUpdater.DownloadAsync(Serving(status, body), "https://x/y.json", target, log.Add, CancellationToken.None);

            Assert.IsFalse(updated);
            Assert.AreEqual(existing, File.ReadAllText(target));
            Assert.AreEqual(1, log.Count, string.Join(" | ", log));
        }

        [TestMethod]
        public async Task Download_TooBig_Refused()
        {
            var target = PathOf("dl.json");
            var huge = "{\"format_version\": 2, \"signatures\": {}, \"pad\": \"" + new string('x', Ams2SignatureUpdater.MaxDownloadBytes) + "\"}";

            var updated = await Ams2SignatureUpdater.DownloadAsync(Serving(HttpStatusCode.OK, huge), "https://x/y.json", target, null, CancellationToken.None);

            Assert.IsFalse(updated);
            Assert.IsFalse(File.Exists(target));
        }

        [TestMethod]
        public void Choose_DownloadedNewerOrEqual_UsesDownloaded()
        {
            File.WriteAllText(PathOf("shipped.json"), FileJson(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)));
            File.WriteAllText(PathOf("dl.json"), FileJson(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)));

            Assert.AreEqual(PathOf("dl.json"), Ams2SignatureUpdater.ChooseSignatureFile(PathOf("shipped.json"), PathOf("dl.json"), null));
        }

        [TestMethod]
        public void Choose_ShippedNewer_UsesShipped()
        {
            File.WriteAllText(PathOf("shipped.json"), FileJson(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)));
            File.WriteAllText(PathOf("dl.json"), FileJson(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)));

            Assert.AreEqual(PathOf("shipped.json"), Ams2SignatureUpdater.ChooseSignatureFile(PathOf("shipped.json"), PathOf("dl.json"), null));
        }

        [TestMethod]
        public void Choose_DownloadedUnreadableOrMissing_UsesShipped()
        {
            File.WriteAllText(PathOf("shipped.json"), FileJson(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)));
            File.WriteAllText(PathOf("broken.json"), "{ not json");

            Assert.AreEqual(PathOf("shipped.json"), Ams2SignatureUpdater.ChooseSignatureFile(PathOf("shipped.json"), PathOf("broken.json"), null));
            Assert.AreEqual(PathOf("shipped.json"), Ams2SignatureUpdater.ChooseSignatureFile(PathOf("shipped.json"), PathOf("none.json"), null));
        }

        [TestMethod]
        public void Choose_OnlyDownloaded_UsesDownloaded()
        {
            File.WriteAllText(PathOf("dl.json"), FileJson(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)));

            Assert.AreEqual(PathOf("dl.json"), Ams2SignatureUpdater.ChooseSignatureFile(PathOf("none.json"), PathOf("dl.json"), null));
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _body;

            public StubHandler(HttpStatusCode status, string body)
            {
                _status = status;
                _body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
        }
    }
}
