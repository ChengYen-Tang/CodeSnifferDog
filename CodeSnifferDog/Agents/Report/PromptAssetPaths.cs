namespace CodeSnifferDog.Agents.Report;

/// <summary>
/// Prompt asset paths used by report agents and report-context compaction.
/// </summary>
public static class PromptAssetPaths
{
    /// <summary>
    /// Prompt asset for the report aggregator agent.
    /// </summary>
    public const string ReportAggregatorAgentPrompt = "report-aggregator-agent.md";

    /// <summary>
    /// Prompt asset for the report verifier agent.
    /// </summary>
    public const string ReportVerifierAgentPrompt = "report-verifier-agent.md";

    /// <summary>
    /// Prompt asset used when summarizing report context during compaction.
    /// </summary>
    public const string ReportSummaryPrompt = "compaction/report-summary.md";
}
