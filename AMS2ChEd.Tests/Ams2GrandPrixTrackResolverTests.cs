using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class Ams2GrandPrixTrackResolverTests
    {
        private static Ams2TrackMappingEntry BrazilianGpEntry(string dlcId = null, int year = 1990) => new Ams2TrackMappingEntry
        {
            GrandPrixNamePatterns = new List<string> { "Brazilian", "do Brasil" },
            Year = year,
            BestTrackId = "interlagos_2020",
            DlcId = dlcId,
            DefaultTrackId = "interlagos_1990s",
            DefaultNumberOfLaps = 71,
        };

        private static Ams2GrandPrixTrackResolver MakeResolver(
            IReadOnlyList<Ams2TrackMappingEntry> mappings, bool dlcOwned = false)
        {
            var loader = new Mock<ITrackMappingLoader>();
            loader.Setup(l => l.GetAll()).Returns(mappings);

            var dlcChecker = new Mock<IAms2DlcOwnershipChecker>();
            dlcChecker.Setup(c => c.IsOwned(It.IsAny<string>())).Returns(dlcOwned);

            return new Ams2GrandPrixTrackResolver(loader.Object, dlcChecker.Object);
        }

        [TestMethod]
        public void ResolveTrack_NoDlc_ReturnsDefaultTrackId()
        {
            var resolver = MakeResolver(new List<Ams2TrackMappingEntry> { BrazilianGpEntry(dlcId: null) });

            var result = resolver.ResolveTrack("Brazilian Grand Prix", "BRA", seasonYear: 1996);

            Assert.IsNotNull(result);
            Assert.AreEqual("interlagos_1990s", result.TrackId);
            Assert.AreEqual(71, result.DefaultNumberOfLaps);
        }

        [TestMethod]
        public void ResolveTrack_DlcSetAndOwned_ReturnsBestTrackId()
        {
            var resolver = MakeResolver(
                new List<Ams2TrackMappingEntry> { BrazilianGpEntry(dlcId: "interlagos_dlc") },
                dlcOwned: true);

            var result = resolver.ResolveTrack("Brazilian Grand Prix", "BRA", seasonYear: 1996);

            Assert.AreEqual("interlagos_2020", result.TrackId);
        }

        [TestMethod]
        public void ResolveTrack_DlcSetButNotOwned_FallsBackToDefaultTrackId()
        {
            var resolver = MakeResolver(
                new List<Ams2TrackMappingEntry> { BrazilianGpEntry(dlcId: "interlagos_dlc") },
                dlcOwned: false);

            var result = resolver.ResolveTrack("Brazilian Grand Prix", "BRA", seasonYear: 1996);

            Assert.AreEqual("interlagos_1990s", result.TrackId);
        }

        [TestMethod]
        public void ResolveTrack_MatchesShortNamePatternCaseInsensitively()
        {
            var entry = new Ams2TrackMappingEntry
            {
                GrandPrixNamePatterns = new List<string> { "aus" },
                Year = 1996,
                DefaultTrackId = "albert_park",
                DefaultNumberOfLaps = 58,
            };
            var resolver = MakeResolver(new List<Ams2TrackMappingEntry> { entry });

            var result = resolver.ResolveTrack("Australian Grand Prix", "AUS", seasonYear: 1996);

            Assert.AreEqual("albert_park", result.TrackId);
        }

        [TestMethod]
        public void ResolveTrack_NoMatchingEntry_ReturnsNull()
        {
            var resolver = MakeResolver(new List<Ams2TrackMappingEntry> { BrazilianGpEntry() });

            var result = resolver.ResolveTrack("Monaco Grand Prix", "MON", seasonYear: 1996);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ResolveTrack_EmptyRegistry_ReturnsNull()
        {
            var resolver = MakeResolver(new List<Ams2TrackMappingEntry>());

            var result = resolver.ResolveTrack("Brazilian Grand Prix", "BRA", seasonYear: 1996);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ResolveTrack_MultipleErasForSamePattern_PicksLargestYearNotAfterSeasonYear()
        {
            var old = BrazilianGpEntry(year: 1990);
            old.DefaultTrackId = "interlagos_1990";
            var mid = BrazilianGpEntry(year: 1993);
            mid.DefaultTrackId = "interlagos_1993";
            var newest = BrazilianGpEntry(year: 2000);
            newest.DefaultTrackId = "interlagos_2000";

            var resolver = MakeResolver(new List<Ams2TrackMappingEntry> { old, mid, newest });

            Assert.AreEqual("interlagos_1990", resolver.ResolveTrack("Brazilian Grand Prix", "BRA", seasonYear: 1991).TrackId);
            Assert.AreEqual("interlagos_1993", resolver.ResolveTrack("Brazilian Grand Prix", "BRA", seasonYear: 1996).TrackId);
            Assert.AreEqual("interlagos_1993", resolver.ResolveTrack("Brazilian Grand Prix", "BRA", seasonYear: 1999).TrackId);
            Assert.AreEqual("interlagos_2000", resolver.ResolveTrack("Brazilian Grand Prix", "BRA", seasonYear: 2005).TrackId);
        }

        [TestMethod]
        public void ResolveTrack_SeasonYearBeforeEveryEntrysYear_ReturnsNull()
        {
            var resolver = MakeResolver(new List<Ams2TrackMappingEntry> { BrazilianGpEntry(year: 1990) });

            var result = resolver.ResolveTrack("Brazilian Grand Prix", "BRA", seasonYear: 1985);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ResolveTrack_SeasonYearExactlyMatchesEntrysYear_Matches()
        {
            var resolver = MakeResolver(new List<Ams2TrackMappingEntry> { BrazilianGpEntry(year: 1990) });

            var result = resolver.ResolveTrack("Brazilian Grand Prix", "BRA", seasonYear: 1990);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void ResolveTrack_MatchingEntryHasNullDefaultTrackIdAndNoDlc_ReturnsNullRatherThanCrashing()
        {
            var entry = new Ams2TrackMappingEntry
            {
                GrandPrixNamePatterns = new List<string> { "Australian" },
                Year = 0,
                DlcId = null,
                DefaultTrackId = null,
                DefaultNumberOfLaps = 58,
            };
            var resolver = MakeResolver(new List<Ams2TrackMappingEntry> { entry });

            var result = resolver.ResolveTrack("Australian Grand Prix", "AUS", seasonYear: 1996);

            Assert.IsNull(result);
        }
    }
}
