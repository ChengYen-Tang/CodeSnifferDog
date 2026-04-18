using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Concurrency;
using CodeSnifferDog.Workflows.Preparation;
using CodeSnifferDog.Workflows.ReviewGroup;
using CodeSnifferDog.Workflows.ReviewStage;
using FluentResults;

namespace CodeSnifferDog.Modules.ReviewAgentTeam;

public sealed class ReviewAgentTeamWorker : IDisposable
{
    private readonly RepositoryPreparationWorkflow _preparationWorkflow;
    private readonly ReviewStageWorkflow _reviewStageWorkflow;
    private readonly ReviewAgentConcurrencyGate _concurrencyGate;
    private bool _disposed;

    public ReviewAgentTeamWorker(
        int maxParallelAgents,
        Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> scanWorkflowRunner,
        Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> projectPlanWorkflowRunner,
        Func<string, string, StoredProjectPlanTaskItem, CancellationToken, Task<Result<RuleFlowWorkflowResult>>> ruleFlowWorkflowRunner,
        ReviewGroupWorkflow reviewGroupWorkflow)
    {
        ArgumentNullException.ThrowIfNull(scanWorkflowRunner);
        ArgumentNullException.ThrowIfNull(projectPlanWorkflowRunner);
        ArgumentNullException.ThrowIfNull(ruleFlowWorkflowRunner);
        ArgumentNullException.ThrowIfNull(reviewGroupWorkflow);

        MaxParallelAgents = maxParallelAgents;
        _concurrencyGate = new ReviewAgentConcurrencyGate(maxParallelAgents);

        ReviewStageRuleLaneScheduler scheduler = new(ruleFlowWorkflowRunner, _concurrencyGate);
        _preparationWorkflow = new RepositoryPreparationWorkflow(scanWorkflowRunner, projectPlanWorkflowRunner, _concurrencyGate);
        _reviewStageWorkflow = new ReviewStageWorkflow(
            scheduler,
            (taskItem, ruleMarkdowns, flowResults) => reviewGroupWorkflow.Run(taskItem, ruleMarkdowns, flowResults));
    }

    public int MaxParallelAgents { get; }

    public Task<Result<RepositoryPreparationWorkflowResult>> RunPreparationAsync(
        string repositoryRootPath,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _preparationWorkflow.RunAsync(repositoryRootPath, cancellationToken);
    }

    public Task<Result<ReviewStageWorkflowResult>> RunReviewStageAsync(
        string repositoryRootPath,
        RepositoryPreparationWorkflowResult preparationResult,
        IReadOnlyList<string> ruleMarkdowns,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _reviewStageWorkflow.RunAsync(repositoryRootPath, preparationResult, ruleMarkdowns, cancellationToken);
    }

    public async Task<Result<ReviewAgentTeamRunResult>> RunAsync(
        string repositoryRootPath,
        IReadOnlyList<string> ruleMarkdowns,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Result<RepositoryPreparationWorkflowResult> preparationResult =
            await RunPreparationAsync(repositoryRootPath, cancellationToken).ConfigureAwait(false);

        if (preparationResult.IsFailed)
            return preparationResult.ToResult<ReviewAgentTeamRunResult>();

        Result<ReviewStageWorkflowResult> reviewStageResult =
            await RunReviewStageAsync(repositoryRootPath, preparationResult.Value, ruleMarkdowns, cancellationToken).ConfigureAwait(false);

        if (reviewStageResult.IsFailed)
            return reviewStageResult.ToResult<ReviewAgentTeamRunResult>();

        return Result.Ok(new ReviewAgentTeamRunResult
        {
            PreparationResult = preparationResult.Value,
            ReviewStageResult = reviewStageResult.Value,
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _concurrencyGate.Dispose();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ReviewAgentTeamWorker));
    }
}
