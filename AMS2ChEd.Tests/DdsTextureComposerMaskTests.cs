using Ams2ChEd.Business.AMS2.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class DdsTextureComposerMaskTests
    {
        // A 128x64 body: grey panel with material mask 0, and an old painted number - a black block
        // around (52, 32) - whose mask is 200. A plate centred on (64, 32) covers it.
        private const int OldNumberMask = 200;

        [DataTestMethod]
        [DataRow(0.0, DisplayName = "upright")]
        [DataRow(180.0, DisplayName = "upside down")]
        [DataRow(20.0, DisplayName = "angled")]
        public void FilledPlate_MaskFollowsTheNewNumber_NotThePaintedOver_One(double rotation)
        {
            using var folder = new TempFolder();
            string sheet = folder.NumberSheet();
            var body = new Image<Rgba32>(128, 64, new Rgba32(100, 100, 100, 0));
            for (int y = 24; y < 40; y++)
                for (int x = 44; x < 60; x++)
                    body[x, y] = new Rgba32(0, 0, 0, OldNumberMask);

            // the plate's top-left corner, so that its centre is (64, 32)
            const int length = 80, height = 24;
            double r = rotation * Math.PI / 180;
            double ux = Math.Cos(r), uy = Math.Sin(r), vx = -uy, vy = ux;
            var placement = new DdsTextureComposer.NumberPlacementData
            {
                NumbersTexture = Path.GetFileName(sheet),
                PlateWidth = length,
                StartX = (int)Math.Round(64 - length / 2.0 * ux - height / 2.0 * vx),
                StartY = (int)Math.Round(32 - length / 2.0 * uy - height / 2.0 * vy),
                Rotation = rotation,
                FillColor = "#646464"
            };
            string output = Path.Combine(folder.Root, "out.dds");

            DdsTextureComposer.ApplyCarNumbers(body, new List<DdsTextureComposer.NumberPlacementData> { placement }, 1, folder.Root, output);

            using var result = DdsTextureComposer.LoadImage(output);
            var newDigit = new List<int>();
            var paintedOver = new List<int>();
            for (int y = 24; y < 40; y++)
                for (int x = 44; x < 84; x++)
                {
                    var p = result[x, y];
                    if (p.R > 200 && p.G > 200 && p.B > 200) newDigit.Add(p.A);
                    else if (x < 60 && Math.Abs(p.R - 100) < 20) paintedOver.Add(p.A);
                }

            Assert.IsTrue(newDigit.Count > 20, "the new digit was drawn");
            Assert.IsTrue(paintedOver.Count > 20, "the old number was painted over");
            Assert.IsTrue(newDigit.Average() > 150, $"the new digit takes the old number's finish (mask {newDigit.Average():0})");
            Assert.IsTrue(paintedOver.Average() < 50, $"the painted-over number takes the panel's finish (mask {paintedOver.Average():0})");
            Assert.IsTrue(result[4, 4].A < 10, "the mask outside the plate is untouched");
        }

        private sealed class TempFolder : IDisposable
        {
            public readonly string Root = Path.Combine(Path.GetTempPath(), "DdsTextureComposerMaskTests_" + Guid.NewGuid().ToString("N"));

            public TempFolder() => Directory.CreateDirectory(Root);

            // Ten 12x24 cells, each with a white bar in the middle on a transparent background.
            public string NumberSheet()
            {
                string path = Path.Combine(Root, "numbers.png");
                using var sheet = new Image<Rgba32>(120, 24, new Rgba32(0, 0, 0, 0));
                for (int digit = 0; digit < 10; digit++)
                    for (int y = 4; y < 20; y++)
                        for (int x = 3; x < 9; x++)
                            sheet[digit * 12 + x, y] = new Rgba32(255, 255, 255, 255);
                sheet.SaveAsPng(path);
                return path;
            }

            public void Dispose()
            {
                try { Directory.Delete(Root, true); } catch (IOException) { }
            }
        }
    }
}
