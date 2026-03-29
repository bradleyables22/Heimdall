using System.Text.Json;
using System.Text.Json.Serialization;

namespace Heimdall.Server
{
    internal sealed class BoolFromStringConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.True)
                return true;
            if (reader.TokenType == JsonTokenType.False)
                return false;

            if (reader.TokenType == JsonTokenType.Number)
            {
                // Accept 0/1 as well
                if (reader.TryGetInt64(out var n))
                    return n != 0;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString()?.Trim();

                if (s is null) return false;

                if (s.Equals("true", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (s.Equals("false", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (s.Equals("on", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (s.Equals("off", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (s == "1")
                    return true;
                if (s == "0")
                    return false;
            }

            throw new JsonException($"Invalid boolean value (token: {reader.TokenType}).");
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
            => writer.WriteBooleanValue(value);
    }
}
