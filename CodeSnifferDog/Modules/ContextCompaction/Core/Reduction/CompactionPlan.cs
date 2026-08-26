using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Modules.ContextCompaction.Core.Reduction;

/// <summary>
/// Describes the decision and preserved non-system tail for one compaction evaluation.
/// </summary>
/// <param name="ShouldCompact">Whether the transcript should be compacted.</param>
/// <param name="MessagesToKeep">The preselected non-system messages that remain active after compaction.</param>
internal sealed record CompactionPlan(
    bool ShouldCompact,
    IReadOnlyList<ChatMessage> MessagesToKeep)
{
    /// <summary>
    /// Gets the shared result for an evaluation that does not require compaction.
    /// </summary>
    public static CompactionPlan Skip { get; } = new(false, []);
}
