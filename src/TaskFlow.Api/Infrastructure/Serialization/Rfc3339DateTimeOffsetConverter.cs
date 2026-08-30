using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskFlow.Api.Infrastructure.Serialization;

/// <summary>
/// Serializa <see cref="DateTimeOffset"/> em UTC no formato RFC 3339 com
/// milissegundos e sufixo <c>Z</c> (ex.: <c>2026-08-30T13:50:44.173Z</c>),
/// alinhado aos exemplos do contrato. Aplica-se também a
/// <see cref="DateTimeOffset"/>? via o suporte a anuláveis do System.Text.Json.
/// </summary>
public sealed class Rfc3339DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTimeOffset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var text = reader.GetString();

        if (string.IsNullOrWhiteSpace(text) ||
            !DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var value))
        {
            throw new JsonException("Valor de data/hora inválido.");
        }

        return value.ToUniversalTime();
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTimeOffset value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(
            value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture));
    }
}
