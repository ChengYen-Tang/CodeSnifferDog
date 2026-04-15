using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public interface IOperationalContextSummaryPromptProvider
{
    ValueTask<string> GetPromptAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken);
}
