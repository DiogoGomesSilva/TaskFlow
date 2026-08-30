using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskFlow.Api.Infrastructure.Serialization;

public sealed class StrictSnakeCaseLowerEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var converterType = typeof(StrictSnakeCaseLowerEnumConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class StrictSnakeCaseLowerEnumConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        private static readonly IReadOnlyDictionary<string, TEnum> ValuesByName =
            Enum.GetValues<TEnum>().ToDictionary(
                value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()),
                value => value,
                StringComparer.Ordinal);

        private static readonly IReadOnlyDictionary<TEnum, string> NamesByValue =
            ValuesByName.ToDictionary(pair => pair.Value, pair => pair.Key);

        public override TEnum Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String ||
                !ValuesByName.TryGetValue(reader.GetString()!, out var value))
            {
                throw new JsonException($"The value is not valid for {typeof(TEnum).Name}.");
            }

            return value;
        }

        public override void Write(
            Utf8JsonWriter writer,
            TEnum value,
            JsonSerializerOptions options)
        {
            if (!NamesByValue.TryGetValue(value, out var name))
            {
                throw new JsonException($"The value is not valid for {typeof(TEnum).Name}.");
            }

            writer.WriteStringValue(name);
        }
    }
}
