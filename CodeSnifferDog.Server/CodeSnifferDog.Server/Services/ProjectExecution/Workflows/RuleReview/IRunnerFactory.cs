using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.RuleReview;
using FluentResults;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using RuleReviewWorkflowResult = CodeSnifferDog.Models.RuleReview.WorkflowResult;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReview;

internal interface IRunnerFactory
{
    Task<Result<RuleReviewWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        string ruleKey,
        string ruleMarkdown,
        StoredTaskItem taskItem,
        CompactionOptions compactionOptions,
        IIssueStore issueStore,
        CancellationToken cancellationToken);
}
