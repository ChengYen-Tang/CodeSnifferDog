using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Report;
using FluentResults;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using ReportWorkflowResult = CodeSnifferDog.Models.Report.WorkflowResult;
using RuleReviewStoredIssue = CodeSnifferDog.Models.RuleReview.StoredIssue;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReport;

/// <summary>
/// Runs the workflow stage that aggregates reviewed issues into a repository report.
/// </summary>
internal interface IRunnerFactory
{
    /// <summary>
    /// Runs the rule-report workflow for a single task item.
    /// </summary>
    /// <param name="context">Shared runtime services for the workflow execution.</param>
    /// <param name="repositoryRootPath">Repository root path being reviewed.</param>
    /// <param name="ruleKey">Rule key associated with the current task item.</param>
    /// <param name="ruleMarkdown">Rule markdown supplied to report agents.</param>
    /// <param name="taskItem">Task item currently being processed.</param>
    /// <param name="currentFlowIssues">Issues produced by the rule-review stage for the same flow.</param>
    /// <param name="compactionOptions">Compaction behavior applied to report agents.</param>
    /// <param name="reportIssueStore">Issue store that receives repository report findings.</param>
    /// <param name="cancellationToken">Token that cancels the workflow execution.</param>
    /// <returns>The result produced by the rule-report workflow.</returns>
    Task<Result<ReportWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredTaskItem taskItem,
        IReadOnlyList<RuleReviewStoredIssue> currentFlowIssues,
        CompactionOptions compactionOptions,
        IIssueStore reportIssueStore,
        CancellationToken cancellationToken);
}
