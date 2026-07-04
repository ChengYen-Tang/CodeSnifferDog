
namespace CodeSnifferDog.Models.ContextCompaction.Collapse;

/// <summary>
/// Holds the full mutable state of context-collapse staging, commits, and projection snapshots.
/// </summary>
public sealed class CollapseState
{
    /// <summary>
    /// Gets currently staged collapse spans that have not yet been committed.
    /// </summary>
    public IReadOnlyList<StagedCollapseSpan> StagedSpans { get; init; } = [];

    /// <summary>
    /// Gets the last collapse reason recorded by the controller, when one exists.
    /// </summary>
    public string? LastCollapseReason { get; init; }

    /// <summary>
    /// Gets committed collapse spans that represent archived transcript history.
    /// </summary>
    public IReadOnlyList<CommittedCollapseSpan> Commits { get; init; } = [];

    /// <summary>
    /// Gets the latest collapse snapshot used for projection and arming decisions.
    /// </summary>
    public CollapseSnapshot Snapshot { get; init; } = new();
}
