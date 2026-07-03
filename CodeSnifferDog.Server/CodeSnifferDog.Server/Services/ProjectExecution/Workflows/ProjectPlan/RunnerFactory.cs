using CodeSnifferDog.Agents.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Review;

using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Workflows.ProjectPlan;
using FluentResults;
using System.Diagnostics;
using CodeSnifferDog.Models.ContextCompaction.Compaction;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows.ProjectPlan;

internal sealed class RunnerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IRunnerFactory
{
    internal static string SummaryPromptAssetPath => ProjectPlanAgentPromptAssets.ProjectPlanSummaryPrompt;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<RunnerFactory> _logger = loggerFactory.CreateLogger<RunnerFactory>();

    public Func<string, StoredScanProject, CancellationToken, Task<Result<WorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        CompactionOptions compactionOptions) =>
        (repositoryRootPath, scanProject, cancellationToken) =>
            RunAsync(context, repositoryRootPath, scanProject, compactionOptions, cancellationToken);

    private async Task<Result<WorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        StoredScanProject scanProject,
        CompactionOptions compactionOptions,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Project plan workflow started for project {ProjectName}. Repository: {RepositoryRootPath}.",
            scanProject.ProjectName,
            repositoryRootPath);

        InMemoryTaskItemStore taskItemStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        Workflow workflow = new(
            (planRepositoryRootPath, eventScope) => new AgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope,
                    _loggerFactory),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, planRepositoryRootPath, taskItemStore, verdictBuffer, eventScope),
            (planRepositoryRootPath, verifierScanProject, eventScope) => new VerifierFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope,
                    _loggerFactory),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, planRepositoryRootPath, verifierScanProject, taskItemStore, verdictBuffer, eventScope),
            taskItemStore,
            verdictBuffer,
            context.PromptAssetReader,
            CreateWorkflowOptions(context.ExecutionOptions),
            agentEventBus: context.AgentEventBus);

        Result<WorkflowResult> result = await workflow.RunAsync(repositoryRootPath, scanProject, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "Project plan workflow completed in {DurationMs} ms for project {ProjectName}. Success: {Succeeded}; task item count: {TaskItemCount}.",
            stopwatch.ElapsedMilliseconds,
            scanProject.ProjectName,
            result.IsSuccess,
            result.IsSuccess ? result.Value.TaskItems.Count : 0);
        return result;
    }

    internal static WorkflowOptions CreateWorkflowOptions(ExecutionOptions executionOptions) =>
        new()
        {
            AgentRunTimeout = executionOptions.AgentRunTimeout,
            MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
            MaxMissingSubmissionAttempts = executionOptions.MaxMissingSubmissionAttempts,
            MaxVerifierRejectionAttempts = executionOptions.MaxVerifierRejectionAttempts,
            MaxProjectPlanAgentResets = executionOptions.MaxVerifierRejectionAttempts,
        };
}
