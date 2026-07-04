namespace CodeSnifferDog.Agents.Scan;

/// <summary>
/// Prompt asset paths used by scan agents and scan-context compaction.
/// </summary>
public static class PromptAssetPaths
{
    /// <summary>
    /// Prompt asset for the scan agent.
    /// </summary>
    public const string ScanAgentPrompt = "scan-agent.md";

    /// <summary>
    /// Prompt asset for the scan verifier agent.
    /// </summary>
    public const string ScanVerifierAgentPrompt = "scan-verifier-agent.md";

    /// <summary>
    /// Prompt asset used when summarizing scan context during compaction.
    /// </summary>
    public const string ScanSummaryPrompt = "compaction/scan-summary.md";
}
