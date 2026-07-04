using CodeSnifferDog.Models.Review;

namespace CodeSnifferDog.Models.Scan;

/// <summary>
/// Holds the outputs and execution metadata produced by one scan workflow run.
/// </summary>
public sealed class ScanWorkflowResult
{
    /// <summary>
    /// Gets the projects emitted by the scan.
    /// </summary>
    public required IReadOnlyList<StoredScanProject> Projects { get; init; }

    /// <summary>
    /// Gets the final review verdict for the scan output.
    /// </summary>
    public required ReviewVerdict Verdict { get; init; }

    /// <summary>
    /// Gets how many scan-agent attempts were executed.
    /// </summary>
    public required int ScanAttempts { get; init; }

    /// <summary>
    /// Gets how many verifier attempts were executed.
    /// </summary>
    public required int VerifierAttempts { get; init; }

    /// <summary>
    /// Gets how many times the scan agent was reset.
    /// </summary>
    public required int ScanAgentResetCount { get; init; }
}
