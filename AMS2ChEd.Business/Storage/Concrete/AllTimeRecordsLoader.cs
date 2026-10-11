using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Storage.Contracts;
using System.Text.Json;

namespace AMS2ChEd.Business.Storage.Concrete
{
    public class AllTimeRecordsLoader : IAllTimeRecordsLoader
    {
        private readonly string _filePath;

        public AllTimeRecordsLoader() : this(AppPaths.AllTimeDriversPath)
        {
        }

        public AllTimeRecordsLoader(string filePath)
        {
            _filePath = filePath;
        }

        public IReadOnlyList<AllTimeDriverRecord> LoadDrivers()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new List<AllTimeDriverRecord>();

                var records = JsonSerializer.Deserialize<AllTimeRecords>(File.ReadAllText(_filePath), DefaultJsonSerializerOptions.Instance);
                return records?.Drivers ?? new List<AllTimeDriverRecord>();
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException)
            {
                return new List<AllTimeDriverRecord>();
            }
        }
    }
}
