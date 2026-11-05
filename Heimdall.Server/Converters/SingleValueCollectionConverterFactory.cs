using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heimdall.Server;

internal sealed class SingleValueCollectionConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => IsSupportedCollection(typeToConvert);

    internal static bool IsSupportedCollection(Type type) => GetElementType(type) is not null;

    private static Type? GetElementType(Type type)
    {
        // byte[] has a base64 string contract in System.Text.Json.
        if (type.IsSZArray && type != typeof(byte[]))
            return type.GetElementType();

        if (!type.IsGenericType)
            return null;

        var genericType = type.GetGenericTypeDefinition();
        return genericType == typeof(List<>) ||
            genericType == typeof(IEnumerable<>) ||
            genericType == typeof(IReadOnlyCollection<>) ||
            genericType == typeof(IReadOnlyList<>) ||
            genericType == typeof(ICollection<>) ||
            genericType == typeof(IList<>)
                ? type.GetGenericArguments()[0]
                : null;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var converterType = typeof(SingleValueCollectionConverter<,>)
            .MakeGenericType(typeToConvert, GetElementType(typeToConvert)!);
        var writeOptions = new JsonSerializerOptions(options);
        writeOptions.Converters.Remove(this);
        return (JsonConverter)Activator.CreateInstance(converterType, writeOptions)!;
    }

    private sealed class SingleValueCollectionConverter<TCollection, TElement>(JsonSerializerOptions writeOptions)
        : JsonConverter<TCollection>
    {
        public override TCollection? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var values = new List<TElement>();
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    values.Add(JsonSerializer.Deserialize<TElement>(ref reader, options)!);

                if (reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException("Expected the end of the collection payload.");
            }
            else
            {
                values.Add(JsonSerializer.Deserialize<TElement>(ref reader, options)!);
            }

            return (TCollection)(typeof(TCollection).IsArray ? (object)values.ToArray() : values);
        }

        public override void Write(Utf8JsonWriter writer, TCollection value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value, writeOptions);
    }
}
