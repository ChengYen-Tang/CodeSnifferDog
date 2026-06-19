using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Services.ProjectExecution.Status;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class AgentStatusRuntimeFactoryTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task Create_RuntimeHandlerPersistsEventsAndPublishesLiveUpdates()
    {
        Guid projectId = Guid.NewGuid();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using ServiceProvider services = CreateServices(liveUpdateNotifier);
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        AgentStatusRuntimeFactory factory = new(
            dbContextFactory,
            liveUpdateNotifier,
            new AgentStatusProjectionMapper());
        AgentStatusRuntime runtime = factory.Create(projectId);

        await runtime.EventHandler.HandleAsync(
            new AgentGroupCreatedEvent
            {
                GroupKey = "group-1",
                DisplayName = "Group 1",
                OccurredAtUtc = DateTimeOffset.UtcNow,
            },
            TestContext.CancellationToken);
        await runtime.EventHandler.HandleAsync(
            new AgentCreatedEvent
            {
                GroupKey = "group-1",
                AgentKey = "agent-1",
                DisplayName = "Agent 1",
                SystemPrompt = "System prompt",
                InitialStatus = "Waiting",
                OccurredAtUtc = DateTimeOffset.UtcNow,
            },
            TestContext.CancellationToken);

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(group => group.ProjectId == projectId && group.RuntimeKey == "group-1", TestContext.CancellationToken);
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(agent => agent.ProjectAgentGroupId == group.Id && agent.RuntimeKey == "agent-1", TestContext.CancellationToken);

        Assert.AreEqual("Group 1", group.DisplayName);
        Assert.AreEqual("Agent 1", agent.DisplayName);
        Assert.HasCount(2, liveUpdateNotifier.Updates);
        Assert.AreEqual(ProjectAgentLiveUpdateKind.AgentGroupUpserted, liveUpdateNotifier.Updates[0].Kind);
        Assert.AreEqual(ProjectAgentLiveUpdateKind.AgentUpserted, liveUpdateNotifier.Updates[1].Kind);
    }

    [TestMethod]
    public async Task Create_RuntimeUsesInjectedProjectionMapper()
    {
        Guid projectId = Guid.NewGuid();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        TrackingProjectionMapper projectionMapper = new();
        using ServiceProvider services = CreateServices(liveUpdateNotifier);
        AgentStatusRuntimeFactory factory = new(
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>(),
            liveUpdateNotifier,
            projectionMapper);
        AgentStatusRuntime runtime = factory.Create(projectId);

        await runtime.EventHandler.HandleAsync(
            new AgentGroupCreatedEvent
            {
                GroupKey = "group-1",
                DisplayName = "Group 1",
                OccurredAtUtc = DateTimeOffset.UtcNow,
            },
            TestContext.CancellationToken);
        await runtime.EventHandler.HandleAsync(
            new AgentCreatedEvent
            {
                GroupKey = "group-1",
                AgentKey = "agent-1",
                DisplayName = "Agent 1",
                SystemPrompt = "System prompt",
                InitialStatus = "Waiting",
                OccurredAtUtc = DateTimeOffset.UtcNow,
            },
            TestContext.CancellationToken);

        Assert.AreEqual(1, projectionMapper.MapGroupCallCount);
        Assert.AreEqual(1, projectionMapper.MapAgentCallCount);
    }

    private static ServiceProvider CreateServices(
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier)
    {
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName = Guid.NewGuid().ToString("N");
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddSingleton<IProjectAgentStatusLiveUpdateNotifier>(liveUpdateNotifier);
        return services.BuildServiceProvider();
    }

    private sealed class CollectingProjectAgentStatusLiveUpdateNotifier : IProjectAgentStatusLiveUpdateNotifier
    {
        public List<ProjectAgentLiveUpdateDto> Updates { get; } = [];

        public Task NotifyAsync(ProjectAgentLiveUpdateDto update, CancellationToken cancellationToken = default)
        {
            Updates.Add(update);
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingProjectionMapper : IAgentStatusProjectionMapper
    {
        private readonly AgentStatusProjectionMapper _inner = new();

        public int MapGroupCallCount { get; private set; }

        public int MapAgentCallCount { get; private set; }

        public ProjectStatus MapProjectStatus(ProjectProcessingStatus status) =>
            _inner.MapProjectStatus(status);

        public ProjectAgentRunStatus MapAgentStatus(
            PersistedAgentStatus status,
            AgentStatusProjectionExceptionStyle exceptionStyle = AgentStatusProjectionExceptionStyle.Persisted) =>
            _inner.MapAgentStatus(status, exceptionStyle);

        public ProjectAgentTimelineEntryKind MapTimelineEntryKind(
            ProjectAgentTimelineEntryType entryType,
            AgentStatusProjectionExceptionStyle exceptionStyle = AgentStatusProjectionExceptionStyle.Persisted) =>
            _inner.MapTimelineEntryKind(entryType, exceptionStyle);

        public ProjectAgentGroupLiveDto MapGroup(AgentStatusGroupProjection group)
        {
            MapGroupCallCount++;
            return _inner.MapGroup(group);
        }

        public ProjectAgentLiveDto MapAgent(
            AgentStatusAgentProjection agent,
            AgentStatusProjectionExceptionStyle exceptionStyle = AgentStatusProjectionExceptionStyle.Persisted)
        {
            MapAgentCallCount++;
            return _inner.MapAgent(agent, exceptionStyle);
        }

        public ProjectAgentTimelineEntryDto MapTimelineEntry(
            AgentStatusTimelineEntryProjection entry,
            AgentStatusProjectionExceptionStyle exceptionStyle = AgentStatusProjectionExceptionStyle.Persisted) =>
            _inner.MapTimelineEntry(entry, exceptionStyle);
    }
}
