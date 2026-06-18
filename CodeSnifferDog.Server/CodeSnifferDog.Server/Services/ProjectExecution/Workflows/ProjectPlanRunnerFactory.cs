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

namespace CodeSnifferDog.Server.Services.ProjectExecution.Workflows;

internal sealed class ProjectPlanRunnerFactory(
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider) : IProjectPlanRunnerFactory
{
    internal static string SummaryPromptAssetPath => ProjectPlanPromptAssetPaths.ProjectPlanSummaryPrompt;

    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public Func<string, StoredScanProject, CancellationToken, Task<Result<ProjectPlanWorkflowResult>>> CreateRunner(
        WorkflowRuntimeContext context,
        OperationalContextCompactionOptions compactionOptions) =>
        (repositoryRootPath, scanProject, cancellationToken) =>
            RunAsync(context, repositoryRootPath, scanProject, compactionOptions, cancellationToken);

    private Task<Result<ProjectPlanWorkflowResult>> RunAsync(
        WorkflowRuntimeContext context,
        string repositoryRootPath,
        StoredScanProject scanProject,
        OperationalContextCompactionOptions compactionOptions,
        CancellationToken cancellationToken)
    {
        InMemoryProjectPlanTaskItemStore taskItemStore = new();
        ReviewVerdictBuffer verdictBuffer = new();
        ProjectPlanWorkflow workflow = new(
            (planRepositoryRootPath, eventScope) => new ProjectPlanAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, planRepositoryRootPath, taskItemStore, verdictBuffer, eventScope),
            (planRepositoryRootPath, verifierScanProject, eventScope) => new ProjectVerifierAgentFactory(
                RunnerCompactionOptions.Create(
                    context.CompactionOptionsFactory,
                    SummaryPromptAssetPath,
                    compactionOptions,
                    eventScope),
                context.PromptAssetReader,
                loggerFactory: _loggerFactory,
                serviceProvider: _serviceProvider).Create(context.ChatClient, planRepositoryRootPath, verifierScanProject, taskItemStore, verdictBuffer, eventScope),
            taskItemStore,
            verdictBuffer,
            context.PromptAssetReader,
            CreateWorkflowOptions(context.ExecutionOptions),
            agentEventBus: context.AgentEventBus);

        return workflow.RunAsync(repositoryRootPath, scanProject, cancellationToken);
    }

    internal static ProjectPlanWorkflowOptions CreateWorkflowOptions(ExecutionOptions executionOptions) =>
        new()
        {
            AgentRunTimeout = executionOptions.AgentRunTimeout,
            MaxConsecutiveRunFailures = executionOptions.MaxConsecutiveAgentRunFailures,
        };
}
