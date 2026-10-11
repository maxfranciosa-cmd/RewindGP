using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models
{
    /// <summary>
    /// A driver's real-world record, season by season, from the all-time records file shipped with
    /// the app. Kept per season so the same file serves a save that starts in any year: only the
    /// seasons before the save began count as history.
    /// </summary>
    public class AllTimeDriverRecord
    {
        [JsonPropertyName("driver_id")]
        public string DriverId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("nationality")]
        public string Nationality { get; set; }

        // season year -> races won that season
        [JsonPropertyName("wins_by_year")]
        public Dictionary<int, int> WinsByYear { get; set; } = new();

        [JsonPropertyName("championships")]
        public List<int> Championships { get; set; } = new();
    }

    public class AllTimeRecords
    {
        // last season the file covers
        [JsonPropertyName("up_to")]
        public int UpTo { get; set; }

        [JsonPropertyName("drivers")]
        public List<AllTimeDriverRecord> Drivers { get; set; } = new();
    }
}
