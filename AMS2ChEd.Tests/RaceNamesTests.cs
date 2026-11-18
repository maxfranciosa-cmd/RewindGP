using AMS2ChEd.Business.Localization;
using AMS2ChEd.Business.Models.Concrete;
using System.Globalization;

namespace AMS2ChEd.Tests.Business
{
    [TestClass]
    public class RaceNamesTests
    {
        private static Race Race(string name, string key) => new Race { RaceId = 1, RaceName = name, RaceNameKey = key };

        private static T InCulture<T>(string culture, Func<T> action)
        {
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            try { return action(); }
            finally { CultureInfo.CurrentUICulture = previous; }
        }

        [TestMethod]
        public void KnownKey_IsTranslated()
        {
            var race = Race("Italian Grand Prix", "RaceName_ItalianGrandPrix");

            Assert.AreEqual("Italian Grand Prix", InCulture("en", race.DisplayName));
            Assert.AreEqual("Gran Premio d'Italia", InCulture("it", race.DisplayName));
            Assert.AreEqual("Großer Preis von Italien", InCulture("de", race.DisplayName));
            Assert.AreEqual("Grande Prêmio da Itália", InCulture("pt-BR", race.DisplayName));
            Assert.AreEqual("Gran Premio de Italia", InCulture("es-MX", race.DisplayName));
        }

        [TestMethod]
        public void NoKeyOrUnknownKey_ShowsTheEnglishName()
        {
            Assert.AreEqual("Gran Premio Fantasma", InCulture("it", Race("Gran Premio Fantasma", null).DisplayName));
            Assert.AreEqual("Moon Grand Prix", InCulture("it", Race("Moon Grand Prix", "RaceName_MoonGrandPrix").DisplayName));
        }

        [TestMethod]
        public void KeyFor_FindsTheKeyOfAnEnglishName()
        {
            Assert.AreEqual("RaceName_SaoPauloGrandPrixSprint", RaceNames.KeyFor("São Paulo Grand Prix (Sprint)"));
            Assert.IsNull(RaceNames.KeyFor("Moon Grand Prix"));
        }

        [TestMethod]
        public void Result_TakesTheKeyOfItsRaceInTheSameSeason()
        {
            var season = new Season { Year = 1996, Races = new List<Race> { Race("Italian Grand Prix", "RaceName_ItalianGrandPrix") } };
            var result = new GrandPrixResult { RaceId = 1, Year = 1996, GrandPrixName = "Italian Grand Prix" };
            var lastYear = new GrandPrixResult { RaceId = 1, Year = 1995, GrandPrixName = "Italian Grand Prix" };

            Assert.AreEqual("Gran Premio d'Italia", InCulture("it", () => result.DisplayName(season)));
            Assert.AreEqual("Italian Grand Prix", InCulture("it", () => lastYear.DisplayName(season)));
        }
    }
}
