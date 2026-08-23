using AMS2ChEd.Business.Models.Concrete;
using System;
using System.Collections.Generic;

namespace AMS2ChEd.SeasonPackEditor.Services
{
    public class DriverPerformanceGenerator
    {
        private static readonly Random _random = new Random();

        private class ReputationRange
        {
            public double Min { get; set; }
            public double Max { get; set; }
            public double Variance { get; set; } = 0.03; // ±3% variance
        }

        // Map reputation to base performance ranges (0.0 to 1.0 scale)
        private static readonly Dictionary<DriverReputation, ReputationRange> _baseRanges = new()
        {
            // Pay Drivers - Still talented, just least experienced/skilled (0.800-0.830)
            [DriverReputation.PAY_DRIVER_WILD_CARD] = new() { Min = 0.800, Max = 0.820 },
            [DriverReputation.PAY_DRIVER_SEASON] = new() { Min = 0.820, Max = 0.840 },

            // Midfield - Solid F1 drivers (0.840-0.880)
            [DriverReputation.YOUNG_TALENT] = new() { Min = 0.840, Max = 0.860 },
            [DriverReputation.PRIME_MIDFIELD] = new() { Min = 0.860, Max = 0.880 },
            [DriverReputation.AGEING_MIDFIELD] = new() { Min = 0.850, Max = 0.870 },

            // Strong Midfield - Race winners on their day (0.880-0.920)
            [DriverReputation.PRIME_STRONG_MIDFIELD] = new() { Min = 0.900, Max = 0.920 },
            [DriverReputation.AGEING_STRONG_MIDFIELD] = new() { Min = 0.890, Max = 0.910 },
            [DriverReputation.JUST_ONE_LAST_DANCE] = new() { Min = 0.885, Max = 0.905 },

            // Championship Level - Washed (0.920-0.950)
            [DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED] = new() { Min = 0.920, Max = 0.935 },
            [DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED] = new() { Min = 0.930, Max = 0.945 },

            // Championship Level - Unproven (0.940-0.970)
            [DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN] = new() { Min = 0.940, Max = 0.960 },
            [DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN] = new() { Min = 0.955, Max = 0.975 },

            // Championship Level - Proven Elite (0.960-0.995)
            [DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL] = new() { Min = 0.960, Max = 0.980 },
            [DriverReputation.PRIME_CHAMPIONSHIP_LEVEL] = new() { Min = 0.975, Max = 0.995 },
            [DriverReputation.AGEING_CHAMPIONSHIP_LEVEL] = new() { Min = 0.965, Max = 0.985 }
        };

        public static Dictionary<string, double> Generate(TeamReputation reputation)
        {
            double t = _random.NextDouble();

            var (weightMin, weightMax, powerMin, powerMax, dragMin, dragMax, veichle_reliability) = reputation switch
            {
                TeamReputation.TOP_TEAM => (0.988, 0.996, 1.004, 1.012, 0.988, 0.996, 0.5),
                TeamReputation.MIDFIELD_HIGH => (1.000, 1.005, 0.995, 1.000, 1.000, 1.005, 0.4),
                TeamReputation.MIDFIELD => (1.007, 1.013, 0.984, 0.991, 1.007, 1.013, 0.3),
                TeamReputation.MINNOW => (1.016, 1.022, 0.972, 0.980, 1.016, 1.022, 0.3),
                TeamReputation.SUPER_MINNOW => (1.026, 1.035, 0.958, 0.968, 1.026, 1.035, 0.2),
                _ => (1.026, 1.035, 0.958, 0.968, 1.026, 1.035, 0.2)
            };

            return new Dictionary<string, double>
            {
                ["weight_scalar"] = -Lerp(weightMin, weightMax, t),
                ["power_scalar"] = -Lerp(powerMax, powerMin, t),  // inverted: higher t = weaker car
                ["drag_scalar"] = -Lerp(dragMin, dragMax, t),
                ["vehicle_reliability"] = -veichle_reliability
            };
        }

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        /// <summary>
        /// Clamps a corrected power_scalar to the same safety envelope <see cref="Generate(double, double)"/>
        /// already enforces, for callers (e.g. in-sim calibration) that adjust an existing scalar
        /// directly instead of generating one from scratch.
        /// </summary>
        public static double ClampPowerScalar(double value) => Math.Clamp(value, -PowerSafetyBest, -PowerSafetyWorst);

        /// <summary>
        /// Clamps a corrected weight_scalar to the same safety envelope <see cref="Generate(double, double)"/>
        /// already enforces, for callers (e.g. in-sim calibration) that adjust an existing scalar
        /// directly instead of generating one from scratch.
        /// </summary>
        public static double ClampWeightScalar(double value) => Math.Clamp(value, -WeightSafetyWorst, -WeightSafetyBest);

