using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Workflows.Scan;

/// <summary>
/// Creates scan workflow results from workflow execution state.
/// </summary>
internal static class ResultFactory
{
    /// <summary>
    /// Creates one scan workflow result.
    /// </summary>
    /// <param name="projects">Projects produced by the scan workflow.</param>
    /// <param name="verdict">Latest verifier verdict.</param>
    /// <param name="scanAttempts">Number of scan-agent attempts performed.</param>
    /// <param name="verifierAttempts">Number of verifier attempts performed.</param>
    /// <param name="scanAgentResetCount">Number of scan-agent resets triggered after missing submissions.</param>
    /// <returns>The composed scan workflow result.</returns>
    public static ScanWorkflowResult Create(
        IReadOnlyList<StoredScanProject> projects,
        ReviewVerdict verdict,
        int scanAttempts,
        int verifierAttempts,
        int scanAgentResetCount) => new()
        {
            Projects = projects,
            Verdict = verdict,
            ScanAttempts = scanAttempts,
            VerifierAttempts = verifierAttempts,
            ScanAgentResetCount = scanAgentResetCount,
        };
}
