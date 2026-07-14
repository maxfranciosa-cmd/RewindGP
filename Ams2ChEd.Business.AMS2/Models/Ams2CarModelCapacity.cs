using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.AMS2.Models
{
    public class CarModelCapacitiesFile
    {
        [JsonPropertyName("classes")]
        public List<Ams2ClassCapacity> Classes { get; set; }
    }

    public class Ams2ClassCapacity
    {
        [JsonPropertyName("class")]
        public string Class { get; set; }

        [JsonPropertyName("models")]
        public List<Ams2ModelCapacity> Models { get; set; }
    }

    public class Ams2ModelCapacity
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("slots")]
        public int Slots { get; set; }
    }
}
