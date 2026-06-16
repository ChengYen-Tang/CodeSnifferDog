namespace CodeSnifferDog.Models.ReviewAgentTeam;

public sealed class ReviewAgentTeamAnalysisCompletionDecision
{
    public required bool IsSuccess { get; init; }

    public required bool ShouldPersistReports { get; init; }

    public string? FailureMessage { get; init; }
}
