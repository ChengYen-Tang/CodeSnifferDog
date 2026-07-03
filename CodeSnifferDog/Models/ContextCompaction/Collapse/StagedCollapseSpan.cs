
namespace CodeSnifferDog.Models.ContextCompaction.Collapse;

public sealed class StagedCollapseSpan : CollapseSpan
{
    public required DateTimeOffset StagedAtUtc { get; init; }
}
