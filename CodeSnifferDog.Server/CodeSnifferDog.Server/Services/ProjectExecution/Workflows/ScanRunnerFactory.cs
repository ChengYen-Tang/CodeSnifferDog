using CodeSnifferDog.Agents.Scan;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Workflows.Scan;
using FluentResults;
using System.Diagnostics;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal sealed class ScanRunnerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IScanRunnerFactory
{
    internal static string SummaryPromptAssetPath => ScanPromptAssetPaths.ScanSummaryPrompt;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ScanRunnerFactory> _logger = loggerFactory.CreateLogger<ScanRunnerFactory>();

    public Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        OperationalContextCompactionOptions compactionOptions) =>
        (repositoryRootPath, cancellationToken) =>
            RunAsync(context, repositoryRootPath, compactionOptions, cancellationToken);

    private async Task<Result<ScanWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        OperationalContextCompactionOptions compactionOptions,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Scan workflow started for repository {RepositoryRootPath}.", repositoryRootPath);

        InMemoryScanProjectStore scanProjectStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        ScanWorkflow workflow = new(
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
            agentEventBus: context.AgentEventBus);

        Result<ScanWorkflowResult> result = await workflow.RunAsync(repositoryRootPath, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "Scan workflow completed in {DurationMs} ms. Success: {Succeeded}; project count: {ProjectCount}.",
            stopwatch.ElapsedMilliseconds,
            result.IsSuccess,
            result.IsSuccess ? result.Value.Projects.Count : 0);
        return result;
    }

    internal static ScanWorkflowOptions CreateWorkflowOptions(ExecutionOptions executionOptions) =>
        new()
        {
            AgentRunTimeout = executionOptions.AgentRunTimeout,
            MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
        };
}
