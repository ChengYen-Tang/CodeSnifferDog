using CodeSnifferDog.Agents.Scan;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Modules.Tools.Scan;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Workflows.Scan;
using FluentResults;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal sealed class ScanRunnerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider)
{
    internal static string SummaryPromptAssetPath => ScanPromptAssetPaths.ScanSummaryPrompt;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public Func<string, CancellationToken, Task<Result<ScanWorkflowResult>>> CreateRunner(
        RunnerFactoryContext context,
        OperationalContextCompactionOptions compactionOptions) =>
        (repositoryRootPath, cancellationToken) =>
            RunAsync(context, repositoryRootPath, compactionOptions, cancellationToken);

    private Task<Result<ScanWorkflowResult>> RunAsync(
        RunnerFactoryContext context,
        string repositoryRootPath,
        OperationalContextCompactionOptions compactionOptions,
        CancellationToken cancellationToken)
    {
        InMemoryScanProjectStore scanProjectStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        ScanWorkflow workflow = new(
            (scanRepositoryRootPath, eventScope) => new ScanAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope),
                context.PromptAssetReader,
                _loggerFactory,
                _serviceProvider).Create(context.ChatClient, scanRepositoryRootPath, scanProjectStore, verdictBuffer, eventScope),
            (scanRepositoryRootPath, eventScope) => new ScanVerifierAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, scanRepositoryRootPath, scanProjectStore, verdictBuffer, eventScope),
            scanProjectStore,
            verdictBuffer,
            context.PromptAssetReader,
            CreateWorkflowOptions(context.ExecutionOptions),
            agentEventBus: context.AgentEventBus);

        return workflow.RunAsync(repositoryRootPath, cancellationToken);
    }

    internal static ScanWorkflowOptions CreateWorkflowOptions(ExecutionOptions executionOptions) =>
        new()
        {
            AgentRunTimeout = executionOptions.AgentRunTimeout,
            MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
        };
}
