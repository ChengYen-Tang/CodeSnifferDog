
namespace CodeSnifferDog.Models.ContextCompaction.Compaction;

/// <summary>
/// Configures thresholds, budgets, and feature toggles for transcript compaction.
/// </summary>
public sealed class CompactionOptions
{
    /// <summary>
    /// Default reserved output-token budget for generated summaries.
    /// </summary>
    public const int DefaultSummaryReservedOutputTokens = 20_000;
    /// <summary>
    /// Default token buffer that triggers automatic compaction before the context window is exhausted.
    /// </summary>
    public const int DefaultAutoCompactBufferTokens = 13_000;
    /// <summary>
    /// Default minimum token budget preserved at the transcript tail.
    /// </summary>
    public const int DefaultPreservedTailMinTokens = 10_000;
    /// <summary>
    /// Default minimum number of recent messages preserved at the transcript tail.
    /// </summary>
    public const int DefaultPreservedTailMinMessages = 5;
    /// <summary>
    /// Default maximum token budget allowed for the preserved transcript tail.
    /// </summary>
    public const int DefaultPreservedTailMaxTokens = 40_000;
    /// <summary>
    /// Default token budget reserved for preserved attachment messages after compaction.
    /// </summary>
    public const int DefaultPostCompactAttachmentTokenBudget = 50_000;
    /// <summary>
    /// Default maximum number of consecutive automatic-compaction failures before the circuit breaker opens.
    /// </summary>
    public const int DefaultMaxConsecutiveAutomaticFailures = 3;
    /// <summary>
    /// Default proactive context-collapse threshold percentage.
    /// </summary>
    public const int DefaultCollapseProactiveThresholdPercentage = 90;
    /// <summary>
    /// Default blocking context-collapse threshold percentage.
    /// </summary>
    public const int DefaultCollapseBlockingThresholdPercentage = 95;

    /// <summary>
    /// Gets the total model context window in tokens.
    /// </summary>
    public required long ModelContextWindowTokens { get; init; }

    /// <summary>
    /// Gets the optional model identifier used for summaries.
    /// </summary>
    public string? SummaryModelId { get; init; }

    /// <summary>
    /// Gets the reserved output-token budget for generated summaries.
    /// </summary>
    public int SummaryReservedOutputTokens { get; init; } = DefaultSummaryReservedOutputTokens;

    /// <summary>
    /// Gets the token buffer that triggers automatic compaction before exhaustion.
    /// </summary>
    public int AutoCompactBufferTokens { get; init; } = DefaultAutoCompactBufferTokens;

    /// <summary>
    /// Gets the minimum token budget preserved at the transcript tail.
    /// </summary>
    public int PreservedTailMinTokens { get; init; } = DefaultPreservedTailMinTokens;

    /// <summary>
    /// Gets the minimum number of recent messages preserved at the transcript tail.
    /// </summary>
    public int PreservedTailMinMessages { get; init; } = DefaultPreservedTailMinMessages;

    /// <summary>
    /// Gets the maximum token budget allowed for the preserved transcript tail.
    /// </summary>
    public int PreservedTailMaxTokens { get; init; } = DefaultPreservedTailMaxTokens;

    /// <summary>
    /// Gets the token budget reserved for preserved attachment messages after compaction.
    /// </summary>
    public int PostCompactAttachmentTokenBudget { get; init; } = DefaultPostCompactAttachmentTokenBudget;

    /// <summary>
    /// Gets the maximum number of consecutive automatic-compaction failures before the circuit breaker opens.
    /// </summary>
    public int MaxConsecutiveAutomaticFailures { get; init; } = DefaultMaxConsecutiveAutomaticFailures;

    /// <summary>
    /// Gets whether proactive automatic compaction is enabled.
    /// </summary>
    public bool EnableAutomaticCompaction { get; init; } = true;

    /// <summary>
    /// Gets the selected compaction mode.
    /// </summary>
    public CompactionMode Mode { get; init; } = CompactionMode.Standard;

