using CodeSnifferDog.Models.Preparation;
using CodeSnifferDog.Models.Report;
using CodeSnifferDog.Models.Review;
using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewStage;
using CodeSnifferDog.Modules.Concurrency;
using CodeSnifferDog.Modules.Tools.Report;
using CodeSnifferDog.Workflows.Report;
using CodeSnifferDog.Workflows.ReviewGroup;
using FluentResults;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;
using CodeSnifferDog.Modules.ReviewAgentTeam.Scheduling;
using PreparationWorkflow = CodeSnifferDog.Workflows.Preparation.Workflow;
using PreparationWorkflowResult = CodeSnifferDog.Models.Preparation.WorkflowResult;
using ReportStoredIssue = CodeSnifferDog.Models.Report.StoredIssue;
using ReviewGroupWorkflow = CodeSnifferDog.Workflows.ReviewGroup.Workflow;
using ReviewStageWorkflow = CodeSnifferDog.Workflows.ReviewStage.Workflow;
using ReviewStageWorkflowResult = CodeSnifferDog.Models.ReviewStage.WorkflowResult;

namespace CodeSnifferDog.Modules.ReviewAgentTeam.Runtime;

public sealed class Worker : IDisposable, IAsyncDisposable
{
    private readonly string _repositoryRootPath;
    private readonly RuleDefinition[] _ruleDefinitions;
    private readonly PreparationWorkflow _preparationWorkflow;
    private readonly ReviewStageWorkflow _reviewStageWorkflow;
    private readonly ReviewAgentConcurrencyGate _concurrencyGate;
    private readonly IIssueStore _ruleReportIssueStore;
    private readonly IAgentEventBus _agentEventBus;
    private readonly Func<CancellationToken, ValueTask>? _cleanupAsync;
    private bool _disposed;

