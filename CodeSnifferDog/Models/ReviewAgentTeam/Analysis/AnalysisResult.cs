using CodeSnifferDog.Models.ReviewAgentTeam.Results;

namespace CodeSnifferDog.Models.ReviewAgentTeam.Analysis;

public sealed class AnalysisResult
{
    public required bool PreparationSucceeded { get; init; }

    public required bool ReviewStageSucceeded { get; init; }

    public required bool HasAnyFindings { get; init; }

    public required bool AllRuleFlowsSucceeded { get; init; }

    public required IReadOnlyList<string> ExecutionErrors { get; init; }

    public required IReadOnlyList<RuleReport> RuleReports { get; init; }
}