        // Overall envelope spanned by the TOP_TEAM...SUPER_MINNOW buckets above - the continuous
        // Generate(double, double) overload interpolates across this same range instead of picking
        // one of the 5 discrete buckets.
        private const double PowerBest = 1.030;
        private const double PowerWorst = 0.970;
        private const double WeightBest = 0.970;
        private const double WeightWorst = 1.030;
        private const double DragBest = 0.970;
        private const double DragWorst = 1.030;
        private const double ReliabilityBest = 0.5;
        private const double ReliabilityWorst = 0.2;

        // The envelope width this system was originally tuned at (+-10%, i.e. PowerBest-PowerWorst
        // = 0.200 back when those constants were 1.100/0.900). baseRelativeStrength correction below
        // was calibrated against that width. If the envelope above is narrowed to compress the
        // grid's overall spread, a fixed-magnitude baseRelativeStrength correction becomes
        // proportionally much stronger relative to the (now smaller) competitiveness-based spread,
        // and can dominate/invert it. CorrectionDamping scales the correction's influence down by
        // the same ratio the envelope was narrowed, so it stays subordinate to the real
        // competitiveness signal regardless of how wide/narrow the envelope currently is.
        private const double ReferenceEnvelopeSpan = 0.200;
        private static double CorrectionDamping => (PowerBest - PowerWorst) / ReferenceEnvelopeSpan;

        // Wider safety bounds applied only after the baseRelativeStrength correction, so that
        // correcting for a team's assigned AMS2 car (see Generate(double, double) below) has
        // headroom to actually take effect instead of being clamped straight back into the same
        // +-10% envelope used for the uncorrected score-only value.
        private const double PowerSafetyBest = 1.20;
        private const double PowerSafetyWorst = 0.80;
        private const double WeightSafetyBest = 0.80;
        private const double WeightSafetyWorst = 1.20;

        /// <summary>
        /// Normalizes a team's actual championship points into a 0 (last place) - 1 (champion)
        /// competitiveness score, relative to the season leader's points so it stays meaningful
        /// across eras with very different points systems. Blended evenly with final-position
        /// fraction so a single runaway leader (e.g. a team that doubles up 2nd place) doesn't
        /// compress every other team's points ratio toward "backmarker" territory - position
        /// keeps a mid-table team reading as mid-table even when its points share of the leader
        /// looks small.
        /// </summary>
        public static double ComputeCompetitivenessScore(double points, double leaderPoints, int position, int fieldSize)
        {
            double pointsShare = leaderPoints > 0 ? points / leaderPoints : 0.0;
            double positionFraction = fieldSize > 1 ? 1.0 - (position - 1) / (double)(fieldSize - 1) : 1.0;
            return Clamp(pointsShare * 0.5 + positionFraction * 0.5);
        }

        /// <summary>
        /// Generates power/weight/drag scalars from a continuous competitiveness score (1.0 =
        /// champion, 0.0 = last place) instead of a hand-picked TeamReputation bucket.
        /// <paramref name="baseRelativeStrength"/> corrects for the team's assigned AMS2 car
        /// already having a different real-world power-to-weight ratio than the season's field
        /// average (&gt;1 = inherently stronger car). Power and weight move in opposite arithmetic
        /// directions for "worse" (power down, weight up), so a car that's already stronger than
        /// average needs power divided and weight multiplied by the same factor to end up equally
        /// less favorable in both. Drag is not baseline-corrected (no drag baseline is collected).
        /// </summary>
        public static Dictionary<string, double> Generate(double competitivenessScore, double baseRelativeStrength = 1.0)
        {
            double weakness = Clamp(1.0 - competitivenessScore);
            double dampedRelativeStrength = 1.0 + (baseRelativeStrength - 1.0) * CorrectionDamping;

            double power = Math.Clamp(-Lerp(PowerBest, PowerWorst, weakness) / dampedRelativeStrength, -PowerSafetyBest, -PowerSafetyWorst);
            double weight = Math.Clamp(-Lerp(WeightBest, WeightWorst, weakness) * dampedRelativeStrength, -WeightSafetyWorst, -WeightSafetyBest);
            double drag = -Lerp(DragBest, DragWorst, weakness);
            double reliability = -Lerp(ReliabilityBest, ReliabilityWorst, weakness);

            return new Dictionary<string, double>
            {
                ["weight_scalar"] = Convert.ToDouble(weight.ToString("N3")),
                ["power_scalar"] = Convert.ToDouble(power.ToString("N3")),
                ["drag_scalar"] = Convert.ToDouble(drag.ToString("N3")),
                ["vehicle_reliability"] = Convert.ToDouble(reliability.ToString("N3"))
            };
        }

