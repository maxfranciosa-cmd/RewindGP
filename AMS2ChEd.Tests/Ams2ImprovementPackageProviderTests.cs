using AMS2ChEd.Business.AMS2.GameLogic;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AMS2ChEd.Tests.Business.GameLogic
{
    [TestClass]
    public class Ams2ImprovementPackageProviderTests
    {
        private const double TOLERANCE = 0.0001;
        private const int DRAWS = 3000;

        [TestMethod]
        public void GetSpread_IsTheGapBetweenTheBestAndTheWorstCar()
        {
            var season = CreateSeason();

            Assert.AreEqual(0.05, Ams2ImprovementPackageProvider.GetSpread(season, "power_scalar"), TOLERANCE);
        }

        [TestMethod]
        public void GetSpread_CountsTheSecondCarsOwnValues()
        {
            var season = CreateSeason();
            ((Ams2TeamEntry)season.Teams.First()).Ams2CarPerformanceMalusDriver2 = new Dictionary<string, double> { { "power_scalar", -0.92 } };

            Assert.AreEqual(0.10, Ams2ImprovementPackageProvider.GetSpread(season, "power_scalar"), TOLERANCE);
        }

        [TestMethod]
        public void GetSpread_LevelField_FallsBackToTheMinimum()
        {
            var season = CreateSeason();

            // every team runs the same drag; nobody sets a weight at all
            Assert.AreEqual(Ams2ImprovementPackageProvider.MIN_SPREAD, Ams2ImprovementPackageProvider.GetSpread(season, "drag_scalar"), TOLERANCE);
            Assert.AreEqual(Ams2ImprovementPackageProvider.MIN_SPREAD, Ams2ImprovementPackageProvider.GetSpread(season, "weight_scalar"), TOLERANCE);
        }

        [TestMethod]
        public void GenerateValues_TouchesOneOrTwoScalars()
        {
            var provider = new Ams2ImprovementPackageProvider();
            var season = CreateSeason();
            var random = new Random(1996);

            var counts = Enumerable.Range(0, DRAWS).Select(_ => provider.GenerateValues(season, random).Count).ToList();

            Assert.IsTrue(counts.All(c => c == 1 || c == 2));
            Assert.IsTrue(counts.Contains(1));
            Assert.IsTrue(counts.Contains(2));
        }

        [TestMethod]
        public void GenerateValues_EveryScalarCanBeTouchedAndCanGainOrLose()
        {
            var provider = new Ams2ImprovementPackageProvider();
            var season = CreateSeason();
            var random = new Random(1996);

            var packages = Enumerable.Range(0, DRAWS).Select(_ => provider.GenerateValues(season, random)).ToList();

            foreach (var stat in provider.Stats)
            {
                var benefits = packages
                    .Where(p => p.ContainsKey(stat.Key))
                    .Select(p => stat.HigherIsBetter ? p[stat.Key] : -p[stat.Key])
                    .ToList();

                Assert.IsTrue(benefits.Any(b => b > 0), stat.Key);
                Assert.IsTrue(benefits.Any(b => b < 0), stat.Key);
                Assert.IsFalse(benefits.Any(b => b == 0), stat.Key);
            }
        }

        [TestMethod]
        public void GenerateValues_StaysWithinItsShareOfTheSeasonsSpread()
        {
            var provider = new Ams2ImprovementPackageProvider();
            var season = CreateSeason();
            var random = new Random(1996);

            var packages = Enumerable.Range(0, DRAWS).Select(_ => provider.GenerateValues(season, random)).ToList();

            foreach (var stat in provider.Stats)
            {
                var spread = Ams2ImprovementPackageProvider.GetSpread(season, stat.Key);
                var benefits = packages
                    .Where(p => p.ContainsKey(stat.Key))
                    .Select(p => stat.HigherIsBetter ? p[stat.Key] : -p[stat.Key])
                    .ToList();

                // half a thousandth of slack for the rounding to three decimals
                Assert.IsTrue(benefits.Max() <= Ams2ImprovementPackageProvider.MAX_GAIN_SHARE_OF_SPREAD * spread + 0.0005, stat.Key);
                Assert.IsTrue(benefits.Min() >= -Ams2ImprovementPackageProvider.MAX_LOSS_SHARE_OF_SPREAD * spread - 0.0005, stat.Key);
            }
        }

        // power ranges from 0.97 to 1.02, drag is level, and no team sets a weight
        private static ISeason CreateSeason()
        {
            return new Ams2Season
            {
                Year = 1996,
                Teams = new List<ITeamEntry>
                {
                    CreateTeam("T1", power: 1.02),
                    CreateTeam("T2", power: 1.00),
                    CreateTeam("T3", power: 0.97),
                }
            };
        }

        private static Ams2TeamEntry CreateTeam(string teamId, double power)
        {
            return new Ams2TeamEntry
            {
                TeamId = teamId,
                // the scalars are stored as a malus, i.e. with their sign flipped
                Ams2CarPerformanceMalus = new Dictionary<string, double> { { "power_scalar", -power }, { "drag_scalar", -1.0 } }
            };
        }
    }
}
