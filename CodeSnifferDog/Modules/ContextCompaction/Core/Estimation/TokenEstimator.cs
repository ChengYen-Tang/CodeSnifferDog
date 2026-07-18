using Microsoft.Extensions.AI;
using System.Text.Json;
using SharedTokenEstimator = CodeSnifferDog.Modules.Estimation.TokenEstimator;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Estimation;

/// <summary>
/// Provides coarse token estimates based on UTF-8 byte counts across supported AI content payloads.
/// </summary>
internal static class TokenEstimator
{
    private static readonly JsonSerializerOptions StructuredPayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Estimates the token cost of a message sequence.
    /// </summary>
    /// <param name="messages">Messages whose text and content payloads should be counted.</param>
    /// <returns>A coarse token estimate that never drops below one for a non-empty estimate operation.</returns>
    public static int Estimate(IReadOnlyList<ChatMessage> messages)
    {
        int byteCount = 0;

        foreach (ChatMessage message in messages)
        {
            byteCount += GetStringByteCount(message.Text);

            foreach (AIContent content in message.Contents)
                byteCount += EstimateContentBytes(content);
        }

        return Math.Max(1, SharedTokenEstimator.EstimateBytes(byteCount));
    }

    /// <summary>
    /// Estimates the token cost of one AI content payload.
    /// </summary>
    /// <param name="content">Content payload to estimate.</param>
    /// <returns>A coarse token estimate for the supplied content.</returns>
    public static int EstimateContent(AIContent content) =>
        Math.Max(1, SharedTokenEstimator.EstimateBytes(EstimateContentBytes(content)));

    private static int EstimateContentBytes(AIContent content) =>
        content switch
        {
            TextContent text => GetStringByteCount(text.Text),
            TextReasoningContent reasoning => GetStringByteCount(reasoning.Text) + GetStringByteCount(reasoning.ProtectedData),
            DataContent data => data.Data.Length + GetStringByteCount(data.MediaType) + GetStringByteCount(data.Name),
            UriContent uri => GetStringByteCount(uri.Uri?.OriginalString) + GetStringByteCount(uri.MediaType),
            FunctionCallContent call => GetStringByteCount(call.CallId) + GetStringByteCount(call.Name) + EstimateArgumentsBytes(call.Arguments),
            FunctionResultContent result => GetStringByteCount(result.CallId) + GetStructuredValueByteCount(result.Result),
            ErrorContent error => GetStringByteCount(error.Message) + GetStringByteCount(error.ErrorCode) + GetStringByteCount(error.Details),
            HostedFileContent file => GetStringByteCount(file.FileId) + GetStringByteCount(file.MediaType) + GetStringByteCount(file.Name),
            _ => 0,
        };

    private static int EstimateArgumentsBytes(IDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0)
            return 0;

        int total = 0;

        foreach ((string key, object? value) in arguments)
            total += GetStringByteCount(key) + GetStructuredValueByteCount(value);

        return total;
    }

    /// <summary>
    /// Measures a tool value without losing collection or object content through <see cref="object.ToString"/>.
    /// </summary>
    /// <param name="value">The argument or result value carried by a tool message.</param>
    /// <returns>The estimated UTF-8 byte count of the value as it would be represented structurally.</returns>
    private static int GetStructuredValueByteCount(object? value)
    {
        if (value is null)
            return 0;

        if (value is string text)
            return GetStringByteCount(text);

        if (value is JsonElement jsonElement)
            return GetStringByteCount(jsonElement.GetRawText());

        if (value is JsonDocument jsonDocument)
            return GetStringByteCount(jsonDocument.RootElement.GetRawText());

        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                value,
                value.GetType(),
                StructuredPayloadSerializerOptions).Length;
        }
        catch (JsonException)
        {
            return GetStringByteCount(value.ToString());
        }
        catch (NotSupportedException)
        {
            return GetStringByteCount(value.ToString());
        }
    }

    private static int GetStringByteCount(string? value) =>
        SharedTokenEstimator.GetUtf8ByteCount(value);
}