    /// <summary>
    /// Gets whether micro-compaction of old tool results is enabled.
    /// </summary>
    public bool EnableMicroCompaction { get; init; } = true;

    /// <summary>
    /// Gets how many compactable tool results trigger micro-compaction.
    /// </summary>
    public int MicroCompactTriggerToolResultCount { get; init; } = 6;

    /// <summary>
    /// Gets how many recent tool results remain untouched during micro-compaction.
    /// </summary>
    public int MicroCompactKeepRecentToolResultCount { get; init; } = 2;

    /// <summary>
    /// Gets the replacement text used when old tool results are cleared.
    /// </summary>
    public string MicroCompactClearedResultText { get; init; } = "[Old tool result content cleared]";

    /// <summary>
    /// Gets whether snipping compactable tool results is enabled.
    /// </summary>
    public bool EnableSnip { get; init; } = true;

    /// <summary>
    /// Gets how many compactable tool results trigger snipping.
    /// </summary>
    public int SnipTriggerToolResultCount { get; init; } = 10;

    /// <summary>
    /// Gets how many recent tool results remain untouched during snipping.
    /// </summary>
    public int SnipKeepRecentToolResultCount { get; init; } = 1;

    /// <summary>
    /// Gets the proactive context-collapse threshold percentage.
    /// </summary>
    public int CollapseProactiveThresholdPercentage { get; init; } = DefaultCollapseProactiveThresholdPercentage;

    /// <summary>
    /// Gets the blocking context-collapse threshold percentage.
    /// </summary>
    public int CollapseBlockingThresholdPercentage { get; init; } = DefaultCollapseBlockingThresholdPercentage;

    /// <summary>
    /// Gets the tool names whose old results may be compacted or snipped.
    /// </summary>
    public IReadOnlyList<string> CompactableToolNames { get; init; } =
    [
        "RunShellCommand",
        "RunRipgrepCommand",
    ];

    /// <summary>
    /// Gets required fragments that a generated summary must contain.
    /// </summary>
    public IReadOnlyList<string> RequiredSummaryFragments { get; init; } =
    [
        "Current objective",
        "Completed work",
        "Next steps",
    ];

    /// <summary>
    /// Gets the usable context window after reserving summary output tokens.
    /// </summary>
    /// <returns>The effective positive context-window size.</returns>
    public int GetEffectiveContextWindowTokens() =>
        ClampToPositiveInt(ModelContextWindowTokens - SummaryReservedOutputTokens);

    /// <summary>
    /// Gets the token threshold that triggers automatic compaction.
    /// </summary>
    /// <returns>The positive automatic-compaction threshold.</returns>
    public int GetAutoCompactThreshold() =>
        Math.Max(1, GetEffectiveContextWindowTokens() - AutoCompactBufferTokens);

    /// <summary>
    /// Gets the proactive threshold that arms context collapse.
    /// </summary>
    /// <returns>The positive proactive collapse threshold.</returns>
    public int GetCollapseProactiveThreshold() =>
        Math.Max(1, (int)Math.Floor(GetEffectiveContextWindowTokens() * (CollapseProactiveThresholdPercentage / 100d)));

    /// <summary>
    /// Gets the blocking threshold that requires context collapse.
    /// </summary>
    /// <returns>The positive blocking collapse threshold.</returns>
    public int GetCollapseBlockingThreshold() =>
        Math.Max(1, (int)Math.Floor(GetEffectiveContextWindowTokens() * (CollapseBlockingThresholdPercentage / 100d)));

    /// <summary>
    /// Clamps a token count to a positive <see cref="int" /> range.
    /// </summary>
    /// <param name="value">Token count to clamp.</param>
    /// <returns>The clamped positive token count.</returns>
    private static int ClampToPositiveInt(long value) =>
        (int)Math.Max(1, Math.Min(int.MaxValue, value));
}
