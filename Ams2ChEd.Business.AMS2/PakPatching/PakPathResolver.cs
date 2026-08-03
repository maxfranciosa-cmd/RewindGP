namespace Ams2ChEd.Business.AMS2.PakPatching
{
    /// <summary>
    /// Resolves which of a model's possible .bff pak file variants actually exist on disk. A
    /// model may ship a base pak plus low/high-downforce variants - only the ones that exist are
    /// ever touched (see AMS2-livery-modding-knowledge.md's note that not every car has LD/HD
    /// variants).
    /// </summary>
    public static class PakPathResolver
    {
        public static IReadOnlyList<string> GetPerCarPakPaths(string ams2InstallFolder, string carModel, Func<string, bool> fileExists)
        {
            string vehiclesFolder = Path.Combine(ams2InstallFolder, "Pakfiles", "Vehicles");

            var candidates = new[]
            {
                Path.Combine(vehiclesFolder, $"{carModel}.bff"),
                Path.Combine(vehiclesFolder, $"{carModel}_LD.bff"),
                Path.Combine(vehiclesFolder, $"{carModel}_HD.bff"),
            };

            return candidates.Where(fileExists).ToList();
        }

        public static string GetPersistentPakPath(string ams2InstallFolder) =>
            Path.Combine(ams2InstallFolder, "Pakfiles", "Vehicles", "vehiclespersistent.bff");
    }
}
