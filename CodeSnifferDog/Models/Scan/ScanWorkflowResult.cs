using CodeSnifferDog.Models.Review;

namespace CodeSnifferDog.Models.Scan;

public sealed class ScanWorkflowResult
{
    public required IReadOnlyList<StoredScanProject> Projects { get; init; }

    public required ReviewVerdict Verdict { get; init; }

    public required bool ScanVerifierApproved { get; init; }

    public required bool ContinuedAfterVerifierRejectionLimit { get; init; }

    public required bool ShouldEnterProjectPlanning { get; init; }

    public required int ScanAttempts { get; init; }

    public required int VerifierAttempts { get; init; }

    public required int ScanAgentResetCount { get; init; }
}
