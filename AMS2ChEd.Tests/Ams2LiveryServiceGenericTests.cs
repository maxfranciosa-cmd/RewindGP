using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Xml.Linq;

namespace AMS2ChEd.Tests.Ams2
{
    // A team with a generic_livery and no season pack, on a tiny kit: the livery service writes
    // the kit's livery.xml with the generated body, suit and tyres, into a fake AMS2 folder.
    [TestClass]
    public class Ams2LiveryServiceGenericTests
    {
        private const string CarModel = "formula_v10_g1";

        [TestMethod]
        public void TeamWithGenericLiveryAndNoPack_GetsKitXmlBodySuitAndTyres()
        {
            using var folder = new TempFolder();
            var (service, entries) = Build(folder, withLivery: true);

            service.GenerateLiveriesOnly(1, entries, folder.SeasonDirectory, folder.Ams2Root);

            string carFolder = Path.Combine(folder.Ams2Root, "Vehicles", "Textures", "CustomLiveries", "Overrides", CarModel);
            var xml = XDocument.Load(Path.Combine(carFolder, $"{CarModel}.xml")).Root;
            var livery = xml.Element("LIVERY_OVERRIDE");
            Assert.AreEqual(@"temporary_textures\williams_5_1996.dds", Texture(livery, "BODY"));
            Assert.AreEqual("", livery.Element("PREVIEWIMAGE").Attribute("PATH").Value);
            Assert.AreEqual(@"..\tyres\tire_diff.dds", Texture(livery, "TIREDIFF"));
            Assert.IsTrue(File.Exists(Path.Combine(carFolder, "..", "tyres", "tire_diff.dds")));
            Assert.IsTrue(File.Exists(Path.Combine(carFolder, "temporary_textures", "williams_5_1996.dds")));

            var outfit = xml.Element("OUTFIT_OVERRIDE");
            Assert.AreEqual("51", outfit.Attribute("LIVERY").Value);
            Assert.AreEqual(@"temporary_textures\williams_body_diff_1996.dds", Texture(outfit, "BODY_DIFF"));
            Assert.IsNull(outfit.Elements("TEXTURE").FirstOrDefault(t => t.Attribute("NAME").Value == "ARMS_DIFF"), "a part the outfit set lacks is left out");
            Assert.IsTrue(File.Exists(Path.Combine(carFolder, "temporary_textures", "williams_body_diff_1996.dds")));
        }

        [TestMethod]
        public void TeamWithNoGenericLiveryAndNoPack_GetsSolidColour_AndNoOutfit()
        {
            using var folder = new TempFolder();
            var (service, entries) = Build(folder, withLivery: false);

            service.GenerateLiveriesOnly(1, entries, folder.SeasonDirectory, folder.Ams2Root);

            string carFolder = Path.Combine(folder.Ams2Root, "Vehicles", "Textures", "CustomLiveries", "Overrides", CarModel);
            var xml = XDocument.Load(Path.Combine(carFolder, $"{CarModel}.xml")).Root;
            Assert.AreEqual(@"temporary_textures\williams_5_1996.dds", Texture(xml.Element("LIVERY_OVERRIDE"), "BODY"));
            Assert.IsNull(Texture(xml.Element("LIVERY_OVERRIDE"), "TIREDIFF"));
            Assert.IsNull(xml.Element("OUTFIT_OVERRIDE"));
        }

        private static string Texture(XElement element, string name) =>
            element.Elements("TEXTURE").FirstOrDefault(t => t.Attribute("NAME")?.Value == name)?.Attribute("PATH")?.Value;

