using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.AMS2.Models
{
    /// <summary>
    /// A team's livery generated from its car model's template kit: the kit template recoloured
    /// with the palette, with the car numbers stamped on. Used when no season pack supplies the
    /// team's artwork.
    /// </summary>
    public class GenericLivery
    {
        /// <summary>Id of a template in the kit of the team's car model.</summary>
        [JsonPropertyName("template")]
        public string Template { get; set; }

        /// <summary>
        /// Key colour name (red, green, blue, yellow, magenta, cyan) to "#RRGGBB". A key left
        /// out keeps its template colour.
        /// </summary>
        [JsonPropertyName("palette")]
        public Dictionary<string, string> Palette { get; set; }

        /// <summary>Id of a number style in the kit.</summary>
        [JsonPropertyName("number_style")]
        public string NumberStyle { get; set; }

        [JsonPropertyName("number_fill")]
        public string NumberFill { get; set; }

        /// <summary>Outline colour, for two-colour number styles only.</summary>
        [JsonPropertyName("number_outline")]
        public string NumberOutline { get; set; }

        /// <summary>Id of a tyre set in the app's tyre library. Null keeps the car's stock tyres.</summary>
        [JsonPropertyName("tyres")]
        public string Tyres { get; set; }

        /// <summary>Null: the suit takes the car palette.</summary>
        [JsonPropertyName("suit")]
        public GenericSuit Suit { get; set; }
    }

    public class GenericSuit
    {
        /// <summary>Recolours torso, arms, gloves and legs, keyed like GenericLivery.Palette.</summary>
        [JsonPropertyName("palette")]
        public Dictionary<string, string> Palette { get; set; }

        /// <summary>"#RRGGBB" tint for the harness.</summary>
        [JsonPropertyName("harness")]
        public string Harness { get; set; }
    }

    /// <summary>
    /// A driver's helmet generated from one of the generic designs, recoloured. Belongs to the
    /// driver, so it is the same in every season and every helmet era.
    /// </summary>
    public class GenericHelmet
    {
        /// <summary>Design name without the era prefix ("06_stripes").</summary>
        [JsonPropertyName("design")]
        public string Design { get; set; }

        /// <summary>Key colour name to "#RRGGBB", as GenericLivery.Palette.</summary>
        [JsonPropertyName("palette")]
        public Dictionary<string, string> Palette { get; set; }
    }
}
