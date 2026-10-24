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

        /// <summary>
        /// Baked data only: the helmet generated when no season pack or save supplies the
        /// driver's own. Null (and absent from the JSON) in packs and saves made before it existed.
        /// </summary>
        [JsonPropertyName("generic_helmet")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GenericHelmet GenericHelmet { get; set; }

    }
}
