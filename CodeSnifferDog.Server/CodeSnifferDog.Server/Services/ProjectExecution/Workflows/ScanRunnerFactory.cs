using CodeSnifferDog.Agents.Scan;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Workflows.Scan;
using FluentResults;
using System.Diagnostics;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

/// <summary>
/// Creates the runner that executes the scan workflow.
/// </summary>
internal sealed class ScanRunnerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IScanRunnerFactory
{
    /// <summary>
    /// Gets the prompt asset used when scan agents summarize compacted history.
    /// </summary>
    internal static string SummaryPromptAssetPath => ScanAgentPromptAssets.ScanSummaryPrompt;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ScanRunnerFactory> _logger = loggerFactory.CreateLogger<ScanRunnerFactory>();

    /// <inheritdoc />
    public Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        CompactionOptions compactionOptions) =>
        (repositoryRootPath, cancellationToken) =>
            RunAsync(context, repositoryRootPath, compactionOptions, cancellationToken);

    private async Task<Result<ScanWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        CompactionOptions compactionOptions,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Scan workflow started for repository {RepositoryRootPath}.", repositoryRootPath);

        InMemoryScanProjectStore scanProjectStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        Workflow workflow = new(
            (scanRepositoryRootPath, eventScope) => new ScanAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope,
                    _loggerFactory),
                context.PromptAssetReader,
                _loggerFactory,
                _serviceProvider).Create(context.ChatClient, scanRepositoryRootPath, scanProjectStore, verdictBuffer, eventScope),
            (scanRepositoryRootPath, eventScope) => new ScanVerifierAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope,
                    _loggerFactory),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, scanRepositoryRootPath, scanProjectStore, verdictBuffer, eventScope),
            scanProjectStore,
            verdictBuffer,
            context.PromptAssetReader,
            CreateWorkflowOptions(context.ExecutionOptions),
            agentEventBus: context.AgentEventBus,
            logger: _logger);

        Result<ScanWorkflowResult> result = await workflow.RunAsync(repositoryRootPath, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "Scan workflow completed in {DurationMs} ms. Success: {Succeeded}; project count: {ProjectCount}.",
            stopwatch.ElapsedMilliseconds,
            result.IsSuccess,
            result.IsSuccess ? result.Value.Projects.Count : 0);
        return result;
    }

    /// <summary>
    /// Creates workflow options for scan runs from the configured execution limits.
    /// </summary>
    /// <param name="executionOptions">Execution limits shared across review workflows.</param>
    /// <returns>The workflow options passed to the scan workflow.</returns>
    internal static ScanWorkflowOptions CreateWorkflowOptions(ExecutionOptions executionOptions) =>
        new()
        {
            AgentRunTimeout = executionOptions.AgentRunTimeout,
            MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
            MaxMissingSubmissionAttempts = executionOptions.MaxMissingSubmissionAttempts,
            MaxVerifierRejectionAttempts = executionOptions.MaxVerifierRejectionAttempts,
            MaxScanAgentResets = executionOptions.MaxVerifierRejectionAttempts,
        };
}
