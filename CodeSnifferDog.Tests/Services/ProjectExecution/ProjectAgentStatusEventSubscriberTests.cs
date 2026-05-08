using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Modules.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectAgentStatusEventSubscriberTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task MessageEvents_PersistInputAndOutputTimelineEntries()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
            new(projectId, dbContextFactory, eventStream.Events);

        string groupKey = "group-1";
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, "agent-1");

        await eventStream.PublishGroupCreatedAsync(groupKey, "Review Task 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Rule Review", "Waiting", TestContext.CancellationToken);
        await agentScope.PublishUserMessageAsync("Inspect Program.cs", TestContext.CancellationToken);
        await agentScope.PublishAssistantMessageAsync("I will inspect Program.cs.", TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);

        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(candidate => candidate.ProjectId == projectId && candidate.RuntimeKey == groupKey, TestContext.CancellationToken);
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(candidate => candidate.ProjectAgentGroupId == group.Id && candidate.RuntimeKey == "agent-1", TestContext.CancellationToken);
        List<ProjectAgentTimelineEntryRecord> entries = await dbContext.ProjectAgentTimelineEntries
            .Where(entry => entry.ProjectAgentId == agent.Id)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(TestContext.CancellationToken);

        Assert.HasCount(2, entries);
        Assert.AreEqual(ProjectAgentTimelineEntryType.Input, entries[0].EntryType);
        Assert.AreEqual("Inspect Program.cs", entries[0].Message);
        Assert.AreEqual(ProjectAgentTimelineEntryType.Output, entries[1].EntryType);
        Assert.AreEqual("I will inspect Program.cs.", entries[1].Message);
        Assert.AreEqual(1L, entries[0].Sequence);
        Assert.AreEqual(2L, entries[1].Sequence);
        Assert.IsTrue(entries.All(entry => entry.ToolName is null));
        Assert.IsTrue(entries.All(entry => entry.ToolArguments is null));
        Assert.IsTrue(entries.All(entry => entry.ToolResult is null));
    }

    [TestMethod]
    public async Task CompactionEvent_PersistsCompactionTimelineEntry()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        using AgentStatusEventStream eventStream = new();
        await using ProjectAgentStatusEventSubscriber subscriber =
            new(projectId, dbContextFactory, eventStream.Events);

        string groupKey = "group-1";
        IAgentEventScope agentScope = eventStream.CreateScope(groupKey, "agent-1");

        await eventStream.PublishGroupCreatedAsync(groupKey, "Review Task 1", TestContext.CancellationToken);
        await agentScope.PublishCreatedAsync("Rule Review", "Waiting", TestContext.CancellationToken);
        await agentScope.PublishCompactionAsync(TestContext.CancellationToken);
        await agentScope.PublishCompactionAsync(TestContext.CancellationToken);

        eventStream.Complete();
        await subscriber.DisposeAsync();

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);

        ProjectAgentGroupRecord group = await dbContext.ProjectAgentGroups
            .SingleAsync(candidate => candidate.ProjectId == projectId && candidate.RuntimeKey == groupKey, TestContext.CancellationToken);
        ProjectAgentRecord agent = await dbContext.ProjectAgents
            .SingleAsync(candidate => candidate.ProjectAgentGroupId == group.Id && candidate.RuntimeKey == "agent-1", TestContext.CancellationToken);
        List<ProjectAgentTimelineEntryRecord> entries = await dbContext.ProjectAgentTimelineEntries
            .Where(entry => entry.ProjectAgentId == agent.Id)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(TestContext.CancellationToken);

        Assert.AreEqual(ProjectAgentStatus.Waiting, agent.Status);
        Assert.HasCount(2, entries);
        Assert.IsTrue(entries.All(entry => entry.EntryType == ProjectAgentTimelineEntryType.Compaction));
        Assert.AreEqual(1L, entries[0].Sequence);
        Assert.AreEqual(2L, entries[1].Sequence);
        Assert.IsTrue(entries.All(entry => entry.Message is null));
        Assert.IsTrue(entries.All(entry => entry.ToolName is null));
        Assert.IsTrue(entries.All(entry => entry.ToolArguments is null));
        Assert.IsTrue(entries.All(entry => entry.ToolResult is null));
    }

    private static ServiceProvider CreateServices()
    {
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName = Guid.NewGuid().ToString("N");
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        return services.BuildServiceProvider();
    }
}
