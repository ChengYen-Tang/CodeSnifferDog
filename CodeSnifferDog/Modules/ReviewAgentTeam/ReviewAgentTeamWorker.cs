using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Modules.Concurrency;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Workflows.Preparation;
using CodeSnifferDog.Workflows.Report;
using CodeSnifferDog.Workflows.ReviewGroup;
using CodeSnifferDog.Workflows.ReviewStage;
using FluentResults;

namespace CodeSnifferDog.Modules.ReviewAgentTeam;

public sealed class ReviewAgentTeamWorker : IDisposable, IAsyncDisposable
{
    private readonly string _repositoryRootPath;
    private readonly ReviewAgentRuleDefinition[] _ruleDefinitions;
    private readonly RepositoryPreparationWorkflow _preparationWorkflow;
    private readonly ReviewStageWorkflow _reviewStageWorkflow;
    private readonly ReviewAgentConcurrencyGate _concurrencyGate;
    private readonly IRuleReportIssueStore _ruleReportIssueStore;
    private readonly IAgentStatusEventPublisher _agentStatusEventPublisher;
    private readonly Func<CancellationToken, ValueTask>? _cleanupAsync;
    private bool _disposed;

    internal ReviewAgentTeamWorker(
        string repositoryRootPath,
        IReadOnlyList<ReviewAgentRuleDefinition> ruleDefinitions,
        ReviewAgentTeamExecutionOptions executionOptions,
        ReviewAgentTeamDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRootPath);
        ArgumentNullException.ThrowIfNull(ruleDefinitions);
        ArgumentNullException.ThrowIfNull(executionOptions);

        if (executionOptions.MaxParallelAgents <= 0)
            throw new ArgumentOutOfRangeException(nameof(executionOptions), "Max parallel agents must be greater than zero.");
        if (executionOptions.ModelContextWindowTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(executionOptions), "Model context window tokens must be greater than zero.");

        _repositoryRootPath = repositoryRootPath.Trim();
        _ruleDefinitions =
            [.. ruleDefinitions.Select(ruleDefinition => new ReviewAgentRuleDefinition
            {
                RuleKey = ruleDefinition?.RuleKey?.Trim() ?? string.Empty,
                RuleMarkdown = ruleDefinition?.RuleMarkdown?.Trim() ?? string.Empty,
            })];
        _ruleReportIssueStore = dependencies.RuleReportIssueStore;
        _agentStatusEventPublisher = dependencies.AgentStatusEventPublisher ?? NoOpAgentStatusEventPublisher.Instance;
        _cleanupAsync = dependencies.CleanupAsync;
        ExecutionOptions = executionOptions;
        MaxParallelAgents = executionOptions.MaxParallelAgents;
        _concurrencyGate = new ReviewAgentConcurrencyGate(executionOptions.MaxParallelAgents);
        ReviewStageRuleLaneScheduler scheduler = new(dependencies.RuleFlowWorkflowRunner, _concurrencyGate);
        _preparationWorkflow = new RepositoryPreparationWorkflow(
            dependencies.ScanWorkflowRunner,
            dependencies.ProjectPlanWorkflowRunner,
            _concurrencyGate);
        _reviewStageWorkflow = new ReviewStageWorkflow(
            scheduler,
            ReviewGroupWorkflow.Run,
            _agentStatusEventPublisher);
    }

    public int MaxParallelAgents { get; }

    public ReviewAgentTeamExecutionOptions ExecutionOptions { get; }

    public Task<Result> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return AnalyzeCoreAsync(cancellationToken);
    }

    public Task<IReadOnlyList<ReviewAgentTeamRuleReport>> GetRuleReportsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return BuildRuleReportsAsync(cancellationToken);
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
        return _reviewStageWorkflow.RunAsync(_repositoryRootPath, preparationResult, _ruleDefinitions, cancellationToken);
    }

    private async Task<Result> AnalyzeCoreAsync(CancellationToken cancellationToken)
    {
        Result<RepositoryPreparationWorkflowResult> preparationResult =
            await RunPreparationAsync(cancellationToken).ConfigureAwait(false);

        if (preparationResult.IsFailed)
            return preparationResult.ToResult();

        Result<ReviewStageWorkflowResult> reviewStageResult =
            await RunReviewStageAsync(preparationResult.Value, cancellationToken).ConfigureAwait(false);

        if (reviewStageResult.IsFailed)
            return reviewStageResult.ToResult();

        return Result.Ok();
    }

    private async Task<IReadOnlyList<ReviewAgentTeamRuleReport>> BuildRuleReportsAsync(CancellationToken cancellationToken)
    {
        List<ReviewAgentTeamRuleReport> ruleReports = [];

        foreach (ReviewAgentRuleDefinition ruleDefinition in _ruleDefinitions)
        {
            RuleReportKey ruleReportKey = RuleScopeKeyFactory.CreateRuleReportKey(_repositoryRootPath, ruleDefinition.RuleKey);
            IReadOnlyList<StoredRuleReportIssue> issues =
                await _ruleReportIssueStore.GetLatestSnapshotAsync(ruleReportKey, cancellationToken).ConfigureAwait(false);

            ruleReports.Add(new ReviewAgentTeamRuleReport
            {
                RuleKey = ruleDefinition.RuleKey,
                MarkdownContent = RuleMarkdownReportRenderer.Render(ruleDefinition.RuleKey, issues),
            });
        }

        return ruleReports;
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
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
