using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class SeasonPackExportFilesTests
    {
        [TestMethod]
        public void OverrideTextureTag_NoOverrideForThatRace_IsEmpty()
        {
            var overrides = new List<LiveryOverride> { Override(9, "car_liveries\\Willis_NT.png") };

            Assert.AreEqual("", SeasonPackExportFiles.OverrideTextureTag(overrides, 3));
            Assert.AreEqual("", SeasonPackExportFiles.OverrideTextureTag(null, 3));
        }

        [TestMethod]
        public void OverrideTextureTag_SameOverrideOverSeveralRaces_SharesTheFirstRacesTag()
        {
            var overrides = new List<LiveryOverride>
            {
                Override(9, "car_liveries\\Willis_NT.png"),
                Override(10, "car_liveries\\Willis_NT.png"),
                Override(11, "car_liveries\\Willis_NT.png")
            };

            Assert.AreEqual("_r9", SeasonPackExportFiles.OverrideTextureTag(overrides, 9));
            Assert.AreEqual("_r9", SeasonPackExportFiles.OverrideTextureTag(overrides, 11));
        }

        [TestMethod]
        public void OverrideTextureTag_DifferentOverrides_GetDifferentTags()
        {
            var overrides = new List<LiveryOverride>
            {
                Override(4, "car_liveries\\Special_A.png"),
                Override(12, "car_liveries\\Special_B.png")
            };

            Assert.AreEqual("_r4", SeasonPackExportFiles.OverrideTextureTag(overrides, 4));
            Assert.AreEqual("_r12", SeasonPackExportFiles.OverrideTextureTag(overrides, 12));
        }

        [TestMethod]
        public void OverrideTextureTag_SameLiveryDifferentHelmetSponsors_AreDifferentOverrides()
        {
            var first = Override(4, "car_liveries\\Special.png");
            var second = Override(5, "car_liveries\\Special.png");
            second.HelmetSponsors = "helmet_sponsors\\Other.png";

            var overrides = new List<LiveryOverride> { first, second };

            Assert.AreEqual("_r5", SeasonPackExportFiles.OverrideTextureTag(overrides, 5));
        }

        [TestMethod]
        [DataRow(1, "Brazilian Grand Prix", "01 - Brazilian Grand Prix")]
        [DataRow(12, "Grande Prêmio do Brasil", "12 - Grande Premio do Brasil")]
        [DataRow(3, "Monaco GP (Night!) & more", "03 - Monaco GP Night more")]
        [DataRow(4, "Rd. 4: 50% distance", "04 - Rd. 4 50 distance")]
        [DataRow(5, "", "05 - Race")]
        public void RaceFolderName_RoundFirstAndBatchSafe(int round, string raceName, string expected)
        {
            Assert.AreEqual(expected, SeasonPackExportFiles.RaceFolderName(round, raceName));
        }

        [TestMethod]
        public void BuildSelectorScript_PointsAtTheSetupsFolderWithWindowsLineEndings()
        {
            var script = SeasonPackExportFiles.BuildSelectorScript(1996, "RewindGP_1996_RaceSetups");

            StringAssert.StartsWith(script, "@echo off\r\n");
            StringAssert.Contains(script, "set \"SETUPS=RewindGP_1996_RaceSetups\"");
            StringAssert.Contains(script, "xcopy \"%SETUPS%\\!CHOSEN!\\*\" \".\" /E /Y /I /Q");
            Assert.IsFalse(script.Replace("\r\n", "").Contains('\n'), "every line must end in CRLF");
            Assert.IsTrue(script.All(c => c < 128), "the script must be plain ASCII");
        }

        private static LiveryOverride Override(int raceId, string livery) =>
            new LiveryOverride { RaceId = raceId, Driver1Livery = livery, Driver2Livery = livery };
    }
}
