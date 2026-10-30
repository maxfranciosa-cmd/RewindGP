namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// AMS2's vehicle_reliability is not a percentage: each class has a minimum and maximum mean
    /// time between failures (minutes), and the value is a ratio placing the car between them,
    /// squared - MTBF = min + ratio² * (max - min). This holds those ranges, from Reiza's guide to
    /// custom AI drivers, by the class's CustomAIDrivers file name.
    /// </summary>
    public static class Ams2ReliabilityRanges
    {
        private static readonly (double Min, double Max) VeryLow = (5, 400);
        private static readonly (double Min, double Max) Low = (25, 700);
        private static readonly (double Min, double Max) Medium = (50, 1000);
        private static readonly (double Min, double Max) High = (100, 1500);
        private static readonly (double Min, double Max) VeryHigh = (300, 2000);

        private static readonly Dictionary<string, (double Min, double Max)> RangesByClass = new(StringComparer.OrdinalIgnoreCase)
        {
            ["F-Junior"] = VeryLow,

            ["F-Vintage_Gen1"] = Low,
            ["F-Vintage_Gen2"] = Low,
            ["F-Retro_Gen1"] = Low,
            ["F-Retro_Gen2"] = Low,
            ["F-Retro_Gen3"] = Low,
            ["F-Classic_Gen1"] = Low,
            ["F-Classic_Gen2"] = Low,
            ["F-Classic_Gen3"] = Low,
            ["F-Classic_Gen4"] = Low,
            ["F-Hitech_Gen1"] = Low,
            ["F-Hitech_Gen2"] = Low,

            ["F-3"] = Medium,
            ["F-Inter"] = Medium,
            ["F-Dirt"] = Medium,
            ["F-Vee"] = Medium,
            ["F-Vee_Gen2"] = Medium,
            ["F-USA_Gen1"] = Medium,
            ["F-USA_Gen3"] = Medium,
            // the guide's reliability list has no FE-G1 but an "F-V12", which has no file name of
            // its own in its class list - assumed to be the same class
            ["FE-G1"] = Medium,

            ["F-V10_Gen1"] = High,
            ["F-V10_Gen2"] = High,
            ["F-V10_Gen3"] = High,
            ["F-V8_Gen1"] = High,
            ["F-V8_Gen2"] = High,
            ["F-USA_Gen2"] = High,
            ["Group C"] = High,

            ["F-Reiza"] = VeryHigh,
            ["F-Ultimate"] = VeryHigh,
            ["F-Ultimate_Gen1"] = VeryHigh,
            ["F-Ultimate_Gen2"] = VeryHigh,
        };

        public static bool TryGetRange(string ams2Class, out (double Min, double Max) range)
        {
            range = default;
            return !string.IsNullOrWhiteSpace(ams2Class) && RangesByClass.TryGetValue(ams2Class, out range);
        }

        /// <summary>
        /// The vehicle_reliability ratio for a target mean time between failures, with Reiza's
        /// formulas: sqrt((x - min) / (max - min)) within the range, -(min / x - 1) below it. Capped
        /// at 1.0 (the class's maximum), as the guide doesn't say what a ratio beyond it does.
        /// </summary>
        public static double ToRatio((double Min, double Max) range, double mtbfMinutes)
        {
            if (mtbfMinutes < range.Min)
                return -(range.Min / mtbfMinutes - 1);

            return Math.Min(1.0, Math.Sqrt((mtbfMinutes - range.Min) / (range.Max - range.Min)));
        }

        /// <summary>
        /// The mean time between failures that gives a car the chance
        /// <paramref name="failureChance"/> of breaking down within <paramref name="raceMinutes"/>,
        /// taking failures to strike at random over the time a car runs.
        /// </summary>
        public static double MtbfForFailureChance(double failureChance, double raceMinutes) =>
            -raceMinutes / Math.Log(1 - failureChance);
    }
}
