using CodeSnifferDog.Server.Client.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Client.Services.Projects.Sidebar;
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
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam;
using CodeSnifferDog.Server.Services.ProjectExecution.Worker.ReviewTeam.Compaction;
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Server.Services.ProjectIntake.Deletion;
using CodeSnifferDog.Server.Services.ProjectIntake.Queue;
using CodeSnifferDog.Server.Services.ProjectIntake.Upload;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Server.Services.ProjectReports.Export;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using CodeSnifferDog.Server.Services.ProjectReports.Queries;
using CodeSnifferDog.Server.Services.Projects.Sidebar;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Queries;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Services.ProjectStorage;
using Microsoft.EntityFrameworkCore;
using AgentStatusProjectionMapper = CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection.ProjectionMapper;
using AgentStatusProjectionMapperInterface = CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection.IProjectionMapper;
using AgentStatusSnapshotService = CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.SnapshotService;
using AgentStatusSnapshotServiceInterface = CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.ISnapshotService;
using ReportProjectionMapper = CodeSnifferDog.Server.Services.ProjectReports.Projection.ProjectionMapper;
using ReportProjectionMapperInterface = CodeSnifferDog.Server.Services.ProjectReports.Projection.IProjectionMapper;
using ReportQueryService = CodeSnifferDog.Server.Services.ProjectReports.Queries.QueryService;
using ReportQueryServiceInterface = CodeSnifferDog.Server.Services.ProjectReports.Queries.IQueryService;
using SidebarQueryService = CodeSnifferDog.Server.Services.Projects.Sidebar.Queries.QueryService;
using SidebarQueryServiceInterface = CodeSnifferDog.Server.Services.Projects.Sidebar.Queries.IQueryService;
using SidebarSnapshotService = CodeSnifferDog.Server.Services.Projects.Sidebar.SnapshotService;
using SidebarSnapshotServiceInterface = CodeSnifferDog.Server.Services.Projects.Sidebar.ISnapshotService;

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
        services.AddHostedService<HostedService>();

        return services;
    }

    private static IServiceCollection AddProjectReviewPipeline(this IServiceCollection services)
    {
        services.AddScoped<OptionsFactory>();
        services.AddScoped<IScanRunnerFactory, ScanRunnerFactory>();
        services.AddScoped<IProjectPlanRunnerFactory, ProjectPlanRunnerFactory>();
        services.AddScoped<IRuleReviewRunnerFactory, RuleReviewRunnerFactory>();
        services.AddScoped<IRuleReportRunnerFactory, RuleReportRunnerFactory>();
        services.AddScoped<IRuleFlowRunnerFactory, RuleFlowRunnerFactory>();
        services.AddScoped<IReviewRunnerFactory, ReviewRunnerFactory>();
        services.AddScoped<IDependenciesFactory, DependenciesFactory>();
        services.AddScoped<IWorkerFactory, WorkerFactory>();
        services.AddScoped<IReviewAnalysisExecutor, ReviewAnalysisExecutor>();
        services.AddScoped<ICompletionService, CompletionService>();
        services.AddScoped<IProjectAnalysisRunner, Runner>();

        return services;
    }

    private static IServiceCollection AddAgentStatusServices(this IServiceCollection services)
    {
        services.AddScoped<ITimelinePersistenceService, TimelinePersistenceService>();
        services.AddScoped<IRuntimeComponentsFactory, RuntimeComponentsFactory>();
        services.AddScoped<IRuntimeFactory, RuntimeFactory>();
        services.AddScoped<IEventSubscriberFactory, EventSubscriberFactory>();
        services.AddScoped<AgentStatusProjectionMapperInterface, AgentStatusProjectionMapper>();
        services.AddScoped<ISnapshotQueryService, SnapshotQueryService>();
        services.AddScoped<IBackfillQueryService, BackfillQueryService>();
        services.AddScoped<AgentStatusSnapshotServiceInterface, AgentStatusSnapshotService>();
        services.AddScoped<ILiveBackfillService, LiveBackfillService>();
        services.AddSingleton<ILiveUpdateNotifier, SignalRLiveUpdateNotifier>();

        return services;
    }

    private static IServiceCollection AddProjectSurfaceServices(this IServiceCollection services)
    {
        services.AddScoped<ILiveSubscriptionClient, NoOpLiveSubscriptionClient>();
        services.AddScoped<IController, ServerPrerenderController>();
        services.AddScoped<IProjectProjectionMapper, ProjectProjectionMapper>();
        services.AddScoped<IUploadService, UploadService>();
        services.AddScoped<IQueueService, QueueService>();
        services.AddScoped<IDeletionService, DeletionService>();
        services.AddScoped<IProjectIntakeService, ProjectIntakeService>();
        services.AddScoped<ReportProjectionMapperInterface, ReportProjectionMapper>();
        services.AddScoped<ReportQueryServiceInterface, ReportQueryService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IProjectChangePublisher, ProjectChangePublisher>();
        services.AddScoped<SidebarQueryServiceInterface, SidebarQueryService>();
        services.AddScoped<SidebarSnapshotServiceInterface, SidebarSnapshotService>();

        services.AddSingleton<IProjectUpdatesNotifier, SignalRProjectUpdatesNotifier>();

        return services;
    }
}
