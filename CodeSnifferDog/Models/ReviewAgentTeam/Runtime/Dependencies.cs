using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Tools.Report;
using FluentResults;
using ProjectPlanWorkflowResult = CodeSnifferDog.Models.ProjectPlan.WorkflowResult;
using RuleFlowWorkflowResult = CodeSnifferDog.Models.RuleFlow.WorkflowResult;

namespace CodeSnifferDog.Models.ReviewAgentTeam.Runtime;

/// <summary>
/// Collects workflow runners and infrastructure dependencies required by the review-agent runtime.
/// </summary>
public sealed class Dependencies
{
    /// <summary>
    /// Gets the repository scan workflow runner.
    /// </summary>
    public required Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> ScanWorkflowRunner { get; init; }

    /// <summary>
    /// Gets the project-plan workflow runner for one scanned project.
    /// </summary>
    public required Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> ProjectPlanWorkflowRunner { get; init; }

    /// <summary>
    /// Gets the rule-flow workflow runner for one task item and rule.
    /// </summary>
    public required Func<string, string, string, StoredTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> RuleFlowWorkflowRunner { get; init; }

    /// <summary>
    /// Gets the issue store used for repository-level rule reports.
    /// </summary>
    public required IIssueStore RuleReportIssueStore { get; init; }

    /// <summary>
    /// Gets the optional event bus used to publish agent lifecycle and transcript events.
    /// </summary>
    public IAgentEventBus? AgentEventBus { get; init; }

    /// <summary>
    /// Gets the optional cleanup callback invoked after execution completes.
    /// </summary>
    public Func<CancellationToken, ValueTask>? CleanupAsync { get; init; }
}
