using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;
using RuleReviewWorkflowResult = CodeSnifferDog.Models.RuleReview.WorkflowResult;

namespace CodeSnifferDog.Workflows.RuleReview;

/// <summary>
/// Creates rule-review workflow results from workflow execution state.
/// </summary>
internal static class ResultFactory
{
    /// <summary>
    /// Creates one rule-review workflow result.
    /// </summary>
    /// <param name="taskItem">Task item that was reviewed.</param>
    /// <param name="ruleKey">Rule key that was reviewed.</param>
    /// <param name="issues">Issues produced by the reviewer.</param>
    /// <param name="noIssueConclusion">No-issue conclusion produced when no issues were reported.</param>
    /// <param name="verdict">Latest verifier verdict.</param>
    /// <param name="reviewAttempts">Number of review-agent attempts performed.</param>
    /// <param name="verifierAttempts">Number of verifier attempts performed.</param>
    /// <param name="ruleReviewAgentResetCount">Number of reviewer resets triggered after missing submissions.</param>
    /// <param name="continuedAfterVerifierRejectionLimit">Whether the result was accepted after reaching the verifier rejection limit.</param>
    /// <param name="stoppedAfterMissingSubmissionLimit">Whether execution stopped after exhausting the missing-submission limit.</param>
    /// <returns>The composed rule-review workflow result.</returns>
    public static RuleReviewWorkflowResult Create(
        StoredTaskItem taskItem,
        string ruleKey,
        IReadOnlyList<StoredIssue> issues,
        NoIssueConclusion? noIssueConclusion,
        ReviewVerdict verdict,
        int reviewAttempts,
        int verifierAttempts,
        int ruleReviewAgentResetCount,
        bool continuedAfterVerifierRejectionLimit,
        bool stoppedAfterMissingSubmissionLimit) =>
        new()
        {
            TaskItem = taskItem,
            RuleKey = ruleKey,
            Issues = issues,
            NoIssueConclusion = noIssueConclusion,
            Verdict = verdict,
            ContinuedAfterVerifierRejectionLimit = continuedAfterVerifierRejectionLimit,
            StoppedAfterMissingSubmissionLimit = stoppedAfterMissingSubmissionLimit,
            ReviewAttempts = reviewAttempts,
            VerifierAttempts = verifierAttempts,
            RuleReviewAgentResetCount = ruleReviewAgentResetCount,
        };
}
