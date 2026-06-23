using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace AMS2ChEd.Business.Helpers
{
    public class DefaultJsonSerializerOptions
    {
        public static JsonSerializerOptions Instance => new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            TypeInfoResolver = new AutoDiscoveryResolver()
        };
    }

    public class AutoDiscoveryResolver : DefaultJsonTypeInfoResolver
    {
        public AutoDiscoveryResolver()
        {
            Modifiers.Add(AddDiscoveredTypes);
            Modifiers.Add(CopyInterfacePropertyAttributes);
        }

        private static void AddDiscoveredTypes(JsonTypeInfo typeInfo)
        {
            // Only process interface types
            if (!typeInfo.Type.IsInterface)
                return;

            // Only process types from your namespaces
            if (!(typeInfo.Type.Namespace?.StartsWith("AMS2ChEd") == true ||
                  typeInfo.Type.Namespace?.StartsWith("Ams2ChEd") == true))
                return;

            // Find all implementations
            var implementations = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                })
                .Where(t => !t.IsInterface &&
                           !t.IsAbstract &&
                           typeInfo.Type.IsAssignableFrom(t))
                .ToArray();

            if (!implementations.Any())
                return;

            // Create or update polymorphism options
            typeInfo.PolymorphismOptions ??= new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor,
                IgnoreUnrecognizedTypeDiscriminators = true
            };

            // Add each discovered implementation
            foreach (var implType in implementations)
            {
                // Check if not already added
                if (!typeInfo.PolymorphismOptions.DerivedTypes.Any(dt => dt.DerivedType == implType))
                {
                    typeInfo.PolymorphismOptions.DerivedTypes.Add(
                        new JsonDerivedType(implType, implType.FullName));
                }
            }
        }
        private static void CopyInterfacePropertyAttributes(JsonTypeInfo typeInfo)
        {
            // Only process concrete types (not interfaces)
            if (typeInfo.Type.IsInterface || typeInfo.Type.IsAbstract)
                return;

            // Get all interfaces implemented by this type
            var interfaces = typeInfo.Type.GetInterfaces();

            foreach (var @interface in interfaces)
            {
                // Only process interfaces from your namespaces
                if (!(@interface.Namespace?.StartsWith("AMS2ChEd") == true ||
                      @interface.Namespace?.StartsWith("Ams2ChEd") == true))
                    continue;

                foreach (var interfaceProperty in @interface.GetProperties())
                {
                    // Check if interface property has JsonPropertyName attribute
                    var jsonPropertyNameAttr = interfaceProperty
                        .GetCustomAttributes(typeof(JsonPropertyNameAttribute), true)
                        .FirstOrDefault() as JsonPropertyNameAttribute;

                    if (jsonPropertyNameAttr == null)
                        continue;

                    // Find the corresponding property in the concrete type's JsonTypeInfo
                    var propertyInfo = typeInfo.Properties
                        .FirstOrDefault(p => p.Name == interfaceProperty.Name);

                    if (propertyInfo != null)
                    {
                        // Apply the JSON property name from the interface
                        propertyInfo.Name = jsonPropertyNameAttr.Name;
                    }
                }
            }
        }
    }
}
