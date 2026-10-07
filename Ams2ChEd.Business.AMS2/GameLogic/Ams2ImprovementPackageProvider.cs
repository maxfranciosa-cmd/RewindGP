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
        // each scalar moves by up to this much, in either direction
        public const double MAX_VARIATION = 0.05;

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
                s => Math.Round((random.NextDouble() * 2 - 1) * MAX_VARIATION, 3));
        }
    }
}
