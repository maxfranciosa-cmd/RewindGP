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
            var ratings = new Dictionary<string, double>();

            switch (reputation)
            {
                case TeamReputation.TOP_TEAM:
                    ratings["weight_scalar"] = - 1.000 - (_random.NextDouble() * 0.002);
                    ratings["power_scalar"] = - 0.995 - (_random.NextDouble() * 0.005);
                    ratings["drag_scalar"] = - 1.000 - (_random.NextDouble() * 0.005);
                    break;
                case TeamReputation.MIDFIELD_HIGH:
                    ratings["weight_scalar"] = -1.004 - (_random.NextDouble() * 0.003);
                    ratings["power_scalar"] = -0.978 - (_random.NextDouble() * 0.007);
                    ratings["drag_scalar"] = -1.007 - (_random.NextDouble() * 0.003);
                    break;
                case TeamReputation.MIDFIELD:
                    ratings["weight_scalar"] = -1.009 - (_random.NextDouble() * 0.002);
                    ratings["power_scalar"] = -0.971 - (_random.NextDouble() * 0.005);
                    ratings["drag_scalar"] = -1.012 - (_random.NextDouble() * 0.002);
                    break;
                case TeamReputation.MINNOW:
                    ratings["weight_scalar"] = -1.011 - (_random.NextDouble() * 0.005);
                    ratings["power_scalar"] = -0.963 - (_random.NextDouble() * 0.003);
                    ratings["drag_scalar"] = -1.016 - (_random.NextDouble() * 0.003);
                    break;
                case TeamReputation.SUPER_MINNOW:
                    ratings["weight_scalar"] = -1.019 - (_random.NextDouble() * 0.003);
                    ratings["power_scalar"] = -0.953 - (_random.NextDouble() * 0.006);
                    ratings["drag_scalar"] = -1.021 - (_random.NextDouble() * 0.003);
                    break;
                default:
                    ratings["weight_scalar"] = -1.019 - (_random.NextDouble() * 0.003);
                    ratings["power_scalar"] = -0.953 - (_random.NextDouble() * 0.006);
                    ratings["drag_scalar"] = -1.021 - (_random.NextDouble() * 0.003);
                    break;
            }

            return ratings;
        }

        public static Dictionary<string, double> Generate(DriverReputation reputation)
        {
            if (!_baseRanges.TryGetValue(reputation, out var range))
            {
                throw new ArgumentException($"Unknown reputation: {reputation}");
            }

            // Generate base value
            double baseValue = range.Min + _random.NextDouble() * (range.Max - range.Min);

            var ratings = new Dictionary<string, double>();

            // Generate all AMS2 ratings with slight variations from base
            ratings["qualifying_skill"] = Vary(baseValue, range.Variance);
            ratings["race_skill"] = Vary(baseValue, range.Variance);
            ratings["aggression"] = Vary(baseValue, range.Variance * 1.5); // More variance
            ratings["defending"] = Vary(baseValue, range.Variance);
            ratings["stamina"] = Vary(baseValue, range.Variance);
            ratings["consistency"] = Vary(baseValue, range.Variance * 0.8); // Less variance
            ratings["start_reactions"] = Vary(baseValue, range.Variance);
            ratings["wet_skill"] = Vary(baseValue, range.Variance * 1.2);
            ratings["tyre_management"] = Vary(baseValue, range.Variance);
            ratings["fuel_management"] = Vary(baseValue, range.Variance);
            ratings["blue_flag_conceding"] = Vary(baseValue, range.Variance);
            ratings["weather_tyre_changes"] = Vary(baseValue, range.Variance);
            ratings["avoidance_of_mistakes"] = Vary(baseValue, range.Variance);
            ratings["avoidance_of_forced_mistakes"] = Vary(baseValue, range.Variance);
            ratings["vehicle_reliability"] = Vary(baseValue, range.Variance * 0.5); // Very little variance

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
                    ratings["consistency"] = Math.Max(0.0, ratings["consistency"] - 0.03);
                    ratings["tyre_management"] = Math.Max(0.0, ratings["tyre_management"] - 0.02);
                    ratings["fuel_management"] = Math.Max(0.0, ratings["fuel_management"] - 0.02);
                    break;

                // Ageing drivers: Higher consistency/management, lower stamina
                case DriverReputation.AGEING_MIDFIELD:
                case DriverReputation.AGEING_STRONG_MIDFIELD:
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL:
                case DriverReputation.AGEING_CHAMPIONSHIP_LEVEL_WASHED:
                case DriverReputation.JUST_ONE_LAST_DANCE:
                    ratings["consistency"] = Math.Min(1.0, ratings["consistency"] + 0.04);
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