using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models
{
    public interface IAccolades
    {
        int Wins { get; set; }
        int Podiums { get; set; }
        int PolePositions { get; set; }
        List<int> Championships { get; set; }
    }

    public class Accolades : IAccolades
    {
        [JsonPropertyName("wins")]
        public int Wins { get; set; }

        [JsonPropertyName("podiums")]
        public int Podiums { get; set; }

        [JsonPropertyName("polePositions")]
        public int PolePositions { get; set; }

        [JsonPropertyName("championships")]
        public List<int> Championships { get; set; } = new();
    }

    public class HistoricalAccolades
    {
        [JsonPropertyName("driverAccolades")]
        public Dictionary<string, Accolades> DriverAccolades { get; set; }

        [JsonPropertyName("teamsAccolades")]
        public Dictionary<string, Accolades> TeamsAccolades { get; set; }
    }
}
