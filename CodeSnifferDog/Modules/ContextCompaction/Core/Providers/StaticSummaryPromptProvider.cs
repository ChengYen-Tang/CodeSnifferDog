namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

public sealed class StaticSummaryPromptProvider(string prompt) : ISummaryPromptProvider
{
    public ValueTask<string> GetPromptAsync(CancellationToken _) => ValueTask.FromResult(prompt);
}
