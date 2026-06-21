using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;
using CodeSnifferDog.Server.Shared.AgentStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CodeSnifferDog.Tests.Services.ProjectAgentStatus;

[TestClass]
public sealed class ProjectAgentStatusBackfillQueryServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task GetBackfillAsync_WhenProjectIsMissing_ReturnsEmptyReadModel()
    {
        ProjectAgentStatusBackfillQueryService service = new(CreateDbContextFactory());
        Guid projectId = Guid.NewGuid();

        ProjectAgentStatusBackfillReadModel result = await service.GetBackfillAsync(
            new ProjectAgentLiveSubscriptionRequestDto
            {
                ProjectId = projectId,
                SnapshotGeneratedAtUtc = DateTimeOffset.UtcNow,
            },
            TestContext.CancellationToken);

        Assert.AreEqual(projectId, result.ProjectId);
        Assert.IsNull(result.ProjectStatus);
        Assert.IsEmpty(result.Groups);
        Assert.IsEmpty(result.Agents);
        Assert.IsEmpty(result.TimelineEntries);
    }

    [TestMethod]
    public async Task GetBackfillAsync_ReturnsOrderedProjectTreeAndRequestedTimelineTail()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.NewGuid();
        Guid groupId = Guid.NewGuid();
        Guid agentId = Guid.NewGuid();
        await SeedProjectTreeAsync(dbContextFactory, projectId, groupId, agentId);
        ProjectAgentStatusBackfillQueryService service = new(dbContextFactory);

        ProjectAgentStatusBackfillReadModel result = await service.GetBackfillAsync(
            new ProjectAgentLiveSubscriptionRequestDto
            {
                ProjectId = projectId,
                SnapshotGeneratedAtUtc = DateTimeOffset.UtcNow,
                AgentId = agentId,
                LatestSequence = 1,
            },
            TestContext.CancellationToken);

        Assert.AreEqual(ProjectProcessingStatus.Reviewing, result.ProjectStatus);
        Assert.AreEqual(groupId, result.Groups.Single().GroupId);
        Assert.AreEqual(agentId, result.Agents.Single().AgentId);
        Assert.HasCount(1, result.TimelineEntries);
        Assert.AreEqual(2L, result.TimelineEntries.Single().Sequence);
    }

    [TestMethod]
    public async Task GetBackfillAsync_WhenRequestedAgentBelongsToDifferentProject_ReturnsEmptyTimelineTail()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.NewGuid();
        Guid groupId = Guid.NewGuid();
        Guid agentId = Guid.NewGuid();
        Guid otherProjectId = Guid.NewGuid();
        Guid otherGroupId = Guid.NewGuid();
        Guid otherAgentId = Guid.NewGuid();
        await SeedProjectTreeAsync(dbContextFactory, projectId, groupId, agentId);
        await SeedProjectTreeAsync(dbContextFactory, otherProjectId, otherGroupId, otherAgentId);
        ProjectAgentStatusBackfillQueryService service = new(dbContextFactory);

        ProjectAgentStatusBackfillReadModel result = await service.GetBackfillAsync(
            new ProjectAgentLiveSubscriptionRequestDto
            {
                ProjectId = projectId,
                SnapshotGeneratedAtUtc = DateTimeOffset.UtcNow,
                AgentId = otherAgentId,
                LatestSequence = 0,
            },
            TestContext.CancellationToken);

        Assert.AreEqual(ProjectProcessingStatus.Reviewing, result.ProjectStatus);
        Assert.AreEqual(agentId, result.Agents.Single().AgentId);
        Assert.IsEmpty(result.TimelineEntries);
    }

    private static IDbContextFactory<CodeSnifferDogServerDbContext> CreateDbContextFactory()
    {
        DbContextOptions<CodeSnifferDogServerDbContext> options = new DbContextOptionsBuilder<CodeSnifferDogServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PooledDbContextFactory<CodeSnifferDogServerDbContext>(options);
    }

    private async Task SeedProjectTreeAsync(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        Guid projectId,
        Guid groupId,
        Guid agentId)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
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
                Id = Guid.NewGuid(),
                ProjectAgentId = agentId,
                Sequence = 1,
                EntryType = ProjectAgentTimelineEntryType.Output,
                Message = "old",
                OccurredAtUtc = nowUtc,
            },
            new ProjectAgentTimelineEntryRecord
            {
                Id = Guid.NewGuid(),
                ProjectAgentId = agentId,
                Sequence = 2,
                EntryType = ProjectAgentTimelineEntryType.Output,
                Message = "new",
                OccurredAtUtc = nowUtc,
            });
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
    }
}
