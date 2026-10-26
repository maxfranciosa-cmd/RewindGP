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

        // ---- cosmetic fallbacks: teams ----

        private static readonly string SeasonsRoot = Path.Combine(Path.GetTempPath(), "rewind_gp", "Seasons");

        private static Ams2Season Merge(Ams2Season baked, Ams2Season pack, Dictionary<int, Ams2Season> others, List<string> warnings = null) =>
            BakedSeasonMerge.MergeSeason(baked, pack, y => others.TryGetValue(y, out var s) ? s : null, warnings ?? new List<string>(), SeasonsRoot);

        private static Ams2TeamEntry WithFallbacks(Ams2TeamEntry team, params CosmeticFallback[] fallbacks)
        {
            team.CosmeticFallbacks = fallbacks.ToList();
            return team;
        }

        [TestMethod]
        public void Fallbacks_OwnPackWithALivery_Wins()
        {
            var baked = Season(WithFallbacks(BakedTeam("williams", "formula_v10_g1"), new CosmeticFallback { Season = 1995 }));
            var other = PackTeam("williams", "formula_v10_g1");
            other.BaseLiveryDriver1 = "car_liveries\\Other.png";

            var team = Team(Merge(baked, Season(PackTeam("williams", "formula_v10_g1")), new() { { 1995, Season(other) } }), "williams");

            Assert.AreEqual("car_liveries\\Willis.png", team.BaseLiveryDriver1);
        }

        [TestMethod]
        public void Fallbacks_OwnPackLacksTheTeam_FirstInstalledFallbackOnTheCar_IsBorrowedAndRebased()
        {
            var bakedTeam = WithFallbacks(BakedTeam("williams", "formula_v10_g1"), new CosmeticFallback { Season = 1995 }, new CosmeticFallback { Season = 1997 });
            var baked = Season(bakedTeam);
            var source = PackTeam("williams", "formula_v10_g1");
            source.DriversSpecificHelmet["villeneuve"] = "../../Drivers/base_helmet_liveries/jv.png";
            var unused = PackTeam("williams", "formula_v10_g1");
            unused.BaseLiveryDriver1 = "car_liveries\\Unused.png";

            var team = Team(Merge(baked, Season(), new() { { 1997, Season(source) }, { 1998, Season(unused) } }), "williams");

            Assert.AreEqual("../1997/car_liveries/Willis.png", team.BaseLiveryDriver1);
            Assert.AreEqual("../1997/car_liveries/Willis2.png", team.BaseLiveryDriver2);
            Assert.AreEqual("../1997/previews/williams.dds", team.LiveryPreview);
            Assert.AreEqual("../1997/helmet_sponsors/williams.png", team.HelmetSponsors);
            Assert.AreEqual("../1997/helmet_sponsors/williams_visor.png", team.VisorSponsors);
            Assert.AreEqual("../1997/helmet_liveries/hill_special.png", team.DriversSpecificHelmet["damon_hill"]);
            Assert.AreEqual("../../Drivers/base_helmet_liveries/jv.png", team.DriversSpecificHelmet["villeneuve"]);
            Assert.AreEqual("../1997/numbers/williams.png", team.NumbersPlacements.Single().NumbersTexture);
            Assert.IsNull(team.LiveryOverrides);
            Assert.IsNull(team.LiveryXml);
            // the source pack itself is left as it was
            Assert.AreEqual("car_liveries\\Willis.png", source.BaseLiveryDriver1);
            Assert.AreEqual("numbers\\williams.png", source.NumbersPlacements.Single().NumbersTexture);
        }

        [TestMethod]
        public void Fallbacks_KeepTheBakedLiveryXml()
        {
            var bakedTeam = WithFallbacks(BakedTeam("williams", "formula_v10_g1"), new CosmeticFallback { Season = 1997 });
            bakedTeam.LiveryXml = "<USER_OVERRIDES baked=\"1\" />";

            var team = Team(Merge(Season(bakedTeam), null, new() { { 1997, Season(PackTeam("williams", "formula_v10_g1")) } }), "williams");

            Assert.AreEqual("../1997/car_liveries/Willis.png", team.BaseLiveryDriver1);
            Assert.AreEqual("<USER_OVERRIDES baked=\"1\" />", team.LiveryXml);
        }

        [TestMethod]
        public void Fallbacks_OwnPackTeamOnAnotherCar_GivesWayToAFallback()
        {
            var baked = Season(WithFallbacks(BakedTeam("williams", "formula_v10_g1"), new CosmeticFallback { Season = 1997 }));
            var warnings = new List<string>();

            var team = Team(Merge(baked, Season(PackTeam("williams", "formula_v10_g2")), new() { { 1997, Season(PackTeam("williams", "formula_v10_g1")) } }, warnings), "williams");

            Assert.AreEqual("../1997/car_liveries/Willis.png", team.BaseLiveryDriver1);
            Assert.AreEqual(1, warnings.Count);
        }

        [TestMethod]
        public void Fallbacks_OwnPackTeamWithoutABodyLivery_GivesWayToAFallback_OrKeepsItsArtworkWhenNoneResolves()
        {
            var partial = PackTeam("williams", "formula_v10_g1");
            partial.BaseLiveryDriver1 = partial.BaseLiveryDriver2 = null;

            var borrowed = Team(Merge(Season(WithFallbacks(BakedTeam("williams", "formula_v10_g1"), new CosmeticFallback { Season = 1997 })),
                Season(partial), new() { { 1997, Season(PackTeam("williams", "formula_v10_g1")) } }), "williams");
            Assert.AreEqual("../1997/car_liveries/Willis.png", borrowed.BaseLiveryDriver1);
            Assert.AreEqual("../1997/helmet_sponsors/williams.png", borrowed.HelmetSponsors);

            var own = Team(Merge(Season(WithFallbacks(BakedTeam("williams", "formula_v10_g1"), new CosmeticFallback { Season = 1997 })),
                Season(partial), new()), "williams");
            Assert.IsNull(own.BaseLiveryDriver1);
            Assert.AreEqual("helmet_sponsors\\williams.png", own.HelmetSponsors);
        }

        [TestMethod]
        public void Fallbacks_NotInstalledOrWithoutTheTeamOrALiveryOrOfTheOwnYear_AreSkippedSilently()
        {
            var noLivery = PackTeam("williams", "formula_v10_g1");
            noLivery.BaseLiveryDriver1 = noLivery.BaseLiveryDriver2 = null;
            var baked = Season(WithFallbacks(BakedTeam("williams", "formula_v10_g1"),
                new CosmeticFallback { Season = 1996 },
                new CosmeticFallback { Season = 1990 },
                new CosmeticFallback { Season = 1991 },
                new CosmeticFallback { Season = 1992 },
                new CosmeticFallback { Season = 1993 }));
            var warnings = new List<string>();

            var team = Team(Merge(baked, null, new()
            {
                { 1996, Season(PackTeam("williams", "formula_v10_g1")) },
                { 1991, Season(PackTeam("benetton", "formula_v10_g1")) },
                { 1992, Season(noLivery) },
                { 1993, Season(PackTeam("williams", "formula_v10_g1")) }
            }, warnings), "williams");

            Assert.AreEqual("../1993/car_liveries/Willis.png", team.BaseLiveryDriver1);
            Assert.AreEqual(0, warnings.Count);
        }

        [DataTestMethod]
        [DataRow("formula_v10_g2", DisplayName = "another car")]
        [DataRow(null, DisplayName = "no car named")]
        public void Fallbacks_NotOnTheSameCar_AreSkippedWithAWarning(string sourceCar)
        {
            var baked = Season(WithFallbacks(BakedTeam("williams", "formula_v10_g1"), new CosmeticFallback { Season = 1995 }, new CosmeticFallback { Season = 1997 }));
            var warnings = new List<string>();

            var team = Team(Merge(baked, null, new()
            {
                { 1995, Season(PackTeam("williams", sourceCar)) },
                { 1997, Season(PackTeam("williams", "formula_v10_g1")) }
            }, warnings), "williams");

            Assert.AreEqual("../1997/car_liveries/Willis.png", team.BaseLiveryDriver1);
            Assert.AreEqual(1, warnings.Count);
            StringAssert.Contains(warnings[0], "1995");
        }

        [TestMethod]
        public void Fallbacks_WithATeamId_LookUpThatTeam()
        {
            var baked = Season(WithFallbacks(BakedTeam("stewart", "formula_v10_g1"), new CosmeticFallback { Season = 1997, TeamId = "jaguar" }));
            var jaguar = PackTeam("jaguar", "formula_v10_g1");
            jaguar.BaseLiveryDriver1 = "car_liveries\\Jaguar.png";

            var team = Team(Merge(baked, null, new() { { 1997, Season(PackTeam("stewart", "formula_v10_g1"), jaguar) } }), "stewart");

            Assert.AreEqual("../1997/car_liveries/Jaguar.png", team.BaseLiveryDriver1);
        }

        [TestMethod]
        public void Fallbacks_WithAnOverride_LiftItToTheTeam_TeamValuesFillItsGaps()
        {
            var baked = Season(WithFallbacks(BakedTeam("williams", "formula_v10_g1"), new CosmeticFallback { Season = 1997, LiveryOverrideRaceId = 7 }));
            var source = PackTeam("williams", "formula_v10_g1");
            source.LiveryOverrides = new[]
            {
                new LiveryOverride
                {
                    RaceId = 7, Driver1Livery = "car_liveries\\Special.png", LiveryPreview = "previews\\special.dds",
                    NumbersPlacements = new[] { new NumbersPlacement { NumbersTexture = "numbers\\special.png" } }
                }
            };

            var team = Team(Merge(baked, null, new() { { 1997, Season(source) } }), "williams");

            Assert.AreEqual("../1997/car_liveries/Special.png", team.BaseLiveryDriver1);
            Assert.AreEqual("../1997/car_liveries/Willis2.png", team.BaseLiveryDriver2);
            Assert.AreEqual("../1997/previews/special.dds", team.LiveryPreview);
            Assert.AreEqual("../1997/numbers/special.png", team.NumbersPlacements.Single().NumbersTexture);
            Assert.AreEqual("../1997/helmet_sponsors/williams.png", team.HelmetSponsors);
            Assert.IsNull(team.LiveryOverrides);
        }

        [TestMethod]
        public void Fallbacks_WithAMissingOverride_AreSkippedWithAWarning()
        {
            var baked = Season(WithFallbacks(BakedTeam("williams", "formula_v10_g1"), new CosmeticFallback { Season = 1997, LiveryOverrideRaceId = 3 }));
            var warnings = new List<string>();

            var team = Team(Merge(baked, null, new() { { 1997, Season(PackTeam("williams", "formula_v10_g1")) } }, warnings), "williams");

            Assert.IsNull(team.BaseLiveryDriver1);
            Assert.AreEqual(1, warnings.Count);
        }

        [TestMethod]
        public void Fallbacks_NoneResolves_LeavesTheGenericLivery()
        {
            var baked = Season(WithFallbacks(BakedTeam("williams", "formula_v10_g1"), new CosmeticFallback { Season = 1997 }));

            var team = Team(Merge(baked, null, new()), "williams");

            Assert.IsNull(team.BaseLiveryDriver1);
            Assert.IsNull(team.LiveryPreview);
            Assert.IsNull(team.NumbersPlacements);
            Assert.AreEqual("williams_1996", team.GenericLivery.Template);
        }

        [TestMethod]
        public void Fallbacks_NeverTouchGameplay()
        {
            var baked = Season(WithFallbacks(BakedTeam("williams", "formula_v10_g1"), new CosmeticFallback { Season = 1997 }));
            var source = PackTeam("williams", "formula_v10_g1");
            source.TeamName = "Willis Rellaux";
            source.Reputation = TeamReputation.MINNOW;
            source.Ams2CarPerformanceMalus = new Dictionary<string, double> { { "power_scalar", -2 } };
            source.Driver1Contract = new DriverContract { DriverId = "someone" };

            var team = Team(Merge(baked, null, new() { { 1997, Season(source) } }), "williams");

            Assert.AreEqual("../1997/car_liveries/Willis.png", team.BaseLiveryDriver1);
            Assert.AreEqual("Williams Renault", team.TeamName);
            Assert.AreEqual(TeamReputation.TOP_TEAM, team.Reputation);
            Assert.AreEqual(-1.03, team.Ams2CarPerformanceMalus["power_scalar"]);
            Assert.AreEqual("damon_hill", team.Driver1Contract.DriverId);
            Assert.AreEqual("formula_v10_g1", team.Ams2Car);
            Assert.AreEqual("williams_1996", team.GenericLivery.Template);
        }

        // ---- cosmetic fallbacks: drivers ----

        private static Dictionary<string, Ams2DriverData> Drivers(params Ams2DriverData[] drivers) => drivers.ToDictionary(d => d.DriverId);

        private static Dictionary<string, Ams2DriverData> MergeDrivers(Ams2DriverData baked, Ams2DriverData ownPack, Dictionary<int, Ams2DriverData> others) =>
            BakedSeasonMerge.MergeDrivers(
                Drivers(baked),
                ownPack == null ? null : Drivers(ownPack),
                1996,
                others.ToDictionary(kv => kv.Key, kv => Drivers(kv.Value)),
                SeasonsRoot);

        [TestMethod]
        public void DriverFallbacks_OwnPackSlotWins()
        {
            var merged = MergeDrivers(
                new Ams2DriverData { DriverId = "hill" },
                new Ams2DriverData { DriverId = "hill", BaseHelmetFile = "helmet_liveries/own.png" },
                new() { { 1995, new Ams2DriverData { DriverId = "hill", BaseHelmetFile = "helmet_liveries/other.png" } } });

            Assert.AreEqual("helmet_liveries/own.png", merged["hill"].BaseHelmetFile);
        }

        [TestMethod]
        public void DriverFallbacks_NearestYearWins_TheEarlierOnATie()
        {
            var merged = MergeDrivers(
                new Ams2DriverData { DriverId = "hill" },
                null,
                new()
                {
                    { 1990, new Ams2DriverData { DriverId = "hill", BaseHelmetFile = "h/1990.png" } },
                    { 1994, new Ams2DriverData { DriverId = "hill", BaseHelmetFile = "h/1994.png" } },
                    { 1998, new Ams2DriverData { DriverId = "hill", BaseHelmetFile = "h/1998.png" } }
                });

            Assert.AreEqual("../1994/h/1994.png", merged["hill"].BaseHelmetFile);
        }

        [TestMethod]
        public void DriverFallbacks_SlotsResolveIndependently_PathsRebased_PortraitAsItIs()
        {
            var merged = MergeDrivers(
                new Ams2DriverData { DriverId = "hill" },
                new Ams2DriverData { DriverId = "hill", PictureUrl = "Drivers/pictures/hill.png" },
                new()
                {
                    { 1998, new Ams2DriverData { DriverId = "hill", BaseHelmetFile90s = "helmet_liveries/hill_90s.png" } },
                    { 2022, new Ams2DriverData { DriverId = "hill", BaseHelmetFile = "../../Drivers/base_helmet_liveries/hill.png", BaseVisorFile = "helmet_liveries/hill_visor.png", PictureUrl = "https://example.org/hill.png" } }
                });

            var hill = merged["hill"];
            Assert.AreEqual("../1998/helmet_liveries/hill_90s.png", hill.BaseHelmetFile90s);
            Assert.AreEqual("../../Drivers/base_helmet_liveries/hill.png", hill.BaseHelmetFile);
            Assert.AreEqual("../2022/helmet_liveries/hill_visor.png", hill.BaseVisorFile);
            Assert.AreEqual("Drivers/pictures/hill.png", hill.PictureUrl);
            Assert.IsNull(hill.BaseHelmetFile80s);
        }

        [TestMethod]
        public void DriverFallbacks_PortraitFromAnotherYear_IsNotRebased()
        {
            var merged = MergeDrivers(
                new Ams2DriverData { DriverId = "hill" },
                null,
                new() { { 1997, new Ams2DriverData { DriverId = "hill", PictureUrl = "Drivers/pictures/hill.png" } } });

            Assert.AreEqual("Drivers/pictures/hill.png", merged["hill"].PictureUrl);
        }

        [TestMethod]
        public void DriverFallbacks_NoPackHasTheDriver_SlotsStayEmpty()
        {
            var merged = MergeDrivers(
                new Ams2DriverData { DriverId = "hill", GenericHelmet = new GenericHelmet { Design = "15_swoosh" } },
                null,
                new() { { 1997, new Ams2DriverData { DriverId = "villeneuve", BaseHelmetFile = "h/jv.png" } } });

            var hill = merged["hill"];
            Assert.IsNull(hill.BaseHelmetFile);
            Assert.IsNull(hill.BaseHelmetFile90s);
            Assert.IsNull(hill.PictureUrl);
            Assert.AreEqual("15_swoosh", hill.GenericHelmet.Design);
        }

        // ---- paths ----

        [DataTestMethod]
        [DataRow("car_liveries/rb.dds", 2022, "../2022/car_liveries/rb.dds")]
        [DataRow("car_liveries\\rb.dds", 2022, "../2022/car_liveries/rb.dds")]
        [DataRow("../../Drivers/base_helmet_liveries/x.png", 2022, "../../Drivers/base_helmet_liveries/x.png")]
        [DataRow("../1996/helmet_liveries/hill.png", 1997, "../1996/helmet_liveries/hill.png")]
        [DataRow("https://example.org/x.png", 2022, "https://example.org/x.png")]
        [DataRow(null, 2022, null)]
        public void RebaseFromSeason_ResolvesFromAnySeasonFolder(string path, int year, string expected)
        {
            Assert.AreEqual(expected, BakedSeasonMerge.RebaseFromSeason(path, year, SeasonsRoot));
        }

        [TestMethod]
        public void RebaseFromSeason_RootedPath_IsUnchanged()
        {
            var rooted = Path.Combine(Path.GetTempPath(), "x.png");
            Assert.AreEqual(rooted, BakedSeasonMerge.RebaseFromSeason(rooted, 2022, SeasonsRoot));
        }

        [TestMethod]
        public void PrefixTeamTexturePaths_OverARebasedPath_StillReachesTheSourceYear_AndLeavesTheXml()
        {
            var team = new Ams2TeamEntry
            {
                BaseLiveryDriver1 = BakedSeasonMerge.RebaseFromSeason("car_liveries/rb.dds", 2022, SeasonsRoot),
                LiveryXml = "<USER_OVERRIDES />"
            };

            AMS2ChEd.Business.AMS2.GameLogic.Ams2GameEngine.PrefixTeamTexturePaths(team, 2023);

            Assert.AreEqual(
                Path.GetFullPath(Path.Combine(SeasonsRoot, "2022", "car_liveries", "rb.dds")),
                Path.GetFullPath(Path.Combine(SeasonsRoot, "1990", team.BaseLiveryDriver1)));
            Assert.AreEqual("<USER_OVERRIDES />", team.LiveryXml);
            Assert.AreEqual(2022, BakedSeasonMerge.OtherSeasonYear(team.BaseLiveryDriver1));
        }

        [DataTestMethod]
        [DataRow("../2022/previews/x.dds", 2022)]
        [DataRow("../2023/../2022/previews/x.dds", 2022)]
        [DataRow("..\\2022\\previews\\x.dds", 2022)]
        [DataRow("previews/x.dds", null)]
        [DataRow("../../Drivers/x.png", null)]
        public void OtherSeasonYear_ReadsTheYearAPathLeadsInto(string path, int? expected)
        {
            Assert.AreEqual(expected, BakedSeasonMerge.OtherSeasonYear(path));
        }

        [TestMethod]
        public void BorrowedFileName_IsUniquePerSourceFile()
        {
            var stewart = BakedSeasonMerge.BorrowedFileName("../2025/external_liveries/previews/stewart/default.dds");
            var williams = BakedSeasonMerge.BorrowedFileName("../2023/../2025/external_liveries/previews/williams/default.dds");

            Assert.AreEqual("2025_external_liveries_previews_stewart_default.dds", stewart);
            Assert.AreEqual("2025_external_liveries_previews_williams_default.dds", williams);
            Assert.IsNull(BakedSeasonMerge.BorrowedFileName("previews/stewart/default.dds"));
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
            NumbersPlacements = new[] { new NumbersPlacement { NumberPlateWidth = 120, NumbersTexture = "numbers\\williams.png" } },
            HelmetSponsors = "helmet_sponsors\\williams.png",
            VisorSponsors = "helmet_sponsors\\williams_visor.png",
            DriversSpecificHelmet = new Dictionary<string, string> { { "damon_hill", "helmet_liveries\\hill_special.png" } },
            LiveryOverrides = new[] { new LiveryOverride { RaceId = 7 } },
            LiveryXml = "<USER_OVERRIDES />"
        };
    }
}
