using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Models.Report;

public sealed class RuleReportWorkflowResult
{
    public required string RuleKey { get; init; }

    public required StoredProjectPlanTaskItem TaskItem { get; init; }

    public required RuleReportDiff Diff { get; init; }

    public required IReadOnlyList<StoredRuleReportIssue> RepositoryIssues { get; init; }

    public required ReviewVerdict Verdict { get; init; }

    public required bool ContinuedAfterVerifierRejectionLimit { get; init; }

    public required int AggregatorAttempts { get; init; }

    public required int VerifierAttempts { get; init; }
}
