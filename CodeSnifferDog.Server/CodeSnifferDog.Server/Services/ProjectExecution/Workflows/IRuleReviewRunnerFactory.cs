using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.RuleReview;
using FluentResults;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal interface IRuleReviewRunnerFactory
{
    Task<Result<RuleReviewWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredProjectPlanTaskItem taskItem,
        OperationalContextCompactionOptions compactionOptions,
        IRuleReviewIssueStore issueStore,
        CancellationToken cancellationToken);
}
