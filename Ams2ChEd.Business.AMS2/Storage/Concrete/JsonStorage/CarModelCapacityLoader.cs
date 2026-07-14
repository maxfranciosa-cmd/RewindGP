using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Contracts;
using AMS2ChEd.Business.Helpers;
using System.Text.Json;

namespace AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage
{
    public class CarModelCapacityLoader : ICarModelCapacityLoader
    {
        private static Dictionary<string, List<(string Model, int Slots)>> cache;

        public IReadOnlyList<(string Model, int Slots)> GetModelsForClass(string ams2Class)
        {
            var all = LoadAll();
            return !string.IsNullOrEmpty(ams2Class) && all.TryGetValue(ams2Class, out var models)
                ? models
                : null;
        }

        private static Dictionary<string, List<(string Model, int Slots)>> LoadAll()
        {
            if (cache != null)
                return cache;

            // This registry is new and opt-in: most installs/season packs won't have it yet,
            // and its absence must mean "everything uncapped", never a crash.
            if (!File.Exists(StoragePaths.CarModelCapacitiesFilePath))
            {
                cache = new Dictionary<string, List<(string, int)>>();
                return cache;
            }

            string json = File.ReadAllText(StoragePaths.CarModelCapacitiesFilePath);
            var parsed = JsonSerializer.Deserialize<CarModelCapacitiesFile>(json, DefaultJsonSerializerOptions.Instance);

            cache = (parsed?.Classes ?? new List<Ams2ClassCapacity>())
                .Where(c => !string.IsNullOrEmpty(c.Class))
                .ToDictionary(
                    c => c.Class,
                    c => (c.Models ?? new List<Ams2ModelCapacity>())
                        .Select(m => (m.Model, m.Slots))
                        .ToList());

            return cache;
        }
    }
}
