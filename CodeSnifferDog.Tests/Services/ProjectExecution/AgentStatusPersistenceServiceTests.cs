using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class AgentStatusPersistenceServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public void ParseStatus_UnsupportedStatusThrowsOriginalException()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => AgentStatusPersistenceService.ParseStatus("Paused"));

        Assert.AreEqual("Unsupported agent status 'Paused'.", exception.Message);
    }

    [TestMethod]
    public async Task AppendTimelineEntry_DelegatesTimelineMutationAndPublishesAfterSave()
    {
        Guid projectId = Guid.NewGuid();
        CollectingProjectAgentStatusLiveUpdateNotifier notifier = new();
        FakeTimelinePersistenceService timelinePersistenceService = new();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        await SeedAgentAsync(dbContextFactory, projectId);
        AgentStatusPersistenceService service = new(
            projectId,
            dbContextFactory,
            notifier,
            new AgentStatusLiveUpdateFactory(new AgentStatusProjectionMapper()),
            timelinePersistenceService);

        await service.AppendTimelineEntryAsync(
            "group",
            "agent",
            ProjectAgentTimelineEntryType.Output,
            "message",
            DateTimeOffset.UtcNow,
            TestContext.CancellationToken);

        Assert.AreEqual(ProjectAgentTimelineEntryType.Output, timelinePersistenceService.EntryTypes.Single());
        Assert.HasCount(1, notifier.Updates);
        Assert.AreEqual(ProjectAgentLiveUpdateKind.TimelineEntryUpserted, notifier.Updates[0].Kind);
        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        Assert.AreEqual(1, await dbContext.ProjectAgentTimelineEntries.CountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RemoveTranscriptEntries_WhenTimelineServiceReturnsNull_DoesNotNotify()
    {
        Guid projectId = Guid.NewGuid();
        CollectingProjectAgentStatusLiveUpdateNotifier notifier = new();
        FakeTimelinePersistenceService timelinePersistenceService = new()
        {
            RemovalResult = null,
        };
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        await SeedAgentAsync(dbContextFactory, projectId);
        AgentStatusPersistenceService service = new(
            projectId,
            dbContextFactory,
            notifier,
            new AgentStatusLiveUpdateFactory(new AgentStatusProjectionMapper()),
            timelinePersistenceService);

        await service.RemoveTranscriptEntriesAsync(
            new AgentTranscriptClearedEvent
            {
                GroupKey = "group",
                AgentKey = "agent",
                ClearAfterUtc = DateTimeOffset.UtcNow,
                OccurredAtUtc = DateTimeOffset.UtcNow,
            },
            TestContext.CancellationToken);

        Assert.IsTrue(timelinePersistenceService.RemoveTranscriptCalled);
        Assert.HasCount(0, notifier.Updates);
    }

    private static ServiceProvider CreateServices()
    {
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot()));
        return services.BuildServiceProvider();
    }

    private static async Task SeedAgentAsync(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        Guid projectId)
    {
        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        ProjectAgentGroupRecord group = new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RuntimeKey = "group",
            DisplayName = "Group",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        dbContext.ProjectAgentGroups.Add(group);
        dbContext.ProjectAgents.Add(new ProjectAgentRecord
        {
            Id = Guid.NewGuid(),
            ProjectAgentGroupId = group.Id,
            RuntimeKey = "agent",
            DisplayName = "Agent",
            SystemPrompt = "Prompt",
            Status = PersistedAgentStatus.Waiting,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
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

    private sealed class FakeTimelinePersistenceService : IAgentTimelinePersistenceService
    {
        public List<ProjectAgentTimelineEntryType> EntryTypes { get; } = [];

        public bool RemoveTranscriptCalled { get; private set; }

        public AgentTimelineRemovalMutationResult? RemovalResult { get; init; } =
            new(Guid.NewGuid(), [Guid.NewGuid()], DateTimeOffset.UtcNow);

        public Task<AgentTimelineEntryMutationResult> AppendTimelineEntryAsync(
            CodeSnifferDogServerDbContext dbContext,
            Guid agentId,
            ProjectAgentTimelineEntryType entryType,
            string? message,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken)
        {
            EntryTypes.Add(entryType);
            ProjectAgentTimelineEntryRecord entry = new()
            {
                Id = Guid.NewGuid(),
                ProjectAgentId = agentId,
                Sequence = 1,
                EntryType = entryType,
                Message = message,
                OccurredAtUtc = occurredAtUtc,
            };
            dbContext.ProjectAgentTimelineEntries.Add(entry);
            return Task.FromResult(new AgentTimelineEntryMutationResult(entry));
        }

        public Task<AgentTimelineEntryMutationResult> AppendToolCallStartedEntryAsync(
            CodeSnifferDogServerDbContext dbContext,
            Guid agentId,
            ToolCallStartedEvent agentEvent,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AgentTimelineEntryMutationResult> CompleteToolCallEntryAsync(
            CodeSnifferDogServerDbContext dbContext,
            Guid agentId,
            ToolCallCompletedEvent agentEvent,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AgentTimelineRemovalMutationResult?> RemoveTranscriptEntriesAsync(
            CodeSnifferDogServerDbContext dbContext,
            Guid agentId,
            AgentTranscriptClearedEvent agentEvent,
            CancellationToken cancellationToken)
        {
            RemoveTranscriptCalled = true;
            return Task.FromResult(RemovalResult);
        }
    }
}
