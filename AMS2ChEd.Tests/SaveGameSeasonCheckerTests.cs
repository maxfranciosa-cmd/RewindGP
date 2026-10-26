using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Updater;
using AMS2ChEd.Business.Updater.Models;

namespace AMS2ChEd.Tests.Business.Updater
{
    [TestClass]
    public class SaveGameSeasonCheckerTests
    {
        private static readonly DateTime BaseDate = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        private static SaveGame CreateSave(int year, SeasonDataStamp stamp, DateTime? timestamp = null) => new SaveGame
        {
            CurrentSeason = new Season { Year = year },
            SeasonDataStamp = stamp,
            Timestamp = timestamp ?? BaseDate
        };

        private static SeasonDataStamp Stamp(int year, string baked, string pack) =>
            new SeasonDataStamp { Year = year, Baked = baked, Pack = pack };

        private static MockSeasonLoader LoaderWith(int year, string baked, string pack)
        {
            var loader = new MockSeasonLoader();
            loader.AddBakedOnlySeason(year.ToString());
            loader.SetSeasonDataStamp(year, baked, pack);
            return loader;
        }

        [TestMethod]
        public void SameData_Proceeds()
        {
            var save = CreateSave(1996, Stamp(1996, "b1", "p1"));
            var checker = new SaveGameSeasonChecker(LoaderWith(1996, "b1", "p1"));

            Assert.AreEqual(SaveGameSeasonCheckerResult.Proceed, checker.CheckIfSaveGameNeedsRefresh(save));
        }

        [TestMethod]
        public void AppDataChanged_NeedsRefresh()
        {
            var save = CreateSave(1996, Stamp(1996, "b1", "p1"));
            var checker = new SaveGameSeasonChecker(LoaderWith(1996, "b2", "p1"));

            Assert.AreEqual(SaveGameSeasonCheckerResult.NeedsRefresh, checker.CheckIfSaveGameNeedsRefresh(save));
            Assert.AreEqual("b2", save.SeasonDataStamp.Baked, "the save takes on the data it is refreshed with");
        }

        [DataTestMethod]
        [DataRow(null, "p1", DisplayName = "pack installed")]
        [DataRow("p1", "p2", DisplayName = "pack updated")]
        [DataRow("p1", null, DisplayName = "pack removed")]
        public void PackChangedOverAppData_NeedsArtworkRefresh(string packBefore, string packNow)
        {
            var save = CreateSave(1996, Stamp(1996, "b1", packBefore));
            var checker = new SaveGameSeasonChecker(LoaderWith(1996, "b1", packNow));

            Assert.AreEqual(SaveGameSeasonCheckerResult.NeedsArtworkRefresh, checker.CheckIfSaveGameNeedsRefresh(save));
            Assert.AreEqual(packNow, save.SeasonDataStamp.Pack);
        }

        [DataTestMethod]
        [DataRow("c1", "c2", DisplayName = "another year's pack changed")]
        [DataRow(null, "c1", DisplayName = "stamped before cosmetic fallbacks")]
        public void OtherPacksChangedOverAppData_NeedsArtworkRefresh(string cosmeticsBefore, string cosmeticsNow)
        {
            var save = CreateSave(1996, new SeasonDataStamp { Year = 1996, Baked = "b1", Pack = "p1", Cosmetics = cosmeticsBefore });
            var loader = LoaderWith(1996, "b1", "p1");
            loader.GetSeasonDataStamp(1996).Cosmetics = cosmeticsNow;
            var checker = new SaveGameSeasonChecker(loader);

            Assert.AreEqual(SaveGameSeasonCheckerResult.NeedsArtworkRefresh, checker.CheckIfSaveGameNeedsRefresh(save));
            Assert.AreEqual(cosmeticsNow, save.SeasonDataStamp.Cosmetics);
            Assert.AreEqual(SaveGameSeasonCheckerResult.Proceed, checker.CheckIfSaveGameNeedsRefresh(save), "refreshed once");
        }

        [TestMethod]
        public void OtherPacksChangedWithNoAppData_Proceeds()
        {
            var save = CreateSave(1994, new SeasonDataStamp { Year = 1994, Pack = "p1", Cosmetics = "c1" });
            var loader = LoaderWith(1994, null, "p1");
            loader.GetSeasonDataStamp(1994).Cosmetics = "c2";
            var checker = new SaveGameSeasonChecker(loader);

            Assert.AreEqual(SaveGameSeasonCheckerResult.Proceed, checker.CheckIfSaveGameNeedsRefresh(save));
        }

        [TestMethod]
        public void PackChangedWithNoAppData_NeedsRefresh()
        {
            var save = CreateSave(1994, Stamp(1994, null, "p1"));
            var checker = new SaveGameSeasonChecker(LoaderWith(1994, null, "p2"));

            Assert.AreEqual(SaveGameSeasonCheckerResult.NeedsRefresh, checker.CheckIfSaveGameNeedsRefresh(save));
        }

        [TestMethod]
        public void SeasonReusedWhenItHadNoData_DataArrived_NeedsRefresh()
        {
            // stamped at the off-season, when the year had nothing and the previous season was reused
            var save = CreateSave(1999, Stamp(1999, null, null));
            var checker = new SaveGameSeasonChecker(LoaderWith(1999, "b1", null));

            Assert.AreEqual(SaveGameSeasonCheckerResult.NeedsRefresh, checker.CheckIfSaveGameNeedsRefresh(save));
        }

        [TestMethod]
        public void NoStamp_FileTimesDecide_AndTheSaveGetsStamped()
        {
            var loader = new MockSeasonLoader();
            loader.AddSeason("1996", BaseDate);
            loader.SetSeasonDataStamp(1996, "b1", "p1");
            var checker = new SaveGameSeasonChecker(loader);

            var older = CreateSave(1996, null, BaseDate.AddDays(-1));
            Assert.AreEqual(SaveGameSeasonCheckerResult.NeedsRefresh, checker.CheckIfSaveGameNeedsRefresh(older));
            Assert.AreEqual("b1", older.SeasonDataStamp.Baked);

            var newer = CreateSave(1996, null, BaseDate.AddDays(1));
            Assert.AreEqual(SaveGameSeasonCheckerResult.Proceed, checker.CheckIfSaveGameNeedsRefresh(newer));
        }

        [TestMethod]
        public void StampOfAnEarlierSeason_FileTimesDecide()
        {
            var loader = new MockSeasonLoader();
            loader.AddSeason("1997", BaseDate);
            loader.SetSeasonDataStamp(1997, "b1", "p1");
            var checker = new SaveGameSeasonChecker(loader);

            var save = CreateSave(1997, Stamp(1996, "b1", "p1"), BaseDate.AddDays(1));

            Assert.AreEqual(SaveGameSeasonCheckerResult.Proceed, checker.CheckIfSaveGameNeedsRefresh(save));
            Assert.AreEqual(1997, save.SeasonDataStamp.Year);
        }

        [TestMethod]
        public void SeasonNotAvailable_Proceeds_AndKeepsTheStamp()
        {
            var stamp = Stamp(1999, null, null);
            var save = CreateSave(1999, stamp);
            var checker = new SaveGameSeasonChecker(new MockSeasonLoader());

            Assert.AreEqual(SaveGameSeasonCheckerResult.Proceed, checker.CheckIfSaveGameNeedsRefresh(save));
            Assert.AreSame(stamp, save.SeasonDataStamp);
        }
    }
}
