using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models.Concrete
{
    /// <summary>
    /// A rookie FP1 outing of the modern driver market (PracticeOutings): an academy member without
    /// a race seat takes a race driver's car for first practice, measured against the team's first
    /// driver. Kept on the save, every season of it, like the Casualties.
    /// </summary>
    public class PracticeOuting
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("race_id")]
        public int RaceId { get; set; }

        [JsonPropertyName("team_id")]
        public string TeamId { get; set; }

        [JsonPropertyName("driver_id")]
        public string DriverId { get; set; }

        /// <summary>The replaced race driver (the car the outing driver takes).</summary>
        [JsonPropertyName("replaced_driver_id")]
        public string ReplacedDriverId { get; set; }

        /// <summary>The benchmark: the team's first driver at this race.</summary>
        [JsonPropertyName("benchmark_driver_id")]
        public string BenchmarkDriverId { get; set; }

        /// <summary>Performance score 0-3 (PracticeOutings.Score), null until the outing has happened.</summary>
        [JsonPropertyName("score")]
        public int? Score { get; set; }
    }
}
