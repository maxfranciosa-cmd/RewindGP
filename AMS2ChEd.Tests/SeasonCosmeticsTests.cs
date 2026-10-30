using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class SeasonCosmeticsTests
    {
        private string _root;
        private string Seasons => Path.Combine(_root, "Seasons");

        [TestInitialize]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "SeasonCosmeticsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Seasons, "2000"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(_root, true); } catch { }
        }

        // a file under Seasons/2000, written as the season data names it
        private void Touch(string path)
        {
            var full = Path.GetFullPath(Path.Combine(Seasons, "2000", path));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, "x");
        }

        [TestMethod]
        public void EffectiveSeason_TeamWhoseSkinPackIsNotImported_IsLeftOut()
        {
            var cosmetics = Cosmetics(("ferrari", ShippedTeam("ferrari")));

            Assert.IsNull(SeasonCosmeticsFiles.EffectiveSeason(cosmetics, 2000, Seasons));
        }

        [TestMethod]
        public void EffectiveSeason_KeepsWhatIsOnDisk_DropsTheRest()
        {
            Touch("external_liveries/car_liveries/ferrari/default.dds");
            Touch("../../BakedSeasons/2000/cosmetics/numbers/ferrari_nose.png");
            Touch("external_liveries/helmet_sponsors/ferrari/default.png");
            // no visor layer, no preview, the race livery not imported

            var season = SeasonCosmeticsFiles.EffectiveSeason(Cosmetics(("ferrari", ShippedTeam("ferrari"))), 2000, Seasons);
            var team = season.Teams.OfType<Ams2TeamEntry>().Single();

            Assert.AreEqual("ferrari", team.TeamId);
            Assert.AreEqual("external_liveries/car_liveries/ferrari/default.dds", team.BaseLiveryDriver1);
            Assert.AreEqual("external_liveries/helmet_sponsors/ferrari/default.png", team.HelmetSponsors);
            Assert.IsNull(team.VisorSponsors);
            Assert.IsNull(team.LiveryPreview);
            Assert.AreEqual(1, team.NumbersPlacements.Count());
            Assert.AreEqual(0, team.LiveryOverrides.Count());
        }

        [TestMethod]
        public void EffectiveSeason_AnOverrideOfTheHelmetSponsorsAlone_CountsOnceItsLayerIsOnDisk()
        {
            Touch("external_liveries/car_liveries/ferrari/default.dds");
            var team = ShippedTeam("ferrari");
            team.LiveryOverrides = new[] { new LiveryOverride { RaceId = 9, HelmetSponsors = "external_liveries/helmet_sponsors/ferrari/Marlboro_NT.png" } };
            var cosmetics = Cosmetics(("ferrari", team));

            var before = SeasonCosmeticsFiles.EffectiveSeason(cosmetics, 2000, Seasons).Teams.OfType<Ams2TeamEntry>().Single().LiveryOverrides.Single();
            Assert.IsNull(before.HelmetSponsors, "not imported: the team's own layer stays");

            Touch("external_liveries/helmet_sponsors/ferrari/Marlboro_NT.png");

            var after = SeasonCosmeticsFiles.EffectiveSeason(cosmetics, 2000, Seasons).Teams.OfType<Ams2TeamEntry>().Single().LiveryOverrides.Single();
            Assert.AreEqual("external_liveries/helmet_sponsors/ferrari/Marlboro_NT.png", after.HelmetSponsors);
        }

        [TestMethod]
        public void EffectiveSeason_NullEntriesInLists_AreSkipped()
        {
            Touch("external_liveries/car_liveries/ferrari/default.dds");
            Touch("external_liveries/car_liveries/ferrari/gbr.dds");
            var team = ShippedTeam("ferrari");
            team.NumbersPlacements = new NumbersPlacement[] { null };
            team.LiveryOverrides = new[] { null, new LiveryOverride { RaceId = 9, Driver1Livery = "external_liveries/car_liveries/ferrari/gbr.dds", NumbersPlacements = new NumbersPlacement[] { null } } };

            var narrowed = SeasonCosmeticsFiles.EffectiveSeason(Cosmetics(("ferrari", team)), 2000, Seasons).Teams.OfType<Ams2TeamEntry>().Single();

            Assert.AreEqual(0, narrowed.NumbersPlacements.Count());
            Assert.AreEqual(9, narrowed.LiveryOverrides.Single().RaceId);
            Assert.AreEqual(0, narrowed.LiveryOverrides.Single().NumbersPlacements.Count());
        }

        [TestMethod]
        public void EffectiveDrivers_ShippedHelmetCounts_SkinPackHelmetOnlyOnceImported()
        {
            Touch("../../Drivers/own_helmets/michael_schumacher/0a1b2c3d.png");
            var cosmetics = new SeasonCosmetics
            {
                Drivers = new Dictionary<string, Ams2DriverData>
                {
                    ["michael_schumacher"] = new Ams2DriverData { BaseHelmetFile = "../../Drivers/own_helmets/michael_schumacher/0a1b2c3d.png", BaseVisorFile = "../../Drivers/own_helmets/michael_schumacher/missing_visor.png" },
                    ["luciano_burti"] = new Ams2DriverData { BaseHelmetFile = "external_liveries/helmets/burti.dds" }
                }
            };

            var drivers = SeasonCosmeticsFiles.EffectiveDrivers(cosmetics, 2000, Seasons);

            Assert.AreEqual("../../Drivers/own_helmets/michael_schumacher/0a1b2c3d.png", drivers["michael_schumacher"].BaseHelmetFile);
            Assert.IsNull(drivers["michael_schumacher"].BaseVisorFile);
            Assert.IsNull(drivers["luciano_burti"].BaseHelmetFile);
        }

        [TestMethod]
        public void DropMissingFiles_ClearsDeadPaths_KeepsFilesOnDiskAndPortrait()
        {
            Touch("helmet_liveries/alonso.png");
            var drivers = new Dictionary<string, Ams2DriverData>
            {
                ["fernando_alonso"] = new Ams2DriverData { BaseHelmetFile = "helmet_liveries/alonso.png", BaseVisorFile = "helmet_liveries/missing_visor.png" },
                ["michael_schumacher"] = new Ams2DriverData { BaseHelmetFile = "../2000/helmet_liveries/gone.png", BaseVisorFile = "helmet_liveries/alonso.png", PictureUrl = "https://example.org/schumacher.png" }
            };

            SeasonCosmeticsFiles.DropMissingFiles(drivers, 2000, Seasons);

            Assert.AreEqual("helmet_liveries/alonso.png", drivers["fernando_alonso"].BaseHelmetFile);
            Assert.IsNull(drivers["fernando_alonso"].BaseVisorFile);
            Assert.IsNull(drivers["michael_schumacher"].BaseHelmetFile);
            Assert.IsNull(drivers["michael_schumacher"].BaseVisorFile);
            Assert.AreEqual("https://example.org/schumacher.png", drivers["michael_schumacher"].PictureUrl);
        }

        [TestMethod]
        public void EffectiveDrivers_Portrait_OnlyOnceTheSkinPackIsImported()
        {
            var cosmetics = new SeasonCosmetics
            {
                Drivers = new Dictionary<string, Ams2DriverData>
                {
                    ["michael_schumacher"] = new Ams2DriverData { PictureUrl = "https://example.org/schumacher.png" }
                }
            };

            Assert.IsNull(SeasonCosmeticsFiles.EffectiveDrivers(cosmetics, 2000, Seasons)["michael_schumacher"].PictureUrl);

            Touch("skin_pack.json");

            Assert.AreEqual("https://example.org/schumacher.png", SeasonCosmeticsFiles.EffectiveDrivers(cosmetics, 2000, Seasons)["michael_schumacher"].PictureUrl);
        }

        [TestMethod]
        public void MergeSeason_PackTeamWins_ThenShippedCosmetics_ThenNothing()
        {
            var baked = Season(BakedTeam("ferrari"), BakedTeam("williams"), BakedTeam("minardi"));
            var pack = Season(Painted("ferrari", "pack"));
            var cosmetics = Season(Painted("ferrari", "shipped"), Painted("williams", "shipped"));

            var merged = BakedSeasonMerge.MergeSeason(baked, pack, cosmetics, _ => null, new List<string>());

            Assert.AreEqual("pack/ferrari.dds", Team(merged, "ferrari").BaseLiveryDriver1);
            Assert.AreEqual("shipped/williams.dds", Team(merged, "williams").BaseLiveryDriver1);
            Assert.IsNull(Team(merged, "minardi").BaseLiveryDriver1);
        }

        [TestMethod]
        public void MergeSeason_PackTeamWithoutBody_LetsShippedCosmeticsPaintIt()
        {
            var baked = Season(BakedTeam("ferrari"));
            var packTeam = new Ams2TeamEntry { TeamId = "ferrari", Ams2Car = "formula_v10_g2_b", HelmetSponsors = "pack/sponsors.png" };

            var merged = BakedSeasonMerge.MergeSeason(baked, Season(packTeam), Season(Painted("ferrari", "shipped")), _ => null, new List<string>());

            Assert.AreEqual("shipped/ferrari.dds", Team(merged, "ferrari").BaseLiveryDriver1);
        }

        [TestMethod]
        public void MergeSeason_ShippedCosmeticsBeatAFallback()
        {
            var team = BakedTeam("ferrari");
            team.CosmeticFallbacks = new List<CosmeticFallback> { new CosmeticFallback { Season = 1999 } };
            var others = new Dictionary<int, Ams2Season> { [1999] = Season(Painted("ferrari", "borrowed")) };

            var merged = BakedSeasonMerge.MergeSeason(Season(team), null, Season(Painted("ferrari", "shipped")), y => others.GetValueOrDefault(y), new List<string>(), Seasons);

            Assert.AreEqual("shipped/ferrari.dds", Team(merged, "ferrari").BaseLiveryDriver1);
        }

        [TestMethod]
        public void CombineArtworkSources_PackWhereItPaints_ShippedElsewhere()
        {
            var bodyless = new Ams2TeamEntry { TeamId = "williams", Ams2Car = "formula_v10_g2_b" };

            var combined = BakedSeasonMerge.CombineArtworkSources(
                Season(Painted("ferrari", "pack"), bodyless),
                Season(Painted("ferrari", "shipped"), Painted("williams", "shipped")));

            Assert.AreEqual("pack/ferrari.dds", Team(combined, "ferrari").BaseLiveryDriver1);
            Assert.AreEqual("shipped/williams.dds", Team(combined, "williams").BaseLiveryDriver1);
        }

        [TestMethod]
        public void MergeDrivers_PerSlot_PackThenShippedThenOtherYears()
        {
            var baked = new Dictionary<string, Ams2DriverData> { ["jean_alesi"] = new Ams2DriverData { DriverId = "jean_alesi" } };
            var pack = new Dictionary<string, Ams2DriverData> { ["jean_alesi"] = new Ams2DriverData { DriverId = "jean_alesi", BaseHelmetFile90s = "pack/alesi_90s.png" } };
            var shipped = new Dictionary<string, Ams2DriverData> { ["jean_alesi"] = new Ams2DriverData { DriverId = "jean_alesi", BaseHelmetFile = "shipped/alesi.png", BaseHelmetFile90s = "shipped/alesi_90s.png" } };
            var others = new Dictionary<int, Dictionary<string, Ams2DriverData>>
            {
                [1999] = new() { ["jean_alesi"] = new Ams2DriverData { DriverId = "jean_alesi", BaseHelmetFile80s = "helmet_liveries/alesi_80s.png" } }
            };

            var alesi = BakedSeasonMerge.MergeDrivers(baked, pack, shipped, 2000, others, Seasons)["jean_alesi"];

            Assert.AreEqual("pack/alesi_90s.png", alesi.BaseHelmetFile90s);
            Assert.AreEqual("shipped/alesi.png", alesi.BaseHelmetFile);
            Assert.AreEqual("../1999/helmet_liveries/alesi_80s.png", alesi.BaseHelmetFile80s.Replace('\\', '/'));
        }

        private static SeasonCosmetics Cosmetics(params (string Id, Ams2TeamEntry Team)[] teams) => new SeasonCosmetics
        {
            Teams = teams.ToDictionary(t => t.Id, t => t.Team)
        };

        private static Ams2TeamEntry ShippedTeam(string id) => new Ams2TeamEntry
        {
            Ams2Car = "formula_v10_g2_b",
            BaseLiveryDriver1 = $"external_liveries/car_liveries/{id}/default.dds",
            BaseLiveryDriver2 = $"external_liveries/car_liveries/{id}/default.dds",
            LiveryPreview = $"external_liveries/previews/{id}/default.dds",
            HelmetSponsors = $"external_liveries/helmet_sponsors/{id}/default.png",
            VisorSponsors = $"external_liveries/helmet_sponsors/{id}/default_visor.png",
            NumbersPlacements = new[] { new NumbersPlacement { NumberPlateWidth = 120, NumbersTexture = $"../../BakedSeasons/2000/cosmetics/numbers/{id}_nose.png" } },
            LiveryOverrides = new[] { new LiveryOverride { RaceId = 9, Driver1Livery = $"external_liveries/car_liveries/{id}/gbr.dds" } }
        };

        private static Ams2TeamEntry Painted(string id, string source) => new Ams2TeamEntry
        {
            TeamId = id,
            Ams2Car = "formula_v10_g2_b",
            BaseLiveryDriver1 = $"{source}/{id}.dds",
            BaseLiveryDriver2 = $"{source}/{id}.dds"
        };

        private static Ams2TeamEntry BakedTeam(string id) => new Ams2TeamEntry { TeamId = id, Ams2Car = "formula_v10_g2_b" };

        private static Ams2Season Season(params Ams2TeamEntry[] teams) => new Ams2Season
        {
            Year = 2000,
            Teams = teams.Cast<ITeamEntry>().ToList(),
            Races = new List<AMS2ChEd.Business.Models.Concrete.Race>()
        };

        private static Ams2TeamEntry Team(Ams2Season season, string id) => season.Teams.OfType<Ams2TeamEntry>().Single(t => t.TeamId == id);
    }
}
