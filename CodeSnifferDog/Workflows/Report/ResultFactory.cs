using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;
using ReportWorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;

namespace CodeSnifferDog.Workflows.Report;

internal static class ResultFactory
{
    public static ReportWorkflowResult Create(
        string ruleKey,
        StoredTaskItem taskItem,
        Diff diff,
        IReadOnlyList<ReportStoredIssue> repositoryIssues,
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
