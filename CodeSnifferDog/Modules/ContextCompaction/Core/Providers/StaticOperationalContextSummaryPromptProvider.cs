using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public sealed class StaticOperationalContextSummaryPromptProvider(string prompt) : IOperationalContextSummaryPromptProvider
{
    public ValueTask<string> GetPromptAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken) => ValueTask.FromResult(prompt);
}
