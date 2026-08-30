using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Microsoft.OpenApi.YamlReader;
using Yaml2JsonNode;

namespace TaskFlow.ContractTests;

internal sealed class OpenApiResponseValidator
{
    private const string OpenApiFileName = "openapi.yaml";

    private static readonly JsonSerializerOptions DiagnosticJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly JsonNode _rawDocument;
    private readonly EvaluationOptions _evaluationOptions = new()
    {
        OutputFormat = OutputFormat.List,
        RequireFormatValidation = true
    };

    public OpenApiResponseValidator()
    {
        var openApiPath = Path.Combine(AppContext.BaseDirectory, OpenApiFileName);
        var yaml = File.ReadAllText(openApiPath);

        _rawDocument = YamlSerializer.Deserialize<JsonNode>(yaml)
            ?? throw new InvalidOperationException("Não foi possível carregar openapi.yaml.");
        var openApiVersion = _rawDocument["openapi"]?.GetValue<string>();
        if (openApiVersion != "3.0.3")
        {
            throw new InvalidOperationException(
                $"Versão OpenAPI inesperada: {openApiVersion}.");
        }
    }

    public static async Task AssertDocumentIsValidAsync()
    {
        var openApiPath = Path.Combine(AppContext.BaseDirectory, OpenApiFileName);
        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();

        await using var stream = File.OpenRead(openApiPath);
        var result = await OpenApiDocument.LoadAsync(
            stream,
            "yaml",
            settings,
            CancellationToken.None);

        Assert.NotNull(result.Document);
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Diagnostic!.Errors);
    }

    public async Task AssertResponseAsync(
        string pathTemplate,
        HttpMethod method,
        HttpResponseMessage response)
    {
        var statusCode = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        var mediaType = response.Content.Headers.ContentType?.MediaType;

        Assert.False(
            string.IsNullOrWhiteSpace(mediaType),
            $"A resposta {statusCode} não informou Content-Type.");

        var schemaNode = FindResponseSchema(
            pathTemplate,
            method,
            statusCode,
            mediaType!);
        var resolvedSchema = ResolveSchemaReferences(schemaNode, []);
        var schemaObject = Assert.IsType<JsonObject>(resolvedSchema);
        schemaObject["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        var schema = JsonSchema.FromText(schemaObject.ToJsonString());

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var responseDocument = await JsonDocument.ParseAsync(responseStream);

        var result = schema.Evaluate(responseDocument.RootElement, _evaluationOptions);

        Assert.True(
            result.IsValid,
            $"Response incompatível com {method.Method} {pathTemplate} {statusCode}:\n" +
            JsonSerializer.Serialize(result, DiagnosticJsonOptions));
    }

    public async Task AssertEmptyResponseAsync(
        string pathTemplate,
        HttpMethod method,
        HttpResponseMessage response)
    {
        var statusCode = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        var responseNode = FindResponse(pathTemplate, method, statusCode);

        Assert.Null(responseNode["content"]);
        Assert.Null(response.Content.Headers.ContentType);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    private JsonNode FindResponseSchema(
        string pathTemplate,
        HttpMethod method,
        string statusCode,
        string mediaType)
    {
        var responseNode = FindResponse(pathTemplate, method, statusCode);

        var contentNode = Assert.IsType<JsonObject>(responseNode["content"]);
        var mediaTypeNode = Assert.IsType<JsonObject>(contentNode[mediaType]);
        return Assert.IsAssignableFrom<JsonNode>(mediaTypeNode["schema"]);
    }

    private JsonObject FindResponse(
        string pathTemplate,
        HttpMethod method,
        string statusCode)
    {
        var paths = Assert.IsType<JsonObject>(_rawDocument["paths"]);
        var path = Assert.IsType<JsonObject>(paths[pathTemplate]);
        var methodName = method.Method.ToLowerInvariant();
        var operation = Assert.IsType<JsonObject>(path[methodName]);
        var responses = Assert.IsType<JsonObject>(operation["responses"]);
        var response = Assert.IsType<JsonObject>(responses[statusCode]);

        if (response["$ref"] is not JsonValue referenceValue)
        {
            return response;
        }

        var reference = referenceValue.GetValue<string>();
        Assert.StartsWith("#/", reference);

        var resolved = ResolveLocalReference(reference[1..]);
        Assert.NotNull(resolved);
        return Assert.IsType<JsonObject>(resolved);
    }

    private JsonNode? ResolveLocalReference(string pointer)
    {
        JsonNode? current = _rawDocument;

        foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var propertyName = segment
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            current = current?[propertyName];
        }

        return current;
    }

    private JsonNode ResolveSchemaReferences(
        JsonNode schemaNode,
        HashSet<string> referenceChain)
    {
        if (schemaNode is JsonObject schemaObject &&
            schemaObject["$ref"] is JsonValue referenceValue)
        {
            var reference = referenceValue.GetValue<string>();
            if (!reference.StartsWith("#/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Somente referências locais são suportadas: {reference}");
            }

            if (schemaObject.Count != 1)
            {
                throw new InvalidOperationException(
                    $"$ref com propriedades irmãs ainda não é suportado: {reference}");
            }

            if (!referenceChain.Add(reference))
            {
                throw new InvalidOperationException(
                    $"Referência circular encontrada: {reference}");
            }

            var referencedNode = ResolveLocalReference(reference[1..])
                ?? throw new InvalidOperationException(
                    $"Referência OpenAPI não resolvida: {reference}");
            var resolved = ResolveSchemaReferences(referencedNode, referenceChain);
            referenceChain.Remove(reference);
            return resolved;
        }

        if (schemaNode is JsonObject objectNode)
        {
            var resolvedObject = new JsonObject();
            var nullable = objectNode["nullable"] is JsonValue nullableValue &&
                nullableValue.TryGetValue(out bool nullableFlag) &&
                nullableFlag;

            foreach (var property in objectNode)
            {
                // "nullable" (OpenAPI 3.0) não existe em JSON Schema 2020-12;
                // é traduzido abaixo para uma união de tipos com "null".
                if (property.Key == "nullable")
                {
                    continue;
                }

                resolvedObject[property.Key] = property.Value is null
                    ? null
                    : ResolveSchemaReferences(property.Value, referenceChain);
            }

            if (nullable &&
                resolvedObject["type"] is JsonValue typeValue &&
                typeValue.TryGetValue(out string? typeName) &&
                typeName is not null)
            {
                resolvedObject["type"] = new JsonArray(typeName, "null");
            }

            return resolvedObject;
        }

        if (schemaNode is JsonArray arrayNode)
        {
            var resolvedArray = new JsonArray();
            foreach (var item in arrayNode)
            {
                resolvedArray.Add(item is null
                    ? null
                    : ResolveSchemaReferences(item, referenceChain));
            }

            return resolvedArray;
        }

        return schemaNode.DeepClone();
    }
}
