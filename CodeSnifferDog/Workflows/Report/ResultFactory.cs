using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;
using ReportWorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;

namespace CodeSnifferDog.Workflows.Report;

/// <summary>
/// Creates report workflow results from workflow execution state.
/// </summary>
internal static class ResultFactory
{
    /// <summary>
    /// Creates one report workflow result.
    /// </summary>
    /// <param name="ruleKey">Rule key whose report was generated.</param>
    /// <param name="taskItem">Task item whose rule flow produced the report.</param>
    /// <param name="diff">Diff between the previous report snapshot and the current issues.</param>
    /// <param name="repositoryIssues">Latest repository-wide issues after promotion.</param>
    /// <param name="verdict">Latest verifier verdict.</param>
    /// <param name="continuedAfterVerifierRejectionLimit">Whether the report was accepted after reaching the verifier rejection limit.</param>
    /// <param name="aggregatorAttempts">Number of aggregator attempts performed.</param>
    /// <param name="verifierAttempts">Number of verifier attempts performed.</param>
    /// <returns>The composed report workflow result.</returns>
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
