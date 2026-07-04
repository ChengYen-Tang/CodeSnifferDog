using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Models.RuleReview;
using CodeSnifferDog.Modules.Tools.RuleReview;
using FluentResults;
using CodeSnifferDog.Models.ContextCompaction.Compaction;
using RuleReviewWorkflowResult = CodeSnifferDog.Models.RuleReview.WorkflowResult;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReview;

/// <summary>
/// Runs the workflow stage that reviews a rule against a planned task item.
/// </summary>
internal interface IRunnerFactory
{
    /// <summary>
    /// Runs the rule-review workflow for a single task item.
    /// </summary>
    /// <param name="context">Shared runtime services for the workflow execution.</param>
    /// <param name="repositoryRootPath">Repository root path being reviewed.</param>
    /// <param name="ruleKey">Rule key associated with the current task item.</param>
    /// <param name="ruleMarkdown">Rule markdown supplied to review agents.</param>
    /// <param name="taskItem">Task item currently being processed.</param>
    /// <param name="compactionOptions">Compaction behavior applied to review agents.</param>
    /// <param name="issueStore">Issue store that receives review findings.</param>
    /// <param name="cancellationToken">Token that cancels the workflow execution.</param>
    /// <returns>The result produced by the rule-review workflow.</returns>
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
