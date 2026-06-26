using Ams2ChEd.Business.AMS2.Helpers;
using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Storage.Contracts;
using System.Text.Json;

namespace AMS2ChEd.Business.AMS2.Storage.Concrete.JsonStorage
{
    public class AccoladesLoader : IAccoladesLoader
    {
        public HistoricalAccolades LoadAccolades(int seasonYear)
        {
            var path = StoragePaths.AccoladesFilePath(seasonYear);
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<HistoricalAccolades>(json, DefaultJsonSerializerOptions.Instance);
        }
    }
}
