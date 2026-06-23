using AMS2ChEd.Business.Models.Concrete;
using AMS2ChEd.Business.Updater.Models;
using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models
{

    public interface ISeason
    {
        [JsonPropertyName("year")]
        int Year { get; set; }

        [JsonPropertyName("original_year")]
        int? OriginalYear { get; set; }

        [JsonPropertyName("points_system")]
        Dictionary<string, int> PointsSystem { get; set; }

        [JsonPropertyName("points_for_fastest_lap")]
        double? PointsForFastestLap { get; set; }

        [JsonPropertyName("races")]
        IEnumerable<Race> Races { get; set; }

        [JsonPropertyName("teams")]
        IEnumerable<ITeamEntry> Teams { get; set; }

        [JsonPropertyName("absences")]
        IEnumerable<Absence> Absences { get; set; }

        [JsonPropertyName("max_drivers_per_race")]
        int? MaxDriversPerRace { get; set; }

        [JsonPropertyName("slug")]
        string? Slug { get; set; }
    }

    public interface ITeamEntry
    {
        [JsonPropertyName("team_id")]
        string TeamId { get; set; }

        [JsonPropertyName("team_name")]
        string TeamName { get; set; }

        [JsonPropertyName("team_principal")]
        string TeamPrincipal { get; set; }

        [JsonPropertyName("reputation")]
        TeamReputation Reputation { get; set; }

        [JsonPropertyName("driver1_contract")]
        DriverContract Driver1Contract { get; set; }

        [JsonPropertyName("driver2_contract")]
        DriverContract Driver2Contract { get; set; }

        [JsonPropertyName("color")]
        string Color { get; set; }

        [JsonPropertyName("default_prequalifying")]
        public bool DefaultPrequalifying { get; set; }
    }
}