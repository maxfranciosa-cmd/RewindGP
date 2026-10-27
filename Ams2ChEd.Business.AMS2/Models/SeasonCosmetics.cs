using System.Text.Json.Serialization;
using Ams2ChEd.Business.AMS2.Models;

namespace AMS2ChEd.Business.AMS2.Models
{
    /// <summary>
    /// The artwork the app ships for a baked season (BakedSeasons/&lt;year&gt;/cosmetics/cosmetics.json),
    /// in place of a season pack: each team's and driver's artwork fields, as a pack would give them,
    /// and the community skin pack those fields point into. Paths are relative to
    /// Seasons/&lt;year&gt;/, like a pack's: a skin-pack file lands there when the player imports the
    /// skin pack, and a file the app ships is reached from there ("../../BakedSeasons/...",
    /// "../../Drivers/own_helmets/..."). A field whose file is not on disk is left out at load.
    /// </summary>
    public class SeasonCosmetics
    {
        /// <summary>Per team_id: only the artwork fields are read (as BakedSeasonMerge.CopyTeamArtwork copies them) and ams2car, the car they were painted for.</summary>
        [JsonPropertyName("teams")]
        public Dictionary<string, Ams2TeamEntry> Teams { get; set; } = new();

        /// <summary>Per driver_id: only the helmet, visor and portrait fields are read.</summary>
        [JsonPropertyName("drivers")]
        public Dictionary<string, Ams2DriverData> Drivers { get; set; } = new();

        /// <summary>The community skin pack this season's artwork comes from; null when there is none.</summary>
        [JsonPropertyName("skin_pack")]
        public SkinPackSource SkinPack { get; set; }
    }

    public class SkinPackSource
    {
        /// <summary>The page the player downloads it from.</summary>
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>A few paths inside the archive that identify it, matched by their ending (the archive's top folder may be renamed).</summary>
        [JsonPropertyName("detect")]
        public List<string> Detect { get; set; } = new();

        /// <summary>Archive file to Seasons/&lt;year&gt;/ file, as external_liveries.json had them.</summary>
        [JsonPropertyName("entries")]
        public List<ExternalLiveriesEntry> Entries { get; set; } = new();

        /// <summary>Per team_id, the pack's helmet and visor the team's helmet and visor sponsor layers are cut from at import.</summary>
        [JsonPropertyName("helmets")]
        public Dictionary<string, SkinPackTeamHelmets> Helmets { get; set; } = new();

        /// <summary>The helmet model the pack's helmets are painted for (Modern, Nineties, Eighties, Seventies).</summary>
        [JsonPropertyName("helmet_model")]
        public string HelmetModel { get; set; }
    }

    public class SkinPackTeamHelmets
    {
        /// <summary>The archive path of a helmet the pack painted for one of the team's drivers: the team's sponsors are cut from it.</summary>
        [JsonPropertyName("helmet")]
        public string Helmet { get; set; }

        /// <summary>
        /// Where the team's sponsors sit on that helmet: a PNG of the helmet's layout (its alpha),
        /// relative to the season's cosmetics folder. Null: the helmet model's generic map.
        /// </summary>
        [JsonPropertyName("map")]
        public string Map { get; set; }

        /// <summary>The archive path of a visor of one of the team's drivers; its sponsor strip becomes the team's visor layer.</summary>
        [JsonPropertyName("visor")]
        public string Visor { get; set; }
    }

    /// <summary>What was imported for a season (Seasons/&lt;year&gt;/skin_pack.json).</summary>
    public class SkinPackRecord
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("archive")]
        public string Archive { get; set; }

        [JsonPropertyName("imported")]
        public DateTime Imported { get; set; }

        /// <summary>The files written under Seasons/&lt;year&gt;/, relative to it.</summary>
        [JsonPropertyName("files")]
        public List<string> Files { get; set; } = new();

        /// <summary>Per team, what the helmet and visor sponsor extraction produced, or why not.</summary>
        [JsonPropertyName("notes")]
        public List<string> Notes { get; set; } = new();
    }
}
