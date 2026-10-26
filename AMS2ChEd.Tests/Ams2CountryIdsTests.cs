using Ams2ChEd.Business.AMS2.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class Ams2CountryIdsTests
    {
        // the two ids confirmed in the game: its default (United Kingdom) and Italy
        [TestMethod]
        public void For_IsoCode_GivesTheGamesCountryId()
        {
            Assert.AreEqual(0x759924D0u, Ams2CountryIds.For("GBR"));
            Assert.AreEqual(0x2CEC4167u, Ams2CountryIds.For("ita"));
        }

        [TestMethod]
        public void For_TimingScreenCode_IsReadAsItsIsoCode()
        {
            Assert.AreEqual(Ams2CountryIds.For("DEU"), Ams2CountryIds.For("GER"));
            Assert.AreEqual(Ams2CountryIds.For("NLD"), Ams2CountryIds.For("NED"));
            Assert.AreEqual(Ams2CountryIds.For("CHE"), Ams2CountryIds.For("SUI"));
        }

        [TestMethod]
        public void For_UnknownOrEmpty_IsNull()
        {
            Assert.IsNull(Ams2CountryIds.For("XYZ"));
            Assert.IsNull(Ams2CountryIds.For(""));
            Assert.IsNull(Ams2CountryIds.For(null));
        }
    }
}
