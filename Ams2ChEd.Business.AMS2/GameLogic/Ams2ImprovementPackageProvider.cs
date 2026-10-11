using Ams2ChEd.Business.AMS2.Resources;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models;

namespace AMS2ChEd.Business.AMS2.GameLogic
{
    /// <summary>
    /// In AMS2 an improvement package varies one or two of the three car-performance scalars of
    /// the custom AI file. The values are applied on export (see Ams2LiveryService).
    /// </summary>
    public class Ams2ImprovementPackageProvider : IImprovementPackageProvider
    {
        // A variation is sized against the gap between the season's best and worst car on that
        // scalar, so a package weighs the same in a close field as in a spread-out one. A scalar
        // can improve by more than it can get worse, so a package is usually a net gain while
        // still carrying a risk.
        public const double MAX_GAIN_SHARE_OF_SPREAD = 0.24;
        public const double MAX_LOSS_SHARE_OF_SPREAD = 0.12;
        // used when the field is (nearly) level on a scalar, e.g. a season where no team alters drag
        public const double MIN_SPREAD = 0.02;
        public const int MAX_STATS_PER_PACKAGE = 2;
        // scalars are exported with three decimals: a touched scalar always moves by at least this
        private const double SMALLEST_VARIATION = 0.001;

        public IReadOnlyList<ImprovementStat> Stats => new[]
        {
            new ImprovementStat("power_scalar", Strings.Ams2ImprovementPackage_Power_Name, true,
                Strings.Ams2ImprovementPackage_Power_Improved, Strings.Ams2ImprovementPackage_Power_Worsened),
            new ImprovementStat("drag_scalar", Strings.Ams2ImprovementPackage_Drag_Name, false,
                Strings.Ams2ImprovementPackage_Drag_Improved, Strings.Ams2ImprovementPackage_Drag_Worsened),
            new ImprovementStat("weight_scalar", Strings.Ams2ImprovementPackage_Weight_Name, false,
                Strings.Ams2ImprovementPackage_Weight_Improved, Strings.Ams2ImprovementPackage_Weight_Worsened)
        };

        public Dictionary<string, double> GenerateValues(ISeason season, Random random)
        {
            // one or two scalars, picked at random
            var touchedStats = Stats.ToList();
            int statsCount = 1 + random.Next(MAX_STATS_PER_PACKAGE);
            while (touchedStats.Count > statsCount)
                touchedStats.RemoveAt(random.Next(touchedStats.Count));

            return touchedStats.ToDictionary(
                s => s.Key,
                s =>
                {
                    // positive = better for the car, whichever way that scalar has to move
                    double share = -MAX_LOSS_SHARE_OF_SPREAD + random.NextDouble() * (MAX_GAIN_SHARE_OF_SPREAD + MAX_LOSS_SHARE_OF_SPREAD);
                    double benefit = Math.Round(share * GetSpread(season, s.Key), 3);
                    if (benefit == 0)
                        benefit = share < 0 ? -SMALLEST_VARIATION : SMALLEST_VARIATION;

                    return s.HigherIsBetter ? benefit : -benefit;
                });
        }

        /// <summary>
        /// The gap between the highest and the lowest value of a scalar among the season's cars
        /// (both cars of each team, as the second one can have its own values).
        /// </summary>
        public static double GetSpread(ISeason season, string scalarKey)
        {
            var values = (season?.Teams ?? Enumerable.Empty<ITeamEntry>())
                .OfType<Ams2TeamEntry>()
                .SelectMany(t => new[] { t.GetAms2CarPerformanceMalus(1), t.GetAms2CarPerformanceMalus(2) })
                // the scalars are stored as a malus, i.e. with their sign flipped; a car that doesn't
                // set one runs the neutral 1.0
                .Select(malus => malus != null && malus.TryGetValue(scalarKey, out var value) ? -value : 1.0)
                .ToList();

            return values.Any() ? Math.Max(MIN_SPREAD, values.Max() - values.Min()) : MIN_SPREAD;
        }
    }
}
