namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextStagedCollapseSpan : OperationalContextCollapseSpan
{
    public required DateTimeOffset StagedAtUtc { get; init; }
}
