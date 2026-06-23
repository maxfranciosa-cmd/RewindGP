using AMS2ChEd.Business.Models.Concrete;
using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models.Concrete
{
    public class Season : ISeason
    {
        public int Year { get; set; }
        public int? OriginalYear { get; set; }
        public Dictionary<string, int> PointsSystem { get; set; }
        public double? PointsForFastestLap { get; set; }
        public IEnumerable<Race> Races { get; set; }
        public IEnumerable<ITeamEntry> Teams { get; set; }
        public IEnumerable<Absence> Absences { get; set; }
        public int? MaxDriversPerRace { get; set; }
        public string? Slug { get; set; }
    }

    public class TeamEntry : ITeamEntry
    {
        public string TeamId { get; set; }
        public string TeamName { get; set; }
        public string TeamPrincipal { get; set; }
        public TeamReputation Reputation { get; set; }
        public DriverContract Driver1Contract { get; set; }
        public DriverContract Driver2Contract { get; set; }

        public string Color { get; set; }
        public bool DefaultPrequalifying { get; set; }
    }

    public class Race
    {
        [JsonPropertyName("race_id")]
        public int RaceId { get; set; }

        [JsonPropertyName("race_name")]
        public string RaceName { get; set; }

        [JsonPropertyName("race_short_name")]
        public string RaceShortName { get; set; }

        [JsonPropertyName("race_date")]
        public string RaceDate { get; set; }

        [JsonPropertyName("circuit")]
        public string Circuit { get; set; }

        [JsonPropertyName("cover_picture")]
        public string CoverPictureUrl { get; set; }

        [JsonPropertyName("points_system")]
        public Dictionary<string, int> PointsSystem { get; set; }

        [JsonPropertyName("points_for_fastest_lap")]
        public double? PointsForFastestLap { get; set; }

        [JsonPropertyName("ignore_for_positions_tally")]
        public bool IgnoreForPositionsTally { get; set; }
    }

    public class DriverContract
    {
        [JsonPropertyName("driver_id")]
        public string DriverId { get; set; }

        [JsonPropertyName("races")]
        public int Races { get; set; }

        [JsonPropertyName("drivernumber")]
        public int DriverNumber { get; set; }
    }

    public class Absence
    {
        [JsonPropertyName("race_id")]
        public int RaceId { get; set; }

        [JsonPropertyName("teamid")]
        public string TeamId { get; set; }

        [JsonPropertyName("driver_out")]
        public string DriverOut { get; set; }

        [JsonPropertyName("driver_in")]
        public string DriverIn { get; set; }

        [JsonPropertyName("chainedAbsence")]
        public Absence ChainedAbsence { get; set; }
    }
}