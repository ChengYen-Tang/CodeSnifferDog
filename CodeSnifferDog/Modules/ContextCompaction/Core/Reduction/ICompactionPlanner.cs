using CodeSnifferDog.Models.ContextCompaction.Compaction;
using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

/// <summary>
/// Decides whether a transcript should be compacted and selects its preserved tail in one operation.
/// </summary>
internal interface ICompactionPlanner
{
    /// <summary>
    /// Evaluates one compaction pass.
    /// </summary>
    /// <param name="messages">Complete transcript messages in original order.</param>
    /// <param name="reason">Reason that initiated this evaluation.</param>
    /// <param name="additionalEstimatedInputTokens">Provider-request overhead not represented by transcript messages.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The compaction decision and, when needed, its preserved non-system tail.</returns>
    ValueTask<CompactionPlan> PlanAsync(
        IReadOnlyList<ChatMessage> messages,
        CompactionReason reason,
        int additionalEstimatedInputTokens,
        CancellationToken cancellationToken);
}
