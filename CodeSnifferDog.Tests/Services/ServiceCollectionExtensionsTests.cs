using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Server;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
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
using CodeSnifferDog.Server.Services.ProjectExecution.Workflows;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.ProjectIntake.Deletion;
using CodeSnifferDog.Server.Services.ProjectIntake.Queue;
using CodeSnifferDog.Server.Services.ProjectIntake.Upload;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Server.Services.ProjectReports.Export;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using CodeSnifferDog.Server.Services.ProjectReports.Queries;
using CodeSnifferDog.Server.Services.Projects.Sidebar;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Queries;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CodeSnifferDog.Modules.ReviewAgentTeam.Events;
using ReportProjectionMapper = CodeSnifferDog.Server.Services.ProjectReports.Projection.ProjectionMapper;
using ReportProjectionMapperInterface = CodeSnifferDog.Server.Services.ProjectReports.Projection.IProjectionMapper;
using AgentStatusSnapshotService = CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.SnapshotService;
using AgentStatusSnapshotServiceInterface = CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.ISnapshotService;
using ReportQueryService = CodeSnifferDog.Server.Services.ProjectReports.Queries.QueryService;
using ReportQueryServiceInterface = CodeSnifferDog.Server.Services.ProjectReports.Queries.IQueryService;
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

namespace CodeSnifferDog.Tests.Services;

