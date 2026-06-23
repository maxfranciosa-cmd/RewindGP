using AMS2ChEd.Business.Models.Concrete;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AMS2ChEd.SeasonPackEditor
{
    /// <summary>
    /// Persisted configuration that the Scenario Creator wizard builds.
    /// Stored inside ScenarioEntry so it survives project save/load.
    /// The original season is never mutated – all overrides live here.
    /// </summary>
    public class ScenarioSaveConfig
    {
        /// <summary>Timestamp of season data at wizard-save time (ISO 8601). Used for stale-detection.</summary>
        [JsonPropertyName("season_synced_at")]
        public string SeasonSyncedAt { get; set; }

        /// <summary>One slot per team seat. Replaces the season's Driver1Contract / Driver2Contract.</summary>
        [JsonPropertyName("driver_slots")]
        public List<ScenarioDriverSlot> DriverSlots { get; set; } = new();

        /// <summary>Driver ID the player will control. May be an existing driver or a new custom driver.</summary>
        [JsonPropertyName("player_driver_id")]
        public string PlayerDriverId { get; set; }

        /// <summary>Populated only when the player chose to create a brand-new driver.</summary>
        [JsonPropertyName("new_player_driver")]
        public ScenarioNewDriver NewPlayerDriver { get; set; }

        /// <summary>Scenario-specific absences. Pre-loaded from season on first open, then freely editable.</summary>
        [JsonPropertyName("absences")]
        public List<Absence> Absences { get; set; } = new();

        /// <summary>Results for races 0..N-1. Races beyond the last entry have no results (career starts there).</summary>
        [JsonPropertyName("predefined_results")]
        public List<ScenarioRaceResult> PredefinedResults { get; set; } = new();
    }

    public class ScenarioDriverSlot
    {
        [JsonPropertyName("team_id")]
        public string TeamId { get; set; }

        /// <summary>1 or 2</summary>
        [JsonPropertyName("slot")]
        public int Slot { get; set; }

        [JsonPropertyName("driver_id")]
        public string DriverId { get; set; }

        [JsonPropertyName("car_number")]
        public int CarNumber { get; set; }

        /// <summary>Bitmask of race IDs this driver participates in.</summary>
        [JsonPropertyName("races")]
        public int Races { get; set; }
    }

    public class ScenarioNewDriver
    {
        [JsonPropertyName("first_name")]
        public string FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string LastName { get; set; }

        [JsonPropertyName("nationality")]
        public string Nationality { get; set; }

        /// <summary>Absolute paths on disk. Copied to Seasons/[YEAR]/Scenarios/Helmets/ on export.</summary>
        [JsonPropertyName("base_helmet_file_full_path")]
        public string BaseHelmetFileFullPath { get; set; }

        [JsonPropertyName("base_visor_file_full_path")]
        public string BaseVisorFileFullPath { get; set; }

        [JsonPropertyName("base_helmet_file_90s_full_path")]
        public string BaseHelmetFile90sFullPath { get; set; }

        [JsonPropertyName("base_helmet_file_80s_full_path")]
        public string BaseHelmetFile80sFullPath { get; set; }

        [JsonPropertyName("base_visor_file_80s_full_path")]
        public string BaseVisorFile80sFullPath { get; set; }

        [JsonPropertyName("base_helmet_file_70s_full_path")]
        public string BaseHelmetFile70sFullPath { get; set; }

        [JsonPropertyName("base_visor_file_70s_full_path")]
        public string BaseVisorFile70sFullPath { get; set; }

        [JsonIgnore]
        public string DriverId => $"player_{FirstName}_{LastName}";
    }

    public class ScenarioRaceResult
    {
        [JsonPropertyName("race_id")]
        public int RaceId { get; set; }

        [JsonPropertyName("results")]
        public List<ScenarioDriverResult> Results { get; set; } = new();
    }

    public class ScenarioDriverResult
    {
        [JsonPropertyName("driver_id")]
        public string DriverId { get; set; }

        [JsonPropertyName("team_id")]
        public string TeamId { get; set; }

        /// <summary>Numeric finish position, or 0 for DNF/DNQ.</summary>
        [JsonPropertyName("position")]
        public int Position { get; set; }

        [JsonPropertyName("dnf")]
        public bool DNF { get; set; }

        [JsonPropertyName("did_not_prequalify")]
        public bool DidNotPreQualify { get; set; }

        [JsonPropertyName("fastest_lap")]
        public bool FastestLap { get; set; }
    }
}