using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.ProjectStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CodeSnifferDog.Tests.Services.ProjectIntake;

[TestClass]
public sealed class ProjectIntakeServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task CancelAsync_DoesNotPublishProjectChanges_BeforeExecutionCompletesCancellation()
    {
        Guid projectId = Guid.NewGuid();
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        dbContext.Projects.Add(new ProjectRecord
        {
            Id = projectId,
            OriginalFileName = "repo.zip",
            StoredZipRelativePath = "uploads/repo.zip",
            Status = ProjectProcessingStatus.Reviewing,
            FileSizeBytes = 10,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            QueueTimestampUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);

        TrackingProjectChangePublisher projectChangePublisher = new();
        ProjectIntakeService service = new(
            dbContextFactory,
            projectChangePublisher,
            new ProjectTemporaryStoragePaths(),
            new StubProjectExecutionLeaseRegistry(projectId),
            new ImmediateProjectExecutionQueueLock(),
            Options.Create(new ProjectExecutionOptions()),
            NullLogger<ProjectIntakeService>.Instance);

        bool canceled = await service.CancelAsync(projectId, TestContext.CancellationToken);

        Assert.IsTrue(canceled);
        Assert.AreEqual(0, projectChangePublisher.PublishCallCount);
    }

    private static IDbContextFactory<CodeSnifferDogServerDbContext> CreateDbContextFactory()
    {
        DbContextOptions<CodeSnifferDogServerDbContext> options = new DbContextOptionsBuilder<CodeSnifferDogServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PooledDbContextFactory<CodeSnifferDogServerDbContext>(options);
    }

    private sealed class TrackingProjectChangePublisher : IProjectChangePublisher
    {
        public int PublishCallCount { get; private set; }

        public Task PublishProjectsChangedAsync(CancellationToken cancellationToken = default)
        {
            PublishCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubProjectExecutionLeaseRegistry(Guid expectedProjectId) : IProjectExecutionLeaseRegistry
    {
        public ProjectExecutionLease Register(Guid projectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> CancelAndWaitAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public bool TryCancel(Guid projectId, out Task? completion)
        {
            completion = Task.CompletedTask;
            return projectId == expectedProjectId;
        }
    }

    private sealed class ImmediateProjectExecutionQueueLock : IProjectExecutionQueueLock
    {
        public Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IDisposable>(new NoopDisposable());
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
