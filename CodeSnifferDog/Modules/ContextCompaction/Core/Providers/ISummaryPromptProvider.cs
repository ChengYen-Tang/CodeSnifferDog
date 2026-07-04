namespace CodeSnifferDog.Modules.ContextCompaction.Core.Providers;

/// <summary>
/// Supplies the prompt template used to instruct the compaction summarizer.
/// </summary>
public interface ISummaryPromptProvider
{
    /// <summary>
    /// Gets the summary prompt that defines the required compaction output contract.
    /// </summary>
    /// <param name="cancellationToken">Cancels prompt retrieval.</param>
    /// <returns>The prompt text that should be passed to the summarizer.</returns>
    ValueTask<string> GetPromptAsync(CancellationToken cancellationToken);
}
