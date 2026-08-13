using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.AMS2.Models
{
    public class Ams2TrackMappingFile
    {
        [JsonPropertyName("mappings")]
        public List<Ams2TrackMappingEntry> Mappings { get; set; }
    }

    /// <summary>
    /// One DLC-gated track candidate inside an Ams2TrackMappingEntry's TrackOptions list.
    /// </summary>
    public class Ams2TrackOption
    {
        [JsonPropertyName("track_id")]
        public string TrackId { get; set; }

        /// <summary>
        /// The id checked via IAms2DlcOwnershipChecker (Ams2ChEd.Business.AMS2.Helpers) to decide
        /// whether this option's TrackId can be used.
        /// </summary>
        [JsonPropertyName("dlc_id")]
        public string DlcId { get; set; }
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
        /// DLC-gated track candidates, most preferred first. The resolver picks the first one whose
        /// DlcId is owned; DefaultTrackId is used only if none of these are owned (or this list is
        /// null/empty). A single-candidate entry is just a one-element list - e.g. for a venue where
        /// more than one DLC can plausibly supply an equivalent-or-close track for the same era
        /// (say, an exact-era layout gated behind one pack, with a same-venue-but-different-era
        /// layout from a second pack as a still-much-better-than-default fallback), list the more
        /// historically-accurate option first.
        /// </summary>
        [JsonPropertyName("track_options")]
        public List<Ams2TrackOption> TrackOptions { get; set; }

        [JsonPropertyName("default_track_id")]
        public string DefaultTrackId { get; set; }

        [JsonPropertyName("default_number_of_laps")]
        public int DefaultNumberOfLaps { get; set; }
    }
}
