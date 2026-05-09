using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentSnapshots;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSnifferDog.Tests.Services.ProjectAgentSnapshots;

[TestClass]
public sealed class ProjectAgentStatusSnapshotServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task GetSnapshotAsync_WhenProjectDoesNotExist_ReturnsNull()
    {
        using ServiceProvider services = CreateServices();
        IProjectAgentStatusSnapshotService service = services.GetRequiredService<IProjectAgentStatusSnapshotService>();

        ProjectAgentStatusSnapshotDto? snapshot = await service.GetSnapshotAsync(Guid.NewGuid(), TestContext.CancellationToken);

        Assert.IsNull(snapshot);
    }

    [TestMethod]
    public async Task GetSnapshotAsync_MapsProjectTreeAndSortsByConfiguredRules()
    {
        Guid projectId = Guid.NewGuid();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        IProjectAgentStatusSnapshotService service = services.GetRequiredService<IProjectAgentStatusSnapshotService>();

        await SeedProjectAsync(dbContextFactory, projectId, TestContext.CancellationToken);

        ProjectAgentStatusSnapshotDto? snapshot = await service.GetSnapshotAsync(projectId, TestContext.CancellationToken);

        Assert.IsNotNull(snapshot);
        Assert.AreEqual(projectId, snapshot.ProjectId);
        Assert.AreEqual(ProjectStatus.Reviewing, snapshot.ProjectStatus);
        Assert.IsTrue(snapshot.SnapshotGeneratedAtUtc <= DateTimeOffset.UtcNow);

        Assert.HasCount(2, snapshot.AgentGroups);
        Assert.AreEqual("Alpha Group", snapshot.AgentGroups[0].DisplayName);
        Assert.AreEqual("Zulu Group", snapshot.AgentGroups[1].DisplayName);

        ProjectAgentGroupSnapshotDto firstGroup = snapshot.AgentGroups[0];
        Assert.HasCount(2, firstGroup.Agents);
        Assert.AreEqual("Alpha Agent", firstGroup.Agents[0].DisplayName);
        Assert.AreEqual("Beta Agent", firstGroup.Agents[1].DisplayName);
        Assert.AreEqual(ProjectAgentRunStatus.Completed, firstGroup.Agents[0].Status);
        Assert.AreEqual(ProjectAgentRunStatus.Running, firstGroup.Agents[1].Status);

        IReadOnlyList<ProjectAgentTimelineEntryDto> timeline = firstGroup.Agents[0].TimelineEntries;
        Assert.HasCount(3, timeline);
        Assert.AreEqual(1L, timeline[0].Sequence);
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Output, timeline[0].EntryKind);
        Assert.AreEqual(2L, timeline[1].Sequence);
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Tool, timeline[1].EntryKind);
        Assert.AreEqual("tool-call-1", timeline[1].ToolCallId);
        Assert.AreEqual("RunRipgrepCommand", timeline[1].ToolName);
        Assert.AreEqual("rg foo", timeline[1].ToolArguments);
        Assert.AreEqual("2 matches", timeline[1].ToolResult);
        Assert.AreEqual(3L, timeline[2].Sequence);
        Assert.AreEqual(ProjectAgentTimelineEntryKind.Compaction, timeline[2].EntryKind);
        Assert.AreEqual("Context automatically compacted", timeline[2].Message);
    }

    private static async Task SeedProjectAsync(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        ProjectRecord project = new()
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
        };

        ProjectAgentGroupRecord zuluGroup = new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RuntimeKey = "group-zulu",
            DisplayName = "Zulu Group",
            CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 10, 5, 0, TimeSpan.Zero),
        };

        ProjectAgentGroupRecord alphaGroup = new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            RuntimeKey = "group-alpha",
            DisplayName = "Alpha Group",
            CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 10, 1, 0, TimeSpan.Zero),
        };

        ProjectAgentRecord betaAgent = new()
        {
            Id = Guid.NewGuid(),
            ProjectAgentGroupId = alphaGroup.Id,
            RuntimeKey = "agent-beta",
            DisplayName = "Beta Agent",
            Status = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus.Running,
            CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 10, 3, 0, TimeSpan.Zero),
        };

        ProjectAgentRecord alphaAgent = new()
        {
            Id = Guid.NewGuid(),
            ProjectAgentGroupId = alphaGroup.Id,
            RuntimeKey = "agent-alpha",
            DisplayName = "Alpha Agent",
            Status = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus.Completed,
            CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 10, 2, 0, TimeSpan.Zero),
        };

        ProjectAgentRecord zuluAgent = new()
        {
            Id = Guid.NewGuid(),
            ProjectAgentGroupId = zuluGroup.Id,
            RuntimeKey = "agent-zulu",
            DisplayName = "Zulu Agent",
            Status = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus.Waiting,
            CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 10, 6, 0, TimeSpan.Zero),
        };

        dbContext.Projects.Add(project);
        dbContext.ProjectAgentGroups.AddRange(zuluGroup, alphaGroup);
        dbContext.ProjectAgents.AddRange(betaAgent, alphaAgent, zuluAgent);
        dbContext.ProjectAgentTimelineEntries.AddRange(
            new ProjectAgentTimelineEntryRecord
            {
                Id = Guid.NewGuid(),
                ProjectAgentId = alphaAgent.Id,
                Sequence = 3,
                EntryType = ProjectAgentTimelineEntryType.Compaction,
                Message = "Context automatically compacted",
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 10, 10, 0, TimeSpan.Zero),
            },
            new ProjectAgentTimelineEntryRecord
            {
                Id = Guid.NewGuid(),
                ProjectAgentId = alphaAgent.Id,
                Sequence = 1,
                EntryType = ProjectAgentTimelineEntryType.Output,
                Message = "Started review",
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 10, 8, 0, TimeSpan.Zero),
            },
            new ProjectAgentTimelineEntryRecord
            {
                Id = Guid.NewGuid(),
                ProjectAgentId = alphaAgent.Id,
                Sequence = 2,
                EntryType = ProjectAgentTimelineEntryType.Tool,
                ToolCallId = "tool-call-1",
                ToolName = "RunRipgrepCommand",
                ToolArguments = "rg foo",
                ToolResult = "2 matches",
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 10, 9, 0, TimeSpan.Zero),
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
        services.AddScoped<IProjectAgentStatusSnapshotService, ProjectAgentStatusSnapshotService>();
        return services.BuildServiceProvider();
    }
}
