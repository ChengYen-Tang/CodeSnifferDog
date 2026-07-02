using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;
using Microsoft.EntityFrameworkCore;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Tests.Services.ProjectExecution.Status.Persistence;

[TestClass]
public sealed class TimelinePersistenceServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task AppendTimelineEntry_AssignsNextSequenceAndFields()
    {
        TimelinePersistenceService service = new();
        await using CodeSnifferDogServerDbContext dbContext = CreateDbContext();
        Guid agentId = await SeedAgentAsync(dbContext);
        DateTimeOffset occurredAtUtc = DateTimeOffset.UtcNow;

        await service.AppendTimelineEntryAsync(
            dbContext,
            agentId,
            ProjectAgentTimelineEntryType.Input,
            "hello",
            occurredAtUtc,
            TestContext.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        TimelineEntryMutationResult result = await service.AppendTimelineEntryAsync(
            dbContext,
            agentId,
            ProjectAgentTimelineEntryType.Output,
            "world",
            occurredAtUtc.AddSeconds(1),
            TestContext.CancellationToken);

        Assert.AreEqual(2, result.Entry.Sequence);
        Assert.AreEqual(ProjectAgentTimelineEntryType.Output, result.Entry.EntryType);
        Assert.AreEqual("world", result.Entry.Message);
    }

    [TestMethod]
    public async Task ToolStartedAndCompleted_MergeIntoSingleEntry()
    {
        TimelinePersistenceService service = new();
        await using CodeSnifferDogServerDbContext dbContext = CreateDbContext();
        Guid agentId = await SeedAgentAsync(dbContext);
        DateTimeOffset occurredAtUtc = DateTimeOffset.UtcNow;

        TimelineEntryMutationResult started = await service.AppendToolCallStartedEntryAsync(
            dbContext,
            agentId,
            CreateToolStarted("call-1", occurredAtUtc),
            TestContext.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        TimelineEntryMutationResult completed = await service.CompleteToolCallEntryAsync(
            dbContext,
            agentId,
            CreateToolCompleted("call-1", occurredAtUtc.AddSeconds(1)),
            TestContext.CancellationToken);

        Assert.AreEqual(started.Entry.Id, completed.Entry.Id);
        Assert.AreEqual(1, completed.Entry.Sequence);
        Assert.AreEqual("Tool", completed.Entry.ToolName);
        Assert.AreEqual("{\"x\":1}", completed.Entry.ToolArguments);
        Assert.AreEqual("done", completed.Entry.ToolResult);
    }

    [TestMethod]
    public async Task ToolCompletedBeforeStarted_PreservesResultWhenStartedReplays()
    {
        TimelinePersistenceService service = new();
        await using CodeSnifferDogServerDbContext dbContext = CreateDbContext();
        Guid agentId = await SeedAgentAsync(dbContext);
        DateTimeOffset occurredAtUtc = DateTimeOffset.UtcNow;

        TimelineEntryMutationResult completed = await service.CompleteToolCallEntryAsync(
            dbContext,
            agentId,
            CreateToolCompleted("call-1", occurredAtUtc),
            TestContext.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        TimelineEntryMutationResult started = await service.AppendToolCallStartedEntryAsync(
            dbContext,
            agentId,
            CreateToolStarted("call-1", occurredAtUtc.AddSeconds(1)),
            TestContext.CancellationToken);

        Assert.AreEqual(completed.Entry.Id, started.Entry.Id);
        Assert.AreEqual(1, started.Entry.Sequence);
        Assert.AreEqual("Tool", started.Entry.ToolName);
        Assert.AreEqual("{\"x\":1}", started.Entry.ToolArguments);
        Assert.AreEqual("done", started.Entry.ToolResult);
    }

    [TestMethod]
    public async Task ToolReplay_UpdatesSameEntryWithoutNewSequence()
    {
        TimelinePersistenceService service = new();
        await using CodeSnifferDogServerDbContext dbContext = CreateDbContext();
        Guid agentId = await SeedAgentAsync(dbContext);
        DateTimeOffset occurredAtUtc = DateTimeOffset.UtcNow;

        await service.AppendToolCallStartedEntryAsync(
            dbContext,
            agentId,
            CreateToolStarted("call-1", occurredAtUtc),
            TestContext.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        await service.CompleteToolCallEntryAsync(
            dbContext,
            agentId,
            CreateToolCompleted("call-1", occurredAtUtc.AddSeconds(1), "first"),
            TestContext.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        TimelineEntryMutationResult replayed = await service.CompleteToolCallEntryAsync(
            dbContext,
            agentId,
            CreateToolCompleted("call-1", occurredAtUtc.AddSeconds(2), "second"),
            TestContext.CancellationToken);

        Assert.AreEqual(1, replayed.Entry.Sequence);
        Assert.AreEqual("second", replayed.Entry.ToolResult);
        Assert.AreEqual(1, dbContext.ProjectAgentTimelineEntries.Local.Count(entry => entry.EntryType == ProjectAgentTimelineEntryType.Tool));
    }

    [TestMethod]
    public async Task RemoveTranscriptEntries_RemovesOnlyNonInputEntriesAfterClearTime()
    {
        TimelinePersistenceService service = new();
        await using CodeSnifferDogServerDbContext dbContext = CreateDbContext();
        Guid agentId = await SeedAgentAsync(dbContext);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ProjectAgentTimelineEntryRecord beforeOutput = AddEntry(dbContext, agentId, 1, ProjectAgentTimelineEntryType.Output, now.AddMinutes(-2));
        ProjectAgentTimelineEntryRecord afterInput = AddEntry(dbContext, agentId, 2, ProjectAgentTimelineEntryType.Input, now.AddMinutes(1));
        ProjectAgentTimelineEntryRecord afterOutput = AddEntry(dbContext, agentId, 3, ProjectAgentTimelineEntryType.Output, now.AddMinutes(2));
        ProjectAgentTimelineEntryRecord afterTool = AddEntry(dbContext, agentId, 4, ProjectAgentTimelineEntryType.Tool, now.AddMinutes(3));
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        TimelineRemovalMutationResult? result = await service.RemoveTranscriptEntriesAsync(
            dbContext,
            agentId,
            new TranscriptClearedEvent
            {
                GroupKey = "group",
                AgentKey = "agent",
                ClearAfterUtc = now,
                OccurredAtUtc = now.AddMinutes(4),
            },
            TestContext.CancellationToken);

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(new[] { afterOutput.Id, afterTool.Id }, result.RemovedEntryIds.ToArray());
        Assert.IsFalse(dbContext.ProjectAgentTimelineEntries.Local.Any(entry => entry.Id == afterOutput.Id));
        Assert.IsFalse(dbContext.ProjectAgentTimelineEntries.Local.Any(entry => entry.Id == afterTool.Id));
        Assert.IsTrue(dbContext.ProjectAgentTimelineEntries.Local.Any(entry => entry.Id == beforeOutput.Id));
        Assert.IsTrue(dbContext.ProjectAgentTimelineEntries.Local.Any(entry => entry.Id == afterInput.Id));
    }

    [TestMethod]
    public async Task RemoveTranscriptEntries_WhenNothingRemovedReturnsNull()
    {
        TimelinePersistenceService service = new();
        await using CodeSnifferDogServerDbContext dbContext = CreateDbContext();
        Guid agentId = await SeedAgentAsync(dbContext);

        TimelineRemovalMutationResult? result = await service.RemoveTranscriptEntriesAsync(
            dbContext,
            agentId,
            new TranscriptClearedEvent
            {
                GroupKey = "group",
                AgentKey = "agent",
                ClearAfterUtc = DateTimeOffset.UtcNow,
                OccurredAtUtc = DateTimeOffset.UtcNow,
            },
            TestContext.CancellationToken);

        Assert.IsNull(result);
    }

    private static CodeSnifferDogServerDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<CodeSnifferDogServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<Guid> SeedAgentAsync(CodeSnifferDogServerDbContext dbContext)
    {
        ProjectAgentGroupRecord group = new()
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            RuntimeKey = "group",
            DisplayName = "Group",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        ProjectAgentRecord agent = new()
        {
            Id = Guid.NewGuid(),
            ProjectAgentGroupId = group.Id,
            RuntimeKey = "agent",
            DisplayName = "Agent",
            SystemPrompt = "Prompt",
            Status = PersistedAgentStatus.Waiting,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        dbContext.ProjectAgentGroups.Add(group);
        dbContext.ProjectAgents.Add(agent);
        await dbContext.SaveChangesAsync();
        return agent.Id;
    }

    private static ProjectAgentTimelineEntryRecord AddEntry(
        CodeSnifferDogServerDbContext dbContext,
        Guid agentId,
        long sequence,
        ProjectAgentTimelineEntryType entryType,
        DateTimeOffset occurredAtUtc)
    {
        ProjectAgentTimelineEntryRecord entry = new()
        {
            Id = Guid.NewGuid(),
            ProjectAgentId = agentId,
            Sequence = sequence,
            EntryType = entryType,
            OccurredAtUtc = occurredAtUtc,
        };
        dbContext.ProjectAgentTimelineEntries.Add(entry);
        return entry;
    }

    private static ToolCallStartedEvent CreateToolStarted(string toolCallId, DateTimeOffset occurredAtUtc) =>
        new()
        {
            GroupKey = "group",
            AgentKey = "agent",
            ToolCallId = toolCallId,
            ToolName = "Tool",
            Arguments = "{\"x\":1}",
            OccurredAtUtc = occurredAtUtc,
        };

    private static ToolCallCompletedEvent CreateToolCompleted(
        string toolCallId,
        DateTimeOffset occurredAtUtc,
        string result = "done") =>
        new()
        {
            GroupKey = "group",
            AgentKey = "agent",
            ToolCallId = toolCallId,
            Result = result,
            OccurredAtUtc = occurredAtUtc,
        };
}
