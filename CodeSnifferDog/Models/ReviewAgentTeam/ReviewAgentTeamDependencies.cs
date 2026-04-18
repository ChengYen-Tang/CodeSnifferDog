using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using FluentResults;

namespace CodeSnifferDog.Models.ReviewAgentTeam;

public sealed class ReviewAgentTeamDependencies
{
    public required Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> ScanWorkflowRunner { get; init; }

    public required Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> ProjectPlanWorkflowRunner { get; init; }

    public required Func<string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> RuleFlowWorkflowRunner { get; init; }

    public Func<CancellationToken, ValueTask>? CleanupAsync { get; init; }
}
