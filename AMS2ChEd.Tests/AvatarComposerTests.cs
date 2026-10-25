using AMS2ChEd.Business.Artwork;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models.Concrete;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.Json;

namespace AMS2ChEd.Tests
{
    [TestClass]
    public class AvatarComposerTests
    {
        [TestMethod]
        public void FromId_SameIdSameAvatar_ValidStyles()
        {
            var a = AvatarComposer.FromId("ayrton_senna");
            var b = AvatarComposer.FromId("ayrton_senna");

            Assert.AreEqual(a.Skin, b.Skin);
            Assert.AreEqual(a.Hair, b.Hair);
            Assert.AreEqual(a.HairColour, b.HairColour);
            Assert.AreEqual(a.FacialHair, b.FacialHair);
            CollectionAssert.Contains(AvatarComposer.HairStyles, a.Hair);
            CollectionAssert.Contains(AvatarComposer.FacialHairStyles, a.FacialHair);
        }

        [TestMethod]
        public void Compose_PortraitSize_SkinOnTheFace_EveryStyleDrawsSomethingDifferent()
        {
            var skin = Rgba32.ParseHex("#C68A62");
            var drawn = new HashSet<string>();
            foreach (var hair in AvatarComposer.HairStyles)
            {
                using var image = AvatarComposer.Compose(new GenericAvatar { Skin = "#C68A62", Hair = hair, HairColour = "#1C1814", FacialHair = "none" });
                Assert.AreEqual(AvatarComposer.Width, image.Width);
                Assert.AreEqual(AvatarComposer.Height, image.Height);
                drawn.Add(Fingerprint(image));
            }
            Assert.AreEqual(AvatarComposer.HairStyles.Length, drawn.Count, "two hair styles draw the same picture");

            foreach (var facial in AvatarComposer.FacialHairStyles)
            {
                using var image = AvatarComposer.Compose(new GenericAvatar { Skin = "#C68A62", Hair = "short", HairColour = "#1C1814", FacialHair = facial });
                drawn.Add(Fingerprint(image));
            }
            Assert.AreEqual(AvatarComposer.HairStyles.Length + AvatarComposer.FacialHairStyles.Length - 1, drawn.Count, "two facial hair styles draw the same picture");
        }

        [TestMethod]
        public void GenericAvatar_RoundTripsInDriverJson()
        {
            var driver = new DriverData { DriverId = "x", GenericAvatar = new GenericAvatar { Skin = "#E8BC98", Hair = "mullet", HairColour = "#5C3E26", FacialHair = "moustache" } };

            string json = JsonSerializer.Serialize<AMS2ChEd.Business.Models.IDriverData>(driver, DefaultJsonSerializerOptions.Instance);
            var back = JsonSerializer.Deserialize<DriverData>(json, DefaultJsonSerializerOptions.Instance);

            StringAssert.Contains(json, "generic_avatar");
            Assert.AreEqual("mullet", back.GenericAvatar.Hair);
            Assert.AreEqual("moustache", back.GenericAvatar.FacialHair);
        }

        // a coarse summary of the picture: one averaged colour per 20x20 block
        private static string Fingerprint(Image<Rgba32> image)
        {
            var parts = new List<string>();
            for (int y = 0; y < image.Height; y += 20)
                for (int x = 0; x < image.Width; x += 20)
                {
                    var p = image[x, y];
                    parts.Add($"{p.R / 8},{p.G / 8},{p.B / 8}");
                }
            return string.Join(";", parts);
        }
    }
}
