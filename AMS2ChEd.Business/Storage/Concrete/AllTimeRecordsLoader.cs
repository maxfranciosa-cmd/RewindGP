using AMS2ChEd.Business.Helpers;
using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Storage.Contracts;
using System.Text.Json;

namespace AMS2ChEd.Business.Storage.Concrete
{
    public class AllTimeRecordsLoader : IAllTimeRecordsLoader
    {
        private readonly string _filePath;

        // the file ships with the app and never changes while it runs
        private readonly Lazy<AllTimeRecords> _records;

        public AllTimeRecordsLoader() : this(AppPaths.AllTimeRecordsPath)
        {
        }

        public AllTimeRecordsLoader(string filePath)
        {
            _filePath = filePath;
            _records = new Lazy<AllTimeRecords>(Read);
        }

        public AllTimeRecords Load() => _records.Value;

        private AllTimeRecords Read()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new AllTimeRecords();

                var records = JsonSerializer.Deserialize<AllTimeRecords>(File.ReadAllText(_filePath), DefaultJsonSerializerOptions.Instance) ?? new AllTimeRecords();
                records.Drivers ??= new List<AllTimeDriverRecord>();
                records.Teams ??= new List<AllTimeTeamRecord>();
                return records;
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException)
            {
                return new AllTimeRecords();
            }
        }
    }
}
