namespace CodeSnifferDog.Server.Services.ProjectExecution;

using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed class InferenceProviderOptions
{
    public const string SectionName = "Inference";

    public string Provider { get; init; } = "openai";

    public int? RequestTimeoutSeconds { get; init; }

    public OpenAIInferenceProviderOptions OpenAI { get; init; } = new();

    public AzureOpenAIInferenceProviderOptions AzureOpenAI { get; init; } = new();

    public OpenAICompatibleInferenceProviderOptions OpenAICompatible { get; init; } = new();
}

public sealed class OpenAIInferenceProviderOptions
{
    public string? ApiKey { get; init; }

    public string? ModelId { get; init; }
}

public sealed class AzureOpenAIInferenceProviderOptions
{
    public string? Endpoint { get; init; }

    public string? ApiKey { get; init; }

    public string? DeploymentName { get; init; }
}

public sealed class OpenAICompatibleInferenceProviderOptions
{
    public string? Endpoint { get; init; }

    public string? ApiKey { get; init; }

    public string? ModelId { get; init; }

    public JsonObject? ExtraBody { get; set; }

    internal static JsonObject? ParseExtraBody(IConfigurationSection section)
    {
        if (!section.Exists())
            return null;

        JsonObject? extraBodyObject = ConvertSectionToJsonObject(section);
        return extraBodyObject is null || extraBodyObject.Count == 0 ? null : extraBodyObject;
    }

    private static JsonObject? ConvertSectionToJsonObject(IConfigurationSection section)
    {
        if (!section.Exists())
            return null;

        JsonObject result = [];
        foreach (IConfigurationSection child in section.GetChildren())
        {
            JsonObject? childObject = child.GetChildren().Any()
                ? ConvertSectionToJsonObject(child)
                : null;
            result[child.Key] = childObject ?? ParseScalarValue(child.Value);
        }

        return result;
    }

    private static JsonNode? ParseScalarValue(string? value)
    {
        if (value is null)
            return null;

        if (bool.TryParse(value, out bool booleanValue))
            return JsonValue.Create(booleanValue);

        if (long.TryParse(value, out long longValue))
            return JsonValue.Create(longValue);

        if (decimal.TryParse(value, out decimal decimalValue))
            return JsonValue.Create(decimalValue);

        if ((value.StartsWith('{') || value.StartsWith('[')) && TryParseJsonNode(value, out JsonNode? node))
            return node;

        return JsonValue.Create(value);
    }

    private static bool TryParseJsonNode(string value, out JsonNode? node)
    {
        try
        {
            node = JsonNode.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            node = null;
            return false;
        }
    }
}
