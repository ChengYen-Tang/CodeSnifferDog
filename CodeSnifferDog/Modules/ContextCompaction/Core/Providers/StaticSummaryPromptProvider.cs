namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

/// <summary>
/// Returns a fixed in-memory summary prompt.
/// </summary>
/// <param name="prompt">Prompt text that should be returned for every request.</param>
public sealed class StaticSummaryPromptProvider(string prompt) : ISummaryPromptProvider
{
    /// <inheritdoc />
    public ValueTask<string> GetPromptAsync(CancellationToken _) => ValueTask.FromResult(prompt);
}