        private static (Ams2LiveryService, List<EntryListEntry>) Build(TempFolder folder, bool withLivery)
        {
            string kits = Path.Combine(folder.Root, "LiveryKits");
            string kit = Path.Combine(kits, CarModel);
            folder.Png(Path.Combine(kit, "templates", "t.png"), new Rgba32(255, 0, 0, 255));
            File.WriteAllText(Path.Combine(kit, "kit.json"), $@"{{ ""car_models"": [""{CarModel}""], ""livery_xml"": ""livery.xml"", ""outfit_set"": ""set"",
                ""templates"": [ {{ ""id"": ""t"", ""body"": ""templates/t.png"" }} ] }}");
            File.WriteAllText(Path.Combine(kit, "livery.xml"), @"<USER_OVERRIDES>
  <LIVERY_OVERRIDE LIVERY="""" NAME="""" BASELIVERY=""Default""><PREVIEWIMAGE PATH="""" /><TEXTURE NAME=""BODY"" PATH="""" /></LIVERY_OVERRIDE>
  <HELMET_OVERRIDE LIVERY="""" BASEHELMET=""DEFAULT""><TEXTURE NAME=""BODY_DIFF"" PATH="""" /><TEXTURE NAME=""VISOR_DIFF"" PATH="""" /></HELMET_OVERRIDE>
  <OUTFIT_OVERRIDE LIVERY="""" BASEOUTFIT=""DEFAULT""><TEXTURE NAME=""BODY_DIFF"" PATH="""" /><TEXTURE NAME=""ARMS_DIFF"" PATH="""" /></OUTFIT_OVERRIDE>
</USER_OVERRIDES>");
            string set = Path.Combine(kits, "outfits", "set");
            Directory.CreateDirectory(set);
            using (var shading = new Image<L8>(8, 8, new L8(180)))
                shading.SaveAsPng(Path.Combine(set, "torso.png"));
            File.WriteAllText(Path.Combine(set, "outfit.json"), @"{ ""parts"": { ""BODY_DIFF"": { ""shading"": ""torso.png"", ""key"": ""red"" } } }");
            string tyreFiles = Path.Combine(folder.Root, "Tyres", "files");
            Directory.CreateDirectory(tyreFiles);
            File.WriteAllText(Path.Combine(tyreFiles, "tire_diff.dds"), "tyre");
            string tyreSets = Path.Combine(folder.Root, "Tyres", "tyre_sets.json");
            File.WriteAllText(tyreSets, @"{ ""goodyear"": { ""TIREDIFF"": ""tire_diff.dds"" } }");

            // the default helmet the driver falls back to, found from the season folder
            string helmets = Path.Combine(folder.Root, "Drivers", "base_helmet_liveries");
            folder.Png(Path.Combine(helmets, "defaulthelmet.png"), new Rgba32(200, 200, 200, 255));
            folder.Png(Path.Combine(helmets, "defaultvisor.png"), new Rgba32(20, 20, 20, 255));

            var team = new Ams2TeamEntry
            {
                TeamId = "williams",
                TeamName = "Williams Renault",
                Color = "#0033A0",
                Ams2Car = CarModel,
                Driver1Contract = new DriverContract { DriverId = "damon_hill", DriverNumber = 5 },
                Driver2Contract = new DriverContract(),
                GenericLivery = withLivery
                    ? new GenericLivery { Template = "t", Palette = new Dictionary<string, string> { { "red", "#0C2474" } }, Tyres = "goodyear" }
                    : null
            };
            var driver = new Ams2DriverData { DriverId = "damon_hill", Name = "Damon Hill" };
            var service = new Ams2LiveryService(1996, "F-V10_Gen1", new[] { driver }, new[] { team },
                liveryKits: new LiveryKitLoader(kits, tyreSets, tyreFiles));
            var entries = new List<EntryListEntry> { new EntryListEntry { TeamId = "williams", Driver1Id = "damon_hill", Driver1Number = 5 } };
            return (service, entries);
        }

        private sealed class TempFolder : IDisposable
        {
            public readonly string Root = Path.Combine(Path.GetTempPath(), "Ams2LiveryServiceGenericTests_" + Guid.NewGuid().ToString("N"));
            public string SeasonDirectory => Path.Combine(Root, "Seasons", "1996");
            public string Ams2Root => Path.Combine(Root, "AMS2");

            public TempFolder() => Directory.CreateDirectory(Root);

            public void Png(string path, Rgba32 colour)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using var image = new Image<Rgba32>(8, 8, colour);
                image.SaveAsPng(path);
            }

            public void Dispose()
            {
                try { Directory.Delete(Root, true); } catch (IOException) { }
            }
        }
    }
}
