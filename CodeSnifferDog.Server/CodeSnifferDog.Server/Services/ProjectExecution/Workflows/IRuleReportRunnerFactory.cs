using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.Report;
using FluentResults;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal interface IRuleReportRunnerFactory
{
    Task<Result<RuleReportWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        IReadOnlyList<StoredRuleReviewIssue> currentFlowIssues,
        OperationalContextCompactionOptions compactionOptions,
        IRuleReportIssueStore reportIssueStore,
        CancellationToken cancellationToken);
}
