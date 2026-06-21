using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CodeSnifferDog.Tests.Services.Projects;

[TestClass]
public sealed class ProjectSidebarSnapshotServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task GetSnapshotAsync_ReturnsFixedGroupOrderAndSortedProjects()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        await SeedProjectsAsync(dbContextFactory);

        ProjectSidebarSnapshotService service = new(dbContextFactory, CreateMapper());

        ProjectSidebarSnapshotDto snapshot = await service.GetSnapshotAsync(
            Guid.Parse("70000000-0000-0000-0000-000000000304"),
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { "reviewing", "completed", "queued", "failed", "canceled" },
            snapshot.Groups.Select(group => group.GroupKey).ToList());
        CollectionAssert.AreEqual(
            new[] { "Reviewing", "Completed", "Queued", "Failed", "Canceled" },
            snapshot.Groups.Select(group => group.DisplayName).ToList());
        CollectionAssert.AreEqual(
            new[]
            {
                ProjectStatus.Reviewing,
                ProjectStatus.Completed,
                ProjectStatus.Queued,
                ProjectStatus.Failed,
                ProjectStatus.Canceled,
            },
            snapshot.Groups.Select(group => group.Status).ToList());

        Assert.AreEqual(Guid.Parse("70000000-0000-0000-0000-000000000304"), snapshot.SelectedProjectId);

        CollectionAssert.AreEqual(
            new[] { "review-early.zip", "review-late.zip" },
            snapshot.Groups[0].Projects.Select(project => project.OriginalFileName).ToList());
        CollectionAssert.AreEqual(
            new[] { ProjectStatus.Reviewing, ProjectStatus.Reviewing },
            snapshot.Groups[0].Projects.Select(project => project.Status).ToList());

        CollectionAssert.AreEqual(
            new[] { "queued-a.zip", "queued-b.zip" },
            snapshot.Groups[2].Projects.Select(project => project.OriginalFileName).ToList());
        CollectionAssert.AreEqual(
            new[] { ProjectStatus.Queued, ProjectStatus.Queued },
            snapshot.Groups[2].Projects.Select(project => project.Status).ToList());

        StringAssert.Contains(
            string.Join(",", snapshot.Groups[1].Projects.Select(project => project.OriginalFileName)),
            "complete-a.zip");
        Assert.AreEqual(ProjectStatus.Completed, snapshot.Groups[1].Projects[0].Status);
        StringAssert.Contains(
            string.Join(",", snapshot.Groups[3].Projects.Select(project => project.OriginalFileName)),
            "failed-a.zip");
        Assert.AreEqual(ProjectStatus.Failed, snapshot.Groups[3].Projects[0].Status);
        StringAssert.Contains(
            string.Join(",", snapshot.Groups[4].Projects.Select(project => project.OriginalFileName)),
            "canceled-a.zip");
        Assert.AreEqual(ProjectStatus.Canceled, snapshot.Groups[4].Projects[0].Status);

        Assert.AreEqual(0, snapshot.Groups[0].Projects[0].SortOrder);
        Assert.AreEqual(1, snapshot.Groups[0].Projects[1].SortOrder);
    }

    [TestMethod]
    public async Task GetSnapshotAsync_WhenSelectedProjectIsNull_ResolvesFirstProjectOnServer()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        await SeedProjectsAsync(dbContextFactory);

        ProjectSidebarSnapshotService service = new(dbContextFactory, CreateMapper());

        ProjectSidebarSnapshotDto snapshot = await service.GetSnapshotAsync(null, TestContext.CancellationToken);

        Assert.AreEqual(Guid.Parse("70000000-0000-0000-0000-000000000301"), snapshot.SelectedProjectId);
    }

    [TestMethod]
    public async Task GetSnapshotAsync_WhenSelectedProjectDoesNotExist_FallsBackToFirstProjectOnServer()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        await SeedProjectsAsync(dbContextFactory);

        ProjectSidebarSnapshotService service = new(dbContextFactory, CreateMapper());

        ProjectSidebarSnapshotDto snapshot = await service.GetSnapshotAsync(
            Guid.Parse("79999999-0000-0000-0000-000000000399"),
            TestContext.CancellationToken);

        Assert.AreEqual(Guid.Parse("70000000-0000-0000-0000-000000000301"), snapshot.SelectedProjectId);
    }

    [TestMethod]
    public async Task GetSnapshotAsync_UsesProjectionMapperForSidebarProjects()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.NewGuid();
        await using (CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken))
        {
            dbContext.Projects.Add(CreateProject(
                projectId,
                "reviewing.zip",
                ProjectProcessingStatus.Reviewing,
                createdAtUtc: new DateTimeOffset(2026, 5, 15, 8, 0, 0, TimeSpan.Zero),
                queueTimestampUtc: new DateTimeOffset(2026, 5, 15, 8, 10, 0, TimeSpan.Zero)));
            await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        }

        TrackingProjectProjectionMapper mapper = new();
        ProjectSidebarSnapshotService service = new(dbContextFactory, mapper);

        ProjectSidebarSnapshotDto snapshot = await service.GetSnapshotAsync(null, TestContext.CancellationToken);

        Assert.AreEqual(1, mapper.MapStatusCallCount);
        Assert.AreEqual(1, mapper.MapSidebarProjectCallCount);
        Assert.AreEqual(projectId, snapshot.Groups.Single(group => group.GroupKey == "reviewing").Projects.Single().ProjectId);
    }

    private static IDbContextFactory<CodeSnifferDogServerDbContext> CreateDbContextFactory()
    {
        DbContextOptions<CodeSnifferDogServerDbContext> options = new DbContextOptionsBuilder<CodeSnifferDogServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PooledDbContextFactory<CodeSnifferDogServerDbContext>(options);
    }

    private async Task SeedProjectsAsync(IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory)
    {
        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);

        dbContext.Projects.AddRange(
            CreateProject(
                Guid.Parse("70000000-0000-0000-0000-000000000301"),
                "review-early.zip",
                ProjectProcessingStatus.Reviewing,
                createdAtUtc: new DateTimeOffset(2026, 5, 15, 8, 0, 0, TimeSpan.Zero),
                queueTimestampUtc: new DateTimeOffset(2026, 5, 15, 8, 10, 0, TimeSpan.Zero)),
            CreateProject(
                Guid.Parse("70000000-0000-0000-0000-000000000302"),
                "review-late.zip",
                ProjectProcessingStatus.Reviewing,
                createdAtUtc: new DateTimeOffset(2026, 5, 15, 8, 5, 0, TimeSpan.Zero),
                queueTimestampUtc: new DateTimeOffset(2026, 5, 15, 8, 20, 0, TimeSpan.Zero)),
            CreateProject(
                Guid.Parse("70000000-0000-0000-0000-000000000303"),
                "complete-a.zip",
                ProjectProcessingStatus.Completed,
                createdAtUtc: new DateTimeOffset(2026, 5, 15, 7, 0, 0, TimeSpan.Zero),
                queueTimestampUtc: new DateTimeOffset(2026, 5, 15, 7, 5, 0, TimeSpan.Zero),
                updatedAtUtc: new DateTimeOffset(2026, 5, 15, 9, 0, 0, TimeSpan.Zero),
                finishedAtUtc: new DateTimeOffset(2026, 5, 15, 9, 0, 0, TimeSpan.Zero)),
            CreateProject(
                Guid.Parse("70000000-0000-0000-0000-000000000304"),
                "queued-a.zip",
                ProjectProcessingStatus.Queued,
                createdAtUtc: new DateTimeOffset(2026, 5, 15, 6, 0, 0, TimeSpan.Zero),
                queueTimestampUtc: new DateTimeOffset(2026, 5, 15, 6, 15, 0, TimeSpan.Zero)),
            CreateProject(
                Guid.Parse("70000000-0000-0000-0000-000000000305"),
                "queued-b.zip",
                ProjectProcessingStatus.Queued,
                createdAtUtc: new DateTimeOffset(2026, 5, 15, 6, 5, 0, TimeSpan.Zero),
                queueTimestampUtc: new DateTimeOffset(2026, 5, 15, 6, 30, 0, TimeSpan.Zero)),
            CreateProject(
                Guid.Parse("70000000-0000-0000-0000-000000000306"),
                "failed-a.zip",
                ProjectProcessingStatus.Failed,
                createdAtUtc: new DateTimeOffset(2026, 5, 15, 5, 0, 0, TimeSpan.Zero),
                queueTimestampUtc: new DateTimeOffset(2026, 5, 15, 5, 10, 0, TimeSpan.Zero),
                updatedAtUtc: new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero),
                finishedAtUtc: new DateTimeOffset(2026, 5, 15, 10, 0, 0, TimeSpan.Zero)),
            CreateProject(
                Guid.Parse("70000000-0000-0000-0000-000000000307"),
                "canceled-a.zip",
                ProjectProcessingStatus.Canceled,
                createdAtUtc: new DateTimeOffset(2026, 5, 15, 4, 0, 0, TimeSpan.Zero),
                queueTimestampUtc: new DateTimeOffset(2026, 5, 15, 4, 10, 0, TimeSpan.Zero),
                updatedAtUtc: new DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero),
                finishedAtUtc: new DateTimeOffset(2026, 5, 15, 11, 0, 0, TimeSpan.Zero)));

        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
    }

    private static ProjectRecord CreateProject(
        Guid id,
        string originalFileName,
        ProjectProcessingStatus status,
        DateTimeOffset createdAtUtc,
        DateTimeOffset queueTimestampUtc,
        DateTimeOffset? updatedAtUtc = null,
        DateTimeOffset? finishedAtUtc = null) => new()
        {
            Id = id,
            OriginalFileName = originalFileName,
            StoredZipRelativePath = $"uploads/{id:N}.zip",
            Status = status,
            FileSizeBytes = 10,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc ?? createdAtUtc,
            QueueTimestampUtc = queueTimestampUtc,
            FinishedAtUtc = finishedAtUtc,
        };

    private sealed class TrackingProjectProjectionMapper : IProjectProjectionMapper
    {
        private readonly ProjectProjectionMapper _inner = CreateMapper();

        public int MapStatusCallCount { get; private set; }

        public int MapSidebarProjectCallCount { get; private set; }

        public ProjectStatus MapStatus(ProjectProcessingStatus status)
        {
            MapStatusCallCount++;
            return _inner.MapStatus(status);
        }

        public ProjectSummaryDto MapSummary(ProjectSummaryProjection project) => _inner.MapSummary(project);

        public ProjectListItemDto MapListItem(ProjectListItemProjection project) => _inner.MapListItem(project);

        public ProjectSidebarProjectDto MapSidebarProject(
            ProjectSidebarProjectProjection project,
            ProjectStatus status,
            int sortOrder)
        {
            MapSidebarProjectCallCount++;
            return _inner.MapSidebarProject(project, status, sortOrder);
        }
    }

    private static ProjectProjectionMapper CreateMapper() => new(new ProjectStatusMapper());
}
