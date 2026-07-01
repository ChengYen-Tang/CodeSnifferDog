using CodeSnifferDog.Agents.ProjectPlan;
using CodeSnifferDog.Models.ContextCompaction;
using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Modules.Tools.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Review;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Workflows.ProjectPlan;
using FluentResults;
using System.Diagnostics;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal sealed class ProjectPlanRunnerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IProjectPlanRunnerFactory
{
    internal static string SummaryPromptAssetPath => ProjectPlanPromptAssetPaths.ProjectPlanSummaryPrompt;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ProjectPlanRunnerFactory> _logger = loggerFactory.CreateLogger<ProjectPlanRunnerFactory>();

    public Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        OperationalContextCompactionOptions compactionOptions) =>
        (repositoryRootPath, scanProject, cancellationToken) =>
            RunAsync(context, repositoryRootPath, scanProject, compactionOptions, cancellationToken);

    private async Task<Result<ProjectPlanWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        StoredScanProject scanProject,
        OperationalContextCompactionOptions compactionOptions,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Project plan workflow started for project {ProjectName}. Repository: {RepositoryRootPath}.",
            scanProject.ProjectName,
            repositoryRootPath);

        InMemoryProjectPlanTaskItemStore taskItemStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        ProjectPlanWorkflow workflow = new(
            (planRepositoryRootPath, eventScope) => new ProjectPlanAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope,
                    _loggerFactory),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, planRepositoryRootPath, taskItemStore, verdictBuffer, eventScope),
            (planRepositoryRootPath, verifierScanProject, eventScope) => new ProjectVerifierAgentFactory(
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

        Result<ProjectPlanWorkflowResult> result = await workflow.RunAsync(repositoryRootPath, scanProject, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "Project plan workflow completed in {DurationMs} ms for project {ProjectName}. Success: {Succeeded}; task item count: {TaskItemCount}.",
            stopwatch.ElapsedMilliseconds,
            scanProject.ProjectName,
            result.IsSuccess,
            result.IsSuccess ? result.Value.TaskItems.Count : 0);
        return result;
    }

    internal static ProjectPlanWorkflowOptions CreateWorkflowOptions(ExecutionOptions executionOptions) =>
        new()
        {
            AgentRunTimeout = executionOptions.AgentRunTimeout,
            MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
        };
}
