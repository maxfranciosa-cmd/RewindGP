using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models.Concrete
{
    /// <summary>
    /// A driver's faceless portrait, drawn when there is no picture: skin tone, haircut and facial
    /// hair, each style with its colour (see AvatarComposer for the styles). A driver without one
    /// gets one picked from their id.
    /// </summary>
    public class GenericAvatar
    {
        /// <summary>Skin colour, "#RRGGBB".</summary>
        [JsonPropertyName("skin")]
        public string Skin { get; set; }

        /// <summary>bald, buzz, short, side_part, centre_part, longer_top, medium, long, curly, mullet, receding.</summary>
        [JsonPropertyName("hair")]
        public string Hair { get; set; }

        [JsonPropertyName("hair_colour")]
        public string HairColour { get; set; }

        /// <summary>none, stubble, moustache, goatee, full_beard, sideburns.</summary>
        [JsonPropertyName("facial_hair")]
        public string FacialHair { get; set; }

        /// <summary>Facial hair colour; the hair colour when not given.</summary>
        [JsonPropertyName("facial_hair_colour")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string FacialHairColour { get; set; }
    }
}
