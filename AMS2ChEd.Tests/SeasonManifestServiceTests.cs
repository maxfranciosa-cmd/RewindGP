using AMS2ChEd.Business.Updater.Models;

namespace AMS2ChEd.Tests.Business.Updater
{
    [TestClass]
    public class SeasonManifestServiceTests
    {
        private static readonly DateTime BaseDate = new(2025, 2, 10, 0, 0, 0, DateTimeKind.Utc);

        // -------------------------------------------------------------------------
        // GetAvailability — NotAvailable
        // -------------------------------------------------------------------------

        [TestMethod]
        public void GetAvailability_YearNotInManifestAndNotInstalled_ReturnsNotAvailable()
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var svc = SeasonManifestServiceFactory.Create(manifest, new MockSeasonLoader());

            var result = svc.GetAvailability(1994); // not in manifest, not installed

            Assert.AreEqual(SeasonAvailability.NotAvailable, result);
        }

        // -------------------------------------------------------------------------
        // GetAvailability — NotInstalled
        // -------------------------------------------------------------------------

        [TestMethod]
        public void GetAvailability_PlayableFromAppDataButNoPack_ReturnsBakedPackAvailable()
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var mockSeasonLoader = new MockSeasonLoader();
            mockSeasonLoader.AddBakedOnlySeason("1996");
            var svc = SeasonManifestServiceFactory.Create(manifest, mockSeasonLoader);

