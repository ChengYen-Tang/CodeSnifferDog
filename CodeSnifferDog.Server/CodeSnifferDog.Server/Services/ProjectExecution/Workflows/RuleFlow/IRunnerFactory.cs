using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using FluentResults;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using ReportIssueStore = CodeSnifferDog.Modules.Tools.Report.IIssueStore;
using ReviewIssueStore = CodeSnifferDog.Modules.Tools.RuleReview.IIssueStore;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleFlow;

/// <summary>
/// Creates the workflow delegate that coordinates rule review and report generation.
/// </summary>
internal interface IRunnerFactory
{
    /// <summary>
    /// Creates the rule-flow workflow delegate for a specific runtime context and issue stores.
    /// </summary>
    /// <param name="context">Shared runtime services for the workflow execution.</param>
    /// <param name="ruleReviewCompactionOptions">Compaction behavior for the rule-review stage.</param>
    /// <param name="reportCompactionOptions">Compaction behavior for the report stage.</param>
    /// <param name="ruleReviewIssueStore">Issue store that receives rule-review findings.</param>
    /// <param name="ruleReportIssueStore">Issue store that receives report findings.</param>
    /// <returns>A delegate that runs a rule flow for a single task item.</returns>
    Func<string, string, string, StoredTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        CompactionOptions ruleReviewCompactionOptions,
        CompactionOptions reportCompactionOptions,
        ReviewIssueStore ruleReviewIssueStore,
        ReportIssueStore ruleReportIssueStore);
}
