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

internal interface IRunnerFactory
{
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
