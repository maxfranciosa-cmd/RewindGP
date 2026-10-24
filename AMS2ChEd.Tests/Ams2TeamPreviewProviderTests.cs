using Ams2ChEd.Business.AMS2.GameLogic;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Contracts;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Storage.Contracts;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class Ams2TeamPreviewProviderTests
    {
        [TestMethod]
        public void TeamWithNoPreview_GetsAHelmetsTileOnItsColour_ScaledToTheWidth()
        {
            HelmetPreviewTestMaps.Ensure();
            var team = new Ams2TeamEntry
            {
                TeamId = "williams",
                Color = "#0033A0",
                Ams2Car = "formula_v10_g1",
                Driver1Contract = new DriverContract { DriverId = "damon_hill" },
                Driver2Contract = new DriverContract(),
                GenericLivery = new GenericLivery { Palette = new Dictionary<string, string> { { "red", "#0C2474" } } }
            };
            var season = new Ams2Season { Year = 1996, Ams2Class = "F-V10_Gen1", Teams = new List<ITeamEntry> { team } };
            var drivers = new Mock<IDriversLoader<Ams2DriverData>>();
            drivers.Setup(d => d.LoadDrivers(1996)).Returns(new Dictionary<string, Ams2DriverData>());
            var provider = new Ams2TeamPreviewProvider(drivers.Object, new Mock<ICarModelCapacityLoader>().Object);

            var png = provider.LoadTeamPreviewPng(season, "williams", 1, 512);

            Assert.IsNotNull(png);
            using var tile = Image.Load<Rgba32>(png);
            Assert.AreEqual(512, tile.Width);
            Assert.AreEqual(new Rgba32(12, 36, 116), tile[2, 2]);
        }
    }
}
