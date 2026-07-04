
namespace CodeSnifferDog.Models.ContextCompaction.Collapse;

/// <summary>
/// Represents one collapse span that has been committed to archived transcript history.
/// </summary>
public sealed class CommittedCollapseSpan : CollapseSpan
{
    /// <summary>
    /// Gets when the span was committed, in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
