
namespace CodeSnifferDog.Models.ContextCompaction.Compaction;

public sealed class CompactionOptions
{
    public const int DefaultSummaryReservedOutputTokens = 20_000;
    public const int DefaultAutoCompactBufferTokens = 13_000;
    public const int DefaultPreservedTailMinTokens = 10_000;
    public const int DefaultPreservedTailMinMessages = 5;
    public const int DefaultPreservedTailMaxTokens = 40_000;
    public const int DefaultPostCompactAttachmentTokenBudget = 50_000;
    public const int DefaultMaxConsecutiveAutomaticFailures = 3;
    public const int DefaultCollapseProactiveThresholdPercentage = 90;
    public const int DefaultCollapseBlockingThresholdPercentage = 95;

    public required long ModelContextWindowTokens { get; init; }

    public string? SummaryModelId { get; init; }

    public int SummaryReservedOutputTokens { get; init; } = DefaultSummaryReservedOutputTokens;

    public int AutoCompactBufferTokens { get; init; } = DefaultAutoCompactBufferTokens;

    public int PreservedTailMinTokens { get; init; } = DefaultPreservedTailMinTokens;

    public int PreservedTailMinMessages { get; init; } = DefaultPreservedTailMinMessages;

    public int PreservedTailMaxTokens { get; init; } = DefaultPreservedTailMaxTokens;

    public int PostCompactAttachmentTokenBudget { get; init; } = DefaultPostCompactAttachmentTokenBudget;

    public int MaxConsecutiveAutomaticFailures { get; init; } = DefaultMaxConsecutiveAutomaticFailures;

    public bool EnableAutomaticCompaction { get; init; } = true;

    public CompactionMode Mode { get; init; } = CompactionMode.Standard;

    public bool EnableMicroCompaction { get; init; } = true;

    public int MicroCompactTriggerToolResultCount { get; init; } = 6;

    public int MicroCompactKeepRecentToolResultCount { get; init; } = 2;

    public string MicroCompactClearedResultText { get; init; } = "[Old tool result content cleared]";

    public bool EnableSnip { get; init; } = true;

    public int SnipTriggerToolResultCount { get; init; } = 10;

    public int SnipKeepRecentToolResultCount { get; init; } = 1;

    public int CollapseProactiveThresholdPercentage { get; init; } = DefaultCollapseProactiveThresholdPercentage;

    public int CollapseBlockingThresholdPercentage { get; init; } = DefaultCollapseBlockingThresholdPercentage;

    public IReadOnlyList<string> CompactableToolNames { get; init; } =
    [
        "RunShellCommand",
        "RunRipgrepCommand",
    ];

    public IReadOnlyList<string> RequiredSummaryFragments { get; init; } =
    [
        "Current objective",
        "Completed work",
        "Next steps",
    ];

    public int GetEffectiveContextWindowTokens() =>
        ClampToPositiveInt(ModelContextWindowTokens - SummaryReservedOutputTokens);

    public int GetAutoCompactThreshold() =>
        Math.Max(1, GetEffectiveContextWindowTokens() - AutoCompactBufferTokens);

    public int GetCollapseProactiveThreshold() =>
        Math.Max(1, (int)Math.Floor(GetEffectiveContextWindowTokens() * (CollapseProactiveThresholdPercentage / 100d)));

    public int GetCollapseBlockingThreshold() =>
        Math.Max(1, (int)Math.Floor(GetEffectiveContextWindowTokens() * (CollapseBlockingThresholdPercentage / 100d)));

    private static int ClampToPositiveInt(long value) =>
        (int)Math.Max(1, Math.Min(int.MaxValue, value));
}
