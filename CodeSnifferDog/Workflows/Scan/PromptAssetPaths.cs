namespace CodeSnifferDog.Workflows.Scan;

/// <summary>
/// Prompt asset paths used by the scan workflow.
/// </summary>
public static class PromptAssetPaths
{
    /// <summary>
    /// Prompt asset prefixed to scan-agent input.
    /// </summary>
    public const string ScanInputPrefix = "workflows/scan/scan-input-prefix.md";

    /// <summary>
    /// Prompt asset prefixed to scan-verifier input.
    /// </summary>
    public const string VerifierInputPrefix = "workflows/scan/scan-verifier-input-prefix.md";

    /// <summary>
    /// Prompt asset shown when the scan agent fails to submit results.
    /// </summary>
    public const string MissingScanSubmissionMessage = "workflows/scan/missing-scan-submission.md";
}
