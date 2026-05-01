namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public sealed class StaticOperationalContextSummaryPromptProvider(string prompt) : IOperationalContextSummaryPromptProvider
{
    public ValueTask<string> GetPromptAsync(CancellationToken _) => ValueTask.FromResult(prompt);
}
