using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using CodeSnifferDog.Modules.ContextCompaction.Core.Transcript;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;

/// <summary>
/// Generates compaction summaries by appending the summary prompt to the existing transcript and calling an <see cref="IChatClient" />.
/// </summary>
/// <param name="chatClient">Chat client used to request the summary.</param>
public sealed class ChatClientSummarizer(IChatClient chatClient) : ISummarizer
{
    /// <inheritdoc />
    /// <remarks>
    /// Incomplete trailing tool-call groups are excluded before the summary prompt is appended. Completed groups remain
    /// intact because providers require every function call to retain its matching tool result.
    /// </remarks>
    /// <exception cref="CompactionException">The chat client returns empty summary content.</exception>
    public async ValueTask<string> SummarizeAsync(
        IReadOnlyList<ChatMessage> messages,
        string summaryPrompt,
        CompactionOptions options,
        CancellationToken cancellationToken)
    {
        List<ChatMessage> summaryMessages = [.. ToolCallTranscript.GetCompletePrefix(messages)];
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
}
