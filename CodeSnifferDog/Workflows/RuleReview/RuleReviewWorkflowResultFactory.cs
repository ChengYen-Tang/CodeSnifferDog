using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.RuleReview;

namespace CodeSnifferDog.Workflows.RuleReview;

internal static class RuleReviewWorkflowResultFactory
{
    public static RuleReviewWorkflowResult Create(
        StoredProjectPlanTaskItem taskItem,
        string ruleKey,
        IReadOnlyList<StoredRuleReviewIssue> issues,
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
