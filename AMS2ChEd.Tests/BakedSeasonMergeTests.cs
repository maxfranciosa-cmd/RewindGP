using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class BakedSeasonMergeTests
    {
        [TestMethod]
        public void MergeSeason_PackTeamOnTheSameCar_GivesItsArtwork_KeepsBakedGameplay()
        {
            var baked = Season(BakedTeam("williams", "formula_v10_g1"));
            var packTeam = PackTeam("williams", "formula_v10_g1");
            packTeam.TeamName = "Willis Rellaux";
            packTeam.Reputation = TeamReputation.MINNOW;
            packTeam.Ams2CarPerformanceMalus = new Dictionary<string, double> { { "power_scalar", -2 } };
            var warnings = new List<string>();

            var team = Team(BakedSeasonMerge.MergeSeason(baked, Season(packTeam), warnings), "williams");

            Assert.AreEqual("car_liveries\\Willis.png", team.BaseLiveryDriver1);
            Assert.AreEqual("car_liveries\\Willis2.png", team.BaseLiveryDriver2);
            Assert.AreEqual("previews\\williams.dds", team.LiveryPreview);
            Assert.AreEqual(1, team.NumbersPlacements.Count());
            Assert.AreEqual("helmet_sponsors\\williams.png", team.HelmetSponsors);
            Assert.AreEqual("helmet_sponsors\\williams_visor.png", team.VisorSponsors);
            Assert.AreEqual("helmet_liveries\\hill_special.png", team.DriversSpecificHelmet["damon_hill"]);
            Assert.AreEqual(7, team.LiveryOverrides.Single().RaceId);
            Assert.AreEqual("<USER_OVERRIDES />", team.LiveryXml);

            Assert.AreEqual("Williams Renault", team.TeamName);
            Assert.AreEqual(TeamReputation.TOP_TEAM, team.Reputation);
            Assert.AreEqual(-1.03, team.Ams2CarPerformanceMalus["power_scalar"]);
            Assert.AreEqual("damon_hill", team.Driver1Contract.DriverId);
            Assert.AreEqual("williams_1996", team.GenericLivery.Template);
            Assert.AreEqual(0, warnings.Count);
        }

        [TestMethod]
        public void MergeSeason_PackTeamPaintedForAnotherCar_IsIgnored_WithAWarning()
        {
            var baked = Season(BakedTeam("mclaren", "mclaren_mp4_12"));
            var warnings = new List<string>();

            var team = Team(BakedSeasonMerge.MergeSeason(baked, Season(PackTeam("mclaren", "formula_v10_g1")), warnings), "mclaren");

            Assert.IsNull(team.BaseLiveryDriver1);
            Assert.IsNull(team.LiveryXml);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains(warnings[0], "mclaren");
            StringAssert.Contains(warnings[0], "formula_v10_g1");
        }

        [TestMethod]
        public void MergeSeason_PackTeamNamingNoCar_IsTakenAtItsWord()
        {
            var baked = Season(BakedTeam("ferrari", "formula_v10_g1"));

            var team = Team(BakedSeasonMerge.MergeSeason(baked, Season(PackTeam("ferrari", null)), new List<string>()), "ferrari");

            Assert.AreEqual("car_liveries\\Willis.png", team.BaseLiveryDriver1);
        }

        [TestMethod]
        public void MergeSeason_TeamsOnlyOnOneSide_AreIgnoredOrLeftWithoutArtwork()
        {
            var baked = Season(BakedTeam("williams", "formula_v10_g1"), BakedTeam("forti", "formula_v10_g1"));
            var pack = Season(PackTeam("williams", "formula_v10_g1"), PackTeam("pacific", "formula_v10_g1"));

            var merged = BakedSeasonMerge.MergeSeason(baked, pack, new List<string>());

            CollectionAssert.AreEqual(new[] { "williams", "forti" }, merged.Teams.Select(t => t.TeamId).ToArray());
            Assert.IsNull(Team(merged, "forti").BaseLiveryDriver1);
        }

        [TestMethod]
        public void MergeSeason_CoversBySameRace_AndThePacksSlug_NothingElse()
        {
            var baked = Season();
            baked.Races = new[] { Race(1, null), Race(2, null) };
            var pack = Season();
            pack.Slug = "1996";
            pack.Year = 2000;
            pack.PointsSystem = new Dictionary<string, int> { { "1", 25 } };
            pack.Races = new[] { Race(2, "gpcovers/interlagos.jpg"), Race(9, "gpcovers/other.jpg") };

            var merged = BakedSeasonMerge.MergeSeason(baked, pack, new List<string>());

            Assert.IsNull(merged.Races.Single(r => r.RaceId == 1).CoverPictureUrl);
            Assert.AreEqual("gpcovers/interlagos.jpg", merged.Races.Single(r => r.RaceId == 2).CoverPictureUrl);
            Assert.AreEqual(2, merged.Races.Count());
            Assert.AreEqual("1996", merged.Slug);
            Assert.AreEqual(1996, merged.Year);
            Assert.AreEqual(10, merged.PointsSystem["1"]);
        }

        [TestMethod]
        public void MergeSeason_NoPack_LeavesTheBakedSeasonAsItIs()
        {
            var baked = Season(BakedTeam("williams", "formula_v10_g1"));
            baked.Slug = null;

            var merged = BakedSeasonMerge.MergeSeason(baked, null, new List<string>());

            Assert.AreSame(baked, merged);
            Assert.IsNull(Team(merged, "williams").BaseLiveryDriver1);
            Assert.IsNull(merged.Slug);
        }

        [TestMethod]
        public void MergeDrivers_PackGivesHelmetsAndPortraits_BakedKeepsTheRest()
        {
            var bakedHill = new Ams2DriverData { DriverId = "damon_hill", Name = "Damon Hill", Reputation = DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, GenericHelmet = new GenericHelmet { Design = "15_swoosh" } };
            var packHill = new Ams2DriverData
            {
                DriverId = "damon_hill", Name = "Damien Hull", Reputation = DriverReputation.YOUNG_TALENT,
                BaseHelmetFile = "../1996/helmet_liveries/hill.png", BaseVisorFile = "../1996/helmet_liveries/hill_visor.png",
                BaseHelmetFile90s = "h90", BaseHelmetFile80s = "h80", BaseVisorFile80s = "v80", BaseHelmetFile70s = "h70", BaseVisorFile70s = "v70",
                PictureUrl = "https://example.org/hill.png"
            };
            var packOnly = new Ams2DriverData { DriverId = "someone_else" };

            var merged = BakedSeasonMerge.MergeDrivers(
                new Dictionary<string, Ams2DriverData> { { "damon_hill", bakedHill } },
                new Dictionary<string, Ams2DriverData> { { "damon_hill", packHill }, { "someone_else", packOnly } });

            var hill = merged["damon_hill"];
            Assert.AreEqual("../1996/helmet_liveries/hill.png", hill.BaseHelmetFile);
            Assert.AreEqual("../1996/helmet_liveries/hill_visor.png", hill.BaseVisorFile);
            Assert.AreEqual("h90", hill.BaseHelmetFile90s);
            Assert.AreEqual("v70", hill.BaseVisorFile70s);
            Assert.AreEqual("https://example.org/hill.png", hill.PictureUrl);
            Assert.AreEqual("Damon Hill", hill.Name);
            Assert.AreEqual(DriverReputation.PRIME_CHAMPIONSHIP_LEVEL, hill.Reputation);
            Assert.AreEqual("15_swoosh", hill.GenericHelmet.Design);
            Assert.IsFalse(merged.ContainsKey("someone_else"));
        }

        private static Ams2Season Season(params Ams2TeamEntry[] teams) => new Ams2Season
        {
            Year = 1996,
            Slug = "baked",
            PointsSystem = new Dictionary<string, int> { { "1", 10 } },
            Teams = teams.Cast<ITeamEntry>().ToList(),
            Races = new List<Race>()
        };

        private static Race Race(int id, string cover) => new Race { RaceId = id, RaceName = $"Race {id}", CoverPictureUrl = cover };

        private static Ams2TeamEntry Team(Ams2Season season, string id) => season.Teams.OfType<Ams2TeamEntry>().Single(t => t.TeamId == id);

        private static Ams2TeamEntry BakedTeam(string id, string car) => new Ams2TeamEntry
        {
            TeamId = id,
            TeamName = "Williams Renault",
            Reputation = TeamReputation.TOP_TEAM,
            Ams2Car = car,
            Ams2CarPerformanceMalus = new Dictionary<string, double> { { "power_scalar", -1.03 } },
            Driver1Contract = new DriverContract { DriverId = "damon_hill" },
            GenericLivery = new GenericLivery { Template = "williams_1996" }
        };

        private static Ams2TeamEntry PackTeam(string id, string car) => new Ams2TeamEntry
        {
            TeamId = id,
            Ams2Car = car,
            BaseLiveryDriver1 = "car_liveries\\Willis.png",
            BaseLiveryDriver2 = "car_liveries\\Willis2.png",
            LiveryPreview = "previews\\williams.dds",
            NumbersPlacements = new[] { new NumbersPlacement { NumberPlateWidth = 120 } },
            HelmetSponsors = "helmet_sponsors\\williams.png",
            VisorSponsors = "helmet_sponsors\\williams_visor.png",
            DriversSpecificHelmet = new Dictionary<string, string> { { "damon_hill", "helmet_liveries\\hill_special.png" } },
            LiveryOverrides = new[] { new LiveryOverride { RaceId = 7 } },
            LiveryXml = "<USER_OVERRIDES />"
        };
    }
}
