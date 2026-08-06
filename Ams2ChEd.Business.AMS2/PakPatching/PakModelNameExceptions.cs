namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Some car models' .bff pak filenames and internal .rcf entry paths don't match the
    /// canonical model id used everywhere else in the app (car_model_capacities.json, season
    /// data, etc.) - Reiza shipped them under a different internal name. Confirmed exceptions so
    /// far: formula_v10_g2_b ships as "formula_v10", formula_v10_g2_m ships as "formula_v10_m".
    /// Add new rows here - do not reintroduce inline `if` checks at call sites - whenever another
    /// mismatch like this is found; every place that turns a car model id into a pak filename or
    /// an internal .rcf path should route through Resolve() below.
    /// </summary>
    public static class PakModelNameExceptions
    {
        private static readonly Dictionary<string, string> ExceptionsByCanonicalModel =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["formula_v10_g2_b"] = "formula_v10",
                ["formula_v10_g2_m"] = "formula_v10_m",
            };

        private static readonly Dictionary<string, string> RcfFolderExceptionsByCanonicalModel =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["formula_v10_g2_b"] = "formula_v10",
                ["formula_v10_g2_m"] = "formula_v10",
            };

        /// <summary>Returns the pak/.rcf-internal name for a car model id, or the id unchanged if it has no known exception.</summary>
        public static string Resolve(string carModel) =>
            ExceptionsByCanonicalModel.TryGetValue(carModel, out var pakModel) ? pakModel : carModel;

        public static string ResolveRcfFolder(string carModel) =>
            RcfFolderExceptionsByCanonicalModel.TryGetValue(carModel, out var rcfFolder) ? rcfFolder : carModel;
    }
}
