
namespace CodeSnifferDog.Models.ContextCompaction.Collapse;

/// <summary>
/// Represents one collapse span that has been staged but not yet committed.
/// </summary>
public sealed class StagedCollapseSpan : CollapseSpan
{
    /// <summary>
    /// Gets when the span was staged, in UTC.
    /// </summary>
    public required DateTimeOffset StagedAtUtc { get; init; }
}
