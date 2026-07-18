using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Projection;
using CodeSnifferDog.Server.Services.Projects.Sidebar.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CodeSnifferDog.Tests.Services.Projects;

[TestClass]
public sealed class QueryServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task GetSnapshotAsync_ReturnsFixedGroupsSortedProjectsAndSelectedFallback()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid queuedEarlyId = Guid.Parse("90000000-0000-0000-0000-000000000001");
        Guid queuedLateId = Guid.Parse("90000000-0000-0000-0000-000000000002");
        await SeedProjectAsync(
            dbContextFactory,
            queuedLateId,
            "queued-late.zip",
            ProjectProcessingStatus.Queued,
            new DateTimeOffset(2026, 5, 15, 8, 10, 0, TimeSpan.Zero));
        await SeedProjectAsync(
            dbContextFactory,
            queuedEarlyId,
            "queued-early.zip",
            ProjectProcessingStatus.Queued,
            new DateTimeOffset(2026, 5, 15, 8, 0, 0, TimeSpan.Zero));
        QueryService service = new(dbContextFactory, new ProjectStatusMapper());

        SnapshotReadModel snapshot = await service.GetSnapshotAsync(
            selectedProjectId: Guid.CreateVersion7(),
            TestContext.CancellationToken);

        CollectionAssert.AreEqual(
            new[] { "reviewing", "completed", "queued", "failed", "canceled" },
            snapshot.Groups.Select(group => group.GroupKey).ToList());
        Assert.AreEqual(queuedEarlyId, snapshot.SelectedProjectId);
        CollectionAssert.AreEqual(
            new[] { "queued-early.zip", "queued-late.zip" },
            snapshot.Groups.Single(group => group.GroupKey == "queued")
                .Projects
                .Select(project => project.Project.OriginalFileName)
                .ToList());
    }

    [TestMethod]
    public async Task GetSnapshotAsync_WhenNoProjects_ReturnsEmptyGroupsAndNullSelectedProject()
    {
        QueryService service = new(CreateDbContextFactory(), new ProjectStatusMapper());

        SnapshotReadModel snapshot = await service.GetSnapshotAsync(null, TestContext.CancellationToken);

        Assert.IsNull(snapshot.SelectedProjectId);
        Assert.IsTrue(snapshot.Groups.All(group => group.Projects.Count == 0));
    }

    private static IDbContextFactory<CodeSnifferDogServerDbContext> CreateDbContextFactory()
    {
        DbContextOptions<CodeSnifferDogServerDbContext> options = new DbContextOptionsBuilder<CodeSnifferDogServerDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString("N"))
            .Options;

        return new PooledDbContextFactory<CodeSnifferDogServerDbContext>(options);
    }

    private async Task SeedProjectAsync(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        Guid projectId,
        string originalFileName,
        ProjectProcessingStatus status,
        DateTimeOffset queueTimestampUtc)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        dbContext.Projects.Add(new ProjectRecord
        {
            Id = projectId,
            OriginalFileName = originalFileName,
            StoredZipRelativePath = $"uploads/{projectId:N}.zip",
            Status = status,
            FileSizeBytes = 10,
            CreatedAtUtc = queueTimestampUtc,
            UpdatedAtUtc = queueTimestampUtc,
            QueueTimestampUtc = queueTimestampUtc,
        });
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
    }
}
