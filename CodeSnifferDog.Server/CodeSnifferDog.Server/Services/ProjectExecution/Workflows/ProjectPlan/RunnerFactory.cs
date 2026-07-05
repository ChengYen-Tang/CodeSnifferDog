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

/// <summary>
/// Creates the runner that executes the project-plan workflow.
/// </summary>
internal sealed class RunnerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IRunnerFactory
{
    /// <summary>
    /// Gets the prompt asset used when project-plan agents summarize compacted history.
    /// </summary>
    internal static string SummaryPromptAssetPath => ProjectPlanAgentPromptAssets.ProjectPlanSummaryPrompt;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<RunnerFactory> _logger = loggerFactory.CreateLogger<RunnerFactory>();

    /// <inheritdoc />
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
            agentEventBus: context.AgentEventBus,
            logger: _logger);

        Result<WorkflowResult> result = await workflow.RunAsync(repositoryRootPath, scanProject, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "Project plan workflow completed in {DurationMs} ms for project {ProjectName}. Success: {Succeeded}; task item count: {TaskItemCount}.",
            stopwatch.ElapsedMilliseconds,
            scanProject.ProjectName,
            result.IsSuccess,
            result.IsSuccess ? result.Value.TaskItems.Count : 0);
        return result;
    }

    /// <summary>
    /// Creates workflow options for project-plan runs from the configured execution limits.
    /// </summary>
    /// <param name="executionOptions">Execution limits shared across review workflows.</param>
    /// <returns>The workflow options passed to the project-plan workflow.</returns>
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
