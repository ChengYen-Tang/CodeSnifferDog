using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CodeSnifferDog.Tests.Services.ProjectAgentStatus;

[TestClass]
public sealed class SnapshotQueryServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task GetSnapshotAsync_WhenProjectIsMissing_ReturnsNull()
    {
        SnapshotQueryService service = new(CreateDbContextFactory());

        SnapshotReadModel? snapshot = await service.GetSnapshotAsync(
            Guid.CreateVersion7(),
            selectedAgentId: null,
            TestContext.CancellationToken);

        Assert.IsNull(snapshot);
    }

    [TestMethod]
    public async Task GetSnapshotAsync_ResolvesSelectedAgentAndOrdersTimeline()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.CreateVersion7();
        Guid agentId = Guid.CreateVersion7();
        await SeedProjectTreeAsync(dbContextFactory, projectId, agentId);
        SnapshotQueryService service = new(dbContextFactory);

        SnapshotReadModel? snapshot = await service.GetSnapshotAsync(
            projectId,
            selectedAgentId: Guid.CreateVersion7(),
            TestContext.CancellationToken);

        Assert.IsNotNull(snapshot);
        SnapshotAgentRow loadedAgent = snapshot.Groups.Single().Agents.Single();
        Assert.AreEqual(agentId, loadedAgent.Agent.AgentId);
        Assert.IsTrue(loadedAgent.HasLoadedHistory);
        CollectionAssert.AreEqual(
            new[] { 1L, 2L },
            loadedAgent.TimelineEntries.Select(entry => entry.Sequence).ToList());
    }

    [TestMethod]
    public async Task GetAgentHistoryAsync_RequiresAgentToBelongToProject()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.CreateVersion7();
        Guid otherProjectId = Guid.CreateVersion7();
        Guid agentId = Guid.CreateVersion7();
        await SeedProjectTreeAsync(dbContextFactory, projectId, agentId);
        SnapshotQueryService service = new(dbContextFactory);

        HistorySnapshotReadModel? history = await service.GetAgentHistoryAsync(
            otherProjectId,
            agentId,
            TestContext.CancellationToken);

        Assert.IsNull(history);
    }

    private static IDbContextFactory<CodeSnifferDogServerDbContext> CreateDbContextFactory()
    {
        DbContextOptions<CodeSnifferDogServerDbContext> options = new DbContextOptionsBuilder<CodeSnifferDogServerDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString("N"))
            .Options;

        return new PooledDbContextFactory<CodeSnifferDogServerDbContext>(options);
    }

    private async Task SeedProjectTreeAsync(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        Guid projectId,
        Guid agentId)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        Guid groupId = Guid.CreateVersion7();
        dbContext.Projects.Add(new ProjectRecord
        {
            Id = projectId,
            OriginalFileName = "repo.zip",
            StoredZipRelativePath = $"uploads/{projectId:N}.zip",
            Status = ProjectProcessingStatus.Reviewing,
            FileSizeBytes = 10,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            QueueTimestampUtc = nowUtc,
        });
        dbContext.ProjectAgentGroups.Add(new ProjectAgentGroupRecord
        {
            Id = groupId,
            ProjectId = projectId,
            RuntimeKey = "group",
            DisplayName = "Group",
            CreatedAtUtc = nowUtc,
        });
        dbContext.ProjectAgents.Add(new ProjectAgentRecord
        {
            Id = agentId,
            ProjectAgentGroupId = groupId,
            RuntimeKey = "agent",
            DisplayName = "Agent",
            SystemPrompt = "prompt",
            Status = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus.Running,
            CreatedAtUtc = nowUtc,
        });
        dbContext.ProjectAgentTimelineEntries.AddRange(
            new ProjectAgentTimelineEntryRecord
            {
                Id = Guid.CreateVersion7(),
                ProjectAgentId = agentId,
                Sequence = 2,
                EntryType = ProjectAgentTimelineEntryType.Output,
                Message = "second",
                OccurredAtUtc = nowUtc,
            },
            new ProjectAgentTimelineEntryRecord
            {
                Id = Guid.CreateVersion7(),
                ProjectAgentId = agentId,
                Sequence = 1,
                EntryType = ProjectAgentTimelineEntryType.Output,
                Message = "first",
                OccurredAtUtc = nowUtc,
            });
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
    }
}
