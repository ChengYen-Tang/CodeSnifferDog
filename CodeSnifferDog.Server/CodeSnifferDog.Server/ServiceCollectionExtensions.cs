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
using ExecutionSettings = CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Settings;
using SidebarQueryService = CodeSnifferDog.Server.Services.Projects.Sidebar.Queries.QueryService;
using SidebarQueryServiceInterface = CodeSnifferDog.Server.Services.Projects.Sidebar.Queries.IQueryService;
using SidebarSnapshotService = CodeSnifferDog.Server.Services.Projects.Sidebar.SnapshotService;
using SidebarSnapshotServiceInterface = CodeSnifferDog.Server.Services.Projects.Sidebar.ISnapshotService;
using ProjectPlanRunnerFactory = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.ProjectPlan.RunnerFactory;
using ProjectPlanRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.ProjectPlan.IRunnerFactory;
using RuleFlowRunnerFactory = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleFlow.RunnerFactory;
using RuleFlowRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleFlow.IRunnerFactory;
using RuleReportRunnerFactory = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReport.RunnerFactory;
using RuleReportRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReport.IRunnerFactory;
using RuleReviewRunnerFactory = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReview.RunnerFactory;
using RuleReviewRunnerFactoryInterface = CodeSnifferDog.Server.Services.ProjectExecution.Workflows.RuleReview.IRunnerFactory;

namespace CodeSnifferDog.Server;

/// <summary>
/// Registers the server's data, workflow, execution, and presentation services.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all CodeSnifferDog server services to the dependency-injection container.
    /// </summary>
    /// <param name="services">Service collection being configured.</param>
    /// <param name="configuration">Application configuration used to bind options and connection strings.</param>
    /// <param name="configureDbContext">Optional override used to customize the EF Core context registration.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
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

    /// <summary>
    /// Registers shared projection helpers used by multiple server surfaces.
    /// </summary>
    /// <param name="services">Service collection being configured.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    private static IServiceCollection AddSharedProjectionServices(this IServiceCollection services)
    {
        services.AddSingleton<IProjectStatusMapper, ProjectStatusMapper>();

        return services;
    }

    /// <summary>
    /// Registers database access and infrastructure options.
    /// </summary>
    /// <param name="services">Service collection being configured.</param>
    /// <param name="configuration">Application configuration used to bind options and connection strings.</param>
    /// <param name="configureDbContext">Optional override used to customize the EF Core context registration.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
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

        services.Configure<ExecutionSettings>(
            configuration.GetSection(ExecutionSettings.SectionName));
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

    /// <summary>
    /// Registers storage, queueing, hosted execution, and runtime infrastructure services.
    /// </summary>
    /// <param name="services">Service collection being configured.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    private static IServiceCollection AddProjectExecutionInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ProjectTemporaryStoragePaths>();
        services.AddSingleton<ILeaseRegistry, LeaseRegistry>();
        services.AddSingleton<IQueueLock, QueueLock>();
        services.AddSingleton<IProjectChatClientProvider, ProjectChatClientProvider>();
        services.AddSingleton<IReviewRuleMarkdownProvider, FileSystemRuleMarkdownProvider>();
        services.AddSingleton<IGate, Gate>();
        services.AddSingleton<IExecutionArtifactStore, ExecutionArtifactStore>();
        services.AddSingleton<IStateService, StateService>();
        services.AddSingleton<IClaimer, Claimer>();
        services.AddSingleton<IClaimExecutor, ClaimExecutor>();
        services.AddSingleton<IService, Service>();
        services.AddHostedService<HostedService>();

        return services;
    }

    /// <summary>
    /// Registers the end-to-end project review pipeline and its workflow factories.
    /// </summary>
    /// <param name="services">Service collection being configured.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    private static IServiceCollection AddProjectReviewPipeline(this IServiceCollection services)
    {
        services.AddScoped<OptionsFactory>();
        services.AddScoped<IScanRunnerFactory, ScanRunnerFactory>();
        services.AddScoped<ProjectPlanRunnerFactoryInterface, ProjectPlanRunnerFactory>();
        services.AddScoped<RuleReviewRunnerFactoryInterface, RuleReviewRunnerFactory>();
        services.AddScoped<RuleReportRunnerFactoryInterface, RuleReportRunnerFactory>();
        services.AddScoped<RuleFlowRunnerFactoryInterface, RuleFlowRunnerFactory>();
        services.AddScoped<IReviewRunnerFactory, ReviewRunnerFactory>();
        services.AddScoped<IDependenciesFactory, DependenciesFactory>();
        services.AddScoped<IWorkerFactory, WorkerFactory>();
        services.AddScoped<IReviewAnalysisExecutor, ReviewAnalysisExecutor>();
        services.AddScoped<ICompletionService, CompletionService>();
        services.AddScoped<IProjectAnalysisRunner, Runner>();

        return services;
    }

    /// <summary>
    /// Registers project-agent status persistence, snapshots, and live notification services.
    /// </summary>
    /// <param name="services">Service collection being configured.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
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

    /// <summary>
    /// Registers upload, projection, reporting, and UI-facing project surface services.
    /// </summary>
    /// <param name="services">Service collection being configured.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
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
