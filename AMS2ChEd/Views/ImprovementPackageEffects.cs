using AMS2ChEd.Business.GameLogic.Contracts;
using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Resources;
using System.Collections.Generic;
using System.Linq;

namespace AMS2ChEd.Views
{
    /// <summary>
    /// What an improvement package did to the car, in words: the stats it improved and the ones it
    /// made worse (shared by the technical news and the team principal's letter after an install).
    /// </summary>
    public static class ImprovementPackageEffects
    {
        public sealed record Effects(IReadOnlyList<string> Gains, IReadOnlyList<string> Losses);

        public static Effects Describe(ImprovementPackage package, IReadOnlyList<ImprovementStat> stats)
        {
            var gains = new List<string>();
            var losses = new List<string>();

            foreach (var stat in stats ?? new List<ImprovementStat>())
            {
                if (package?.Values == null || !package.Values.TryGetValue(stat.Key, out var value) || value == 0)
                    continue;

                if ((value > 0) == stat.HigherIsBetter)
                    gains.Add(stat.ImprovedText);
                else
                    losses.Add(stat.WorsenedText);
            }

            return new Effects(gains, losses);
        }

        // "a, b and c"
        public static string JoinList(IReadOnlyList<string> parts)
        {
            if (parts.Count == 1)
                return parts[0];

            return string.Join(", ", parts.Take(parts.Count - 1)) + Strings.ImprovementPackageNewsWindow_ListAnd + parts.Last();
        }
    }
}
