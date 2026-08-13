using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.AMS2.Models
{
    public class Ams2TrackMappingFile
    {
        [JsonPropertyName("mappings")]
        public List<Ams2TrackMappingEntry> Mappings { get; set; }
    }

    public class Ams2TrackMappingEntry
    {
        /// <summary>
        /// Strings that, if any is contained (case-insensitively) in the race's Grand Prix name,
        /// mark this entry as the one to use for that race - e.g. ["Brazilian", "do Brasil"].
        /// </summary>
        [JsonPropertyName("grand_prix_name_patterns")]
        public List<string> GrandPrixNamePatterns { get; set; }

        /// <summary>
        /// The season year this track configuration first applies from. Multiple entries can share
        /// the same GrandPrixNamePatterns to model different eras of the same Grand Prix (e.g. a
        /// venue that changed layout, or a GP that moved circuits) - the resolver picks the entry
        /// with the largest Year that is still &lt;= the season's year, so an entry applies from its
        /// own Year up to (but not including) the next dated entry for the same patterns.
        /// </summary>
        [JsonPropertyName("year")]
        public int Year { get; set; }

        /// <summary>
        /// The preferred track id, only used if <see cref="DlcId"/> is set and owned.
        /// </summary>
        [JsonPropertyName("best_track_id")]
        public string BestTrackId { get; set; }

        /// <summary>
        /// Null if there's no DLC-gated alternative to the default track for this race.
        /// Otherwise, the id checked via IAms2DlcOwnershipChecker (Ams2ChEd.Business.AMS2.Helpers)
        /// to decide between <see cref="BestTrackId"/> and <see cref="DefaultTrackId"/>.
        /// </summary>
        [JsonPropertyName("dlc_id")]
        public string DlcId { get; set; }

        [JsonPropertyName("default_track_id")]
        public string DefaultTrackId { get; set; }

        [JsonPropertyName("default_number_of_laps")]
        public int DefaultNumberOfLaps { get; set; }
    }
}
