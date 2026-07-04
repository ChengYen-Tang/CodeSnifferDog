using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;

namespace CodeSnifferDog.Models.RuleReview;

/// <summary>
/// Holds the outputs and execution metadata produced by one rule-review workflow run.
/// </summary>
public sealed class WorkflowResult
{
    /// <summary>
    /// Gets the task item that was reviewed.
    /// </summary>
    public required StoredTaskItem TaskItem { get; init; }

    /// <summary>
    /// Gets the rule key associated with the review.
    /// </summary>
    public required string RuleKey { get; init; }

    /// <summary>
    /// Gets the issues reported by the review.
    /// </summary>
    public required IReadOnlyList<StoredIssue> Issues { get; init; }

    /// <summary>
    /// Gets the no-issue conclusion when the review found no issues.
    /// </summary>
    public NoIssueConclusion? NoIssueConclusion { get; init; }

    /// <summary>
    /// Gets the final review verdict.
    /// </summary>
    public required ReviewVerdict Verdict { get; init; }

    /// <summary>
    /// Gets whether the workflow continued after exhausting verifier rejection attempts.
    /// </summary>
    public required bool ContinuedAfterVerifierRejectionLimit { get; init; }

    /// <summary>
    /// Gets whether the workflow stopped after exhausting missing-submission retries.
    /// </summary>
    public required bool StoppedAfterMissingSubmissionLimit { get; init; }

    /// <summary>
    /// Gets how many review-agent attempts were executed.
    /// </summary>
    public required int ReviewAttempts { get; init; }

    /// <summary>
    /// Gets how many verifier attempts were executed.
    /// </summary>
    public required int VerifierAttempts { get; init; }

    /// <summary>
    /// Gets how many times the rule-review agent was reset.
    /// </summary>
    public required int RuleReviewAgentResetCount { get; init; }
}
