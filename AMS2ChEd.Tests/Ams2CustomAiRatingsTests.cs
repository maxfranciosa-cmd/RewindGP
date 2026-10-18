using Ams2ChEd.Business.AMS2.Helpers;
using Ams2ChEd.Business.AMS2.Services;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models.Concrete;
using System.Globalization;
using System.IO;
using System.Xml.Linq;

namespace AMS2ChEd.Tests.Ams2
{
    [TestClass]
    public class Ams2CustomAiRatingsTests
    {
        private const string AMS2_CLASS = "F-Classic_Gen3";
        private const double TOLERANCE = 0.0001;

        [TestMethod]
        public void Clamp_KeepsScalarsWithinTheRangeAms2Accepts()
        {
            Assert.AreEqual(1.1, Ams2AiRatingLimits.Clamp("power_scalar", 1.15), TOLERANCE);
            Assert.AreEqual(0.9, Ams2AiRatingLimits.Clamp("weight_scalar", 0.8), TOLERANCE);
            Assert.AreEqual(1.03, Ams2AiRatingLimits.Clamp("drag_scalar", 1.03), TOLERANCE);
        }

        [TestMethod]
        public void Clamp_KeepsSkillsWithinZeroAndOne()
        {
            Assert.AreEqual(1.0, Ams2AiRatingLimits.Clamp("race_skill", 1.04), TOLERANCE);
            Assert.AreEqual(0.0, Ams2AiRatingLimits.Clamp("consistency", -0.02), TOLERANCE);
            Assert.AreEqual(0.5, Ams2AiRatingLimits.Clamp("wet_skill", 0.5), TOLERANCE);
        }

        [TestMethod]
        public void Clamp_LeavesReliabilityAsIs()
        {
            // a ratio AMS2 accepts outside 0-1 too
            Assert.AreEqual(-0.3, Ams2AiRatingLimits.Clamp("vehicle_reliability", -0.3), TOLERANCE);
            Assert.AreEqual(1.2, Ams2AiRatingLimits.Clamp("vehicle_reliability", 1.2), TOLERANCE);
        }

        [TestMethod]
        public void CustomAi_WithVariation_TeammatesShareTheSameCar()
        {
            var drivers = GenerateDrivers(aiRatingsVariation: true);
            var driverA = drivers["#5 Team - Driver A"];
            var driverB = drivers["#6 Team - Driver B"];

            // drag_scalar is left unset in the team's malus, so neither car carries it
            foreach (var key in Ams2AiRatingLimits.CarKeys.Where(driverA.ContainsKey))
                Assert.AreEqual(driverA[key], driverB[key], TOLERANCE, key);
            Assert.AreEqual(3, Ams2AiRatingLimits.CarKeys.Count(driverA.ContainsKey));
        }

        [TestMethod]
        public void CustomAi_WithVariation_CarValuesAreTheTeamsOwn()
        {
            var driver = GenerateDrivers(aiRatingsVariation: true)["#5 Team - Driver A"];

            Assert.AreEqual(1.02, driver["weight_scalar"], TOLERANCE);
            // the team's value, not the driver's own 0.99
            Assert.AreEqual(0.4, driver["vehicle_reliability"], TOLERANCE);
        }

        [TestMethod]
        public void CustomAi_WithVariation_StillVariesDriverSkillsWithinRange()
        {
            var raceSkills = Enumerable.Range(0, 20)
                .Select(_ => GenerateDrivers(aiRatingsVariation: true)["#5 Team - Driver A"]["race_skill"])
                .ToList();

            Assert.IsTrue(raceSkills.Distinct().Count() > 1);
            Assert.IsTrue(raceSkills.All(s => s >= 0.95 - TOLERANCE && s <= 1.0));
        }

        [TestMethod]
        public void CustomAi_ScalarOutOfRange_IsClampedNotDropped()
        {
            var driver = GenerateDrivers(aiRatingsVariation: false)["#5 Team - Driver A"];

            Assert.AreEqual(1.1, driver["power_scalar"], TOLERANCE);
        }

        [TestMethod]
        public void CustomAi_UnsetScalar_IsNotExported()
        {
            var driver = GenerateDrivers(aiRatingsVariation: false)["#5 Team - Driver A"];

            // drag_scalar is 0 in the team's malus, the team editor's default for "not set"
            Assert.IsFalse(driver.ContainsKey("drag_scalar"));
        }

        /// <summary>
        /// Writes the Custom AI file of a two-car team, and reads it back as livery name -> ratings.
        /// </summary>
        private static Dictionary<string, Dictionary<string, double>> GenerateDrivers(bool aiRatingsVariation)
        {
            var team = new Ams2TeamEntry
            {
                TeamId = "team",
                TeamName = "Team",
                // the scalars are stored as a malus, i.e. with their sign flipped
                Ams2CarPerformanceMalus = new Dictionary<string, double>
                {
                    { "power_scalar", -1.15 },
                    { "weight_scalar", -1.02 },
                    { "drag_scalar", 0.0 },
                    { "vehicle_reliability", -0.4 }
                }
            };
            var drivers = new[] { CreateDriver("a", "Driver A"), CreateDriver("b", "Driver B") };
            var entries = new List<EntryListEntry>
            {
                new EntryListEntry { TeamId = "team", Driver1Id = "a", Driver1Number = 5, Driver2Id = "b", Driver2Number = 6 }
            };

            var root = Path.Combine(Path.GetTempPath(), "Ams2CustomAiRatingsTests", Guid.NewGuid().ToString("N"));
            try
            {
                new Ams2LiveryService(1990, AMS2_CLASS, drivers, new[] { team }, aiRatingsVariation: aiRatingsVariation)
                    .GenerateCustomAiOnly(1, entries, root);

                return XDocument.Load(Path.Combine(root, "UserData", "CustomAIDrivers", $"{AMS2_CLASS}.xml"))
                    .Root.Elements("driver")
                    .ToDictionary(
                        d => d.Attribute("livery_name").Value,
                        d => d.Elements()
                            .Where(e => e.Name != "name" && e.Name != "country")
                            .ToDictionary(e => e.Name.LocalName, e => double.Parse(e.Value, CultureInfo.InvariantCulture)));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static Ams2DriverData CreateDriver(string driverId, string name)
        {
            return new Ams2DriverData
            {
                DriverId = driverId,
                Name = name,
                Nationality = "GBR",
                RatingValues = new Dictionary<string, double>
                {
                    { "race_skill", 1.0 },
                    { "consistency", 0.4 },
                    { "vehicle_reliability", 0.99 }
                }
            };
        }
    }
}
