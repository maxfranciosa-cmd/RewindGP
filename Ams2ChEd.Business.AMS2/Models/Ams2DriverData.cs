using AMS2ChEd.Business.Models.Concrete;
using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.AMS2.Models
{
    public class Ams2DriverData : DriverData 
    {
        [JsonPropertyName("base_helmet_file")]
        public string BaseHelmetFile { get; set; }

        [JsonPropertyName("base_visor_file")]
        public string BaseVisorFile { get; set; }

        [JsonPropertyName("base_helmet_file_90s")]
        public string BaseHelmetFile90s { get; set; }

        [JsonPropertyName("base_helmet_file_80s")]
        public string BaseHelmetFile80s { get; set; }

        [JsonPropertyName("base_visor_file_80s")]
        public string BaseVisorFile80s { get; set; }

        [JsonPropertyName("base_helmet_file_70s")]
        public string BaseHelmetFile70s { get; set; }

        [JsonPropertyName("base_visor_file_70s")]
        public string BaseVisorFile70s { get; set; }

    }
}
