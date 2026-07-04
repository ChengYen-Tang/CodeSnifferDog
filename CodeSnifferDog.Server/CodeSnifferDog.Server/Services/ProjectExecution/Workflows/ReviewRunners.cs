using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using FluentResults;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

/// <summary>
/// Bundles the workflow delegates that make up a review pipeline.
/// </summary>
internal sealed class ReviewRunners
{
    /// <summary>
    /// Gets the workflow delegate that scans the repository and discovers projects to review.
    /// </summary>
    public required Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> ScanWorkflowRunner { get; init; }

    /// <summary>
    /// Gets the workflow delegate that creates project-plan tasks from scan results.
    /// </summary>
    public required Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> ProjectPlanWorkflowRunner { get; init; }

    /// <summary>
    /// Gets the workflow delegate that runs rule review and report generation for a task item.
    /// </summary>
    public required Func<string, string, string, StoredTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> RuleFlowWorkflowRunner { get; init; }
}
