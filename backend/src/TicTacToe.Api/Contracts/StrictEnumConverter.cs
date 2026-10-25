using System.Text.Json;
using System.Text.Json.Serialization;

namespace TicTacToe.Api.Contracts;

/// <summary>
/// Serializes enums as their names and reads only exact names ("X", "TwoPlayer").
/// The built-in JsonStringEnumConverter also accepts other casing and comma-separated
/// combinations such as "TwoPlayer, Computer", which would silently become a different value.
/// </summary>
public sealed class StrictEnumConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(NameConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class NameConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        private static readonly string[] Names = Enum.GetNames<TEnum>();

        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var name = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            if (name is not null && Names.Contains(name, StringComparer.Ordinal))
            {
                return Enum.Parse<TEnum>(name);
            }

            throw new JsonException($"Expected one of: {string.Join(", ", Names)}.");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}
