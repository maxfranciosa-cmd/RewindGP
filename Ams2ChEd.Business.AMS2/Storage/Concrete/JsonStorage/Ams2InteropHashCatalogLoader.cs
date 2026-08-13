using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Storage.Contracts;
using AMS2ChEd.Business.Helpers;
using System.Text.Json;

namespace AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage
{
    public class Ams2InteropHashCatalogLoader : IAms2HashCatalogProvider
    {
        private static IReadOnlyDictionary<string, int> trackHashesCache;
        private static IReadOnlyDictionary<string, int> carHashesCache;

        public IReadOnlyDictionary<string, int> TrackHashes =>
            trackHashesCache ??= LoadDictionary(StoragePaths.Ams2TrackHashesFilePath);

        public IReadOnlyDictionary<string, int> CarHashes =>
            carHashesCache ??= LoadDictionary(StoragePaths.Ams2CarHashesFilePath);

        private static IReadOnlyDictionary<string, int> LoadDictionary(string filePath)
        {
            // Absence must mean "no data supplied yet", never a crash - car/track resolution in
            // Ams2RaceLaunchAssistant treats a missing hash as "can't auto-configure this race"
            // and falls back to the manual-instructions flow.
            if (!File.Exists(filePath))
            {
                return new Dictionary<string, int>();
            }

            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, int>>(json, DefaultJsonSerializerOptions.Instance)
                ?? new Dictionary<string, int>();
        }
    }
}
