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
        List<ChatMessage> summaryMessages = [.. TrimTrailingToolOnlyMessages(messages)];
        summaryMessages.Add(new ChatMessage(ChatRole.User, summaryPrompt));

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

    private static IReadOnlyList<ChatMessage> TrimTrailingToolOnlyMessages(IReadOnlyList<ChatMessage> messages)
    {
        int lastIndexToKeep = messages.Count - 1;

        while (lastIndexToKeep >= 0 && IsToolOnlyMessage(messages[lastIndexToKeep]))
            lastIndexToKeep--;

        if (lastIndexToKeep < 0)
            return [];

        return [.. messages.Take(lastIndexToKeep + 1)];
    }

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
