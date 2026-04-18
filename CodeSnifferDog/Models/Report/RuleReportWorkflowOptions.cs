namespace CodeSnifferDog.Models.Report;

public sealed class RuleReportWorkflowOptions
{
    public int MaxVerifierRejectionAttempts { get; init; } = 3;
}
