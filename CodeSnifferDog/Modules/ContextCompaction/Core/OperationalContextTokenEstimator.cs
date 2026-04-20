using System.Text;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core;

internal static class OperationalContextTokenEstimator
{
    public static int Estimate(IReadOnlyList<ChatMessage> messages)
    {
        int byteCount = 0;

        foreach (ChatMessage message in messages)
        {
            byteCount += GetStringByteCount(message.Text);

            foreach (AIContent content in message.Contents)
                byteCount += EstimateContentBytes(content);
        }

        return Math.Max(1, byteCount / 4);
    }

    public static int EstimateContent(AIContent content) =>
        Math.Max(1, EstimateContentBytes(content) / 4);

    private static int EstimateContentBytes(AIContent content) =>
        content switch
        {
            TextContent text => GetStringByteCount(text.Text),
            TextReasoningContent reasoning => GetStringByteCount(reasoning.Text) + GetStringByteCount(reasoning.ProtectedData),
            DataContent data => data.Data.Length + GetStringByteCount(data.MediaType) + GetStringByteCount(data.Name),
            UriContent uri => GetStringByteCount(uri.Uri?.OriginalString) + GetStringByteCount(uri.MediaType),
            FunctionCallContent call => GetStringByteCount(call.CallId) + GetStringByteCount(call.Name) + EstimateArgumentsBytes(call.Arguments),
            FunctionResultContent result => GetStringByteCount(result.CallId) + GetStringByteCount(result.Result?.ToString()),
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
            total += GetStringByteCount(key) + GetStringByteCount(value?.ToString());

        return total;
    }

    private static int GetStringByteCount(string? value) =>
        string.IsNullOrEmpty(value) ? 0 : Encoding.UTF8.GetByteCount(value);
}
