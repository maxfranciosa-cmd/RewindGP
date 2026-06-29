using System.Text.Json.Serialization;

namespace Ams2ChEd.Business.AMS2.Models
{
    public class ExternalLiveriesConfig
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("entries")]
        public List<ExternalLiveriesEntry> Entries { get; set; } = new();
    }

    public class ExternalLiveriesEntry
    {
        [JsonPropertyName("sourcePath")]
        public string SourcePath { get; set; }

        [JsonPropertyName("destinationPath")]
        public string DestinationPath { get; set; }
    }
}
