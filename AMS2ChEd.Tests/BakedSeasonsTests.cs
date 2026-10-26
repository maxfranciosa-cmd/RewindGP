using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models.Concrete;
using System.Text.Json;

namespace AMS2ChEd.Tests.Ams2
{
    // The baked seasons that ship with the app load with the app's own models, carry no artwork,
    // and every generic field points at something the shipped kits, number styles, tyre sets and
    // helmet designs have.
    [TestClass]
    public class BakedSeasonsTests
    {
        private static readonly string[] HelmetEraPrefixes = { "", "90s_", "80s_", "70s_" };

        [TestMethod]
        [DataRow(1996)]
        [DataRow(1998)]
        [DataRow(1999)]
        [DataRow(2000)]
        [DataRow(2001)]
        [DataRow(2002)]
        [DataRow(2003)]
        [DataRow(2004)]
        [DataRow(2017)]
        [DataRow(2018)]
        [DataRow(2019)]
        [DataRow(2020)]
        [DataRow(2021)]
        [DataRow(2022)]
        [DataRow(2023)]
        [DataRow(2024)]
        [DataRow(2025)]
        public void ShippedBakedSeason_LoadsAndPointsAtShippedKits(int year)
        {
            string app = Path.Combine(RepoRoot(), "AMS2ChEd");
            string folder = Path.Combine(app, "BakedSeasons", year.ToString());
            var season = JsonSerializer.Deserialize<Ams2Season>(File.ReadAllText(Path.Combine(folder, "season.json")), DefaultJsonSerializerOptions.Instance);
            var drivers = JsonSerializer.Deserialize<DriverRatingsDatabase>(File.ReadAllText(Path.Combine(folder, "drivers.json")), DefaultJsonSerializerOptions.Instance)
                .Drivers.Cast<Ams2DriverData>().ToList();
            var kits = new LiveryKitLoader(Path.Combine(app, "LiveryKits"), Path.Combine(app, "Tyres", "tyre_sets.json"), Path.Combine(app, "Tyres", "files"));

            Assert.AreEqual(year, season.Year);
            foreach (var race in season.Races)
                Assert.IsNull(race.CoverPictureUrl, $"race {race.RaceId}: cover_picture");
            foreach (var team in season.Teams.Cast<Ams2TeamEntry>())
            {
                Assert.IsNull(team.BaseLiveryDriver1, team.TeamId);
                Assert.IsNull(team.LiveryPreview, team.TeamId);
                Assert.IsNull(team.NumbersPlacements, team.TeamId);
                Assert.IsNull(team.HelmetSponsors, team.TeamId);
                Assert.IsNull(team.LiveryXml, team.TeamId);

                var livery = team.GenericLivery;
                Assert.IsNotNull(livery, $"{team.TeamId}: no generic_livery");
                var kit = kits.FindKit(team.Ams2Car);
                Assert.IsNotNull(kit, $"{team.TeamId}: no kit for {team.Ams2Car}");
                Assert.IsNotNull(kit.FindTemplate(livery.Template), $"{team.TeamId}: template {livery.Template}");
                TemplateRecolour.ParsePalette(livery.Palette).Keys.ToList().ForEach(k => Assert.IsTrue(TemplateRecolour.KeyColours.ContainsKey(k), $"{team.TeamId}: key {k}"));
                Assert.IsNotNull(kits.FindNumberStyle(livery.NumberStyle), $"{team.TeamId}: number style {livery.NumberStyle}");
                TemplateRecolour.ParseColour(livery.NumberFill);
                if (kits.FindNumberStyle(livery.NumberStyle).TwoColour)
                    TemplateRecolour.ParseColour(livery.NumberOutline);
                // tyres and suit are optional: without them the car keeps its stock tyres and the
                // suit takes the car palette (2025: stock Pirelli-style tyres, no outfit set)
                if (livery.Tyres != null)
                    Assert.IsNotNull(kits.FindTyreSet(livery.Tyres), $"{team.TeamId}: tyres {livery.Tyres}");
                if (livery.Suit != null)
                {
                    TemplateRecolour.ParsePalette(livery.Suit.Palette);
                    TemplateRecolour.ParseColour(livery.Suit.Harness);
                }

                // a borrowed livery runs on the kit's livery.xml
                if (team.CosmeticFallbacks != null)
                {
                    foreach (var fallback in team.CosmeticFallbacks)
                        Assert.AreNotEqual(year, fallback.Season, $"{team.TeamId}: cosmetic fallback to its own season");
                    Assert.IsTrue(!string.IsNullOrEmpty(kit.LiveryXml) && File.Exists(Path.Combine(kit.Folder, kit.LiveryXml)), $"{team.TeamId}: cosmetic fallbacks, but the {team.Ams2Car} kit has no livery.xml");
                }
            }

            string helmets = Path.Combine(app, "Drivers", "base_helmet_liveries", "generic_designs");
            foreach (var driver in drivers)
            {
                Assert.IsNull(driver.BaseHelmetFile, driver.DriverId);
                Assert.IsNull(driver.PictureUrl, driver.DriverId);
                // from 2014 every driver has his own number
                if (year >= 2014)
                    Assert.IsTrue(driver.FavouriteNumbers?.Any(n => n > 1) == true, $"{driver.DriverId}: no favourite number");
                var helmet = driver.GenericHelmet;
                Assert.IsNotNull(helmet, $"{driver.DriverId}: no generic_helmet");
                foreach (var prefix in HelmetEraPrefixes)
                    Assert.IsTrue(File.Exists(Path.Combine(helmets, prefix + helmet.Design + ".png")), $"{driver.DriverId}: {prefix}{helmet.Design}");
                TemplateRecolour.ParsePalette(helmet.Palette).Keys.ToList().ForEach(k => Assert.IsTrue(TemplateRecolour.KeyColours.ContainsKey(k), $"{driver.DriverId}: key {k}"));
            }
        }

        private static string RepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AMS2ChEd", "AMS2ChEd.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("repository root not found");
        }
    }
}
