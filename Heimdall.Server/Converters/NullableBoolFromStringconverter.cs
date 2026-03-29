using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heimdall.Server
{
    internal sealed class NullableBoolFromStringConverter : JsonConverter<bool?>
    {
        public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            // Reuse same logic as non-nullable
            var inner = new BoolFromStringConverter();
            return inner.Read(ref reader, typeof(bool), options);
        }

        public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteBooleanValue(value.Value);
        }
    }
}
