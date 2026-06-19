using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Services.ProjectExecution.Status;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class AgentStatusEventSubscriberFactoryTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task Create_ReturnsSubscriberThatPersistsEventsAndPublishesLiveUpdates()
    {
        Guid projectId = Guid.NewGuid();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using ServiceProvider services = CreateServices(liveUpdateNotifier);
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        AgentStatusEventSubscriberFactory factory = new(
            dbContextFactory,
            liveUpdateNotifier,
            services.GetRequiredService<IAgentStatusProjectionMapper>());
        using AgentStatusEventStream eventStream = new();

        await using ProjectAgentStatusEventSubscriber subscriber =
            factory.Create(projectId, eventStream.Events);

        await eventStream.PublishGroupCreatedAsync("group-1", "Group 1", TestContext.CancellationToken);
        IAgentEventScope agentScope = eventStream.CreateScope("group-1", "agent-1");
        await agentScope.PublishCreatedAsync("Agent 1", "System prompt", "Waiting", TestContext.CancellationToken);
        eventStream.Complete();
        await subscriber.DisposeAsync();

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
    public async Task Create_DisposeFlushesQueuedSubscriberEvents()
    {
        Guid projectId = Guid.NewGuid();
        CollectingProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using ServiceProvider services = CreateServices(liveUpdateNotifier);
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        AgentStatusEventSubscriberFactory factory = new(
            dbContextFactory,
            liveUpdateNotifier,
            services.GetRequiredService<IAgentStatusProjectionMapper>());
        using AgentStatusEventStream eventStream = new();

        await using ProjectAgentStatusEventSubscriber subscriber =
            factory.Create(projectId, eventStream.Events);

        await eventStream.PublishGroupCreatedAsync("group-1", "Group 1", TestContext.CancellationToken);
        await eventStream.PublishGroupCreatedAsync("group-2", "Group 2", TestContext.CancellationToken);
        eventStream.Complete();
        await subscriber.DisposeAsync();

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        List<string> groupKeys = await dbContext.ProjectAgentGroups
            .Where(group => group.ProjectId == projectId)
            .OrderBy(group => group.RuntimeKey)
            .Select(group => group.RuntimeKey)
            .ToListAsync(TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { "group-1", "group-2" }, groupKeys);
        Assert.HasCount(2, liveUpdateNotifier.Updates);
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
        services.AddSingleton<IAgentStatusProjectionMapper, AgentStatusProjectionMapper>();
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
}
