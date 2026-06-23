namespace AMS2ChEd.Business.Helpers
{
    public static class DeepCloneExtension
    {
        public static T DeepClone<T>(this T source)
        {
            string json = System.Text.Json.JsonSerializer.Serialize(source, DefaultJsonSerializerOptions.Instance);
            return System.Text.Json.JsonSerializer.Deserialize<T>(json, DefaultJsonSerializerOptions.Instance);
        }
    }
}
