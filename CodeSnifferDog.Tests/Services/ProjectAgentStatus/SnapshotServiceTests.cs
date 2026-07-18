using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots.Queries;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSnifferDog.Tests.Services.ProjectAgentStatus;

[TestClass]
public sealed class SnapshotServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task GetSnapshotAsync_WhenProjectDoesNotExist_ReturnsNull()
    {
        using ServiceProvider services = CreateServices();
        ISnapshotService service = services.GetRequiredService<ISnapshotService>();

        StatusSnapshotDto? snapshot = await service.GetSnapshotAsync(
            Guid.CreateVersion7(),
            selectedAgentId: null,
            TestContext.CancellationToken);

        Assert.IsNull(snapshot);
    }

    [TestMethod]
    public async Task GetSnapshotAsync_MapsProjectTreeAndSortsByConfiguredRules()
    {
        Guid projectId = Guid.CreateVersion7();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        ISnapshotService service = services.GetRequiredService<ISnapshotService>();

        await SeedProjectAsync(dbContextFactory, projectId, TestContext.CancellationToken);

        StatusSnapshotDto? snapshot = await service.GetSnapshotAsync(
            projectId,
            selectedAgentId: null,
            TestContext.CancellationToken);

        Assert.IsNotNull(snapshot);
        Assert.AreEqual(projectId, snapshot.ProjectId);
        Assert.AreEqual(ProjectStatus.Reviewing, snapshot.ProjectStatus);
        Assert.IsTrue(snapshot.SnapshotGeneratedAtUtc <= DateTimeOffset.UtcNow);

        Assert.HasCount(2, snapshot.AgentGroups);
        Assert.AreEqual("Alpha Group", snapshot.AgentGroups[0].DisplayName);
        Assert.AreEqual("Zulu Group", snapshot.AgentGroups[1].DisplayName);

        GroupSnapshotDto firstGroup = snapshot.AgentGroups[0];
        Assert.HasCount(2, firstGroup.Agents);
        Assert.AreEqual("Alpha Agent", firstGroup.Agents[0].DisplayName);
        Assert.AreEqual("Beta Agent", firstGroup.Agents[1].DisplayName);
        Assert.AreEqual(RunStatus.Completed, firstGroup.Agents[0].Status);
        Assert.AreEqual(RunStatus.Running, firstGroup.Agents[1].Status);
        Assert.IsTrue(firstGroup.Agents[0].HasLoadedHistory);
        Assert.IsFalse(firstGroup.Agents[1].HasLoadedHistory);

        IReadOnlyList<TimelineEntryDto> timeline = firstGroup.Agents[0].TimelineEntries;
        Assert.HasCount(3, timeline);
        Assert.AreEqual(1L, timeline[0].Sequence);
        Assert.AreEqual(TimelineEntryKind.Output, timeline[0].EntryKind);
        Assert.AreEqual(2L, timeline[1].Sequence);
        Assert.AreEqual(TimelineEntryKind.Tool, timeline[1].EntryKind);
        Assert.AreEqual("tool-call-1", timeline[1].ToolCallId);
        Assert.AreEqual("RunRipgrepCommand", timeline[1].ToolName);
        Assert.AreEqual("rg foo", timeline[1].ToolArguments);
        Assert.AreEqual("2 matches", timeline[1].ToolResult);
        Assert.AreEqual(3L, timeline[2].Sequence);
        Assert.AreEqual(TimelineEntryKind.Compaction, timeline[2].EntryKind);
        Assert.AreEqual("Context automatically compacted", timeline[2].Message);
        Assert.IsEmpty(firstGroup.Agents[1].TimelineEntries);
    }

    [TestMethod]
    public async Task GetAgentHistoryAsync_ReturnsFullTimelineForRequestedAgent()
    {
        Guid projectId = Guid.CreateVersion7();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        ISnapshotService service = services.GetRequiredService<ISnapshotService>();

        await SeedProjectAsync(dbContextFactory, projectId, TestContext.CancellationToken);

        Guid alphaAgentId;
        await using (CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken))
        {
            alphaAgentId = await dbContext.ProjectAgents
                .Where(agent => agent.Group!.DisplayName == "Alpha Group" && agent.DisplayName == "Alpha Agent")
                .Select(agent => agent.Id)
                .SingleAsync(TestContext.CancellationToken);
        }

        HistorySnapshotDto? history = await service.GetAgentHistoryAsync(projectId, alphaAgentId, TestContext.CancellationToken);

        Assert.IsNotNull(history);
        Assert.AreEqual(projectId, history.ProjectId);
        Assert.AreEqual(alphaAgentId, history.AgentId);
        Assert.HasCount(3, history.TimelineEntries);
        Assert.AreEqual(1L, history.TimelineEntries[0].Sequence);
        Assert.AreEqual(3L, history.TimelineEntries[2].Sequence);
    }

    [TestMethod]
    public async Task GetSnapshotAsync_UnsupportedAgentStatusThrowsSnapshotCompatibleException()
    {
        Guid projectId = Guid.CreateVersion7();
        using ServiceProvider services = CreateServices();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory =
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        ISnapshotService service = services.GetRequiredService<ISnapshotService>();
        await SeedProjectAsync(dbContextFactory, projectId, TestContext.CancellationToken);

        await using (CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken))
        {
            ProjectAgentRecord agent = await dbContext.ProjectAgents.FirstAsync(TestContext.CancellationToken);
            agent.Status = (CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus)999;
            await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        }

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.GetSnapshotAsync(projectId, selectedAgentId: null, TestContext.CancellationToken));

        Assert.AreEqual("Unsupported agent status '999'.", exception.Message);
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
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            RuntimeKey = "group-zulu",
            DisplayName = "Zulu Group",
            CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 10, 5, 0, TimeSpan.Zero),
        };

        ProjectAgentGroupRecord alphaGroup = new()
        {
            Id = Guid.CreateVersion7(),
            ProjectId = projectId,
            RuntimeKey = "group-alpha",
            DisplayName = "Alpha Group",
            CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 10, 1, 0, TimeSpan.Zero),
        };

        ProjectAgentRecord betaAgent = new()
        {
            Id = Guid.CreateVersion7(),
            ProjectAgentGroupId = alphaGroup.Id,
            RuntimeKey = "agent-beta",
            DisplayName = "Beta Agent",
            SystemPrompt = "System prompt beta",
            Status = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus.Running,
            CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 10, 3, 0, TimeSpan.Zero),
        };

        ProjectAgentRecord alphaAgent = new()
        {
            Id = Guid.CreateVersion7(),
            ProjectAgentGroupId = alphaGroup.Id,
            RuntimeKey = "agent-alpha",
            DisplayName = "Alpha Agent",
            SystemPrompt = "System prompt alpha",
            Status = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus.Completed,
            CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 10, 2, 0, TimeSpan.Zero),
        };

        ProjectAgentRecord zuluAgent = new()
        {
            Id = Guid.CreateVersion7(),
            ProjectAgentGroupId = zuluGroup.Id,
            RuntimeKey = "agent-zulu",
            DisplayName = "Zulu Agent",
            SystemPrompt = "System prompt zulu",
            Status = CodeSnifferDog.Server.Data.Entities.ProjectAgentStatus.Waiting,
            CreatedAtUtc = new DateTimeOffset(2026, 5, 10, 10, 6, 0, TimeSpan.Zero),
        };

        dbContext.Projects.Add(project);
        dbContext.ProjectAgentGroups.AddRange(zuluGroup, alphaGroup);
        dbContext.ProjectAgents.AddRange(betaAgent, alphaAgent, zuluAgent);
        dbContext.ProjectAgentTimelineEntries.AddRange(
            new ProjectAgentTimelineEntryRecord
            {
                Id = Guid.CreateVersion7(),
                ProjectAgentId = alphaAgent.Id,
                Sequence = 3,
                EntryType = ProjectAgentTimelineEntryType.Compaction,
                Message = "Context automatically compacted",
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 10, 10, 0, TimeSpan.Zero),
            },
            new ProjectAgentTimelineEntryRecord
            {
                Id = Guid.CreateVersion7(),
                ProjectAgentId = alphaAgent.Id,
                Sequence = 1,
                EntryType = ProjectAgentTimelineEntryType.Output,
                Message = "Started review",
                OccurredAtUtc = new DateTimeOffset(2026, 5, 10, 10, 8, 0, TimeSpan.Zero),
            },
            new ProjectAgentTimelineEntryRecord
            {
                Id = Guid.CreateVersion7(),
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
        string databaseName = Guid.CreateVersion7().ToString("N");
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddSingleton<IProjectStatusMapper, ProjectStatusMapper>();
        services.AddScoped<IProjectionMapper, ProjectionMapper>();
        services.AddScoped<ISnapshotQueryService, SnapshotQueryService>();
        services.AddScoped<ISnapshotService, SnapshotService>();
        return services.BuildServiceProvider();
    }
}
