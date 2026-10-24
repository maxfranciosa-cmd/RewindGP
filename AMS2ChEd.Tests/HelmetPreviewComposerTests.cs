using Ams2ChEd.Business.AMS2.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class HelmetPreviewComposerTests
    {
        // A 2x2 view: a shell pixel seeing texel (1,0) at half light, a pixel inside the shell
        // (where the visor goes), and nothing below them.
        private static HelmetPreviewComposer.HelmetMaps Maps() => new HelmetPreviewComposer.HelmetMaps
        {
            Size = 2,
            Uv = new[] { new Rgba64(65535, 0, 0, 65535), new Rgba64(0, 0, 65535, 65535), new Rgba64(0, 0, 0, 0), new Rgba64(0, 0, 0, 0) },
            Shading = new[] { new Rgba32(128, 128, 128, 255), new Rgba32(255, 255, 255, 255), new Rgba32(0, 0, 0, 0), new Rgba32(0, 0, 0, 0) }
        };

        [TestMethod]
        public void RenderHelmet_SamplesTheTextureTimesTheLight_VisorInside_TransparentElsewhere()
        {
            using var texture = new Image<Rgba32>(2, 1);
            texture[0, 0] = new Rgba32(255, 0, 0, 255);
            texture[1, 0] = new Rgba32(200, 100, 50, 255);

            using var helmet = HelmetPreviewComposer.RenderHelmet(texture, Maps());

            Assert.AreEqual(2, helmet.Width);
            Assert.AreEqual(new Rgba32(100, 50, 25, 255), helmet[0, 0]);
            Assert.IsTrue(helmet[1, 0].R < 60 && helmet[1, 0].B > helmet[1, 0].R, $"a dark smoked visor, got {helmet[1, 0]}");
            Assert.AreEqual(0, helmet[0, 1].A);
        }

        [TestMethod]
        public void RenderHelmet_TransparentTexel_ShowsTheBareGreyShell()
        {
            using var texture = new Image<Rgba32>(2, 1, new Rgba32(0, 0, 0, 0));

            using var helmet = HelmetPreviewComposer.RenderHelmet(texture, Maps());

            Assert.AreEqual(new Rgba32(103, 103, 103, 255), helmet[0, 0]);
        }

        [TestMethod]
        public void ComposeCard_IsTheCarSelectSize_OnTheTeamColour()
        {
            using var helmet = new Image<Rgba32>(16, 16, new Rgba32(0, 0, 0, 0));
            var navy = new Rgba32(12, 36, 116);

            using var card = HelmetPreviewComposer.ComposeCard(helmet, navy, 5, null, "Damon Hill", "Williams Renault");

            Assert.AreEqual(HelmetPreviewComposer.CardWidth, card.Width);
            Assert.AreEqual(HelmetPreviewComposer.CardHeight, card.Height);
            Assert.AreEqual(navy, card[2, 2]);
            Assert.AreEqual(navy, card[card.Width - 3, card.Height - 3]);
        }

        [TestMethod]
        public void ComposeCard_NumberFromASheet_DrawsItsDigitCells()
        {
            using var helmet = new Image<Rgba32>(16, 16, new Rgba32(0, 0, 0, 0));
            using var sheet = new Image<Rgba32>(100, 20, new Rgba32(255, 0, 0, 255));

            using var card = HelmetPreviewComposer.ComposeCard(helmet, new Rgba32(0, 0, 0), 7, sheet, "", null);

            Assert.AreEqual(new Rgba32(255, 0, 0, 255), card[830, 200]);
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
