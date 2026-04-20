namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextCommittedCollapseSpan : OperationalContextCollapseSpan
{
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
