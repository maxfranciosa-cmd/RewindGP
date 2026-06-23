using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models.Concrete
{
    public class PositionsTallyConverter : JsonConverter<PositionsTally>
    {
        public override PositionsTally Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            if (BigInteger.TryParse(value, out var result))
            {
                return new PositionsTally(result);
            }
            throw new JsonException($"Unable to convert \"{value}\" to PositionsTally.");
        }

        public override void Write(Utf8JsonWriter writer, PositionsTally value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
