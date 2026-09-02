using Microsoft.Extensions.AI;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CodeSnifferDog.Agents.Common.TokenUsage;

/// <summary>
/// Builds a stable identity for the request options that affect provider-side input accounting.
/// </summary>
internal static class ChatRequestFingerprint
{
    private const string FingerprintVersion = "1";

    /// <summary>
    /// Creates a fingerprint for the known <see cref="ChatOptions" /> contract.
    /// </summary>
    /// <remarks>
    /// A missing options object, a derived options type, or a raw-representation callback means that the
    /// provider request cannot be identified completely. In those cases the caller must use the full local
    /// estimate instead of reusing a provider checkpoint.
    /// </remarks>
    public static string? Create(ChatOptions? options)
    {
        if (options is null ||
            options.GetType() != typeof(ChatOptions) ||
            options.RawRepresentationFactory is not null)
            return null;

        StringBuilder material = new();
        AppendField(material, "version", FingerprintVersion);
        AppendField(material, "conversation", options.ConversationId);
        AppendField(material, "instructions", options.Instructions);
        AppendField(material, "temperature", options.Temperature);
        AppendField(material, "max-output", options.MaxOutputTokens);
        AppendField(material, "top-p", options.TopP);
        AppendField(material, "top-k", options.TopK);
        AppendField(material, "frequency-penalty", options.FrequencyPenalty);
        AppendField(material, "presence-penalty", options.PresencePenalty);
        AppendField(material, "seed", options.Seed);
        AppendField(material, "model", options.ModelId);
        AppendField(material, "stop-sequences", options.StopSequences);
        AppendField(material, "allow-multiple-tool-calls", options.AllowMultipleToolCalls);
        AppendField(material, "allow-background-responses", options.AllowBackgroundResponses);

        if (!AppendToolMode(material, options.ToolMode) ||
            !AppendReasoning(material, options.Reasoning) ||
            !AppendResponseFormat(material, options.ResponseFormat) ||
            !AppendContinuationToken(material, options.ContinuationToken) ||
            !AppendTools(material, options.Tools) ||
            !AppendDictionary(material, "additional", options.AdditionalProperties))
            return null;

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString())));
    }

    private static bool AppendTools(StringBuilder material, IList<AITool>? tools)
    {
        material.Append("tools=");
        if (tools is null)
        {
            material.Append("null\n");
            return true;
        }

        material.Append(tools.Count.ToString(CultureInfo.InvariantCulture)).Append(':');
        foreach (AITool? tool in tools)
        {
            if (tool is null || !AppendTool(material, tool))
                return false;
        }

        material.Append('\n');
        return true;
    }

    private static bool AppendTool(StringBuilder material, AITool tool)
    {
        material.Append("tool{");
        AppendField(material, "type", tool.GetType().FullName);
        AppendField(material, "name", tool.Name);
        AppendField(material, "description", tool.Description);

        if (tool is AIFunctionDeclaration function)
        {
            AppendField(material, "json-schema", function.JsonSchema);
            AppendField(material, "return-json-schema", function.ReturnJsonSchema);
        }
        else if (!AppendPublicToolProperties(material, tool))
        {
            return false;
        }

        if (!AppendDictionary(material, "additional", tool.AdditionalProperties))
            return false;

        material.Append('}');
        return true;
    }

    private static bool AppendPublicToolProperties(StringBuilder material, AITool tool)
    {
        foreach (PropertyInfo property in tool.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.CanRead)
            .OrderBy(static property => property.Name, StringComparer.Ordinal))
        {
            if (property.Name is "Name" or "Description" or "AdditionalProperties" or "DebuggerDisplay")
                continue;

            object? value;
            try
            {
                value = property.GetValue(tool);
            }
            catch (TargetInvocationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }

            material.Append(property.Name).Append('=');
            if (!AppendValue(material, value))
                return false;
            material.Append('\n');
        }

        return true;
    }

    private static bool AppendToolMode(StringBuilder material, ChatToolMode? toolMode)
    {
        material.Append("tool-mode=");
        if (toolMode is null)
        {
            material.Append("null\n");
            return true;
        }

        AppendField(material, "type", toolMode.GetType().FullName);
        if (toolMode is RequiredChatToolMode required)
            AppendField(material, "required-function", required.RequiredFunctionName);
        else if (toolMode is not AutoChatToolMode && toolMode is not NoneChatToolMode)
            return false;

        material.Append('\n');
        return true;
    }

    private static bool AppendReasoning(StringBuilder material, ReasoningOptions? reasoning)
    {
        material.Append("reasoning=");
        if (reasoning is null)
        {
            material.Append("null\n");
            return true;
        }

        AppendField(material, "effort", reasoning.Effort);
        AppendField(material, "output", reasoning.Output);
        material.Append('\n');
        return true;
    }

    private static bool AppendResponseFormat(StringBuilder material, ChatResponseFormat? responseFormat)
    {
        material.Append("response-format=");
        if (responseFormat is null)
        {
            material.Append("null\n");
            return true;
        }

        AppendField(material, "type", responseFormat.GetType().FullName);
        switch (responseFormat)
        {
            case ChatResponseFormatText:
                break;
            case ChatResponseFormatJson json:
                AppendField(material, "schema", json.Schema);
                AppendField(material, "schema-name", json.SchemaName);
                AppendField(material, "schema-description", json.SchemaDescription);
                break;
            default:
                return false;
        }

        material.Append('\n');
        return true;
    }

    private static bool AppendContinuationToken(
        StringBuilder material,
        ResponseContinuationToken? continuationToken)
    {
        material.Append("continuation=");
        if (continuationToken is null)
        {
            material.Append("null\n");
            return true;
        }

        material.Append(Convert.ToHexString(continuationToken.ToBytes().Span)).Append('\n');
        return true;
    }

    private static void AppendField(StringBuilder material, string name, object? value)
    {
        material.Append(name).Append('=');
        _ = AppendValue(material, value);
        material.Append('\n');
    }

    private static bool AppendDictionary(
        StringBuilder material,
        string name,
        IReadOnlyDictionary<string, object?>? properties)
    {
        material.Append(name).Append('=');
        if (properties is null)
        {
            material.Append("null\n");
            return true;
        }

        material.Append('{');
        foreach ((string key, object? value) in properties.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            AppendValue(material, key);
            material.Append(':');
            if (!AppendValue(material, value))
                return false;
            material.Append(';');
        }

        material.Append("}\n");
        return true;
    }

    private static bool AppendValue(StringBuilder material, object? value)
    {
        switch (value)
        {
            case null:
                material.Append("null");
                return true;
            case string text:
                AppendLengthPrefixedString(material, text);
                return true;
            case char character:
                AppendLengthPrefixedString(material, character.ToString());
                return true;
            case bool boolean:
                material.Append(boolean ? "true" : "false");
                return true;
            case JsonElement jsonElement:
                AppendLengthPrefixedString(material, jsonElement.ValueKind == JsonValueKind.Undefined
                    ? "<undefined>"
                    : jsonElement.GetRawText());
                return true;
            case JsonDocument jsonDocument:
                AppendLengthPrefixedString(material, jsonDocument.RootElement.GetRawText());
                return true;
            case byte[] bytes:
                AppendLengthPrefixedString(material, Convert.ToHexString(bytes));
                return true;
            case IDictionary dictionary:
                material.Append('{');
                List<string> entries = [];
                foreach (DictionaryEntry entry in dictionary)
                {
                    StringBuilder entryBuilder = new();
                    if (!AppendValue(entryBuilder, entry.Key) || !AppendValue(entryBuilder, entry.Value))
                        return false;
                    entries.Add(entryBuilder.ToString());
                }

                entries.Sort(StringComparer.Ordinal);
                foreach (string entry in entries)
                    material.Append(entry.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(entry);
                material.Append('}');
                return true;
            case IEnumerable enumerable:
                material.Append('[');
                foreach (object? item in enumerable)
                {
                    if (!AppendValue(material, item))
                        return false;
                    material.Append(',');
                }

                material.Append(']');
                return true;
            case Enum enumValue:
                AppendLengthPrefixedString(material, $"{enumValue.GetType().FullName}:{enumValue}");
                return true;
            case IFormattable formattable:
                AppendLengthPrefixedString(
                    material,
                    $"{value.GetType().FullName}:{formattable.ToString(null, CultureInfo.InvariantCulture)}");
                return true;
            default:
                return false;
        }
    }

    private static void AppendLengthPrefixedString(StringBuilder material, string value) =>
        material.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
}
