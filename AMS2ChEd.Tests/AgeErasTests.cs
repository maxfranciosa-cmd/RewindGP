using AMS2ChEd.Business.GameLogic.Concrete;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public sealed class AgeErasTests
    {
        [TestMethod]
        public void For_PicksTheLatestEraStartedByTheSeason()
        {
            foreach (var era in AgeEras.All)
            {
                Assert.AreSame(era, AgeEras.For(era.FirstSeason));
                var next = AgeEras.All.FirstOrDefault(e => e.FirstSeason > era.FirstSeason);
                if (next != null)
                    Assert.AreSame(era, AgeEras.For(next.FirstSeason - 1));
            }
        }

        [TestMethod]
        public void For_ClassicAndModernSeasons()
        {
            Assert.AreEqual(27, AgeEras.For(1960).YoungDriverAge);
            Assert.AreEqual(33, AgeEras.For(2005).OldDriverAge);
            Assert.AreEqual(25, AgeEras.For(2006).YoungDriverAge);
            Assert.AreEqual(31, AgeEras.For(2025).OldDriverAge);
            Assert.AreEqual(45, AgeEras.For(1996).MaxRetiringAge);
            Assert.AreEqual(45, AgeEras.For(2025).MaxRetiringAge);
        }

        [TestMethod]
        public void For_ASeasonBeforeTheFirstEra_GetsTheFirstEra()
        {
            Assert.AreSame(AgeEras.All[0], AgeEras.For(AgeEras.All[0].FirstSeason - 10));
        }

        [TestMethod]
        public void For_NoYear_IsTheModernEra()
        {
            Assert.AreSame(AgeEras.Modern, AgeEras.For(null));
            Assert.AreSame(AgeEras.All[AgeEras.All.Count - 1], AgeEras.Modern);
        }

        [TestMethod]
        public void All_IsOrderedAndEachEraIsConsistent()
        {
            for (var i = 1; i < AgeEras.All.Count; i++)
                Assert.IsTrue(AgeEras.All[i - 1].FirstSeason < AgeEras.All[i].FirstSeason, "eras must be ordered by FirstSeason, without duplicates");

            foreach (var era in AgeEras.All)
            {
                Assert.IsTrue(era.YoungDriverAge < era.OldDriverAge, $"{era}: young must be below old");
                Assert.IsTrue(era.MinRetiringAge <= era.MaxRetiringAge, $"{era}: min retiring age above max");
            }
        }
    }
}
