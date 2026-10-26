using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Storage.Contracts;
using System.Text.Json;

namespace AMS2ChEd.Tests.Business
{
    [TestClass]
    public class ScenarioTests
    {
        private static Scenario Load(string json) => JsonSerializer.Deserialize<Scenario>(json, DefaultJsonSerializerOptions.Instance);

        [TestMethod]
        public void Translation_IsUsedForItsLanguage_EnglishOtherwise()
        {
            var scenario = Load(@"{ ""name"": ""The Lion's Last Roar"", ""description"": ""One last dance."", ""game_file"": ""x.json"",
                ""translations"": { ""it"": { ""name"": ""L'ultimo ruggito del Leone"", ""description"": ""Un ultimo ballo."" } } }");

            Assert.AreEqual("L'ultimo ruggito del Leone", scenario.LocalizedName("it"));
            Assert.AreEqual("Un ultimo ballo.", scenario.LocalizedDescription("IT"));
            Assert.AreEqual("The Lion's Last Roar", scenario.LocalizedName("en"));
            Assert.AreEqual("One last dance.", scenario.LocalizedDescription("de"));
        }

        [TestMethod]
        public void MissingOrEmptyTranslatedField_FallsBackToEnglish()
        {
            var scenario = Load(@"{ ""name"": ""Senna sempre"", ""description"": ""Lap 2, San Marino."",
                ""translations"": { ""it"": { ""name"": ""Senna per sempre"", ""description"": """" } } }");

            Assert.AreEqual("Senna per sempre", scenario.LocalizedName("it"));
            Assert.AreEqual("Lap 2, San Marino.", scenario.LocalizedDescription("it"));
        }

        [TestMethod]
        public void OlderScenario_WithoutTranslations_LoadsAndWritesAsBefore()
        {
            var scenario = Load(@"{ ""name"": ""Senna sempre"", ""description"": ""Lap 2."", ""picture"": """", ""game_file"": ""x.json"" }");

            Assert.AreEqual("Senna sempre", scenario.LocalizedName("it"));
            Assert.IsFalse(JsonSerializer.Serialize(scenario, DefaultJsonSerializerOptions.Instance).Contains("translations"));
        }

        [TestMethod]
        public void ScenarioFolders_AreTheYearFoldersThatHaveOne()
        {
            var root = Path.Combine(Path.GetTempPath(), "ScenarioTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "1996", "scenarios"));
                Directory.CreateDirectory(Path.Combine(root, "1997"));
                Directory.CreateDirectory(Path.Combine(root, "tyres", "scenarios"));

                var folders = ISeasonLoader.ScenarioFolders(root).ToList();

                Assert.AreEqual(1, folders.Count);
                Assert.AreEqual(1996, folders[0].Year);
                Assert.AreEqual(Path.Combine(root, "1996", "scenarios"), folders[0].Folder);
                Assert.AreEqual(0, ISeasonLoader.ScenarioFolders(Path.Combine(root, "missing")).Count());
            }
            finally
            {
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }
    }
}