        public static Dictionary<string, double> Generate(DriverReputation reputation)
        {
            if (!_baseRanges.TryGetValue(reputation, out var range))
            {
                throw new ArgumentException($"Unknown reputation: {reputation}");
            }

            // Generate base value
            double baseValue = range.Min + _random.NextDouble() * (range.Max - range.Min);

            // Generate base value for consistency
            double baseValueConsistency = 0.2 + _random.NextDouble() * (0.5 - 0.2);

            var ratings = new Dictionary<string, double>();

            // Generate all AMS2 ratings with slight variations from base
            ratings["qualifying_skill"] = Vary(baseValue, range.Variance);
            ratings["race_skill"] = Vary(baseValue, range.Variance);
            ratings["aggression"] = Vary(baseValue, range.Variance * 1.5); // More variance
            ratings["defending"] = Vary(baseValue, range.Variance);
            ratings["stamina"] = Vary(baseValue, range.Variance);
            ratings["consistency"] = Vary(baseValueConsistency, range.Variance * 0.8); // Less variance
            ratings["start_reactions"] = Vary(baseValue, range.Variance);
            ratings["wet_skill"] = Vary(baseValue, range.Variance * 1.2);
            ratings["tyre_management"] = Vary(baseValue, range.Variance);
            ratings["fuel_management"] = Vary(baseValue, range.Variance);
            ratings["blue_flag_conceding"] = Vary(baseValue, range.Variance);
            ratings["weather_tyre_changes"] = Vary(baseValue, range.Variance);
            ratings["avoidance_of_mistakes"] = Vary(baseValue, range.Variance);
            ratings["avoidance_of_forced_mistakes"] = Vary(baseValue, range.Variance);

            // Apply reputation-specific adjustments
            ApplyReputationModifiers(ratings, reputation, baseValue);

            return ratings;
        }

        private static void ApplyReputationModifiers(Dictionary<string, double> ratings, DriverReputation reputation, double baseValue)
        {
            switch (reputation)
            {
                // Young drivers: Higher aggression, lower consistency/experience skills
                case DriverReputation.YOUNG_TALENT:
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN:
                case DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL:
                    ratings["aggression"] = Math.Min(1.0, ratings["aggression"] + 0.05);
                    ratings["consistency"] = Math.Max(0.2, ratings["consistency"] - 0.03);
                    ratings["tyre_management"] = Math.Max(0.0, ratings["tyre_management"] - 0.02);
                    ratings["fuel_management"] = Math.Max(0.0, ratings["fuel_management"] - 0.02);
                    break;

                // Ageing drivers: Higher consistency/management, lower stamina
                case DriverReputation.AGEING_MIDFIELD:
                case DriverReputation.AGEING_STRONG_MIDFIELD:
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL:
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED:
                case DriverReputation.JUST_ONE_LAST_DANCE:
                    ratings["consistency"] = Math.Min(0.5, ratings["consistency"] + 0.04);
                    ratings["tyre_management"] = Math.Min(1.0, ratings["tyre_management"] + 0.03);
                    ratings["fuel_management"] = Math.Min(1.0, ratings["fuel_management"] + 0.03);
                    ratings["stamina"] = Math.Max(0.0, ratings["stamina"] - 0.04);
                    ratings["aggression"] = Math.Max(0.0, ratings["aggression"] - 0.02);
                    break;

                // Pay drivers: Lower race skill, variable consistency
                case DriverReputation.PAY_DRIVER_WILD_CARD:
                case DriverReputation.PAY_DRIVER_SEASON:
                    ratings["race_skill"] = Math.Max(0.0, ratings["race_skill"] - 0.03);
                    ratings["defending"] = Math.Max(0.0, ratings["defending"] - 0.03);
                    ratings["consistency"] = Vary(baseValue, 0.06); // High variance
                    break;
            }
        }

        private static double Vary(double baseValue, double variance)
        {
            double variation = (_random.NextDouble() - 0.5) * 2 * variance;
            return Clamp(baseValue + variation);
        }

        private static double Clamp(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        public static string GetReputationDescription(DriverReputation reputation)
        {
            return reputation switch
            {
                DriverReputation.PAY_DRIVER_WILD_CARD => "Pay Driver (Wild Card)",
                DriverReputation.PAY_DRIVER_SEASON => "Pay Driver (Season)",
                DriverReputation.YOUNG_TALENT => "Young Talent",
                DriverReputation.PRIME_MIDFIELD => "Prime Midfield",
                DriverReputation.AGEING_MIDFIELD => "Ageing Midfield",
                DriverReputation.PRIME_STRONG_MIDFIELD => "Prime Strong Midfield",
                DriverReputation.AGEING_STRONG_MIDFIELD => "Ageing Strong Midfield",
                DriverReputation.JUST_ONE_LAST_DANCE => "One Last Dance",
                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED => "Faded Champion (Ageing)",
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_WASHED => "Faded Champion (Prime)",
                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL_UNPROVEN => "Young Potential (Unproven)",
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL_UNPROVEN => "Prime Potential (Unproven)",
                DriverReputation.YOUNG_CHAMPIONSHIP_LEVEL => "Young Champion",
                DriverReputation.PRIME_CHAMPIONSHIP_LEVEL => "Prime Champion",
                DriverReputation.AGEING_CHAMPIONSHIP_LEVEL => "Ageing Champion",
                _ => reputation.ToString().Replace('_', ' ')
            };
        }
    }
}