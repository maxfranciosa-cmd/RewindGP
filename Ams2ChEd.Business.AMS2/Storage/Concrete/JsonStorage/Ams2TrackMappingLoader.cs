using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.AMS2.Models;
using AMS2ChEd.Business.AMS2.Storage.Contracts;
using AMS2ChEd.Business.Helpers;
using System.Text.Json;

namespace AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage
{
    public class Ams2TrackMappingLoader : ITrackMappingLoader
    {
        private static List<Ams2TrackMappingEntry> cache;

        public IReadOnlyList<Ams2TrackMappingEntry> GetAll()
        {
            if (cache != null)
                return cache;

            // This registry is new and opt-in: most installs won't have it populated yet, and its
            // absence must mean "no automated track selection available", never a crash.
            if (!File.Exists(StoragePaths.TrackMappingFilePath))
            {
                cache = new List<Ams2TrackMappingEntry>();
                return cache;
            }

            string json = File.ReadAllText(StoragePaths.TrackMappingFilePath);
            var parsed = JsonSerializer.Deserialize<Ams2TrackMappingFile>(json, DefaultJsonSerializerOptions.Instance);

            cache = parsed?.Mappings ?? new List<Ams2TrackMappingEntry>();
            return cache;
        }
    }
}
