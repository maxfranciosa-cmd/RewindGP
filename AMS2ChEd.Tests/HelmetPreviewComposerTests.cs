using Ams2ChEd.Business.AMS2.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class HelmetPreviewComposerTests
    {
        // A 2x2 view: a shell pixel seeing texel (1,0) at half light, a visor pixel seeing visor
        // texel (0,0) in full light, and nothing below them.
        private static HelmetPreviewComposer.HelmetMaps Maps() => new HelmetPreviewComposer.HelmetMaps
        {
            Size = 2,
            Uv = new[] { new Rgba64(65535, 0, 0, 65535), new Rgba64(0, 0, 65535, 65535), new Rgba64(0, 0, 0, 0), new Rgba64(0, 0, 0, 0) },
            Shading = new[] { new Rgba32(128, 128, 128, 255), new Rgba32(255, 255, 255, 255), new Rgba32(0, 0, 0, 0), new Rgba32(0, 0, 0, 0) }
        };

        [TestMethod]
        public void RenderHelmet_SamplesTheTextureTimesTheLight_VisorSmoked_TransparentElsewhere()
        {
            using var texture = new Image<Rgba32>(2, 1);
            texture[0, 0] = new Rgba32(255, 0, 0, 255);
            texture[1, 0] = new Rgba32(200, 100, 50, 255);

            using var helmet = HelmetPreviewComposer.RenderHelmet(texture, Maps());

            Assert.AreEqual(2, helmet.Width);
            Assert.AreEqual(new Rgba32(100, 50, 25, 255), helmet[0, 0]);
            Assert.IsTrue(helmet[1, 0].R < 40 && helmet[1, 0].B > helmet[1, 0].R, $"a dark smoked visor, got {helmet[1, 0]}");
            Assert.AreEqual(0, helmet[0, 1].A);
        }

        [TestMethod]
        public void RenderHelmet_VisorPixel_SamplesTheVisorTexture_SeeThroughShowsSmoke()
        {
            using var texture = new Image<Rgba32>(2, 1, new Rgba32(255, 0, 0, 255));
            using var visor = new Image<Rgba32>(1, 1, new Rgba32(0, 200, 0, 255));
            using var clear = new Image<Rgba32>(1, 1, new Rgba32(0, 200, 0, 0));

            using var tinted = HelmetPreviewComposer.RenderHelmet(texture, Maps(), visor);
            using var seeThrough = HelmetPreviewComposer.RenderHelmet(texture, Maps(), clear);

            Assert.AreEqual(new Rgba32(0, 200, 0, 255), tinted[1, 0]);
            Assert.AreEqual(new Rgba32(18, 20, 28, 255), seeThrough[1, 0]);
        }

        [TestMethod]
        public void RenderHelmet_TransparentTexel_ShowsTheBareGreyShell()
        {
            using var texture = new Image<Rgba32>(2, 1, new Rgba32(0, 0, 0, 0));

            using var helmet = HelmetPreviewComposer.RenderHelmet(texture, Maps());

            Assert.AreEqual(new Rgba32(103, 103, 103, 255), helmet[0, 0]);
        }

        // helmet 708 px, gap 60, square 520: centred, the square starts at x 1148, y 124
        [TestMethod]
        public void ComposeCard_IsTheCarSelectSize_TransparentAroundAHelmetAndTheTeamColourSquare()
        {
            using var helmet = new Image<Rgba32>(16, 16, new Rgba32(0, 0, 0, 0));
            var navy = new Rgba32(12, 36, 116);

            using var card = HelmetPreviewComposer.ComposeCard(helmet, navy, 5, null);

            Assert.AreEqual(HelmetPreviewComposer.CardWidth, card.Width);
            Assert.AreEqual(HelmetPreviewComposer.CardHeight, card.Height);
            Assert.AreEqual(0, card[2, 2].A);
            Assert.AreEqual(0, card[card.Width - 3, card.Height - 3].A);
            Assert.AreEqual(navy, card[1150, 126]);
            Assert.AreEqual(navy, card[1666, 642]);
            Assert.AreEqual(0, card[1146, 384].A, "left of the square");
        }

        [TestMethod]
        public void ComposeCard_NumberFromASheet_CentredInTheSquare()
        {
            using var helmet = new Image<Rgba32>(16, 16, new Rgba32(0, 0, 0, 0));
            using var sheet = new Image<Rgba32>(100, 20, new Rgba32(255, 0, 0, 255));

            using var card = HelmetPreviewComposer.ComposeCard(helmet, new Rgba32(0, 0, 0), 7, sheet);

            Assert.AreEqual(new Rgba32(255, 0, 0, 255), card[1408, 384]);
            Assert.AreEqual(new Rgba32(0, 0, 0, 255), card[1160, 384], "square colour beside the digit");
        }
        [TestMethod]
        public void Contrast_WhiteOnDark_BlackOnLight()
        {
            Assert.AreEqual(new Rgba32(255, 255, 255), HelmetPreviewComposer.Contrast(new Rgba32(12, 36, 116)));
            Assert.AreEqual(new Rgba32(0, 0, 0), HelmetPreviewComposer.Contrast(new Rgba32(252, 252, 4)));
        }

        [TestMethod]
        public void ComposeTeamTile_EmptyTeam_IsJustTheColour()
        {
            using var tile = HelmetPreviewComposer.ComposeTeamTile(new List<Image<Rgba32>>(), new Rgba32(220, 20, 60));

            Assert.AreEqual(new Rgba32(220, 20, 60), tile[tile.Width / 2, tile.Height / 2]);
        }
    }
}
