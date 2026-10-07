using Ams2ChEd.Business.AMS2.Resources;
using AMS2ChEd.Business.GameLogic.Contracts;

namespace AMS2ChEd.Business.AMS2.GameLogic
{
    /// <summary>
    /// In AMS2 an improvement package varies the three car-performance scalars of the custom AI
    /// file. The values are applied on export (see Ams2LiveryService).
    /// </summary>
    public class Ams2ImprovementPackageProvider : IImprovementPackageProvider
    {
        // each scalar can improve by more than it can get worse, so a package is usually a net
        // gain while still carrying a risk
        public const double MAX_GAIN = 0.05;
        public const double MAX_LOSS = 0.02;

        public IReadOnlyList<ImprovementStat> Stats => new[]
        {
            new ImprovementStat("power_scalar", Strings.Ams2ImprovementPackage_Power_Name, true,
                Strings.Ams2ImprovementPackage_Power_Improved, Strings.Ams2ImprovementPackage_Power_Worsened),
            new ImprovementStat("drag_scalar", Strings.Ams2ImprovementPackage_Drag_Name, false,
                Strings.Ams2ImprovementPackage_Drag_Improved, Strings.Ams2ImprovementPackage_Drag_Worsened),
            new ImprovementStat("weight_scalar", Strings.Ams2ImprovementPackage_Weight_Name, false,
                Strings.Ams2ImprovementPackage_Weight_Improved, Strings.Ams2ImprovementPackage_Weight_Worsened)
        };

        public Dictionary<string, double> GenerateValues(Random random)
        {
            return Stats.ToDictionary(
                s => s.Key,
                s =>
                {
                    // positive = better for the car, whichever way that scalar has to move
                    double benefit = -MAX_LOSS + random.NextDouble() * (MAX_GAIN + MAX_LOSS);
                    return Math.Round(s.HigherIsBetter ? benefit : -benefit, 3);
                });
        }
    }
}
