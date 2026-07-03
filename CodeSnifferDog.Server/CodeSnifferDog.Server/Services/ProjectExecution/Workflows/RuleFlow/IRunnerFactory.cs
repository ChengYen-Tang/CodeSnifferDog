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

internal interface IRunnerFactory
{
    Func<string, string, string, StoredTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        CompactionOptions ruleReviewCompactionOptions,
        CompactionOptions reportCompactionOptions,
        ReviewIssueStore ruleReviewIssueStore,
        ReportIssueStore ruleReportIssueStore);
}