            Assert.AreEqual(SeasonAvailability.BakedPackAvailable, svc.GetAvailability(1996));
            Assert.IsFalse(svc.IsInstalled(1996));
        }

        [TestMethod]
        public void GetAvailability_PlayableFromAppDataNoPackListed_ReturnsBakedOnly()
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var mockSeasonLoader = new MockSeasonLoader();
            mockSeasonLoader.AddBakedOnlySeason("1988");
            var svc = SeasonManifestServiceFactory.Create(manifest, mockSeasonLoader);

            Assert.AreEqual(SeasonAvailability.BakedOnly, svc.GetAvailability(1988));
        }

        [TestMethod]
        public void GetAvailability_PackInstalledOverAppData_ChecksThePackForUpdates()
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var mockSeasonLoader = new MockSeasonLoader();
            mockSeasonLoader.AddBakedSeason("1996");
            mockSeasonLoader.AddSeason("1996", BaseDate.AddDays(-5));
            var svc = SeasonManifestServiceFactory.Create(manifest, mockSeasonLoader);

            Assert.AreEqual(SeasonAvailability.UpdateAvailable, svc.GetAvailability(1996));
        }

        [TestMethod]
        public void GetSeasonCatalog_ListsAppDataSeasonsTheManifestDoesNot()
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var mockSeasonLoader = new MockSeasonLoader();
            mockSeasonLoader.AddBakedOnlySeason("1988");
            mockSeasonLoader.AddBakedOnlySeason("1996");
            var svc = SeasonManifestServiceFactory.Create(manifest, mockSeasonLoader);

            var catalog = svc.GetSeasonCatalog();

            CollectionAssert.AreEqual(new[] { 1988, 1996 }, catalog.Select(c => c.Year).ToArray());
            Assert.AreEqual(SeasonAvailability.BakedOnly, catalog[0].Availability);
            Assert.AreEqual(SeasonAvailability.BakedPackAvailable, catalog[1].Availability);
        }

        [TestMethod]
        public void GetAvailability_InManifestButNotInstalled_ReturnsNotInstalled()
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var svc = SeasonManifestServiceFactory.Create(manifest, new MockSeasonLoader());
            // No season.json on disk

            var result = svc.GetAvailability(1996);

            Assert.AreEqual(SeasonAvailability.NotInstalled, result);
        }

        // -------------------------------------------------------------------------
        // GetAvailability — InstalledNoRemote
        // -------------------------------------------------------------------------

        [TestMethod]
        public void GetAvailability_InstalledButNotInManifest_ReturnsInstalledNoRemote()
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var mockSeasonLoader = new MockSeasonLoader();
            var svc = SeasonManifestServiceFactory.Create(manifest, mockSeasonLoader);

            // 1997 is installed but not in the manifest
            mockSeasonLoader.AddSeason("1997", BaseDate);

            var result = svc.GetAvailability(1997);

            Assert.AreEqual(SeasonAvailability.InstalledNoRemote, result);
        }

        // -------------------------------------------------------------------------
        // GetAvailability — UpdateAvailable
        // -------------------------------------------------------------------------

        [TestMethod]
        public void GetAvailability_InstalledWithOlderDate_ReturnsUpdateAvailable()
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var mockSeasonLoader = new MockSeasonLoader();
            var svc = SeasonManifestServiceFactory.Create(manifest, mockSeasonLoader);

            // Installed season.json has an older timestamp
            mockSeasonLoader.AddSeason("1996", BaseDate.AddDays(-5));

            var result = svc.GetAvailability(1996);

            Assert.AreEqual(SeasonAvailability.UpdateAvailable, result);
        }

        [TestMethod]
        public void GetAvailability_InstalledWithSameDate_ReturnsUpToDate()
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var mockSeasonLoader = new MockSeasonLoader();
            var svc = SeasonManifestServiceFactory.Create(manifest, mockSeasonLoader);

            mockSeasonLoader.AddSeason("1996", BaseDate);

            var result = svc.GetAvailability(1996);

            Assert.AreEqual(SeasonAvailability.UpToDate, result);
        }

        [TestMethod]
        public void GetAvailability_InstalledWithNewerDate_ReturnsUpToDate()
        {
            // Edge case: installed file is somehow newer — treat as up-to-date, not an "update"
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var mockSeasonLoader = new MockSeasonLoader();
            var svc = SeasonManifestServiceFactory.Create(manifest, mockSeasonLoader);

            mockSeasonLoader.AddSeason("1996", BaseDate.AddDays(1));

            var result = svc.GetAvailability(1996);

            Assert.AreEqual(SeasonAvailability.UpToDate, result);
        }

        // -------------------------------------------------------------------------
        // Timestamp tolerance
        // -------------------------------------------------------------------------

        [TestMethod]
        public void GetAvailability_InstalledDateWithin5SecondTolerance_ReturnsUpToDate()
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var mockSeasonLoader = new MockSeasonLoader();
            var svc = SeasonManifestServiceFactory.Create(manifest, mockSeasonLoader);

            // 3 seconds behind — within the 5-second tolerance
            mockSeasonLoader.AddSeason("1996", BaseDate.AddSeconds(-3));

            var result = svc.GetAvailability(1996);

            Assert.AreEqual(SeasonAvailability.UpToDate, result);
        }

        [TestMethod]
        public void GetAvailability_InstalledDateOutsideTolerance_ReturnsUpdateAvailable()
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var mockSeasonLoader = new MockSeasonLoader();
            var svc = SeasonManifestServiceFactory.Create(manifest, mockSeasonLoader);

            // 10 seconds behind — outside the 5-second tolerance
            mockSeasonLoader.AddSeason("1996", BaseDate.AddSeconds(-10));

            var result = svc.GetAvailability(1996);

            Assert.AreEqual(SeasonAvailability.UpdateAvailable, result);
        }

        // -------------------------------------------------------------------------
        // Manifest caching
        // -------------------------------------------------------------------------

        [TestMethod]
        public void GetManifest_CalledTwice_ReturnsSameInstance()
        {
            var manifest = ManifestBuilder.Build((1996, BaseDate, "https://example.com/1996.rwgp"));
            var mockSeasonLoader = new MockSeasonLoader();
            var svc = SeasonManifestServiceFactory.Create(manifest, mockSeasonLoader);

            var first = svc.GetManifest();
            var second = svc.GetManifest();

            Assert.AreSame(first, second);
        }

        // -------------------------------------------------------------------------
        // Multiple seasons
        // -------------------------------------------------------------------------

        [TestMethod]
        public void GetAvailability_MultipleSeasons_EachEvaluatedIndependently()
        {
            var manifest = ManifestBuilder.Build(
                (1996, BaseDate, "https://example.com/1996.rwgp"),
                (1997, BaseDate.AddDays(10), "https://example.com/1997.rwgp"));

            var mockSeasonLoader = new MockSeasonLoader();
            var svc = SeasonManifestServiceFactory.Create(manifest, mockSeasonLoader);

            // 1996: installed and up to date
            mockSeasonLoader.AddSeason("1996", BaseDate);

            // 1997: not installed

            Assert.AreEqual(SeasonAvailability.UpToDate, svc.GetAvailability(1996));
            Assert.AreEqual(SeasonAvailability.NotInstalled, svc.GetAvailability(1997));
        }
    }
}