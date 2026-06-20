using CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Client.Services.Projects;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Services.ProjectAgentSnapshots;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Artifacts;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Readiness;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Recovery;
using CodeSnifferDog.Server.Services.ProjectExecution.Status;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.ProjectStorage;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server;

internal static class CodeSnifferDogServerServiceCollectionExtensions
{
    public static IServiceCollection AddCodeSnifferDogServerServices(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDbContext = null)
    {
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
        {
            if (configureDbContext is not null)
            {
                configureDbContext(options);
                return;
            }

            options.UseSqlServer(configuration.GetConnectionString("CodeSnifferDogServer"));
        });

        services.Configure<ProjectExecutionOptions>(
            configuration.GetSection(ProjectExecutionOptions.SectionName));
        services.AddOptions<InferenceProviderOptions>()
            .Bind(configuration.GetSection(InferenceProviderOptions.SectionName))
            .PostConfigure(options =>
            {
                options.OpenAICompatible.ExtraBody = OpenAICompatibleInferenceProviderOptions.ParseExtraBody(
                    configuration
                        .GetSection(InferenceProviderOptions.SectionName)
                        .GetSection(nameof(InferenceProviderOptions.OpenAICompatible))
                        .GetSection(nameof(OpenAICompatibleInferenceProviderOptions.ExtraBody)));
            });

        services.AddSingleton<ProjectTemporaryStoragePaths>();
        services.AddSingleton<IProjectExecutionLeaseRegistry, ProjectExecutionLeaseRegistry>();
        services.AddSingleton<IProjectExecutionQueueLock, ProjectExecutionQueueLock>();
        services.AddSingleton<IProjectChatClientProvider, ProjectChatClientProvider>();
        services.AddSingleton<IReviewRuleMarkdownProvider, FileSystemReviewRuleMarkdownProvider>();
        services.AddSingleton<IExecutionReadinessGate, ExecutionReadinessGate>();
        services.AddSingleton<IExecutionArtifactStore, ExecutionArtifactStore>();
        services.AddSingleton<IExecutionStateService, ExecutionStateService>();
        services.AddSingleton<IExecutionQueueClaimer, ExecutionQueueClaimer>();
        services.AddSingleton<IClaimExecutor, ClaimExecutor>();
        services.AddSingleton<IInterruptedProjectRecoveryService, InterruptedProjectRecoveryService>();

        services.AddScoped<ProjectReviewAgentCompactionOptionsFactory>();
        services.AddScoped<IScanRunnerFactory, ScanRunnerFactory>();
        services.AddScoped<IProjectPlanRunnerFactory, ProjectPlanRunnerFactory>();
        services.AddScoped<IRuleReviewRunnerFactory, RuleReviewRunnerFactory>();
        services.AddScoped<IRuleReportRunnerFactory, RuleReportRunnerFactory>();
        services.AddScoped<IRuleFlowRunnerFactory, RuleFlowRunnerFactory>();
        services.AddScoped<IProjectReviewWorkflowRunnerFactory, ProjectReviewWorkflowRunnerFactory>();
        services.AddScoped<IProjectReviewAgentTeamDependenciesFactory, ProjectReviewAgentTeamDependenciesFactory>();
        services.AddScoped<IProjectReviewAgentTeamWorkerFactory, ProjectReviewAgentTeamWorkerFactory>();

        services.AddScoped<IAgentTimelinePersistenceService, AgentTimelinePersistenceService>();
        services.AddScoped<IAgentStatusRuntimeComponentsFactory, AgentStatusRuntimeComponentsFactory>();
        services.AddScoped<IAgentStatusRuntimeFactory, AgentStatusRuntimeFactory>();
        services.AddScoped<IAgentStatusEventSubscriberFactory, AgentStatusEventSubscriberFactory>();
        services.AddScoped<IAgentStatusProjectionMapper, AgentStatusProjectionMapper>();
        services.AddScoped<IProjectAgentStatusSnapshotService, ProjectAgentStatusSnapshotService>();
        services.AddScoped<IProjectAgentStatusLiveBackfillService, ProjectAgentStatusLiveBackfillService>();

        services.AddScoped<IProjectReviewAnalysisExecutor, ProjectReviewAnalysisExecutor>();
        services.AddScoped<IProjectAnalysisCompletionService, ProjectAnalysisCompletionService>();
        services.AddScoped<IProjectAnalysisRunner, ProjectAnalysisRunner>();
        services.AddScoped<IProjectAgentStatusLiveSubscriptionClient, NoOpProjectAgentStatusLiveSubscriptionClient>();
        services.AddScoped<IProjectSidebarController, ServerPrerenderProjectSidebarController>();
        services.AddScoped<IProjectIntakeService, ProjectIntakeService>();
        services.AddScoped<IProjectReportService, ProjectReportService>();
        services.AddScoped<IProjectChangePublisher, ProjectChangePublisher>();
        services.AddScoped<IProjectSidebarSnapshotService, ProjectSidebarSnapshotService>();

        services.AddSingleton<IProjectUpdatesNotifier, SignalRProjectUpdatesNotifier>();
        services.AddSingleton<IProjectAgentStatusLiveUpdateNotifier, SignalRProjectAgentStatusLiveUpdateNotifier>();
        services.AddHostedService<ProjectExecutionHostedService>();

        return services;
    }
}
