using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Server;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
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
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.ProjectIntake.Deletion;
using CodeSnifferDog.Server.Services.ProjectIntake.Queue;
using CodeSnifferDog.Server.Services.ProjectIntake.Upload;
using CodeSnifferDog.Server.Services.ProjectReports;
using CodeSnifferDog.Server.Services.ProjectReports.Export;
using CodeSnifferDog.Server.Services.ProjectReports.Projection;
using CodeSnifferDog.Server.Services.ProjectReports.Queries;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CodeSnifferDog.Tests.Services;

[TestClass]
public sealed class CodeSnifferDogServerServiceCollectionExtensionsTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public void RegistersAgentStatusRuntimeProductionChain()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.IsInstanceOfType<AgentStatusEventSubscriberFactory>(
            services.GetRequiredService<IAgentStatusEventSubscriberFactory>());
        Assert.IsInstanceOfType<AgentStatusRuntimeFactory>(
            services.GetRequiredService<IAgentStatusRuntimeFactory>());
        Assert.IsInstanceOfType<AgentStatusRuntimeComponentsFactory>(
            services.GetRequiredService<IAgentStatusRuntimeComponentsFactory>());
        Assert.IsInstanceOfType<AgentTimelinePersistenceService>(
            services.GetRequiredService<IAgentTimelinePersistenceService>());
    }

    [TestMethod]
    public async Task CreatesSubscriberFromProductionRegistration()
    {
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using ServiceProvider serviceProvider = CreateServiceProvider(liveUpdateNotifier);
        using IServiceScope scope = serviceProvider.CreateScope();
        Guid projectId = Guid.NewGuid();
        IAgentStatusEventSubscriberFactory subscriberFactory =
            scope.ServiceProvider.GetRequiredService<IAgentStatusEventSubscriberFactory>();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
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
        Assert.IsTrue(liveUpdateNotifier.Updates.Any(update => update.Kind == ProjectAgentLiveUpdateKind.AgentGroupUpserted));
        Assert.IsTrue(liveUpdateNotifier.Updates.Any(update => update.Kind == ProjectAgentLiveUpdateKind.AgentUpserted));
        Assert.IsTrue(liveUpdateNotifier.Updates.Any(update => update.Kind == ProjectAgentLiveUpdateKind.TimelineEntryUpserted));
    }

    [TestMethod]
    public void RegistersProjectExecutionPipelineServices()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        IHostedService hostedService = serviceProvider.GetServices<IHostedService>()
            .Single(service => service is ProjectExecutionHostedService);
        using IServiceScope scope = serviceProvider.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.IsInstanceOfType<ProjectExecutionHostedService>(hostedService);
        Assert.IsInstanceOfType<ProjectReviewAnalysisExecutor>(
            services.GetRequiredService<IProjectReviewAnalysisExecutor>());
        Assert.IsInstanceOfType<ProjectAnalysisRunner>(
            services.GetRequiredService<IProjectAnalysisRunner>());
        Assert.IsInstanceOfType<ProjectAnalysisCompletionService>(
            services.GetRequiredService<IProjectAnalysisCompletionService>());
        Assert.IsInstanceOfType<ProjectReviewAgentTeamWorkerFactory>(
            services.GetRequiredService<IProjectReviewAgentTeamWorkerFactory>());
        Assert.IsInstanceOfType<ProjectReviewAgentTeamDependenciesFactory>(
            services.GetRequiredService<IProjectReviewAgentTeamDependenciesFactory>());
        Assert.IsInstanceOfType<ProjectReviewWorkflowRunnerFactory>(
            services.GetRequiredService<IProjectReviewWorkflowRunnerFactory>());
        Assert.IsInstanceOfType<ScanRunnerFactory>(
            services.GetRequiredService<IScanRunnerFactory>());
        Assert.IsInstanceOfType<ProjectPlanRunnerFactory>(
            services.GetRequiredService<IProjectPlanRunnerFactory>());
        Assert.IsInstanceOfType<RuleFlowRunnerFactory>(
            services.GetRequiredService<IRuleFlowRunnerFactory>());
        Assert.IsInstanceOfType<RuleReviewRunnerFactory>(
            services.GetRequiredService<IRuleReviewRunnerFactory>());
        Assert.IsInstanceOfType<RuleReportRunnerFactory>(
            services.GetRequiredService<IRuleReportRunnerFactory>());
        Assert.IsInstanceOfType<ExecutionReadinessGate>(
            services.GetRequiredService<IExecutionReadinessGate>());
        Assert.IsInstanceOfType<ExecutionQueueClaimer>(
            services.GetRequiredService<IExecutionQueueClaimer>());
        Assert.IsInstanceOfType<ClaimExecutor>(
            services.GetRequiredService<IClaimExecutor>());
        Assert.IsInstanceOfType<ExecutionStateService>(
            services.GetRequiredService<IExecutionStateService>());
        Assert.IsInstanceOfType<ExecutionArtifactStore>(
            services.GetRequiredService<IExecutionArtifactStore>());
        Assert.IsInstanceOfType<InterruptedProjectRecoveryService>(
            services.GetRequiredService<IInterruptedProjectRecoveryService>());
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
        Assert.IsInstanceOfType<ProjectUploadService>(
            services.GetRequiredService<IProjectUploadService>());
        Assert.IsInstanceOfType<ProjectQueueService>(
            services.GetRequiredService<IProjectQueueService>());
        Assert.IsInstanceOfType<ProjectDeletionService>(
            services.GetRequiredService<IProjectDeletionService>());
        Assert.IsInstanceOfType<ProjectIntakeService>(
            services.GetRequiredService<IProjectIntakeService>());
        Assert.IsInstanceOfType<ProjectReportProjectionMapper>(
            services.GetRequiredService<IProjectReportProjectionMapper>());
        Assert.IsInstanceOfType<ProjectReportQueryService>(
            services.GetRequiredService<IProjectReportQueryService>());
        Assert.IsInstanceOfType<ProjectReportExportService>(
            services.GetRequiredService<IProjectReportExportService>());
        Assert.IsInstanceOfType<ProjectReportService>(
            services.GetRequiredService<IProjectReportService>());
        Assert.IsInstanceOfType<ProjectSidebarSnapshotService>(
            services.GetRequiredService<IProjectSidebarSnapshotService>());
    }

    [TestMethod]
    public void ValidateOnBuild_DoesNotMissInternalRegistrations()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();

        Assert.IsNotNull(serviceProvider);
    }

    private static ServiceProvider CreateServiceProvider(
        CollectingProjectAgentStatusLiveUpdateNotifier? liveUpdateNotifier = null)
    {
        ServiceCollection services = [];
        services.AddLogging();
        services.AddSingleton<IHostApplicationLifetime, TestHostApplicationLifetime>();
        services.AddSignalR();
        services.AddCodeSnifferDogServerServices(
            CreateConfiguration(),
            options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));

        if (liveUpdateNotifier is not null)
            services.AddSingleton<IProjectAgentStatusLiveUpdateNotifier>(liveUpdateNotifier);

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

    private sealed class CollectingProjectAgentStatusLiveUpdateNotifier : IProjectAgentStatusLiveUpdateNotifier
    {
        public List<ProjectAgentLiveUpdateDto> Updates { get; } = [];

        public Task NotifyAsync(ProjectAgentLiveUpdateDto update, CancellationToken cancellationToken = default)
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
