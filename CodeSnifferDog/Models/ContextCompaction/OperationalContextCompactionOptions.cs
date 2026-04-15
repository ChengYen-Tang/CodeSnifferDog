using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextCompactionOptions
{
    public required int ContextTokenThreshold { get; init; }

    public long ContextWindowBufferTokens { get; init; } = 8_192;

    public long SummaryReservedOutputTokens { get; init; } = 4_096;

    public string? SummaryModelId { get; init; }

    public IReadOnlyList<string> RequiredSummaryFragments { get; init; } =
    [
        "Current objective",
        "Completed work",
        "Next steps",
    ];
}
