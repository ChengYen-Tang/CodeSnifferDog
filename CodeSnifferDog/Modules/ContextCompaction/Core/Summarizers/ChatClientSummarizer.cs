using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;

/// <summary>
/// Generates compaction summaries by appending the summary prompt to the existing transcript and calling an <see cref="IChatClient" />.
/// </summary>
/// <param name="chatClient">Chat client used to request the summary.</param>
public sealed class ChatClientSummarizer(IChatClient chatClient) : ISummarizer
{
    /// <inheritdoc />
    /// <remarks>
    /// Trailing tool-only messages are trimmed before the summary prompt is appended because they add token cost without
    /// improving narrative continuity for the summarizer.
    /// </remarks>
    /// <exception cref="CompactionException">The chat client returns empty summary content.</exception>
    public async ValueTask<string> SummarizeAsync(
        IReadOnlyList<ChatMessage> messages,
        string summaryPrompt,
        CompactionOptions options,
        CancellationToken cancellationToken)
    {
        List<ChatMessage> summaryMessages = [.. TrimTrailingToolOnlyMessages(messages)];
        summaryMessages.Add(new ChatMessage(ChatRole.User, summaryPrompt));

        ChatOptions chatOptions = new()
        {
            ModelId = options.SummaryModelId,
        };

        ChatResponse response = await chatClient.GetResponseAsync(summaryMessages, chatOptions, cancellationToken).ConfigureAwait(false);
        string? summary = response.Text?.Trim();

        if (string.IsNullOrWhiteSpace(summary))
            throw new CompactionException("Operational context compaction summary call returned empty content.");

        return summary;
    }

    /// <summary>
    /// Removes trailing messages that contain only tool call or tool result payloads.
    /// </summary>
    /// <param name="messages">Transcript messages that will be sent to the summarizer.</param>
    /// <returns>The longest prefix that still contains non-tool narrative context.</returns>
    private static IReadOnlyList<ChatMessage> TrimTrailingToolOnlyMessages(IReadOnlyList<ChatMessage> messages)
    {
        int lastIndexToKeep = messages.Count - 1;

        while (lastIndexToKeep >= 0 && IsToolOnlyMessage(messages[lastIndexToKeep]))
            lastIndexToKeep--;

        if (lastIndexToKeep < 0)
            return [];

        return [.. messages.Take(lastIndexToKeep + 1)];
    }

    /// <summary>
    /// Determines whether a message carries only tool payloads and no human-readable text.
    /// </summary>
    /// <param name="message">Message to inspect.</param>
    /// <returns><see langword="true" /> when the message contains only function call or function result content.</returns>
    private static bool IsToolOnlyMessage(ChatMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Text))
            return false;

        if (message.Contents.Count == 0)
            return false;

        return message.Contents.All(static content =>
            content is FunctionCallContent or FunctionResultContent);
    }
}
