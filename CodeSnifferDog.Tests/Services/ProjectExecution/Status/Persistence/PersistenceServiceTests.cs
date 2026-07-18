using CodeSnifferDog.Models.ReviewAgentTeam;
using CodeSnifferDog.Models.ReviewAgentTeam.Runtime;
using CodeSnifferDog.Models.ReviewAgentTeam.Results;
using CodeSnifferDog.Models.ReviewAgentTeam.Analysis;
using CodeSnifferDog.Models.ReviewAgentTeam.Agents;
using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Persistence;
using CodeSnifferDog.Server.Services.ProjectExecution.Status.Runtime;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using PersistedAgentStatus = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus;
using CodeSnifferDog.Models.ReviewAgentTeam.Events;

namespace CodeSnifferDog.Tests.Services.ProjectExecution.Status.Persistence;

[TestClass]
public sealed class PersistenceServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public void ParseStatus_UnsupportedStatusThrowsOriginalException()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => PersistenceService.ParseStatus("Paused"));

        Assert.AreEqual("Unsupported agent status 'Paused'.", exception.Message);
    }

    [TestMethod]
    public async Task AppendTimelineEntry_DelegatesTimelineMutationAndPublishesAfterSave()
    {
        Guid projectId = Guid.CreateVersion7();
        CollectingLiveUpdateNotifier notifier = new();
        FakeTimelinePersistenceService timelinePersistenceService = new();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        await SeedAgentAsync(dbContextFactory, projectId);
        PersistenceService service = new(
            projectId,
            dbContextFactory,
            notifier,
            new LiveUpdateFactory(new ProjectionMapper(new ProjectStatusMapper())),
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
        Assert.AreEqual(LiveUpdateKind.TimelineEntryUpserted, notifier.Updates[0].Kind);
        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        Assert.AreEqual(1, await dbContext.ProjectAgentTimelineEntries.CountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task RemoveTranscriptEntries_WhenTimelineServiceReturnsNull_DoesNotNotify()
    {
        Guid projectId = Guid.CreateVersion7();
        CollectingLiveUpdateNotifier notifier = new();
        FakeTimelinePersistenceService timelinePersistenceService = new()
        {
            RemovalResult = null,
        };
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        await SeedAgentAsync(dbContextFactory, projectId);
        PersistenceService service = new(
            projectId,
            dbContextFactory,
            notifier,
            new LiveUpdateFactory(new ProjectionMapper(new ProjectStatusMapper())),
            timelinePersistenceService);

        await service.RemoveTranscriptEntriesAsync(
            new TranscriptClearedEvent
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
            options.UseInMemoryDatabase(Guid.CreateVersion7().ToString("N"), new InMemoryDatabaseRoot()));
        return services.BuildServiceProvider();
    }

    private static async Task SeedAgentAsync(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        Guid projectId)
    {
        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        ProjectAgentGroupRecord group = new()
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            RuntimeKey = "group",
            DisplayName = "Group",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        dbContext.ProjectAgentGroups.Add(group);
        dbContext.ProjectAgents.Add(new ProjectAgentRecord
        {
            Id = Guid.CreateVersion7(),
            ProjectAgentGroupId = group.Id,
            RuntimeKey = "agent",
            DisplayName = "Agent",
            SystemPrompt = "Prompt",
            Status = PersistedAgentStatus.Waiting,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
    }

    private sealed class CollectingLiveUpdateNotifier : ILiveUpdateNotifier
    {
        public List<LiveUpdateDto> Updates { get; } = [];

        public Task NotifyAsync(LiveUpdateDto update, CancellationToken cancellationToken = default)
        {
            Updates.Add(update);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTimelinePersistenceService : ITimelinePersistenceService
    {
        public List<ProjectAgentTimelineEntryType> EntryTypes { get; } = [];

        public bool RemoveTranscriptCalled { get; private set; }

        public TimelineRemovalMutationResult? RemovalResult { get; init; } =
            new(Guid.CreateVersion7(), [Guid.CreateVersion7()], DateTimeOffset.UtcNow);

        public Task<TimelineEntryMutationResult> AppendTimelineEntryAsync(
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
                Id = Guid.CreateVersion7(),
                ProjectAgentId = agentId,
                Sequence = 1,
                EntryType = entryType,
                Message = message,
                OccurredAtUtc = occurredAtUtc,
            };
            dbContext.ProjectAgentTimelineEntries.Add(entry);
            return Task.FromResult(new TimelineEntryMutationResult(entry));
        }

        public Task<TimelineEntryMutationResult> AppendToolCallStartedEntryAsync(
            CodeSnifferDogServerDbContext dbContext,
            Guid agentId,
            ToolCallStartedEvent agentEvent,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TimelineEntryMutationResult> CompleteToolCallEntryAsync(
            CodeSnifferDogServerDbContext dbContext,
            Guid agentId,
            ToolCallCompletedEvent agentEvent,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TimelineRemovalMutationResult?> RemoveTranscriptEntriesAsync(
            CodeSnifferDogServerDbContext dbContext,
            Guid agentId,
            TranscriptClearedEvent agentEvent,
            CancellationToken cancellationToken)
        {
            RemoveTranscriptCalled = true;
            return Task.FromResult(RemovalResult);
        }
    }
}
