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

/// <summary>
/// Coordinates preparation, review-stage execution, report generation, and cleanup for one review-agent-team run.
/// </summary>
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

    /// <summary>
    /// Creates a worker bound to one repository, one rule set, and one dependency bundle.
    /// </summary>
    /// <param name="repositoryRootPath">Repository root path that the workflows should analyze.</param>
    /// <param name="ruleDefinitions">Rule definitions that drive review-stage execution and report generation.</param>
    /// <param name="executionOptions">Execution settings such as parallelism and model context size.</param>
    /// <param name="dependencies">Workflow runners, stores, event bus, and cleanup hooks used by the worker.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ruleDefinitions" />, <paramref name="executionOptions" />, or <paramref name="dependencies" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="repositoryRootPath" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="executionOptions" /> declares non-positive parallelism or context-window limits.</exception>
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

    /// <summary>
    /// Gets the maximum number of review agents that may run concurrently.
    /// </summary>
    public int MaxParallelAgents { get; }

    /// <summary>
    /// Gets the execution settings used by this worker.
    /// </summary>
    public ExecutionOptions ExecutionOptions { get; }

    /// <summary>
    /// Runs the full analysis workflow and returns only success or failure.
    /// </summary>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>A successful result when analysis completes with acceptable workflow outcomes; otherwise a failed result.</returns>
    /// <exception cref="ObjectDisposedException">The worker has already been disposed.</exception>
    public Task<Result> AnalyzeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        return AnalyzeCoreAsync(cancellationToken);
    }

    /// <summary>
    /// Builds the rendered rule reports currently stored for this worker's repository and rule set.
    /// </summary>
    /// <param name="cancellationToken">Cancels report generation.</param>
    /// <returns>The rendered rule reports.</returns>
    /// <exception cref="ObjectDisposedException">The worker has already been disposed.</exception>
    public Task<IReadOnlyList<RuleReport>> GetRuleReportsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return BuildRuleReportsAsync(cancellationToken);
    }

    /// <summary>
    /// Runs the full analysis workflow and returns detailed preparation, review-stage, error, and reporting metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>The detailed analysis result.</returns>
    /// <exception cref="ObjectDisposedException">The worker has already been disposed.</exception>
    internal Task<AnalysisResult> AnalyzeDetailedAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return AnalyzeDetailedCoreAsync(cancellationToken);
    }

    /// <summary>
    /// Runs the preparation workflow for the worker's repository.
    /// </summary>
    /// <param name="cancellationToken">Cancels preparation.</param>
    /// <returns>The preparation workflow result.</returns>
    /// <exception cref="ObjectDisposedException">The worker has already been disposed.</exception>
    internal Task<Result<PreparationWorkflowResult>> RunPreparationAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _preparationWorkflow.RunAsync(_repositoryRootPath, cancellationToken);
    }

    /// <summary>
    /// Runs the review stage using an existing preparation result.
    /// </summary>
    /// <param name="preparationResult">Preparation result that supplies the project and task plan inputs.</param>
    /// <param name="cancellationToken">Cancels review-stage execution.</param>
    /// <returns>The review-stage workflow result.</returns>
    /// <exception cref="ObjectDisposedException">The worker has already been disposed.</exception>
    internal Task<Result<ReviewStageWorkflowResult>> RunReviewStageAsync(
        PreparationWorkflowResult preparationResult,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _reviewStageWorkflow.RunAsync(_repositoryRootPath, preparationResult, _ruleDefinitions, cancellationToken);
    }

    /// <summary>
    /// Runs detailed analysis and projects the outcome down to a simple success/failure result.
    /// </summary>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>A successful result when the completion policy accepts the detailed analysis result.</returns>
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

    /// <summary>
    /// Runs preparation, review-stage execution, and report generation, collecting a detailed analysis summary.
    /// </summary>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    /// <returns>The detailed analysis result.</returns>
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

    /// <summary>
    /// Builds rendered rule reports for every configured rule definition.
    /// </summary>
    /// <param name="cancellationToken">Cancels report generation.</param>
    /// <returns>The rendered rule reports.</returns>
    private async Task<IReadOnlyList<RuleReport>> BuildRuleReportsAsync(CancellationToken cancellationToken)
    {
        return (await BuildRuleReportsWithMetadataAsync(cancellationToken).ConfigureAwait(false)).RuleReports;
    }

    /// <summary>
    /// Builds rendered rule reports and records whether any findings were present.
    /// </summary>
    /// <param name="cancellationToken">Cancels report generation.</param>
    /// <returns>The rendered rule reports plus the aggregate findings flag.</returns>
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

    /// <summary>
    /// Determines whether every rule flow inside the review-stage result completed with approval.
    /// </summary>
    /// <param name="reviewStageResult">Review-stage result to inspect.</param>
    /// <returns><see langword="true" /> when every rule flow approved completion.</returns>
    private static bool AllRuleFlowsSucceeded(ReviewStageWorkflowResult reviewStageResult) =>
        reviewStageResult.ProjectResults
            .SelectMany(projectResult => projectResult.ReviewGroupResults)
            .SelectMany(reviewGroupResult => reviewGroupResult.FlowResults)
            .All(flowResult => flowResult.IsApprovedCompletion);

    /// <summary>
    /// Holds rendered rule reports together with an aggregate findings flag.
    /// </summary>
    private sealed class RuleReportBuildResult
    {
        public required IReadOnlyList<RuleReport> RuleReports { get; init; }

        public required bool HasAnyFindings { get; init; }
    }

    /// <summary>
    /// Releases worker resources synchronously and runs the optional cleanup callback.
    /// </summary>
    public void Dispose()
    {
        DisposeCoreAsync(isAsync: false).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Releases worker resources asynchronously and runs the optional cleanup callback.
    /// </summary>
    public ValueTask DisposeAsync() => DisposeCoreAsync(isAsync: true);

    /// <summary>
    /// Releases worker resources and optionally invokes the configured cleanup callback.
    /// </summary>
    /// <param name="isAsync"><see langword="true" /> to await cleanup asynchronously; otherwise cleanup is blocked synchronously.</param>
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

    /// <summary>
    /// Throws when the worker has already been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
