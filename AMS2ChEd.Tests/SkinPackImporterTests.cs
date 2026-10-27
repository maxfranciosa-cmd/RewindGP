using System.IO.Compression;
using System.Text.Json;
using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Models;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class SkinPackImporterTests
    {
        private string _root;
        private string Baked => Path.Combine(_root, "BakedSeasons");
        private string Seasons => Path.Combine(_root, "Seasons");
        private string VisorMasks => Path.Combine(_root, "visor_masks");
        private string HelmetMasks => Path.Combine(_root, "helmet_sponsor_masks");

        [TestInitialize]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "SkinPackImporterTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Baked, "2000", "cosmetics"));
            Directory.CreateDirectory(Seasons);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        private void WriteCosmetics(int year, SeasonCosmetics cosmetics)
        {
            Directory.CreateDirectory(Path.Combine(Baked, year.ToString(), "cosmetics"));
            File.WriteAllText(Path.Combine(Baked, year.ToString(), "cosmetics", "cosmetics.json"), JsonSerializer.Serialize(cosmetics, DefaultJsonSerializerOptions.Instance));
        }

        private static SeasonCosmetics Cosmetics(string top) => new SeasonCosmetics
        {
            Teams = new Dictionary<string, Ams2TeamEntry>
            {
                ["ferrari"] = new Ams2TeamEntry { BaseLiveryDriver1 = "external_liveries/car_liveries/ferrari/default.dds" },
                ["williams"] = new Ams2TeamEntry { BaseLiveryDriver1 = "external_liveries/car_liveries/williams/default.dds" }
            },
            SkinPack = new SkinPackSource
            {
                Name = "F1 Test Season",
                Url = "https://example.invalid/pack",
                HelmetModel = "Modern",
                Detect = new List<string> { $"{top}/Liveries/ferrari.dds", $"{top}/Liveries/williams.dds" },
                Entries = new List<ExternalLiveriesEntry>
                {
                    new() { SourcePath = $"{top}\\Liveries\\ferrari.dds", DestinationPath = "external_liveries/car_liveries/ferrari/default.dds" },
                    new() { SourcePath = $"{top}\\Liveries\\williams.dds", DestinationPath = "external_liveries/car_liveries/williams/default.dds" }
                },
                Helmets = new Dictionary<string, SkinPackTeamHelmets>
                {
                    // its own map; the visor through the model's strip
                    ["ferrari"] = new() { Helmet = $"{top}/Helmets/schumacher.png", Map = "sponsor_maps/ferrari.png", Visor = $"{top}/Helmets/schumacher_visor.png" },
                    // no map: the model's generic one, empty here
                    ["williams"] = new() { Helmet = $"{top}/Helmets/hill.png" }
                }
            }
        };

        [TestMethod]
        public void DetectSeasons_MatchesByPathEnding_WhenTheTopFolderWasRenamed()
        {
            WriteCosmetics(2000, Cosmetics("F1_2000_Season"));
            WriteCosmetics(2001, Cosmetics("F1_2001_Season_Other"));
            var importer = new SkinPackImporter(Baked, Seasons, VisorMasks, HelmetMasks);

            var years = importer.DetectSeasons(new[] { "F1_2000_Season v2.39/Liveries/ferrari.dds", "F1_2000_Season v2.39/Liveries/williams.dds" });

            // both years name the same files under their own top folder, so both match
            CollectionAssert.AreEquivalent(new[] { 2000, 2001 }, years.ToArray());
            Assert.AreEqual(0, importer.DetectSeasons(new[] { "Something/else.dds" }).Count);
        }

        [TestMethod]
        public async Task Import_CopiesTheFiles_CutsTheLayersThroughTheMaps_AndRemoveUndoesIt()
        {
            WriteCosmetics(2000, Cosmetics("F1_2000_Season"));
            // ferrari's own map: the top quarter of the helmet
            Directory.CreateDirectory(Path.Combine(Baked, "2000", "cosmetics", "sponsor_maps"));
            SaveMask(Path.Combine(Baked, "2000", "cosmetics", "sponsor_maps", "ferrari.png"), 256, 256, 64);
            // the model's generic helmet map is empty; its visor strip the top quarter
            Directory.CreateDirectory(HelmetMasks);
            SaveMask(Path.Combine(HelmetMasks, "Modern.png"), 256, 256, 0);
            Directory.CreateDirectory(VisorMasks);
            SaveMask(Path.Combine(VisorMasks, "Modern.png"), 64, 32, 8);
            var archive = Path.Combine(_root, "pack.zip");
            BuildArchive(archive, "F1_2000_Season");
            var importer = new SkinPackImporter(Baked, Seasons, VisorMasks, HelmetMasks);

            var result = await importer.ImportAsync(2000, archive);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(2, result.FilesCopied);
            Assert.IsTrue(importer.IsImported(2000));
            var sponsors = Path.Combine(Seasons, "2000", "external_liveries", "helmet_sponsors");
            Assert.IsTrue(File.Exists(Path.Combine(Seasons, "2000", "external_liveries", "car_liveries", "ferrari", "default.dds")));
            Assert.IsTrue(File.Exists(Path.Combine(sponsors, "ferrari", "default.png")), string.Join("; ", result.Notes));
            Assert.IsTrue(File.Exists(Path.Combine(sponsors, "ferrari", "default_visor.png")));
            Assert.IsFalse(File.Exists(Path.Combine(sponsors, "williams", "default.png")), "an empty generic map makes no layer");
            Assert.IsTrue(result.Notes.Any(n => n.StartsWith("williams") && n.Contains("empty")), string.Join("; ", result.Notes));

            using (var layer = SixLabors.ImageSharp.Image.Load<Rgba32>(Path.Combine(sponsors, "ferrari", "default.png")))
            {
                Assert.AreEqual(255, layer[10, 10].A, "inside the map");
                Assert.AreEqual(200, layer[10, 10].R, "the helmet's colour");
                Assert.AreEqual(0, layer[10, 200].A, "outside the map");
            }

            importer.Remove(2000);

            Assert.IsFalse(importer.IsImported(2000));
            Assert.IsFalse(Directory.Exists(Path.Combine(Seasons, "2000")));
        }

        [TestMethod]
        public void Cut_ScalesTheTextureToTheMap_AndKeepsOnlyWhatTheMapMarks()
        {
            using var texture = new Image<Rgba32>(512, 256, new Rgba32(10, 120, 30));
            using var mask = new Image<Rgba32>(256, 128);
            for (int x = 100; x < 150; x++) for (int y = 40; y < 60; y++) mask[x, y] = new Rgba32(255, 255, 255, 255);

            using var layer = SponsorLayerCutter.Cut(texture, mask);

            Assert.AreEqual(256, layer.Width);
            Assert.AreEqual(new Rgba32(10, 120, 30, 255), layer[120, 50]);
            Assert.AreEqual(0, layer[20, 20].A);
        }

        // a mask of the given size, opaque white over its top rows
        private static void SaveMask(string path, int width, int height, int rows)
        {
            using var mask = new Image<Rgba32>(width, height);
            for (int x = 0; x < width; x++) for (int y = 0; y < rows; y++) mask[x, y] = new Rgba32(255, 255, 255, 255);
            mask.SaveAsPng(path);
        }

        private static void BuildArchive(string path, string top)
        {
            var staging = Path.Combine(Path.GetDirectoryName(path), "staging");
            Directory.CreateDirectory(Path.Combine(staging, top, "Liveries"));
            Directory.CreateDirectory(Path.Combine(staging, top, "Helmets"));
            File.WriteAllText(Path.Combine(staging, top, "Liveries", "ferrari.dds"), "dds");
            File.WriteAllText(Path.Combine(staging, top, "Liveries", "williams.dds"), "dds");
            using (var a = new Image<Rgba32>(256, 256, new Rgba32(200, 0, 0))) a.SaveAsPng(Path.Combine(staging, top, "Helmets", "schumacher.png"));
            using (var b = new Image<Rgba32>(256, 256, new Rgba32(0, 0, 200))) b.SaveAsPng(Path.Combine(staging, top, "Helmets", "hill.png"));
            using (var v = new Image<Rgba32>(128, 64, new Rgba32(30, 30, 30))) v.SaveAsPng(Path.Combine(staging, top, "Helmets", "schumacher_visor.png"));
            ZipFile.CreateFromDirectory(staging, path);
        }
    }
}
