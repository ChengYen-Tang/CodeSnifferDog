namespace CodeSnifferDog.Models.ReviewAgentTeam.Analysis;

public sealed class CompletionDecision
{
    public required bool IsSuccess { get; init; }

    public required bool ShouldPersistReports { get; init; }

    public string? FailureMessage { get; init; }
}
