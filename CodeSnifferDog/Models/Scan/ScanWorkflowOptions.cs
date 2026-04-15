namespace CodeSnifferDog.Models.Scan;

public sealed class ScanWorkflowOptions
{
    public int MaxVerifierRejectionAttempts { get; init; } = 3;

    public int MaxMissingSubmissionAttempts { get; init; } = 3;

    public int MaxScanAgentResets { get; init; } = 3;
}
