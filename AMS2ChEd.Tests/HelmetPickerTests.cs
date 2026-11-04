using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class HelmetPickerTests
    {
        [TestMethod]
        public void CurrentPlayerHelmetPath_PngOfAPlayerHelmetOrVisor_IsTheWebp()
        {
            Assert.AreEqual(Path.ChangeExtension("../../Drivers/base_helmet_liveries/player_helmets/helmets/01_arrow.png", ".webp"),
                HelmetPicker.CurrentPlayerHelmetPath("../../Drivers/base_helmet_liveries/player_helmets/helmets/01_arrow.png"));
            Assert.AreEqual(Path.ChangeExtension(@"..\..\Drivers\base_helmet_liveries\player_helmets\visors\0_CARBON.png", ".webp"),
                HelmetPicker.CurrentPlayerHelmetPath(@"..\..\Drivers\base_helmet_liveries\player_helmets\visors\0_CARBON.png"));
        }

        [TestMethod]
        public void CurrentPlayerHelmetPath_AnyOtherPath_IsLeftAlone()
        {
            foreach (var path in new[] { null, "", "../../Drivers/base_helmet_liveries/defaulthelmet.png",
                "../../Drivers/base_helmet_liveries/player_helmets/previews/01_arrow.png", "helmet_liveries/hill.png",
                "../../Drivers/base_helmet_liveries/player_helmets/helmets/01_arrow.webp" })
                Assert.AreEqual(path, HelmetPicker.CurrentPlayerHelmetPath(path));
        }

        [TestMethod]
        public void PickHelmetFile_SaveNamingTheOldPng_PicksTheWebp()
        {
            var driver = new Ams2DriverData { BaseHelmetFile90s = "../../Drivers/base_helmet_liveries/player_helmets/helmets/90s_01_arrow.png" };

            Assert.AreEqual(".webp", Path.GetExtension(HelmetPicker.PickHelmetFile(driver, HelmetEra.Nineties)));
        }
    }
}
