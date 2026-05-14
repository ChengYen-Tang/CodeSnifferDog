using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSnifferDog.Tests.Services.ProjectAgentStatus;

[TestClass]
public sealed class ProjectAgentStatusLiveBackfillServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task GetBackfillAsync_ReplaysProjectTreeAndOnlyMissingTimelineTail()
    {
        Guid projectId = Guid.NewGuid();
        Guid groupId = Guid.NewGuid();
        Guid agentAId = Guid.NewGuid();
        Guid agentBId = Guid.NewGuid();

        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        IProjectAgentStatusLiveBackfillService service =
            services.GetRequiredService<IProjectAgentStatusLiveBackfillService>();

        await SeedProjectAsync(dbContextFactory, projectId, groupId, agentAId, agentBId, TestContext.CancellationToken);

        IReadOnlyList<ProjectAgentLiveUpdateDto> updates = await service.GetBackfillAsync(
            new ProjectAgentLiveSubscriptionRequestDto
            {
                ProjectId = projectId,
                SnapshotGeneratedAtUtc = new DateTimeOffset(2026, 5, 10, 14, 0, 0, TimeSpan.Zero),
                AgentId = agentAId,
                LatestSequence = 1,
            },
            TestContext.CancellationToken);

        List<ProjectAgentLiveUpdateDto> groupUpdates = updates
            .Where(update => update.Kind == ProjectAgentLiveUpdateKind.AgentGroupUpserted)
            .ToList();
        List<ProjectAgentLiveUpdateDto> projectStatusUpdates = updates
            .Where(update => update.Kind == ProjectAgentLiveUpdateKind.ProjectStatusChanged)
            .ToList();
        List<ProjectAgentLiveUpdateDto> agentUpdates = updates
            .Where(update => update.Kind == ProjectAgentLiveUpdateKind.AgentUpserted)
            .ToList();
        List<ProjectAgentLiveUpdateDto> timelineUpdates = updates
            .Where(update => update.Kind == ProjectAgentLiveUpdateKind.TimelineEntryUpserted)
            .ToList();

        Assert.HasCount(1, projectStatusUpdates);
        Assert.HasCount(1, groupUpdates);
        Assert.HasCount(2, agentUpdates);
        Assert.HasCount(1, timelineUpdates);

        Assert.AreEqual(ProjectStatus.Reviewing, projectStatusUpdates[0].ProjectStatus!.Status);
        Assert.AreEqual("Review Group", groupUpdates[0].Group!.DisplayName);
        Assert.AreEqual("Agent A", agentUpdates[0].Agent!.DisplayName);
        Assert.AreEqual("Agent B", agentUpdates[1].Agent!.DisplayName);

        Assert.IsTrue(timelineUpdates.All(update => update.TimelineEntry is not null));
        Assert.IsTrue(timelineUpdates.Any(update =>
            update.TimelineEntry!.AgentId == agentAId &&
            update.TimelineEntry.Sequence == 2 &&
            update.TimelineEntry.Message == "Agent A second entry"));
        Assert.IsFalse(timelineUpdates.Any(update =>
            update.TimelineEntry!.AgentId == agentAId &&
            update.TimelineEntry.Sequence == 1));
        Assert.IsFalse(timelineUpdates.Any(update => update.TimelineEntry!.AgentId == agentBId));
    }

    private static async Task SeedProjectAsync(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        Guid projectId,
        Guid groupId,
        Guid agentAId,
        Guid agentBId,
        CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        dbContext.Projects.Add(new ProjectRecord
        {
            Id = projectId,
            OriginalFileName = "repo.zip",
            StoredZipRelativePath = "repo/repo.zip",
            Status = ProjectProcessingStatus.Reviewing,
            FileSizeBytes = 42,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-30),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            QueueTimestampUtc = DateTimeOffset.UtcNow.AddMinutes(-29),
            ProcessingStartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-28),
        });

        dbContext.ProjectAgentGroups.Add(new ProjectAgentGroupRecord
        {
            Id = groupId,
            ProjectId = projectId,
            RuntimeKey = "review-group",
            DisplayName = "Review Group",
            CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero),
        });

        dbContext.ProjectAgents.AddRange(
            new ProjectAgentRecord
            {
                Id = agentAId,
                ProjectAgentGroupId = groupId,
                RuntimeKey = "agent-a",
                DisplayName = "Agent A",
                Status = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus.Running,
                CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 13, 1, 0, TimeSpan.Zero),
            },
            new ProjectAgentRecord
            {
                Id = agentBId,
                ProjectAgentGroupId = groupId,
                RuntimeKey = "agent-b",
                DisplayName = "Agent B",
                Status = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus.Waiting,
                CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 13, 2, 0, TimeSpan.Zero),
            });

        dbContext.ProjectAgentTimelineEntries.AddRange(
            new ProjectAgentTimelineEntryRecord
            {
                Id = Guid.NewGuid(),
                ProjectAgentId = agentAId,
                Sequence = 1,
                EntryType = ProjectAgentTimelineEntryType.Output,
                Message = "Agent A first entry",
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 13, 10, 0, TimeSpan.Zero),
            },
            new ProjectAgentTimelineEntryRecord
            {
                Id = Guid.NewGuid(),
                ProjectAgentId = agentAId,
                Sequence = 2,
                EntryType = ProjectAgentTimelineEntryType.Output,
                Message = "Agent A second entry",
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 13, 11, 0, TimeSpan.Zero),
            },
            new ProjectAgentTimelineEntryRecord
            {
                Id = Guid.NewGuid(),
                ProjectAgentId = agentBId,
                Sequence = 1,
                EntryType = ProjectAgentTimelineEntryType.Tool,
                ToolCallId = "tool-call-1",
                ToolName = "RunRipgrepCommand",
                ToolArguments = "rg phase3",
                ToolResult = "2 matches",
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 13, 12, 0, TimeSpan.Zero),
            });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ServiceProvider CreateServices()
    {
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName = Guid.NewGuid().ToString("N");
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddScoped<IProjectAgentStatusLiveBackfillService, ProjectAgentStatusLiveBackfillService>();
        return services.BuildServiceProvider();
    }
}
