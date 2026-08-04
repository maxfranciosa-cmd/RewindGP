using AMS2ChEd.Business.Models;
using AMS2ChEd.Business.Models.Concrete;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.AMS2.Models
{
    public class Ams2Season : Season
    {
        [JsonPropertyName("ams2Class")]
        public string Ams2Class { get; set; }
    }

    public class Ams2TeamEntry : TeamEntry
    {

        [JsonPropertyName("ams2car")]
        public string Ams2Car { get; set; }

        [JsonPropertyName("ams2carPerformanceMalus")]
        public Dictionary<string, double> Ams2CarPerformanceMalus { get; set; }

        /// <summary>
        /// Optional performance malus for the second car. When null, driver 2 uses
        /// Ams2CarPerformanceMalus (backwards compatible with existing saves/seasons).
        /// </summary>
        [JsonPropertyName("ams2carPerformanceMalusDriver2")]
        public Dictionary<string, double> Ams2CarPerformanceMalusDriver2 { get; set; }

        /// <summary>
        /// Returns the performance malus that applies to the given driver slot (1 or 2),
        /// falling back to the primary car's malus if no second car malus is defined.
        /// </summary>
        public Dictionary<string, double> GetAms2CarPerformanceMalus(int driverNumber)
        {
            return driverNumber == 2 && Ams2CarPerformanceMalusDriver2 != null
                ? Ams2CarPerformanceMalusDriver2
                : Ams2CarPerformanceMalus;
        }

        /// <summary>
        /// Convenience accessors for the three car-performance scalars on the primary
        /// (driver 1 / team-level default) malus dictionary, for inline grid editing.
        /// </summary>
        [JsonIgnore]
        public double PowerScalar
        {
            get => Ams2CarPerformanceMalus != null && Ams2CarPerformanceMalus.TryGetValue("power_scalar", out var v) ? v : 0.0;
            set
            {
                Ams2CarPerformanceMalus ??= new Dictionary<string, double>();
                Ams2CarPerformanceMalus["power_scalar"] = value;
            }
        }

        [JsonIgnore]
        public double WeightScalar
        {
            get => Ams2CarPerformanceMalus != null && Ams2CarPerformanceMalus.TryGetValue("weight_scalar", out var v) ? v : 0.0;
            set
            {
                Ams2CarPerformanceMalus ??= new Dictionary<string, double>();
                Ams2CarPerformanceMalus["weight_scalar"] = value;
            }
        }

        [JsonIgnore]
        public double DragScalar
        {
            get => Ams2CarPerformanceMalus != null && Ams2CarPerformanceMalus.TryGetValue("drag_scalar", out var v) ? v : 0.0;
            set
            {
                Ams2CarPerformanceMalus ??= new Dictionary<string, double>();
                Ams2CarPerformanceMalus["drag_scalar"] = value;
            }
        }

        /// <summary>
        /// True if either driver's malus dictionary uses one of the physical performance
        /// scalars (power/weight/drag) rather than (or alongside) qualifying_skill.
        /// </summary>
        [JsonIgnore]
        public bool HasPerformanceScalarMalus =>
            ContainsScalarKey(Ams2CarPerformanceMalus) || ContainsScalarKey(Ams2CarPerformanceMalusDriver2);

        private static bool ContainsScalarKey(Dictionary<string, double> malus) =>
            malus != null && (malus.ContainsKey("power_scalar") || malus.ContainsKey("weight_scalar") || malus.ContainsKey("drag_scalar"));

        [JsonPropertyName("base_livery_driver1")]
        public string BaseLiveryDriver1 { get; set; }

        [JsonPropertyName("base_livery_driver2")]
        public string BaseLiveryDriver2 { get; set; }

        [JsonPropertyName("helmet_sponsors")]
        public string HelmetSponsors { get; set; }

        [JsonPropertyName("visor_sponsors")]
        public string VisorSponsors { get; set; }

        [JsonPropertyName("drivers_specific_helmet")]
        public Dictionary<string, string> DriversSpecificHelmet { get; set; }

        [JsonPropertyName("numbers_placements")]
        public IEnumerable<NumbersPlacement> NumbersPlacements { get; set; }

        [JsonPropertyName("livery_overrides")]
        public IEnumerable<LiveryOverride> LiveryOverrides { get; set; }

        [JsonPropertyName("livery_preview")]
        public string LiveryPreview { get; set; }

        [JsonPropertyName("livery_xml")]
        public string LiveryXml { get; set; }

    }

    public enum NumberRotation
    {
        Deg0 = 0,
        Deg90 = 90,
        Deg180 = 180,
        Deg270 = 270
    }

    public class NumberRotationConverter : JsonConverter<NumberRotation>
    {
        public override NumberRotation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString();
            if (Enum.TryParse<NumberRotation>(value, true, out var result))
            {
                return result;
            }
            throw new JsonException($"Unable to convert \"{value}\" to NumberRotation enum.");
        }

        public override void Write(Utf8JsonWriter writer, NumberRotation value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    public class NumbersPlacement
    {
        [JsonPropertyName("numbers_texture")]
        public string NumbersTexture { get; set; }

        [JsonPropertyName("number_plate_width")]
        public int NumberPlateWidth { get; set; }

        [JsonPropertyName("starting_point")]
        public Point StartingPoint { get; set; }

        [JsonPropertyName("rotation")]
        [JsonConverter(typeof(NumberRotationConverter))]
        public NumberRotation NumberRotation { get; set; }

        [JsonPropertyName("fill_color")]
        public string FillColor { get; set; }
    }

    public class LiveryOverride
    {
        [JsonPropertyName("race_id")]
        public int RaceId { get; set; }

        [JsonPropertyName("driver1_livery")]
        public string Driver1Livery { get; set; }

        [JsonPropertyName("driver2_livery")]
        public string Driver2Livery { get; set; }

        [JsonPropertyName("helmet_sponsors")]
        public string HelmetSponsors { get; set; }

        [JsonPropertyName("visor_sponsors")]
        public string VisorSponsors { get; set; }

        [JsonPropertyName("drivers_specific_helmet")]
        public Dictionary<string, string> DriversSpecificHelmet { get; set; }

        [JsonPropertyName("numbers_placements")]
        public IEnumerable<NumbersPlacement> NumbersPlacements { get; set; }

        [JsonPropertyName("livery_preview")]
        public string LiveryPreview { get; set; }

    }
}