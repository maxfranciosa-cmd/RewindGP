using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Updater.Models
{
    // -------------------------------------------------------------------------
    // seasons.json (local, ships with RewindGP, updated with each app release)
    // -------------------------------------------------------------------------
    public class SeasonDisplayItem
    {
        public int Year { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public double FileSizeMb { get; set; }
        public SeasonAvailability Availability { get; set; }
        public string Credits { get; set; }
        public string PageUrl { get; set; }
        public string? InstalledSlug { get; set; }
        public bool IsInstalled => Availability is SeasonAvailability.UpToDate
                                                or SeasonAvailability.UpdateAvailable
                                                or SeasonAvailability.InstalledNoRemote;

        public string StatusLabel => Availability switch
        {
            SeasonAvailability.UpToDate => "Installed",
            SeasonAvailability.UpdateAvailable => "Update available",
            SeasonAvailability.NotInstalled => "Not installed",
            SeasonAvailability.InstalledNoRemote => "Installed",
            SeasonAvailability.NotAvailable => "Not available",
            SeasonAvailability.BakedPackAvailable => "Playable · artwork pack available",
            SeasonAvailability.BakedOnly => "Playable",
            _ => string.Empty
        };

        public string FileSizeLabel => FileSizeMb > 0
            ? $"{FileSizeMb:F0} MB"
            : "—";
    }

    public class SeasonsManifest
    {
        [JsonPropertyName("seasons")]
        public List<SeasonManifestEntry> Seasons { get; set; } = new();
    }

    public class SeasonManifestEntry
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        /// <summary>
        /// Unique identifier for this pack variant. Falls back to year.ToString()
        /// for backwards compatibility with old packs that have no slug field.
        /// </summary>
        [JsonPropertyName("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonIgnore]
        public string EffectiveSlug => string.IsNullOrWhiteSpace(Slug)
            ? Year.ToString()
            : Slug;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }

        [JsonPropertyName("downloadUrl")]
        public string PageUrl { get; set; } = string.Empty;

        [JsonPropertyName("fileSizeInMb")]
        public int FileSizeMb { get; set; }

        [JsonPropertyName("credits")]
        public string Credits { get; set; } = string.Empty;

        [JsonIgnore]
        public bool IsDefault => FileSizeMb == 0 && string.IsNullOrEmpty(PageUrl);
    }

    // -------------------------------------------------------------------------
    // Season availability state
    // -------------------------------------------------------------------------

    public enum SeasonAvailability
    {
        /// <summary>is the default season pack.</summary>
        DefaultSeason,

        /// <summary>Installed and up to date with seasons.json.</summary>
        UpToDate,

        /// <summary>Installed but a newer update is available for download.</summary>
        UpdateAvailable,

        /// <summary>Listed in seasons.json but not installed locally.</summary>
        NotInstalled,

        /// <summary>Not listed in seasons.json and not installed locally.</summary>
        NotAvailable,

        /// <summary>Installed locally but no longer listed in seasons.json (e.g. pack was retired).</summary>
        InstalledNoRemote,

        /// <summary>
        /// Playable from the data the app ships, with no pack installed; seasons.json lists a pack
        /// that would bring its real artwork. Downloading it is optional.
        /// </summary>
        BakedPackAvailable,

        /// <summary>Playable from the data the app ships; no pack installed and none listed.</summary>
        BakedOnly
    }

    // -------------------------------------------------------------------------
    // What a save's season data was made of
    // -------------------------------------------------------------------------

    /// <summary>
    /// A fingerprint of a season's data: one of the data the app ships, one of the installed
    /// season pack (null when there is none). A save keeps the one it was last refreshed against,
    /// so on load it can tell which of the two changed since. File times can't: every app release
    /// rewrites the app's files.
    /// </summary>
    public class SeasonDataStamp
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("baked")]
        public string? Baked { get; set; }

        [JsonPropertyName("pack")]
        public string? Pack { get; set; }

        /// <summary>
        /// Over data the app ships: a fingerprint of every other year's installed pack, which the
        /// season's teams and drivers may borrow artwork from. Null without such data, with no
        /// other pack installed, and in saves stamped before it existed.
        /// </summary>
        [JsonPropertyName("cosmetics")]
        public string? Cosmetics { get; set; }
    }

    // -------------------------------------------------------------------------
    // Save load result
    // -------------------------------------------------------------------------

    public enum SaveGameSeasonCheckerResult
    {
        /// <summary>Season is current — load the save normally.</summary>
        Proceed,

        /// <summary>Save needs to be migrated to the current season data — load after update.</summary>
        NeedsRefresh,

        /// <summary>
        /// Only the season pack's artwork changed (installed, updated or removed over data the
        /// app ships): refresh the save's artwork and leave its gameplay data alone.
        /// </summary>
        NeedsArtworkRefresh
    }

    // -------------------------------------------------------------------------
    // Season transition result
    // -------------------------------------------------------------------------

    public enum TransitionOutcome
    {
        /// <summary>Next season is ready to use.</summary>
        Ready,

        /// <summary>Next season unavailable — current season data will be reused.</summary>
        FallbackReuse
    }

    // -------------------------------------------------------------------------
    // Version check models
    // -------------------------------------------------------------------------

    public class UpdateCheckResult
    {
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = string.Empty;
        public string LatestVersion { get; set; } = string.Empty;
        public string PageUrl { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public bool CheckFailed { get; set; }
    }
}