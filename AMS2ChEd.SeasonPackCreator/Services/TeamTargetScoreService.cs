using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AMS2ChEd.SeasonPackEditor.Services
{
    /// <summary>
    /// Matches season teams against real-world Jolpica constructor standings and turns the match
    /// into a 0-1 target competitiveness score. Shared by the one-shot "generate performance from
    /// actual results" flow and the in-sim calibration loop, which both need the same target score
    /// per team but use it differently (one converts it straight to scalars, the other compares it
    /// against an observed in-game score).
    /// </summary>
    public static class TeamTargetScoreService
    {
        public class TargetScoreResult
        {
            public bool Matched { get; set; }
            public double Score { get; set; }
            public double Points { get; set; }
            public int Position { get; set; }
        }

        public static Dictionary<string, TargetScoreResult> ComputeTargetScores(
            List<Ams2TeamEntry> teams,
            List<JolpicaConstructorStanding> constructorStandings)
        {
            var result = new Dictionary<string, TargetScoreResult>();

            if (!constructorStandings.Any())
                return result;

            double leaderPoints = constructorStandings.Max(s => ParseDouble(s.Points));
            int fieldSize = constructorStandings.Count;

            foreach (var team in teams)
            {
                var standing = constructorStandings.FirstOrDefault(s =>
                    string.Equals(s.Constructor?.Name?.Trim(), team.TeamName?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (standing == null)
                {
                    // Local team names often include the engine/sponsor (e.g. "Jordan Hart") while
                    // Jolpica's constructor name is just "Jordan" - fall back to matching on that.
                    var firstWord = team.TeamName?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrEmpty(firstWord))
                    {
                        standing = constructorStandings.FirstOrDefault(s =>
                            s.Constructor?.Name?.Contains(firstWord, StringComparison.OrdinalIgnoreCase) == true);
                    }
                }

                if (standing == null)
                {
                    result[team.TeamId] = new TargetScoreResult { Matched = false };
                    continue;
                }

                double points = ParseDouble(standing.Points);
                int position = ParseInt(standing.Position);
                double score = DriverPerformanceGenerator.ComputeCompetitivenessScore(points, leaderPoints, position, fieldSize);

                result[team.TeamId] = new TargetScoreResult
                {
                    Matched = true,
                    Score = score,
                    Points = points,
                    Position = position
                };
            }

            return result;
        }

        private static double ParseDouble(string value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 0.0;

        private static int ParseInt(string value) =>
            int.TryParse(value, out int result) ? result : int.MaxValue;
    }
}
