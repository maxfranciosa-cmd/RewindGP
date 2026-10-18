namespace Ams2ChEd.Business.AMS2.Helpers
{
    /// <summary>
    /// The value ranges AMS2 accepts in a CustomAIDrivers file. A value outside its range is
    /// treated as invalid and the game silently falls back to the original value (1.0 for a
    /// scalar), so anything written there must be clamped to these first.
    /// </summary>
    public static class Ams2AiRatingLimits
    {
        public const double ScalarMin = 0.9;
        public const double ScalarMax = 1.1;

        public static readonly IReadOnlyList<string> ScalarKeys = new[] { "power_scalar", "weight_scalar", "drag_scalar" };

        // values that belong to the car, not the driver: both cars of a team share them
        public static readonly IReadOnlyList<string> CarKeys = new[] { "power_scalar", "weight_scalar", "drag_scalar", "vehicle_reliability" };

        public static bool IsScalar(string key) => ScalarKeys.Contains(key);

        // vehicle_reliability is a ratio that may legitimately go outside 0-1; everything else is 0-1
        public static double Clamp(string key, double value) =>
            IsScalar(key) ? Math.Clamp(value, ScalarMin, ScalarMax)
            : key == "vehicle_reliability" ? value
            : Math.Clamp(value, 0.0, 1.0);
    }
}
