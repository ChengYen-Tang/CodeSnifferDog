using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Modules.Tools.RuleReview;
using FluentResults;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal interface IRuleFlowRunnerFactory
{
    Func<string, string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        OperationalContextCompactionOptions ruleReviewCompactionOptions,
        OperationalContextCompactionOptions reportCompactionOptions,
        IRuleReviewIssueStore ruleReviewIssueStore,
        IRuleReportIssueStore ruleReportIssueStore);
}
