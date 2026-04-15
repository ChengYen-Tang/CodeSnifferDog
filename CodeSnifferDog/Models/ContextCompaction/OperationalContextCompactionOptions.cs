using Microsoft.Extensions.AI;

namespace CodeSnifferDog.Models.ContextCompaction;

public sealed class OperationalContextCompactionOptions
{
    public required int ContextTokenThreshold { get; init; }

    public long ContextWindowBufferTokens { get; init; } = 8_192;

    public long SummaryReservedOutputTokens { get; init; } = 4_096;

    public int PreservedTailMessageCount { get; init; } = 2;

    public int MaxConsecutiveFailures { get; init; } = 3;

    public string SummaryMessageHeader { get; init; } = "Operational summary checkpoint";

    public ChatRole SummaryMessageRole { get; init; } = ChatRole.Assistant;

    public string? SummaryModelId { get; init; }

    public bool PreserveSystemMessages { get; init; } = true;

    public IReadOnlyList<string> RequiredSummaryFragments { get; init; } =
    [
        "Current objective",
        "Completed work",
        "Next steps",
    ];
}
