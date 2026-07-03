using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;

namespace CodeSnifferDog.Models.RuleReview;

public sealed class WorkflowResult
{
    public required StoredTaskItem TaskItem { get; init; }

    public required string RuleKey { get; init; }

    public required IReadOnlyList<StoredIssue> Issues { get; init; }

    public NoIssueConclusion? NoIssueConclusion { get; init; }

    public required ReviewVerdict Verdict { get; init; }

    public required bool ContinuedAfterVerifierRejectionLimit { get; init; }

    public required bool StoppedAfterMissingSubmissionLimit { get; init; }

    public required int ReviewAttempts { get; init; }

    public required int VerifierAttempts { get; init; }

    public required int RuleReviewAgentResetCount { get; init; }
}