    internal Worker(
        string repositoryRootPath,
        IReadOnlyList<RuleDefinition> ruleDefinitions,
        ExecutionOptions executionOptions,
        Dependencies dependencies)
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
            [.. ruleDefinitions.Select(ruleDefinition => new RuleDefinition
            {
                RuleKey = ruleDefinition?.RuleKey?.Trim() ?? string.Empty,
                RuleMarkdown = ruleDefinition?.RuleMarkdown?.Trim() ?? string.Empty,
            })];
        _ruleReportIssueStore = dependencies.RuleReportIssueStore;
        _agentEventBus = dependencies.AgentEventBus ?? NoOpAgentEventBus.Instance;
        _cleanupAsync = dependencies.CleanupAsync;
        ExecutionOptions = executionOptions;
        MaxParallelAgents = executionOptions.MaxParallelAgents;
        _concurrencyGate = new ReviewAgentConcurrencyGate(executionOptions.MaxParallelAgents);
        RuleLaneScheduler scheduler = new(dependencies.RuleFlowWorkflowRunner, _concurrencyGate);
        _preparationWorkflow = new PreparationWorkflow(
            dependencies.ScanWorkflowRunner,
            dependencies.ProjectPlanWorkflowRunner,
            _concurrencyGate);
        _reviewStageWorkflow = new ReviewStageWorkflow(
            scheduler,
            ReviewGroupWorkflow.Run,
            _agentEventBus);
    }

    public int MaxParallelAgents { get; }

    public ExecutionOptions ExecutionOptions { get; }

    public Task<Result> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return AnalyzeCoreAsync(cancellationToken);
    }

    public Task<IReadOnlyList<RuleReport>> GetRuleReportsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return BuildRuleReportsAsync(cancellationToken);
    }

    internal Task<AnalysisResult> AnalyzeDetailedAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return AnalyzeDetailedCoreAsync(cancellationToken);
    }

    internal Task<Result<PreparationWorkflowResult>> RunPreparationAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _preparationWorkflow.RunAsync(_repositoryRootPath, cancellationToken);
    }

    internal Task<Result<ReviewStageWorkflowResult>> RunReviewStageAsync(
        PreparationWorkflowResult preparationResult,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _reviewStageWorkflow.RunAsync(_repositoryRootPath, preparationResult, _ruleDefinitions, cancellationToken);
    }

    private async Task<Result> AnalyzeCoreAsync(CancellationToken cancellationToken)
    {
        AnalysisResult analysisResult =
            await AnalyzeDetailedCoreAsync(cancellationToken).ConfigureAwait(false);
        CompletionDecision completionDecision =
            CompletionPolicy.Evaluate(analysisResult);

        if (!completionDecision.IsSuccess)
            return Result.Fail(completionDecision.FailureMessage ?? "Project analysis failed.");

        return Result.Ok();
    }

    private async Task<AnalysisResult> AnalyzeDetailedCoreAsync(CancellationToken cancellationToken)
    {
        Result<PreparationWorkflowResult> preparationResult =
            await RunPreparationAsync(cancellationToken).ConfigureAwait(false);

        if (preparationResult.IsFailed)
        {
            return new AnalysisResult
            {
                PreparationSucceeded = false,
                ReviewStageSucceeded = false,
                HasAnyFindings = false,
                AllRuleFlowsSucceeded = false,
                ExecutionErrors = [.. preparationResult.Errors.Select(error => error.Message)],
                RuleReports = [],
            };
        }

        Result<ReviewStageWorkflowResult> reviewStageResult =
            await RunReviewStageAsync(preparationResult.Value, cancellationToken).ConfigureAwait(false);
        RuleReportBuildResult reportBuildResult = await BuildRuleReportsWithMetadataAsync(cancellationToken).ConfigureAwait(false);

        return new AnalysisResult
        {
            PreparationSucceeded = true,
            ReviewStageSucceeded = reviewStageResult.IsSuccess,
            HasAnyFindings = reportBuildResult.HasAnyFindings,
            AllRuleFlowsSucceeded = reviewStageResult.IsSuccess && AllRuleFlowsSucceeded(reviewStageResult.Value),
            ExecutionErrors = reviewStageResult.IsFailed
                ? [.. reviewStageResult.Errors.Select(error => error.Message)]
                : [],
            RuleReports = reportBuildResult.RuleReports,
        };
    }

    private async Task<IReadOnlyList<RuleReport>> BuildRuleReportsAsync(CancellationToken cancellationToken)
    {
        return (await BuildRuleReportsWithMetadataAsync(cancellationToken).ConfigureAwait(false)).RuleReports;
    }

    private async Task<RuleReportBuildResult> BuildRuleReportsWithMetadataAsync(CancellationToken cancellationToken)
    {
        List<RuleReport> ruleReports = [];
        bool hasAnyFindings = false;

        foreach (RuleDefinition ruleDefinition in _ruleDefinitions)
        {
            RuleReportKey ruleReportKey = RuleScopeKeyFactory.CreateRuleReportKey(_repositoryRootPath, ruleDefinition.RuleKey);
            IReadOnlyList<ReportStoredIssue> issues =
                await _ruleReportIssueStore.GetLatestSnapshotAsync(ruleReportKey, cancellationToken).ConfigureAwait(false);
            hasAnyFindings |= issues.Count > 0;

            ruleReports.Add(new RuleReport
            {
                RuleKey = ruleDefinition.RuleKey,
                MarkdownContent = RuleMarkdownReportRenderer.Render(ruleDefinition.RuleKey, issues),
            });
        }

        return new RuleReportBuildResult
        {
            RuleReports = ruleReports,
            HasAnyFindings = hasAnyFindings,
        };
    }

    private static bool AllRuleFlowsSucceeded(ReviewStageWorkflowResult reviewStageResult) =>
        reviewStageResult.ProjectResults
            .SelectMany(projectResult => projectResult.ReviewGroupResults)
            .SelectMany(reviewGroupResult => reviewGroupResult.FlowResults)
            .All(flowResult => flowResult.IsApprovedCompletion);

    private sealed class RuleReportBuildResult
    {
        public required IReadOnlyList<RuleReport> RuleReports { get; init; }

        public required bool HasAnyFindings { get; init; }
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
