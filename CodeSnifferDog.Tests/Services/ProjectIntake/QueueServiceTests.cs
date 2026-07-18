using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.ProjectIntake.Queue;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;

namespace CodeSnifferDog.Tests.Services.ProjectIntake;

[TestClass]
public sealed class QueueServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task QueueAsync_CreatesQueuedProjectAndReturnsUploadResult()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        QueueService service = CreateService(dbContextFactory);
        Guid projectId = Guid.CreateVersion7();
        DateTimeOffset nowUtc = new(2026, 5, 15, 8, 0, 0, TimeSpan.Zero);

        ProjectUploadResult result = await service.QueueAsync(
            new Request(projectId, @"C:\upload\repo.zip", 123, @"uploads\repo.zip", nowUtc),
            TestContext.CancellationToken);

        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        ProjectRecord project = await dbContext.Projects.SingleAsync(TestContext.CancellationToken);
        Assert.AreEqual(projectId, project.Id);
        Assert.AreEqual("repo.zip", project.OriginalFileName);
        Assert.AreEqual("uploads/repo.zip", project.StoredZipRelativePath);
        Assert.AreEqual(ProjectProcessingStatus.Queued, project.Status);
        Assert.AreEqual(123, project.FileSizeBytes);
        Assert.AreEqual(nowUtc, project.CreatedAtUtc);
        Assert.AreEqual(nowUtc, project.UpdatedAtUtc);
        Assert.AreEqual(nowUtc, project.QueueTimestampUtc);

        Assert.AreEqual(projectId, result.ProjectId);
        Assert.AreEqual("repo.zip", result.OriginalFileName);
        Assert.AreEqual(ProjectStatus.Queued, result.Status);
        Assert.AreEqual(123, result.FileSizeBytes);
        Assert.AreEqual(nowUtc, result.CreatedAtUtc);
        Assert.AreEqual(nowUtc, result.QueueTimestampUtc);
    }

    [TestMethod]
    public async Task QueueAsync_WhenQueueIsFull_ThrowsOriginalException()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        await using (CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken))
        {
            dbContext.Projects.Add(CreateQueuedProject());
            await dbContext.SaveChangesAsync(TestContext.CancellationToken);
        }

        QueueService service = CreateService(dbContextFactory, maxQueuedProjects: 1);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.QueueAsync(
                new Request(Guid.CreateVersion7(), "repo.zip", 123, "uploads/repo.zip", DateTimeOffset.UtcNow),
                TestContext.CancellationToken));

        Assert.AreEqual("The project queue is full.", exception.Message);
    }

    private static QueueService CreateService(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        int maxQueuedProjects = 100) =>
        new(
            dbContextFactory,
            Options.Create(new Settings { MaxQueuedProjects = maxQueuedProjects }),
            new ProjectProjectionMapper(new ProjectStatusMapper()));

    private static IDbContextFactory<CodeSnifferDogServerDbContext> CreateDbContextFactory()
    {
        DbContextOptions<CodeSnifferDogServerDbContext> options = new DbContextOptionsBuilder<CodeSnifferDogServerDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString("N"))
            .Options;

        return new PooledDbContextFactory<CodeSnifferDogServerDbContext>(options);
    }

    private static ProjectRecord CreateQueuedProject() => new()
    {
        Id = Guid.CreateVersion7(),
        OriginalFileName = "queued.zip",
        StoredZipRelativePath = "uploads/queued.zip",
        Status = ProjectProcessingStatus.Queued,
        FileSizeBytes = 1,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
        QueueTimestampUtc = DateTimeOffset.UtcNow,
    };
}
