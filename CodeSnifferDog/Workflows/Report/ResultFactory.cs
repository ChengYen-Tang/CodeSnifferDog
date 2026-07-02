using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;

namespace CodeSnifferDog.Workflows.Report;

internal static class ResultFactory
{
    public static RuleReportWorkflowResult Create(
        string ruleKey,
        StoredProjectPlanTaskItem taskItem,
        RuleReportDiff diff,
        IReadOnlyList<StoredRuleReportIssue> repositoryIssues,
        ReviewVerdict verdict,
        bool continuedAfterVerifierRejectionLimit,
        int aggregatorAttempts,
        int verifierAttempts) =>
        new()
        {
            RuleKey = ruleKey,
            TaskItem = taskItem,
            Diff = diff,
            RepositoryIssues = repositoryIssues,
            Verdict = verdict,
            ContinuedAfterVerifierRejectionLimit = continuedAfterVerifierRejectionLimit,
            AggregatorAttempts = aggregatorAttempts,
            VerifierAttempts = verifierAttempts,
        };
}
