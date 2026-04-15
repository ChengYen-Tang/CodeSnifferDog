using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;

public sealed class ChatClientOperationalContextCompactionSummarizer(IChatClient chatClient) : IOperationalContextCompactionSummarizer
{
    public async ValueTask<string> SummarizeAsync(
        IReadOnlyList<ChatMessage> messages,
        string summaryPrompt,
        OperationalContextCompactionOptions options,
        CancellationToken cancellationToken)
    {
        List<ChatMessage> summaryMessages = [.. messages, new ChatMessage(ChatRole.User, summaryPrompt)];

        ChatOptions chatOptions = new()
        {
            ModelId = options.SummaryModelId,
        };

        ChatResponse response = await chatClient.GetResponseAsync(summaryMessages, chatOptions, cancellationToken).ConfigureAwait(false);
        string? summary = response.Text?.Trim();

        if (string.IsNullOrWhiteSpace(summary))
            throw new OperationalContextCompactionException("Operational context compaction summary call returned empty content.");

        return summary;
    }
}
