
namespace CodeSnifferDog.Models.ContextCompaction.Collapse;

public sealed class CommittedCollapseSpan : CollapseSpan
{
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
