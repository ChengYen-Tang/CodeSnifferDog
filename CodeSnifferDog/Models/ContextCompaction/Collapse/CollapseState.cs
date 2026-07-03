
namespace CodeSnifferDog.Models.ContextCompaction.Collapse;

public sealed class CollapseState
{
    public IReadOnlyList<StagedCollapseSpan> StagedSpans { get; init; } = [];

    public string? LastCollapseReason { get; init; }

    public IReadOnlyList<CommittedCollapseSpan> Commits { get; init; } = [];

    public CollapseSnapshot Snapshot { get; init; } = new();
}
