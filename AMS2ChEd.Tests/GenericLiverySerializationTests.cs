using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models.Concrete;
using System.Text.Json;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class GenericLiverySerializationTests
    {
        private const string TeamType = "\"$type\": \"AMS2ChEd.Business.AMS2.Models.Ams2TeamEntry\"";

        [TestMethod]
        public void Season_WithoutGenericLivery_LoadsAndWritesNoField()
        {
            var season = Deserialize<Ams2Season>("{ \"Teams\": [ { " + TeamType + ", \"ams2car\": \"formula_v10_g1\" } ] }");

            var team = (Ams2TeamEntry)season.Teams.Single();
            Assert.IsNull(team.GenericLivery);
            Assert.IsFalse(Serialize(season).Contains("generic_livery"));
        }

        [TestMethod]
        public void Season_WithGenericLivery_RoundTrips()
        {
            var season = Deserialize<Ams2Season>("{ \"Teams\": [ { " + TeamType + @",
                ""generic_livery"": {
                  ""template"": ""v10_t3"",
                  ""palette"": { ""red"": ""#0A2A6B"", ""green"": ""#FFFFFF"" },
                  ""number_style"": ""block"",
                  ""number_fill"": ""#FFFFFF"",
                  ""number_outline"": ""#000000"",
                  ""tyres"": ""goodyear_f93"",
                  ""suit"": { ""palette"": { ""red"": ""#FFFFFF"" }, ""harness"": ""#E30613"" }
                } } ] }");

            var livery = ((Ams2TeamEntry)season.DeepClone().Teams.Single()).GenericLivery;

            Assert.AreEqual("v10_t3", livery.Template);
            Assert.AreEqual("#0A2A6B", livery.Palette["red"]);
            Assert.AreEqual("#FFFFFF", livery.Palette["green"]);
            Assert.AreEqual("block", livery.NumberStyle);
            Assert.AreEqual("#FFFFFF", livery.NumberFill);
            Assert.AreEqual("#000000", livery.NumberOutline);
            Assert.AreEqual("goodyear_f93", livery.Tyres);
            Assert.AreEqual("#FFFFFF", livery.Suit.Palette["red"]);
            Assert.AreEqual("#E30613", livery.Suit.Harness);
        }

        [TestMethod]
        public void Driver_WithoutGenericHelmet_LoadsAndWritesNoField()
        {
            var driver = Deserialize<Ams2DriverData>("{ \"base_helmet_file_90s\": \"helmet.png\" }");

            Assert.IsNull(driver.GenericHelmet);
            Assert.AreEqual("helmet.png", driver.BaseHelmetFile90s);
            Assert.IsFalse(Serialize(driver).Contains("generic_helmet"));
        }

        [TestMethod]
        public void Driver_WithGenericHelmet_RoundTrips()
        {
            var driver = Deserialize<Ams2DriverData>(
                "{ \"generic_helmet\": { \"design\": \"06_stripes\", \"palette\": { \"red\": \"#FFD700\", \"green\": \"#003399\" } } }");

            var helmet = driver.DeepClone().GenericHelmet;

            Assert.AreEqual("06_stripes", helmet.Design);
            Assert.AreEqual("#FFD700", helmet.Palette["red"]);
            Assert.AreEqual("#003399", helmet.Palette["green"]);
        }

        private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, DefaultJsonSerializerOptions.Instance);

        private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, DefaultJsonSerializerOptions.Instance);
    }
}
