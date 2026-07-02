using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.Scan;

namespace CodeSnifferDog.Workflows.Scan;

internal static class ResultFactory
{
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
