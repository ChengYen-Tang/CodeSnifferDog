using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Models.RuleFlow;
using CodeSnifferDog.Modules.Concurrency;
using CodeSnifferDog.Workflows.Preparation;
using CodeSnifferDog.Workflows.ReviewGroup;
using CodeSnifferDog.Workflows.ReviewStage;
using FluentResults;

namespace CodeSnifferDog.Modules.ReviewAgentTeam;

public sealed class ReviewAgentTeamWorker : IDisposable, IAsyncDisposable
{
    private readonly string _repositoryRootPath;
    private readonly string[] _ruleMarkdowns;
    private readonly RepositoryPreparationWorkflow _preparationWorkflow;
    private readonly ReviewStageWorkflow _reviewStageWorkflow;
    private readonly ReviewAgentConcurrencyGate _concurrencyGate;
    private readonly Func<CancellationToken, ValueTask>? _cleanupAsync;
    private bool _disposed;

    internal ReviewAgentTeamWorker(
        string repositoryRootPath,
        IReadOnlyList<string> ruleMarkdowns,
        ReviewAgentTeamExecutionOptions executionOptions,
        ReviewAgentTeamDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentNullException.ThrowIfNull(ruleMarkdowns);
        ArgumentNullException.ThrowIfNull(executionOptions);

        if (executionOptions.MaxParallelAgents <= 0)
            throw new ArgumentOutOfRangeException(nameof(executionOptions), "Max parallel agents must be greater than zero.");
        if (executionOptions.ModelContextWindowTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(executionOptions), "Model context window tokens must be greater than zero.");

        _repositoryRootPath = repositoryRootPath.Trim();
        _ruleMarkdowns = ruleMarkdowns.Select(ruleMarkdown => ruleMarkdown?.Trim() ?? string.Empty).ToArray();
        _cleanupAsync = dependencies.CleanupAsync;
        ExecutionOptions = executionOptions;
        MaxParallelAgents = executionOptions.MaxParallelAgents;
        _concurrencyGate = new ReviewAgentConcurrencyGate(executionOptions.MaxParallelAgents);
        ReviewGroupWorkflow reviewGroupWorkflow = new();

        ReviewStageRuleLaneScheduler scheduler = new(dependencies.RuleFlowWorkflowRunner, _concurrencyGate);
        _preparationWorkflow = new RepositoryPreparationWorkflow(
            dependencies.ScanWorkflowRunner,
            dependencies.ProjectPlanWorkflowRunner,
            _concurrencyGate);
        _reviewStageWorkflow = new ReviewStageWorkflow(
            scheduler,
            (taskItem, ruleMarkdowns, flowResults) => reviewGroupWorkflow.Run(taskItem, ruleMarkdowns, flowResults));
    }

    public int MaxParallelAgents { get; }

    public ReviewAgentTeamExecutionOptions ExecutionOptions { get; }

    public Task<Result<ReviewAgentTeamRunResult>> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return AnalyzeCoreAsync(cancellationToken);
    }

    internal Task<Result<RepositoryPreparationWorkflowResult>> RunPreparationAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _preparationWorkflow.RunAsync(_repositoryRootPath, cancellationToken);
    }

    internal Task<Result<ReviewStageWorkflowResult>> RunReviewStageAsync(
        RepositoryPreparationWorkflowResult preparationResult,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _reviewStageWorkflow.RunAsync(_repositoryRootPath, preparationResult, _ruleMarkdowns, cancellationToken);
    }

    private async Task<Result<ReviewAgentTeamRunResult>> AnalyzeCoreAsync(CancellationToken cancellationToken)
    {
        Result<RepositoryPreparationWorkflowResult> preparationResult =
            await RunPreparationAsync(cancellationToken).ConfigureAwait(false);

        if (preparationResult.IsFailed)
            return preparationResult.ToResult<ReviewAgentTeamRunResult>();

        Result<ReviewStageWorkflowResult> reviewStageResult =
            await RunReviewStageAsync(preparationResult.Value, cancellationToken).ConfigureAwait(false);

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
        DisposeCoreAsync(isAsync: false).AsTask().GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync() => DisposeCoreAsync(isAsync: true);

    private async ValueTask DisposeCoreAsync(bool isAsync)
    {
        if (_disposed)
            return;

        _disposed = true;
        _concurrencyGate.Dispose();

        if (_cleanupAsync is null)
            return;

        if (isAsync)
        {
            await _cleanupAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        _cleanupAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ReviewAgentTeamWorker));
    }
}
