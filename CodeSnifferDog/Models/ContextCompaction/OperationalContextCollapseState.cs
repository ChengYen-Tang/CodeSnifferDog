namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextCollapseState
{
    public IReadOnlyList<OperationalContextStagedCollapseSpan> StagedSpans { get; init; } = [];

    public string? LastCollapseReason { get; init; }

    public IReadOnlyList<OperationalContextCommittedCollapseSpan> Commits { get; init; } = [];

    public OperationalContextCollapseSnapshot Snapshot { get; init; } = new();
}
