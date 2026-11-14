using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models.Concrete
{
    /// <summary>
    /// A junior academy of the modern driver market: its young members get better fits at its
    /// parent and sister teams (AcademyMembership). Gameplay data: it comes from the baked season.
    /// </summary>
    public class Academy
    {
        /// <summary>Stable across seasons (e.g. "red_bull"): the player's academy_id refers to it.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("parent_teams")]
        public List<string> ParentTeams { get; set; }

        [JsonPropertyName("sister_teams")]
        public List<string> SisterTeams { get; set; }

        /// <summary>The AI members. The player is never listed here (see PlayerData.AcademyId).</summary>
        [JsonPropertyName("drivers")]
        public List<string> Drivers { get; set; }
    }
}
