namespace CodeSnifferDog.Models.ReviewAgentTeam;

public sealed class ReviewAgentTeamAnalysisResult
{
    public required bool PreparationSucceeded { get; init; }

    public required bool ReviewStageSucceeded { get; init; }

    public required bool HasAnyFindings { get; init; }

    public required bool AllRuleFlowsSucceeded { get; init; }

    public required IReadOnlyList<string> ExecutionErrors { get; init; }

    public required IReadOnlyList<ReviewAgentTeamRuleReport> RuleReports { get; init; }
}
