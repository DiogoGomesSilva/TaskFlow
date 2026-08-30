using System.Text.Json;
using System.Text.Json.Serialization;
using TaskFlow.Api.Contracts.Common;

namespace TaskFlow.Api.Infrastructure.Serialization;

public sealed class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType &&
        typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter CreateConverter(
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(OptionalJsonConverter<>).MakeGenericType(valueType);

        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
    {
        public override bool HandleNull => true;

        public override Optional<T> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return Optional<T>.Specified(default!);
            }

            var value = JsonSerializer.Deserialize<T>(ref reader, options);
            return Optional<T>.Specified(value!);
        }

        public override void Write(
            Utf8JsonWriter writer,
            Optional<T> value,
            JsonSerializerOptions options)
        {
            if (!value.IsSpecified)
            {
                writer.WriteNullValue();
                return;
            }

            JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}
