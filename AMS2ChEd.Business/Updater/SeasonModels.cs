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
        InstalledNoRemote
    }

    // -------------------------------------------------------------------------
    // Save load result
    // -------------------------------------------------------------------------

    public enum SaveGameSeasonCheckerResult
    {
        /// <summary>Season is current — load the save normally.</summary>
        Proceed,

        /// <summary>Save needs to be migrated to the current season data — load after update.</summary>
        NeedsRefresh
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
        public bool CheckFailed { get; set; }
    }
}