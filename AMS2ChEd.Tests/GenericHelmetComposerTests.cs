using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class GenericHelmetComposerTests
    {
        [TestMethod]
        public void Palette_DesignsOwnColours_WithTheDriversOnTop()
        {
            using var folder = new TempFolder();
            folder.Design("13_split", HelmetEra.Modern);

            var palette = GenericHelmetComposer.Palette(folder.Root, new GenericHelmet { Design = "13_split", Palette = new Dictionary<string, string> { { "red", "#16306E" } } });

            Assert.AreEqual("#16306E", palette["red"]);
            Assert.AreEqual("#F4F4F4", palette["GREEN"]);
        }

        [TestMethod]
        public void Build_RecoloursTheDesignForTheEra_AndLaysTheOverlayOnTop()
        {
            using var folder = new TempFolder();
            folder.Design("13_split", HelmetEra.Nineties);

            using var helmet = GenericHelmetComposer.Build(new GenericHelmet { Design = "13_split", Palette = new Dictionary<string, string> { { "red", "#16306E" } } }, HelmetEra.Nineties, folder.Root);

            Assert.AreEqual(new Rgba32(22, 48, 110, 255), helmet[0, 0], "red key: the driver's colour");
            Assert.AreEqual(new Rgba32(244, 244, 244, 255), helmet[1, 0], "green key: the design's own white");
            Assert.AreEqual(new Rgba32(10, 10, 10, 255), helmet[2, 0], "the overlay's hardware");
        }

        [TestMethod]
        public void Build_DesignMissingForTheEra_IsNull()
        {
            using var folder = new TempFolder();
            folder.Design("13_split", HelmetEra.Modern);

            Assert.IsNull(GenericHelmetComposer.Build(new GenericHelmet { Design = "13_split" }, HelmetEra.Seventies, folder.Root));
            Assert.IsNull(GenericHelmetComposer.Build(new GenericHelmet { Design = "nope" }, HelmetEra.Modern, folder.Root));
        }

        private sealed class TempFolder : IDisposable
        {
            public readonly string Root = Path.Combine(Path.GetTempPath(), "GenericHelmetComposerTests_" + Guid.NewGuid().ToString("N"));

            public TempFolder() => Directory.CreateDirectory(Root);

            // A 3x1 design: red key, green key, and a texel the overlay covers.
            public void Design(string name, HelmetEra era)
            {
                using (var design = new Image<Rgba32>(3, 1))
                {
                    design[0, 0] = new Rgba32(255, 0, 0, 255);
                    design[1, 0] = new Rgba32(0, 255, 0, 255);
                    design[2, 0] = new Rgba32(255, 0, 0, 255);
                    design.SaveAsPng(GenericHelmetComposer.DesignFile(Root, name, era));
                }
                File.WriteAllText(Path.Combine(Root, name + ".json"), @"{ ""design"": ""13_split"", ""palette"": { ""red"": ""#1C4CA4"", ""green"": ""#F4F4F4"", ""blue"": ""#D4141C"" } }");
                Directory.CreateDirectory(Path.Combine(Root, "overlays"));
                using var overlay = new Image<Rgba32>(3, 1, new Rgba32(0, 0, 0, 0));
                overlay[2, 0] = new Rgba32(10, 10, 10, 255);
                overlay.SaveAsPng(GenericHelmetComposer.OverlayFile(Root, era));
            }

            public void Dispose()
            {
                try { Directory.Delete(Root, true); } catch (IOException) { }
            }
        }
    }
}
