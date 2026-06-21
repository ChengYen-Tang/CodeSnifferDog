using CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Client.Services.Projects;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Artifacts;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Readiness;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Recovery;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Server.Services.ProjectIntake.Deletion;
using CodeSnifferDog.Server.Services.ProjectIntake.Queue;
using CodeSnifferDog.Server.Services.ProjectIntake.Upload;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Server.Services.ProjectReports.Export;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using CodeSnifferDog.Server.Services.ProjectReports.Queries;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Services.Projects.Queries;
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
        services
            .AddDataServices(configuration, configureDbContext)
            .AddSharedProjectionServices()
            .AddProjectExecutionInfrastructure()
            .AddProjectReviewPipeline()
            .AddAgentStatusServices()
            .AddProjectSurfaceServices();

        return services;
    }

    private static IServiceCollection AddSharedProjectionServices(this IServiceCollection services)
    {
        services.AddSingleton<IProjectStatusMapper, ProjectStatusMapper>();

        return services;
    }

    private static IServiceCollection AddDataServices(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDbContext)
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

        return services;
    }

    private static IServiceCollection AddProjectExecutionInfrastructure(this IServiceCollection services)
    {
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
        services.AddHostedService<ProjectExecutionHostedService>();

        return services;
    }

    private static IServiceCollection AddProjectReviewPipeline(this IServiceCollection services)
    {
        services.AddScoped<ProjectReviewAgentCompactionOptionsFactory>();
        services.AddScoped<IScanRunnerFactory, ScanRunnerFactory>();
        services.AddScoped<IProjectPlanRunnerFactory, ProjectPlanRunnerFactory>();
        services.AddScoped<IRuleReviewRunnerFactory, RuleReviewRunnerFactory>();
        services.AddScoped<IRuleReportRunnerFactory, RuleReportRunnerFactory>();
        services.AddScoped<IRuleFlowRunnerFactory, RuleFlowRunnerFactory>();
        services.AddScoped<IProjectReviewWorkflowRunnerFactory, ProjectReviewWorkflowRunnerFactory>();
        services.AddScoped<IProjectReviewAgentTeamDependenciesFactory, ProjectReviewAgentTeamDependenciesFactory>();
        services.AddScoped<IProjectReviewAgentTeamWorkerFactory, ProjectReviewAgentTeamWorkerFactory>();
        services.AddScoped<IProjectReviewAnalysisExecutor, ProjectReviewAnalysisExecutor>();
        services.AddScoped<IProjectAnalysisCompletionService, ProjectAnalysisCompletionService>();
        services.AddScoped<IProjectAnalysisRunner, ProjectAnalysisRunner>();

        return services;
    }

    private static IServiceCollection AddAgentStatusServices(this IServiceCollection services)
    {
        services.AddScoped<IAgentTimelinePersistenceService, AgentTimelinePersistenceService>();
        services.AddScoped<IAgentStatusRuntimeComponentsFactory, AgentStatusRuntimeComponentsFactory>();
        services.AddScoped<IAgentStatusRuntimeFactory, AgentStatusRuntimeFactory>();
        services.AddScoped<IAgentStatusEventSubscriberFactory, AgentStatusEventSubscriberFactory>();
        services.AddScoped<IAgentStatusProjectionMapper, AgentStatusProjectionMapper>();
        services.AddScoped<IProjectAgentStatusSnapshotQueryService, ProjectAgentStatusSnapshotQueryService>();
        services.AddScoped<IProjectAgentStatusBackfillQueryService, ProjectAgentStatusBackfillQueryService>();
        services.AddScoped<IProjectAgentStatusSnapshotService, ProjectAgentStatusSnapshotService>();
        services.AddScoped<IProjectAgentStatusLiveBackfillService, ProjectAgentStatusLiveBackfillService>();
        services.AddSingleton<IProjectAgentStatusLiveUpdateNotifier, SignalRProjectAgentStatusLiveUpdateNotifier>();

        return services;
    }

    private static IServiceCollection AddProjectSurfaceServices(this IServiceCollection services)
    {
        services.AddScoped<IProjectAgentStatusLiveSubscriptionClient, NoOpProjectAgentStatusLiveSubscriptionClient>();
        services.AddScoped<IProjectSidebarController, ServerPrerenderProjectSidebarController>();
        services.AddScoped<IProjectProjectionMapper, ProjectProjectionMapper>();
        services.AddScoped<IProjectUploadService, ProjectUploadService>();
        services.AddScoped<IProjectQueueService, ProjectQueueService>();
        services.AddScoped<IProjectDeletionService, ProjectDeletionService>();
        services.AddScoped<IProjectIntakeService, ProjectIntakeService>();
        services.AddScoped<IProjectReportProjectionMapper, ProjectReportProjectionMapper>();
        services.AddScoped<IProjectReportQueryService, ProjectReportQueryService>();
        services.AddScoped<IProjectReportExportService, ProjectReportExportService>();
        services.AddScoped<IProjectReportService, ProjectReportService>();
        services.AddScoped<IProjectChangePublisher, ProjectChangePublisher>();
        services.AddScoped<IProjectSidebarQueryService, ProjectSidebarQueryService>();
        services.AddScoped<IProjectSidebarSnapshotService, ProjectSidebarSnapshotService>();

        services.AddSingleton<IProjectUpdatesNotifier, SignalRProjectUpdatesNotifier>();

        return services;
    }
}
