using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models
{
    /// <summary>
    /// A driver's or team's real-world record, season by season, from the all-time records file
    /// shipped with the app. Kept per season so the same file serves a save that starts in any
    /// year: only the seasons before the save began count as history.
    /// </summary>
    public class AllTimeRecord
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        // season year -> races won that season
        [JsonPropertyName("wins_by_year")]
        public Dictionary<int, int> WinsByYear { get; set; } = new();

        // season year -> top-three finishes that season, wins included
        [JsonPropertyName("podiums_by_year")]
        public Dictionary<int, int> PodiumsByYear { get; set; } = new();

        [JsonPropertyName("poles_by_year")]
        public Dictionary<int, int> PolesByYear { get; set; } = new();

        [JsonPropertyName("championships")]
        public List<int> Championships { get; set; } = new();
    }

    public class AllTimeDriverRecord : AllTimeRecord
    {
        [JsonPropertyName("driver_id")]
        public string DriverId { get; set; }

        [JsonPropertyName("nationality")]
        public string Nationality { get; set; }
    }

    public class AllTimeTeamRecord : AllTimeRecord
    {
        // the lineage id of Teams/teams.json, so one record spans a team's changes of name
        [JsonPropertyName("team_id")]
        public string TeamId { get; set; }
    }

    /// <summary>
    /// Everyone who has ever finished on the podium or started from pole. Whoever is not listed
    /// has no wins, podiums, poles or titles in the seasons the file covers.
    /// </summary>
    public class AllTimeRecords
    {
        // last season the file covers
        [JsonPropertyName("up_to")]
        public int UpTo { get; set; }

        [JsonPropertyName("drivers")]
        public List<AllTimeDriverRecord> Drivers { get; set; } = new();

        [JsonPropertyName("teams")]
        public List<AllTimeTeamRecord> Teams { get; set; } = new();
    }
}
