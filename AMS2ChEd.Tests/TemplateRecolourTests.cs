using Ams2ChEd.Business.AMS2.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class TemplateRecolourTests
    {
        private static readonly Rgba32 Navy = new Rgba32(10, 42, 107);
        private static readonly Rgba32 White = new Rgba32(255, 255, 255);
        private static readonly Rgba32 Black = new Rgba32(0, 0, 0);

        [TestMethod]
        public void Apply_EveryKey_TakesItsPaletteColour()
        {
            var keys = TemplateRecolour.KeyColours.ToList();
            using var image = Strip(keys.Select(k => k.Value).ToArray());
            var palette = keys.Select((k, i) => (k.Key, Colour: new Rgba32((byte)(10 * i), 100, 200)))
                              .ToDictionary(p => p.Key, p => p.Colour);

            TemplateRecolour.Apply(image, palette);

            for (int i = 0; i < keys.Count; i++)
                Assert.AreEqual(palette[keys[i].Key], image[i, 0], keys[i].Key);
        }

        [TestMethod]
        public void Apply_NonKeyPixels_AreLeftAlone()
        {
            var nearRed = new Rgba32(254, 0, 0);
            using var image = Strip(Black, White, nearRed, Navy);

            TemplateRecolour.Apply(image, new Dictionary<string, Rgba32> { { "red", Navy }, { "green", Navy } });

            Assert.AreEqual(Black, image[0, 0]);
            Assert.AreEqual(White, image[1, 0]);
            Assert.AreEqual(nearRed, image[2, 0]);
            Assert.AreEqual(Navy, image[3, 0]);
        }

        [TestMethod]
        public void Apply_KeyMappedToAnotherKeysColour_IsRecolouredOnce()
        {
            var green = TemplateRecolour.KeyColours["green"];
            using var image = Strip(TemplateRecolour.KeyColours["red"], green);

            TemplateRecolour.Apply(image, new Dictionary<string, Rgba32> { { "red", green }, { "green", White } });

            Assert.AreEqual(green, image[0, 0]);
            Assert.AreEqual(White, image[1, 0]);
        }

        [TestMethod]
        public void Apply_KeyMissingFromPalette_KeepsTemplateColour()
        {
            var blue = TemplateRecolour.KeyColours["blue"];
            using var image = Strip(TemplateRecolour.KeyColours["red"], blue);

            TemplateRecolour.Apply(image, new Dictionary<string, Rgba32> { { "red", Navy } });

            Assert.AreEqual(Navy, image[0, 0]);
            Assert.AreEqual(blue, image[1, 0]);
        }

        [TestMethod]
        public void Apply_MatchesOnRgbWhateverTheAlpha_AndKeysIgnoreCase()
        {
            using var image = Strip(new Rgba32(255, 0, 0, 40));

            TemplateRecolour.Apply(image, new Dictionary<string, Rgba32> { { "Red", Navy } });

            Assert.AreEqual(Navy, image[0, 0]);
        }

        [TestMethod]
        public void Apply_UnknownKey_Throws()
        {
            using var image = Strip(Black);

            Assert.ThrowsExactly<ArgumentException>(() =>
                TemplateRecolour.Apply(image, new Dictionary<string, Rgba32> { { "orange", Navy } }));
        }

        [TestMethod]
        public void ParsePalette_ReadsHexColours()
        {
            var palette = TemplateRecolour.ParsePalette(new Dictionary<string, string> { { "red", "#0A2A6B" }, { "green", "80FFFFFF" } });

            Assert.AreEqual(Navy, palette["RED"]);
            Assert.AreEqual(new Rgba32(255, 255, 255, 128), palette["green"]);
            Assert.AreEqual(0, TemplateRecolour.ParsePalette(null).Count);
        }

        [TestMethod]
        [DataRow("#12345")]
        [DataRow("#GGGGGG")]
        [DataRow("")]
        [DataRow(null)]
        public void ParseColour_NotAHexColour_Throws(string hex)
        {
            Assert.ThrowsExactly<FormatException>(() => TemplateRecolour.ParseColour(hex));
        }

        [TestMethod]
        public void Snap_PixelsNearAKey_MoveOntoIt_KeepingAlpha_FixedArtworkStays()
        {
            var carbon = new Rgba32(30, 30, 30);
            var transparentNearRed = new Rgba32(250, 5, 5, 0);
            using var image = Strip(new Rgba32(240, 10, 12, 128), carbon, transparentNearRed, TemplateRecolour.KeyColours["green"]);

            int changed = TemplateRecolour.Snap(image, 40);

            Assert.AreEqual(1, changed);
            Assert.AreEqual(new Rgba32(255, 0, 0, 128), image[0, 0]);
            Assert.AreEqual(carbon, image[1, 0]);
            Assert.AreEqual(transparentNearRed, image[2, 0]);
        }

        [TestMethod]
        public void Snap_RestrictedToSomeKeys_UsesOnlyThose()
        {
            using var image = Strip(new Rgba32(128, 128, 0));

            TemplateRecolour.Snap(image, double.MaxValue, new[] { "red", "green" });

            var pixel = image[0, 0];
            Assert.IsTrue(pixel.Equals(new Rgba32(255, 0, 0)) || pixel.Equals(new Rgba32(0, 255, 0)), pixel.ToString());
        }

        [TestMethod]
        public void CountNearKey_CountsOnlyAlmostKeyPixels()
        {
            using var image = Strip(new Rgba32(250, 0, 0), TemplateRecolour.KeyColours["blue"], new Rgba32(30, 30, 30), new Rgba32(0, 250, 0, 0));

            Assert.AreEqual(1, TemplateRecolour.CountNearKey(image, 40));
        }

        private static Image<Rgba32> Strip(params Rgba32[] pixels)
        {
            var image = new Image<Rgba32>(pixels.Length, 1);
            for (int i = 0; i < pixels.Length; i++)
                image[i, 0] = pixels[i];
            return image;
        }
    }
}
