using AMS2ChEd.Business.GameLogic.Concrete;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public sealed class RaceRulesErasTests
    {
        [TestMethod]
        public void For_PicksTheLatestEraStartedByTheSeason()
        {
            foreach (var era in RaceRulesEras.All)
            {
                Assert.AreSame(era, RaceRulesEras.For(era.FirstSeason));
                var next = RaceRulesEras.All.FirstOrDefault(e => e.FirstSeason > era.FirstSeason);
                if (next != null)
                    Assert.AreSame(era, RaceRulesEras.For(next.FirstSeason - 1));
            }
        }

        [TestMethod]
        public void For_FollowsTheF1Rules()
        {
            Assert.IsTrue(RaceRulesEras.For(1983).RefuellingAllowed);
            Assert.IsFalse(RaceRulesEras.For(1984).RefuellingAllowed);
            Assert.IsFalse(RaceRulesEras.For(1993).RefuellingAllowed);
            Assert.IsTrue(RaceRulesEras.For(1994).RefuellingAllowed);
            Assert.IsTrue(RaceRulesEras.For(2009).RefuellingAllowed);
            Assert.IsFalse(RaceRulesEras.For(2010).RefuellingAllowed);
            Assert.IsFalse(RaceRulesEras.For(2025).RefuellingAllowed);

            Assert.IsFalse(RaceRulesEras.For(1992).SafetyCar);
            Assert.IsTrue(RaceRulesEras.For(1993).SafetyCar);
            Assert.IsFalse(RaceRulesEras.For(1993).PitSpeedLimit);
            Assert.IsTrue(RaceRulesEras.For(1994).PitSpeedLimit);

            Assert.IsTrue(RaceRulesEras.All.All(e => e.StandingStart));
        }

        [TestMethod]
        public void For_ASeasonBeforeTheFirstEra_UsesTheFirstEra() =>
            Assert.AreSame(RaceRulesEras.All[0], RaceRulesEras.For(1959));

        [TestMethod]
        public void For_NoYear_IsTheModernEra() =>
            Assert.AreSame(RaceRulesEras.Modern, RaceRulesEras.For(null));

        [TestMethod]
        public void All_IsSortedWithoutDuplicateFirstSeasons()
        {
            var firstSeasons = RaceRulesEras.All.Select(e => e.FirstSeason).ToList();
            CollectionAssert.AreEqual(firstSeasons.OrderBy(y => y).ToList(), firstSeasons);
            Assert.AreEqual(firstSeasons.Count, firstSeasons.Distinct().Count());
        }
    }
}
