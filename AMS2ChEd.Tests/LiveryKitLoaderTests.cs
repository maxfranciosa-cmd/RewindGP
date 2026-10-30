using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class LiveryKitLoaderTests
    {
        [TestMethod]
        public void FindKit_ByAnyListedCarModel_IgnoringCase()
        {
            using var folder = new TempFolder();
            folder.Write("v10/kit.json", "{ \"car_models\": [\"formula_v10_g1\", \"other_model\"], \"templates\": [ { \"id\": \"t1\", \"body\": \"templates/t1.png\" } ] }");
            var loader = folder.Loader();

            var kit = loader.FindKit("FORMULA_V10_G1");

            Assert.IsNotNull(kit);
            Assert.AreSame(kit, loader.FindKit("other_model"));
            Assert.AreEqual(Path.Combine(folder.Kits, "v10"), kit.Folder);
            Assert.AreEqual("templates/t1.png", kit.FindTemplate("T1").Body);
            Assert.IsNull(kit.FindTemplate("t2"));
        }

        [TestMethod]
        public void FindKit_NoKitForTheModel_OrNoKitsAtAll_IsNull()
        {
            using var folder = new TempFolder();
            folder.Write("v10/kit.json", "{ \"car_models\": [\"formula_v10_g1\"] }");

            Assert.IsNull(folder.Loader().FindKit("mclaren_mp4_12"));
            Assert.IsNull(folder.Loader().FindKit(null));
            Assert.IsNull(new LiveryKitLoader(Path.Combine(folder.Kits, "missing"), "none.json", "none").FindKit("formula_v10_g1"));
        }

        [TestMethod]
        public void KitPlacement_ReadsSheetSizeAndFillKey()
        {
            using var folder = new TempFolder();
            folder.Write("v10/kit.json", @"{ ""car_models"": [""formula_v10_g1""], ""templates"": [ { ""id"": ""t1"", ""numbers_placements"": [
                { ""number_plate_width"": 120, ""starting_point"": { ""IsEmpty"": false, ""X"": 361, ""Y"": 1079 }, ""rotation"": ""Deg270"", ""sheet_size"": [600, 97], ""fill_key"": ""green"" } ] } ] }");

            var plate = folder.Loader().FindKit("formula_v10_g1").Templates[0].NumbersPlacements[0];

            Assert.AreEqual(120, plate.NumberPlateWidth);
            Assert.AreEqual(361, plate.StartingPoint.X);
            Assert.AreEqual(NumberRotation.Deg270, plate.NumberRotation);
            CollectionAssert.AreEqual(new[] { 600, 97 }, plate.SheetSize);
            Assert.AreEqual("green", plate.FillKey);
        }

        [TestMethod]
        public void NumberStyles_OutfitSets_AndTyreSets_ResolveToFullPaths()
        {
            using var folder = new TempFolder();
            folder.Write("number_styles.json", "{ \"sans\": { \"file\": \"numbers/sans.png\", \"two_colour\": false }, \"sans_outline\": { \"file\": \"numbers/sans_outline.png\", \"two_colour\": true } }");
            folder.Write("outfits/open_wheel_90s/outfit.json", "{ \"parts\": { \"BODY_DIFF\": { \"shading\": \"torso.png\", \"key\": \"red\" } } }");
            File.WriteAllText(folder.TyreSets, "{ \"goodyear_f93\": { \"TIREDIFF\": \"tire_goodyear_f93_diff.dds\" } }");
            var loader = folder.Loader();

            Assert.IsTrue(loader.FindNumberStyle("SANS_OUTLINE").TwoColour);
            Assert.AreEqual(Path.Combine(folder.Kits, "numbers/sans.png"), loader.NumberStylePath("sans"));
            Assert.IsNull(loader.FindNumberStyle("serif"));
            Assert.IsNull(loader.NumberStylePath("serif"));

            var outfit = loader.FindOutfitSet("open_wheel_90s");
            Assert.AreEqual("torso.png", outfit.Parts["BODY_DIFF"].Shading);
            Assert.AreEqual(Path.Combine(folder.Kits, "outfits", "open_wheel_90s"), outfit.Folder);
            Assert.IsNull(loader.FindOutfitSet("missing"));

            Assert.AreEqual(Path.Combine(folder.TyreFiles, "tire_goodyear_f93_diff.dds"), loader.FindTyreSet("GOODYEAR_F93")["tirediff"]);
            Assert.IsNull(loader.FindTyreSet("bridgestone"));
        }

        // The kits, number styles, outfit set and tyre sets that ship with the app: every file they
        // name exists, every template is in exact key colours (fixed artwork aside) and every
        // two-colour strip pure key colours, so a recolour leaves no pixel in its template colour.
        [TestMethod]
        public void ShippedKits_AreComplete_AndInKeyColours()
        {
            string app = Path.Combine(RepoRoot(), "AMS2ChEd");
            var loader = new LiveryKitLoader(Path.Combine(app, "LiveryKits"), Path.Combine(app, "Tyres", "tyre_sets.json"), Path.Combine(app, "Tyres", "files"));

            foreach (var model in new[] { "formula_v10_g1", "mclaren_mp4_12", "formula_v10_g2_b", "formula_v10_g2_m", "formula_ultimate_2024", "formula_ultimate_2019", "formula_reiza", "formula_v8_g1_m", "formula_v8_g1_b", "renault_r26", "formula_v10_g3_m", "formula_v10_g3_b", "renault_r25", "formula_v8_g2", "renault_r28", "formula_edge_g1m1", "formula_edge_g1m2", "formula_edge_g1m3", "formula_hitech_g1m1", "formula_hitech_g1m2", "formula_hitech_g1m3", "formula_hitech_g1m4", "formula_hitech_g2m1", "formula_hitech_g2m2", "formula_hitech_g2m3", "formula_classic_g2m1", "formula_classic_g2m2", "formula_classic_g2m3", "mclaren_mp44", "formula_classic_g3m1", "formula_classic_g3m2", "formula_classic_g3m3", "formula_classic_g3m4", "mclaren_mp45b", "formula_classic_g4m1", "formula_classic_g4m2", "formula_classic_g4m3", "mclaren_mp46", "mclaren_mp47", "mclaren_mp48" })
            {
                var kit = loader.FindKit(model);
                Assert.IsNotNull(kit, model);
                Assert.IsTrue(File.Exists(Path.Combine(kit.Folder, kit.LiveryXml)), $"{model} livery.xml");
                // a kit with no outfit set leaves drivers in AMS2's default suit (formula_ultimate_2024 and _2019:
                // its driver model's torso and gloves have no shading of our own yet)
                if (kit.OutfitSet != null)
                    Assert.IsNotNull(loader.FindOutfitSet(kit.OutfitSet), $"{model} outfit set");
                Assert.IsTrue(kit.Templates.Count > 0, model);
                foreach (var template in kit.Templates)
                {
                    string body = Path.Combine(kit.Folder, template.Body);
                    Assert.IsTrue(File.Exists(body), body);
                    using var image = Image.Load<Rgba32>(body);
                    // fixed artwork (carbon, mirror glass, grilles) may stay, but nothing close to a key
                    Assert.AreEqual(0, TemplateRecolour.CountNearKey(image, 60), $"{template.Id}: pixels near a key colour without being one");
                    Assert.IsTrue(template.NumbersPlacements.All(p => p.SheetSize?.Length == 2), $"{template.Id}: sheet sizes");
                }
            }

            foreach (var style in new[] { "sans", "sans_outline", "serif", "serif_outline", "ferrari", "sans_italic", "sans_outline_italic", "serif_italic", "serif_outline_italic", "ferrari_italic", "arial", "arial_outline", "arial_italic", "arial_outline_italic",
                "angular", "bahnschrift", "prolamina", "jersey_outline", "rounded", "geometric_outline", "lato_outline", "segoe_italic", "franklin_italic", "lato_italic" })
            {
                string path = loader.NumberStylePath(style);
                Assert.IsTrue(File.Exists(path), style);
                if (loader.FindNumberStyle(style).TwoColour)
                {
                    using var image = Image.Load<Rgba32>(path);
                    Assert.AreEqual(0, NonKeyPixels(image), $"{style}: pixels outside the key colours");
                }
            }

            foreach (var part in loader.FindOutfitSet("open_wheel_90s").Parts.Values)
                Assert.IsTrue(File.Exists(Path.Combine(loader.FindOutfitSet("open_wheel_90s").Folder, part.Shading)), part.Shading);

            foreach (var set in new[] { "goodyear_f93", "bridgestone_v10" })
            {
                var tyres = loader.FindTyreSet(set);
                CollectionAssert.AreEquivalent(new[] { "TIREDIFF", "TIRESPEC", "TIREBLURDIFF", "TIREBLURSPEC", "TIRENORM" }, tyres.Keys.ToArray(), set);
                Assert.IsTrue(tyres.Values.All(File.Exists), set);
            }
        }

        private static int NonKeyPixels(Image<Rgba32> image)
        {
            var keys = TemplateRecolour.KeyColours.Values.ToHashSet();
            int count = 0;
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                    foreach (var pixel in accessor.GetRowSpan(y))
                        if (pixel.A != 0 && !keys.Contains(new Rgba32(pixel.R, pixel.G, pixel.B)))
                            count++;
            });
            return count;
        }

        private static string RepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AMS2ChEd", "AMS2ChEd.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
        }

        private sealed class TempFolder : IDisposable
        {
            public readonly string Root = Path.Combine(Path.GetTempPath(), "LiveryKitLoaderTests_" + Guid.NewGuid().ToString("N"));
            public string Kits => Path.Combine(Root, "LiveryKits");
            public string TyreSets => Path.Combine(Root, "tyre_sets.json");
            public string TyreFiles => Path.Combine(Root, "files");

            public TempFolder() => Directory.CreateDirectory(Kits);

            public void Write(string relative, string content)
            {
                string path = Path.Combine(Kits, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, content);
            }

            public LiveryKitLoader Loader() => new LiveryKitLoader(Kits, TyreSets, TyreFiles);

            public void Dispose()
            {
                try { Directory.Delete(Root, true); } catch (IOException) { }
            }
        }
    }
}
