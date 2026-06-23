using System.Text.Json;
using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models.Concrete
{
    public class DriverReputationConverter : JsonConverter<DriverReputation>
    {
        public override DriverReputation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString();
            if (Enum.TryParse<DriverReputation>(value, true, out var result))
            {
                return result;
            }
            throw new JsonException($"Unable to convert \"{value}\" to DriverReputation enum.");
        }

        public override void Write(Utf8JsonWriter writer, DriverReputation value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    public class TeamReputationConverter : JsonConverter<TeamReputation>
    {
        public override TeamReputation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString();
            if (Enum.TryParse<TeamReputation>(value, true, out var result))
            {
                return result;
            }
            throw new JsonException($"Unable to convert \"{value}\" to TeamReputation enum.");
        }

        public override void Write(Utf8JsonWriter writer, TeamReputation value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}