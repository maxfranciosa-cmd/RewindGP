using System.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AMS2ChEd.Business.Models.Concrete
{

    public class GenericEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString();
            if (Enum.TryParse<T>(value, true, out var result))
            {
                return result;
            }
            throw new JsonException($"Unable to convert \"{value}\" to {typeof(T).Name} enum.");
        }
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}