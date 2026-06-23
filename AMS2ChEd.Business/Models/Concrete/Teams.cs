using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models.Concrete
{
    public class TeamsDatabase
    {
        [JsonPropertyName("teams")]
        public List<Team> Teams { get; set; }
    }

    public class Team
    {
        [JsonPropertyName("team_id")]
        public string TeamId { get; set; }

        [JsonPropertyName("team_name")]
        public string TeamName { get; set; }
    }
}