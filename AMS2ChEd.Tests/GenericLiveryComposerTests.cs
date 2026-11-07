using Ams2ChEd.Business.AMS2.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class GenericLiveryComposerTests
    {
        [TestMethod]
        public void BuildBody_RecoloursKeys_AndMakesEveryPixelOpaque()
        {
            using var folder = new TempFolder();
            string template = folder.Png("template.png", 3, 1, new Rgba32(255, 0, 0, 255), new Rgba32(0, 255, 0, 0), new Rgba32(30, 30, 30, 255));

            using var body = GenericLiveryComposer.BuildBody(template, new Dictionary<string, string> { { "red", "#0A2A6B" }, { "green", "#FFFFFF" } });

            Assert.AreEqual(new Rgba32(10, 42, 107, 255), body[0, 0]);
            Assert.AreEqual(new Rgba32(255, 255, 255, 255), body[1, 0]);
            Assert.AreEqual(new Rgba32(30, 30, 30, 255), body[2, 0]);
        }

        [TestMethod]
        public void BuildBody_PaletteColourWithAlpha_LeavesThatIslandSeeThrough()
        {
            using var folder = new TempFolder();
            string template = folder.Png("template.png", 2, 1, new Rgba32(255, 0, 0, 255), new Rgba32(0, 255, 0, 0));

            using var body = GenericLiveryComposer.BuildBody(template, new Dictionary<string, string> { { "red", "#80C0C0C0" }, { "green", "#00000000" } });

            Assert.AreEqual(new Rgba32(192, 192, 192, 128), body[0, 0]);
            Assert.AreEqual(new Rgba32(0, 0, 0, 0), body[1, 0]);
        }

        [TestMethod]
        public void BuildBody_ThenNumbersAndDds_KeepsThePaletteAlpha()
        {
            using var folder = new TempFolder();
            var pixels = Enumerable.Range(0, 64 * 64).Select(i => i % 64 < 32 ? new Rgba32(255, 0, 0, 255) : new Rgba32(255, 255, 0, 255)).ToArray();
            string template = folder.Png("template.png", 64, 64, pixels);
            string output = Path.Combine(folder.Root, "body.dds");

            var body = GenericLiveryComposer.BuildBody(template, new Dictionary<string, string> { { "red", "#309A9EA2" }, { "yellow", "#C8102E" } });
            DdsTextureComposer.ApplyCarNumbers(body, new List<DdsTextureComposer.NumberPlacementData>(), 5, folder.Root, output, opaqueDigits: true);

            using var read = DdsTextureComposer.LoadImage(output);
            Assert.AreEqual(48, read[8, 8].A, 4);
            Assert.AreEqual(255, read[48, 8].A);
        }

        [TestMethod]
        public void BuildFresnel_ListedKeysReflective_EverythingElsePlain()
        {
            using var folder = new TempFolder();
            string template = folder.Png("template.png", 4, 1,
                new Rgba32(255, 0, 0, 255), new Rgba32(0, 255, 0, 255), new Rgba32(255, 255, 0, 255), new Rgba32(20, 20, 20, 0));

            using var fresnel = GenericLiveryComposer.BuildFresnel(template, new[] { "red", "Green" });

            Assert.AreEqual(GenericLiveryComposer.FresnelReflective, fresnel[0, 0]);
            Assert.AreEqual(GenericLiveryComposer.FresnelReflective, fresnel[1, 0]);
            Assert.AreEqual(GenericLiveryComposer.FresnelPlain, fresnel[2, 0]);
            Assert.AreEqual(GenericLiveryComposer.FresnelPlain, fresnel[3, 0]);
        }

        [TestMethod]
        public void ApplyCarNumbers_WithFresnel_DigitsArePlainOnIt_AndItIsWritten()
        {
            using var folder = new TempFolder();
            // a 2x1 sheet per digit: digit cells white, so the stamped plate changes the body
            string sheet = folder.Png("sheet.png", 20, 1, Enumerable.Repeat(new Rgba32(255, 255, 255, 255), 20).ToArray());
            var red = Enumerable.Repeat(new Rgba32(255, 0, 0, 255), 64 * 64).ToArray();
            string template = folder.Png("template.png", 64, 64, red);
            string output = Path.Combine(folder.Root, "body.dds");
            string fresnelOutput = Path.Combine(folder.Root, "body_fresnel.dds");

            var body = GenericLiveryComposer.BuildBody(template, new Dictionary<string, string> { { "red", "#0D7B797B" } });
            var fresnel = GenericLiveryComposer.BuildFresnel(template, new[] { "red" });
            var plate = new DdsTextureComposer.NumberPlacementData { NumbersTexture = sheet, PlateWidth = 8, StartX = 0, StartY = 0, Rotation = 0 };
            DdsTextureComposer.ApplyCarNumbers(body, new List<DdsTextureComposer.NumberPlacementData> { plate }, 5, folder.Root, output, opaqueDigits: true, fresnel, fresnelOutput);

            using var readBody = DdsTextureComposer.LoadImage(output);
            using var read = DdsTextureComposer.LoadImage(fresnelOutput);
            int digitPixels = 0;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    bool digit = readBody[x, y].R > 200;
                    if (digit)
                    {
                        digitPixels++;
                        Assert.IsTrue(read[x, y].R > 200, $"digit pixel {x},{y} should be plain, was {read[x, y]}");
                        Assert.AreEqual(255, readBody[x, y].A, 8);
                    }
                    else if (x > 20 && y > 20)
                        Assert.IsTrue(read[x, y].R < 60, $"chrome pixel {x},{y} should stay reflective, was {read[x, y]}");
                }
            Assert.IsTrue(digitPixels > 0, "the plate painted nothing");
        }

        [TestMethod]
        public void BuildNumberSheet_SingleColour_TakesTheFill_KeepsAlpha_AndIsResized()
        {
            using var folder = new TempFolder();
            string strip = folder.Png("sans.png", 2, 1, new Rgba32(255, 255, 255, 255), new Rgba32(255, 255, 255, 0));

            using var sheet = GenericLiveryComposer.BuildNumberSheet(strip, false, 2, 1, "#1A3C80", null);

            Assert.AreEqual(new Rgba32(26, 60, 128, 255), sheet[0, 0]);
            Assert.AreEqual(0, sheet[1, 0].A);

            using var resized = GenericLiveryComposer.BuildNumberSheet(strip, false, 20, 10, "#1A3C80", null);
            Assert.AreEqual(20, resized.Width);
            Assert.AreEqual(10, resized.Height);
        }

        [TestMethod]
        public void BuildNumberSheet_TwoColour_FillFromRed_OutlineFromGreen()
        {
            using var folder = new TempFolder();
            string strip = folder.Png("outline.png", 2, 1, new Rgba32(255, 0, 0, 255), new Rgba32(0, 255, 0, 255));

            using var sheet = GenericLiveryComposer.BuildNumberSheet(strip, true, 2, 1, "#FFFFFF", "#940706");

            Assert.AreEqual(new Rgba32(255, 255, 255, 255), sheet[0, 0]);
            Assert.AreEqual(new Rgba32(148, 7, 6, 255), sheet[1, 0]);
        }

        [TestMethod]
        public void BuildOutfitPart_MedianShadeGivesTheColour_DarkerAndLighterFollow()
        {
            using var folder = new TempFolder();
            string shading = Path.Combine(folder.Root, "torso.png");
            using (var image = new Image<L8>(3, 1))
            {
                image[0, 0] = new L8(100);
                image[1, 0] = new L8(200);
                image[2, 0] = new L8(250);
                image.SaveAsPng(shading);
            }

            using var part = GenericLiveryComposer.BuildOutfitPart(shading, new Rgba32(200, 100, 50));

            Assert.AreEqual(new Rgba32(200, 100, 50, 255), part[1, 0]);
            Assert.AreEqual(new Rgba32(100, 50, 25, 255), part[0, 0]);
            Assert.AreEqual(new Rgba32(250, 125, 63, 255), part[2, 0]);
        }

        [TestMethod]
        public void Cache_MatchesOnlyTheSameKey_ForAnExistingFile()
        {
            using var folder = new TempFolder();
            string output = Path.Combine(folder.Root, "williams_5_1996.dds");

            Assert.IsFalse(GenericLiveryComposer.IsCached(output, "a"));
            File.WriteAllText(output, "dds");
            GenericLiveryComposer.MarkCached(output, "a");
            Assert.IsTrue(GenericLiveryComposer.IsCached(output, "a"));
            Assert.IsFalse(GenericLiveryComposer.IsCached(output, "b"));
            File.Delete(output);
            Assert.IsFalse(GenericLiveryComposer.IsCached(output, "a"));
        }

        [TestMethod]
        public void Describe_IsStableWhateverTheOrderOrCase()
        {
            var a = new Dictionary<string, string> { { "red", "#111111" }, { "Green", "#222222" } };
            var b = new Dictionary<string, string> { { "green", "#222222" }, { "RED", "#111111" } };

            Assert.AreEqual(GenericLiveryComposer.Describe(a), GenericLiveryComposer.Describe(b));
            Assert.AreEqual("", GenericLiveryComposer.Describe(null));
        }

        private sealed class TempFolder : IDisposable
        {
            public readonly string Root = Path.Combine(Path.GetTempPath(), "GenericLiveryComposerTests_" + Guid.NewGuid().ToString("N"));

            public TempFolder() => Directory.CreateDirectory(Root);

            public string Png(string name, int width, int height, params Rgba32[] pixels)
            {
                string path = Path.Combine(Root, name);
                using var image = new Image<Rgba32>(width, height);
                for (int i = 0; i < pixels.Length; i++)
                    image[i % width, i / width] = pixels[i];
                image.SaveAsPng(path);
                return path;
            }

            public void Dispose()
            {
                try { Directory.Delete(Root, true); } catch (IOException) { }
            }
        }
    }
}
