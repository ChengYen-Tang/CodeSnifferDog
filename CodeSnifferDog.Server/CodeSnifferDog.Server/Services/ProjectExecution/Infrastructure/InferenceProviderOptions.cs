namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Stores inference provider configuration used by project execution workflows.
/// </summary>
public sealed class InferenceProviderOptions
{
    /// <summary>
    /// Gets the configuration section name for inference settings.
    /// </summary>
    public const string SectionName = "Inference";

    /// <summary>
    /// Gets the provider name to use when creating chat clients.
    /// </summary>
    public string Provider { get; init; } = "openai";

    /// <summary>
    /// Gets the optional request timeout, in seconds, applied to provider SDK calls.
    /// </summary>
    public int? RequestTimeoutSeconds { get; init; }

    /// <summary>
    /// Gets the OpenAI-specific configuration.
    /// </summary>
    public OpenAIInferenceProviderOptions OpenAI { get; init; } = new();

    /// <summary>
    /// Gets the Azure OpenAI-specific configuration.
    /// </summary>
    public AzureOpenAIInferenceProviderOptions AzureOpenAI { get; init; } = new();

    /// <summary>
    /// Gets the OpenAI-compatible provider configuration.
    /// </summary>
    public OpenAICompatibleInferenceProviderOptions OpenAICompatible { get; init; } = new();
}

/// <summary>
/// Stores configuration for the OpenAI provider.
/// </summary>
public sealed class OpenAIInferenceProviderOptions
{
    /// <summary>
    /// Gets the API key used to authenticate with OpenAI.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the model identifier used for chat completions.
    /// </summary>
    public string? ModelId { get; init; }
}

/// <summary>
/// Stores configuration for the Azure OpenAI provider.
/// </summary>
public sealed class AzureOpenAIInferenceProviderOptions
{
    /// <summary>
    /// Gets the Azure OpenAI endpoint URI.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Gets the API key used to authenticate with Azure OpenAI.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the deployment name that backs chat completions.
    /// </summary>
    public string? DeploymentName { get; init; }
}

/// <summary>
/// Stores configuration for an OpenAI-compatible backend.
/// </summary>
public sealed class OpenAICompatibleInferenceProviderOptions
{
    /// <summary>
    /// Gets the provider endpoint URI.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Gets the optional API key used to authenticate with the provider.
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the model identifier used for chat completions.
    /// </summary>
    public string? ModelId { get; init; }

    /// <summary>
    /// Gets provider-specific request body fields merged into outgoing requests.
    /// </summary>
    public JsonObject? ExtraBody { get; set; }

    /// <summary>
    /// Parses the <c>ExtraBody</c> configuration section into a JSON object.
    /// </summary>
    /// <param name="section">Configuration section that contains provider-specific request fields.</param>
    /// <returns>The parsed JSON object, or <see langword="null"/> when the section is empty.</returns>
    internal static JsonObject? ParseExtraBody(IConfigurationSection section)
    {
        if (!section.Exists())
            return null;

        JsonObject? extraBodyObject = ConvertSectionToJsonObject(section);
        return extraBodyObject is null || extraBodyObject.Count == 0 ? null : extraBodyObject;
    }

    /// <summary>
    /// Recursively converts a configuration section into a JSON object.
    /// </summary>
    /// <param name="section">Configuration section to convert.</param>
    /// <returns>The resulting JSON object, or <see langword="null"/> when the section is empty.</returns>
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

    /// <summary>
    /// Parses a scalar configuration value into the most appropriate JSON node representation.
    /// </summary>
    /// <param name="value">Scalar configuration value.</param>
    /// <returns>The parsed JSON node.</returns>
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

    /// <summary>
    /// Attempts to parse a JSON string into a <see cref="JsonNode"/>.
    /// </summary>
    /// <param name="value">Candidate JSON string.</param>
    /// <param name="node">Parsed node when parsing succeeds.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise, <see langword="false"/>.</returns>
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
