using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.AMS2.Models
{
    /// <summary>
    /// The template kit of one or more AMS2 car models (LiveryKits/&lt;kit&gt;/kit.json): the
    /// colour-coded body templates a generic livery is recoloured from, where each template's
    /// numbers go, the generic livery XML and the driver outfit set.
    /// </summary>
    public class LiveryKit
    {
        /// <summary>The Ams2Car values the kit fits.</summary>
        [JsonPropertyName("car_models")]
        public List<string> CarModels { get; set; }

        /// <summary>Generic USER_OVERRIDES file, relative to the kit folder.</summary>
        [JsonPropertyName("livery_xml")]
        public string LiveryXml { get; set; }

        /// <summary>Folder name under LiveryKits/outfits holding the colour-coded suit parts.</summary>
        [JsonPropertyName("outfit_set")]
        public string OutfitSet { get; set; }

        /// <summary>
        /// The LIVERY_OVERRIDE textures that take the generated body. A model can read some of its
        /// parts from a second texture with the body's own layout (formula_ultimate_2024's
        /// WINGS). Null: BODY only.
        /// </summary>
        [JsonPropertyName("body_textures")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> BodyTextures { get; set; }

        [JsonPropertyName("templates")]
        public List<KitTemplate> Templates { get; set; }

        /// <summary>The kit's own folder, set by the loader.</summary>
        [JsonIgnore]
        public string Folder { get; set; }

        public KitTemplate FindTemplate(string id) =>
            Templates?.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public class KitTemplate
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Colour-coded body PNG, relative to the kit folder.</summary>
        [JsonPropertyName("body")]
        public string Body { get; set; }

        /// <summary>Where this template's design leaves room for the car numbers.</summary>
        [JsonPropertyName("numbers_placements")]
        public List<KitNumbersPlacement> NumbersPlacements { get; set; }
    }

    /// <summary>
    /// A number plate on a kit template. NumbersTexture is unused: the sheet comes from the team's
    /// number style, resampled to SheetSize (digits are drawn at the sheet's own size, so the size
    /// sets how big the number is on the car).
    /// </summary>
    public class KitNumbersPlacement : NumbersPlacement
    {
        /// <summary>[width, height] of the ten-digit sheet for this plate.</summary>
        [JsonPropertyName("sheet_size")]
        public int[] SheetSize { get; set; }

        /// <summary>
        /// Optional palette key ("green") whose colour fills the plate before the digits are drawn.
        /// Null: no fill, the template's own paint shows behind the number.
        /// </summary>
        [JsonPropertyName("fill_key")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string FillKey { get; set; }

        /// <summary>
        /// Optional palette key the digits take on this plate (a nose plate on white paint wants
        /// different digits from a wing plate on red). Null: the team's number_fill.
        /// </summary>
        [JsonPropertyName("number_fill_key")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string NumberFillKey { get; set; }

        /// <summary>As NumberFillKey, for the outline of a two-colour number style.</summary>
        [JsonPropertyName("number_outline_key")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string NumberOutlineKey { get; set; }
    }

    /// <summary>
    /// A driver outfit set (LiveryKits/outfits/&lt;set&gt;/outfit.json), keyed by OUTFIT_OVERRIDE
    /// texture name (BODY_DIFF, ARMS_DIFF, GLOVES_DIFF, LEGS_DIFF, SEATBELTS_DIFF).
    /// </summary>
    public class OutfitSet
    {
        [JsonPropertyName("parts")]
        public Dictionary<string, OutfitPart> Parts { get; set; }

        /// <summary>The set's own folder, set by the loader.</summary>
        [JsonIgnore]
        public string Folder { get; set; }
    }

    /// <summary>
    /// One suit texture: a greyscale fabric shading multiplied by one palette colour, so the folds
    /// and seams survive the recolour.
    /// </summary>
    public class OutfitPart
    {
        /// <summary>Greyscale shading PNG, relative to the set folder.</summary>
        [JsonPropertyName("shading")]
        public string Shading { get; set; }

        /// <summary>Suit palette key that colours it, or "harness" for the suit's harness tint.</summary>
        [JsonPropertyName("key")]
        public string Key { get; set; }
    }

    /// <summary>A car-number look shared by every kit (LiveryKits/number_styles.json).</summary>
    public class NumberStyle
    {
        /// <summary>Ten-digit master strip, relative to the LiveryKits folder.</summary>
        [JsonPropertyName("file")]
        public string File { get; set; }

        /// <summary>
        /// False: white digits tinted with number_fill. True: fill painted in the red key and
        /// outline in the green key, recoloured with number_fill and number_outline.
        /// </summary>
        [JsonPropertyName("two_colour")]
        public bool TwoColour { get; set; }
    }
}
