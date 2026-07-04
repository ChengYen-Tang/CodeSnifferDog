using Microsoft.Extensions.AI;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Summarizers;

/// <summary>
/// Generates the raw summary text that the compaction pipeline will normalize and validate.
/// </summary>
public interface ISummarizer
{
    /// <summary>
    /// Summarizes the supplied transcript according to the provided summary prompt contract.
    /// </summary>
    /// <param name="messages">Transcript messages that should be summarized.</param>
    /// <param name="summaryPrompt">Prompt text that defines the required output contract.</param>
    /// <param name="options">Compaction settings that may influence model choice or summarization behavior.</param>
    /// <param name="cancellationToken">Cancels the summary request.</param>
    /// <returns>The raw summary text returned by the summarizer.</returns>
    ValueTask<string> SummarizeAsync(
        IReadOnlyList<ChatMessage> messages,
        string summaryPrompt,
        CompactionOptions options,
        CancellationToken cancellationToken);
}