[TestClass]
public sealed class ServiceCollectionExtensionsTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public void RegistersStatusRuntimeProductionChain()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.IsInstanceOfType<EventSubscriberFactory>(
            services.GetRequiredService<IEventSubscriberFactory>());
        Assert.IsInstanceOfType<RuntimeFactory>(
            services.GetRequiredService<IRuntimeFactory>());
        Assert.IsInstanceOfType<RuntimeComponentsFactory>(
            services.GetRequiredService<IRuntimeComponentsFactory>());
        Assert.IsInstanceOfType<TimelinePersistenceService>(
            services.GetRequiredService<ITimelinePersistenceService>());
    }

    [TestMethod]
    public async Task CreatesSubscriberFromProductionRegistration()
    {
        CollectingLiveUpdateNotifier liveUpdateNotifier = new();
        using ServiceProvider serviceProvider = CreateServiceProvider(liveUpdateNotifier);
        using IServiceScope scope = serviceProvider.CreateScope();
        Guid projectId = Guid.NewGuid();
        IEventSubscriberFactory subscriberFactory =
            scope.ServiceProvider.GetRequiredService<IEventSubscriberFactory>();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        using AgentStatusEventStream eventStream = new();
        await using EventSubscriber subscriber =
            subscriberFactory.Create(projectId, eventStream.Events);
        string groupKey = "group-1";
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, "agent-1");

        await eventStream.PublishGroupCreatedAsync(groupKey, "Group 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Agent 1", "System prompt", "Waiting", TestContext.CancellationToken);
        await agentScope.PublishAssistantMessageAsync("analysis complete", TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(group => group.ProjectId == projectId && group.RuntimeKey == groupKey, TestContext.CancellationToken);
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(agent => agent.ProjectAgentGroupId == group.Id && agent.RuntimeKey == "agent-1", TestContext.CancellationToken);
        ProjectAgentTimelineEntryRecord timelineEntry = await dbContext.ProjectAgentTimelineEntries
            .SingleAsync(entry => entry.ProjectAgentId == agent.Id, TestContext.CancellationToken);

        Assert.AreEqual("Group 1", group.DisplayName);
        Assert.AreEqual("Agent 1", agent.DisplayName);
        Assert.AreEqual(ProjectAgentTimelineEntryType.Output, timelineEntry.EntryType);
        Assert.AreEqual("analysis complete", timelineEntry.Message);
        Assert.HasCount(3, liveUpdateNotifier.Updates);
        Assert.IsTrue(liveUpdateNotifier.Updates.Any(update => update.Kind == LiveUpdateKind.AgentGroupUpserted));
        Assert.IsTrue(liveUpdateNotifier.Updates.Any(update => update.Kind == LiveUpdateKind.AgentUpserted));
        Assert.IsTrue(liveUpdateNotifier.Updates.Any(update => update.Kind == LiveUpdateKind.TimelineEntryUpserted));
    }

    [TestMethod]
    public void RegistersProjectExecutionPipelineServices()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        IHostedService hostedService = serviceProvider.GetServices<IHostedService>()
            .Single(service => service is HostedService);
        using IServiceScope scope = serviceProvider.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.IsInstanceOfType<HostedService>(hostedService);
        Assert.IsInstanceOfType<ReviewAnalysisExecutor>(
            services.GetRequiredService<IReviewAnalysisExecutor>());
        Assert.IsInstanceOfType<Runner>(
            services.GetRequiredService<IProjectAnalysisRunner>());
        Assert.IsInstanceOfType<CompletionService>(
            services.GetRequiredService<ICompletionService>());
        Assert.IsInstanceOfType<WorkerFactory>(
            services.GetRequiredService<IWorkerFactory>());
        Assert.IsInstanceOfType<DependenciesFactory>(
            services.GetRequiredService<IDependenciesFactory>());
        Assert.IsInstanceOfType<ReviewRunnerFactory>(
            services.GetRequiredService<IReviewRunnerFactory>());
        Assert.IsInstanceOfType<ScanRunnerFactory>(
            services.GetRequiredService<IScanRunnerFactory>());
        Assert.IsInstanceOfType<ProjectPlanRunnerFactory>(
            services.GetRequiredService<ProjectPlanRunnerFactoryInterface>());
        Assert.IsInstanceOfType<RuleFlowRunnerFactory>(
            services.GetRequiredService<RuleFlowRunnerFactoryInterface>());
        Assert.IsInstanceOfType<RuleReviewRunnerFactory>(
            services.GetRequiredService<RuleReviewRunnerFactoryInterface>());
        Assert.IsInstanceOfType<RuleReportRunnerFactory>(
            services.GetRequiredService<RuleReportRunnerFactoryInterface>());
        Assert.IsInstanceOfType<Gate>(
            services.GetRequiredService<IGate>());
        Assert.IsInstanceOfType<Claimer>(
            services.GetRequiredService<IClaimer>());
        Assert.IsInstanceOfType<ClaimExecutor>(
            services.GetRequiredService<IClaimExecutor>());
        Assert.IsInstanceOfType<StateService>(
            services.GetRequiredService<IStateService>());
        Assert.IsInstanceOfType<ExecutionArtifactStore>(
            services.GetRequiredService<IExecutionArtifactStore>());
        Assert.IsInstanceOfType<Service>(
            services.GetRequiredService<IService>());
    }

    [TestMethod]
    public void RegistersProjectSurfaceServices()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.IsInstanceOfType<ProjectStatusMapper>(
            services.GetRequiredService<IProjectStatusMapper>());
        Assert.IsInstanceOfType<ProjectProjectionMapper>(
            services.GetRequiredService<IProjectProjectionMapper>());
        Assert.IsInstanceOfType<UploadService>(
            services.GetRequiredService<IUploadService>());
        Assert.IsInstanceOfType<QueueService>(
            services.GetRequiredService<IQueueService>());
        Assert.IsInstanceOfType<DeletionService>(
            services.GetRequiredService<IDeletionService>());
        Assert.IsInstanceOfType<ProjectIntakeService>(
            services.GetRequiredService<IProjectIntakeService>());
        Assert.IsInstanceOfType<ReportProjectionMapper>(
            services.GetRequiredService<ReportProjectionMapperInterface>());
        Assert.IsInstanceOfType<ReportQueryService>(
            services.GetRequiredService<ReportQueryServiceInterface>());
        Assert.IsInstanceOfType<ExportService>(
            services.GetRequiredService<IExportService>());
        Assert.IsInstanceOfType<ReportService>(
            services.GetRequiredService<IReportService>());
        Assert.IsInstanceOfType<SidebarQueryService>(
            services.GetRequiredService<SidebarQueryServiceInterface>());
        Assert.IsInstanceOfType<SidebarSnapshotService>(
            services.GetRequiredService<SidebarSnapshotServiceInterface>());
    }

    [TestMethod]
    public void RegistersAgentStatusReadSideServices()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.IsInstanceOfType<SnapshotQueryService>(
            services.GetRequiredService<ISnapshotQueryService>());
        Assert.IsInstanceOfType<BackfillQueryService>(
            services.GetRequiredService<IBackfillQueryService>());
        Assert.IsInstanceOfType<AgentStatusSnapshotService>(
            services.GetRequiredService<AgentStatusSnapshotServiceInterface>());
        Assert.IsInstanceOfType<LiveBackfillService>(
            services.GetRequiredService<ILiveBackfillService>());
    }

    [TestMethod]
    public void ValidateOnBuild_DoesNotMissInternalRegistrations()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        Assert.IsNotNull(serviceProvider);
    }

    private static ServiceProvider CreateServiceProvider(
        CollectingLiveUpdateNotifier? liveUpdateNotifier = null)
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddSignalR();
        services.AddCodeSnifferDogServerServices(
            CreateConfiguration(),
            options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));

        if (liveUpdateNotifier is not null)
            services.AddSingleton<ILiveUpdateNotifier>(liveUpdateNotifier);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CodeSnifferDogServer"] = "Server=(localdb)\\MSSQLLocalDB;Database=CodeSnifferDogTests;Trusted_Connection=True;",
                ["ProjectExecution:MaxConcurrentWorkers"] = "1",
                ["ProjectExecution:QueuePollingIntervalSeconds"] = "1",
                ["InferenceProvider:Provider"] = "OpenAICompatible",
                ["InferenceProvider:OpenAICompatible:Endpoint"] = "http://127.0.0.1:11434/v1",
                ["InferenceProvider:OpenAICompatible:ModelId"] = "test-model",
            })
            .Build();

    private sealed class CollectingLiveUpdateNotifier : ILiveUpdateNotifier
    {
        public List<LiveUpdateDto> Updates { get; } = [];

        public Task NotifyAsync(LiveUpdateDto update, CancellationToken cancellationToken = default)
        {
            Updates.Add(update);
            return Task.CompletedTask;
        }
    }

    private sealed class TestHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;

        public CancellationToken ApplicationStopping => CancellationToken.None;

        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
        }
    }
}
