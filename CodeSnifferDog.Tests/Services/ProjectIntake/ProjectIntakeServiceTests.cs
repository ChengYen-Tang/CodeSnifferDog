using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectIntake.Deletion;
using CodeSnifferDog.Server.Services.ProjectIntake.Queue;
using CodeSnifferDog.Server.Services.ProjectIntake.Upload;
using CodeSnifferDog.Server.Services.ProjectIntake;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.Projects.Projection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSnifferDog.Tests.Services.ProjectIntake;

[TestClass]
public sealed class ProjectIntakeServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task UploadAsync_StoresUploadQueuesProjectAndPublishesChanges()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        TrackingProjectChangePublisher projectChangePublisher = new();
        StubProjectUploadService uploadService = new();
        StubProjectQueueService queueService = new();
        TrackingProjectExecutionQueueLock queueLock = new();
        ProjectIntakeService service = CreateService(
            dbContextFactory,
            projectChangePublisher,
            uploadService,
            queueService,
            queueLock: queueLock);

        ProjectUploadResult result = await service.UploadAsync(
            CreateFormFile("repo.zip", "content"),
            TestContext.CancellationToken);

        Assert.AreEqual(queueService.Result.ProjectId, result.ProjectId);
        Assert.IsNotNull(queueService.Request);
        Assert.AreEqual(uploadService.Artifact.OriginalFileName, queueService.Request.OriginalFileName);
        Assert.AreEqual(uploadService.Artifact.FileSizeBytes, queueService.Request.FileSizeBytes);
        Assert.AreEqual(uploadService.Artifact.StoredZipRelativePath, queueService.Request.StoredZipRelativePath);
        Assert.AreEqual(1, queueLock.AcquireCallCount);
        Assert.AreEqual(1, projectChangePublisher.PublishCallCount);
        Assert.AreEqual(0, uploadService.DeleteCallCount);
    }

    [TestMethod]
    public async Task UploadAsync_WhenQueueFails_DeletesStoredUploadAndDoesNotPublish()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        TrackingProjectChangePublisher projectChangePublisher = new();
        StubProjectUploadService uploadService = new();
        StubProjectQueueService queueService = new(new InvalidOperationException("queue failed"));
        ProjectIntakeService service = CreateService(
            dbContextFactory,
            projectChangePublisher,
            uploadService,
            queueService);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.UploadAsync(CreateFormFile("repo.zip", "content"), TestContext.CancellationToken));

        Assert.AreEqual("queue failed", exception.Message);
        Assert.AreEqual(1, uploadService.DeleteCallCount);
        Assert.AreEqual(uploadService.Artifact, uploadService.DeletedArtifact);
        Assert.AreEqual(0, projectChangePublisher.PublishCallCount);
    }

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
        ProjectIntakeService service = CreateService(
            dbContextFactory,
            projectChangePublisher,
            executionLeaseRegistry: new StubProjectExecutionLeaseRegistry(projectId));

        bool canceled = await service.CancelAsync(projectId, TestContext.CancellationToken);

        Assert.IsTrue(canceled);
        Assert.AreEqual(0, projectChangePublisher.PublishCallCount);
    }

    [TestMethod]
    public async Task DeleteAsync_WhenDeleted_PublishesProjectChanges()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        TrackingProjectChangePublisher projectChangePublisher = new();
        StubProjectDeletionService deletionService = new(deleted: true);
        TrackingProjectExecutionQueueLock queueLock = new();
        ProjectIntakeService service = CreateService(
            dbContextFactory,
            projectChangePublisher,
            deletionService: deletionService,
            queueLock: queueLock);
        Guid projectId = Guid.NewGuid();

        bool deleted = await service.DeleteAsync(projectId, TestContext.CancellationToken);

        Assert.IsTrue(deleted);
        Assert.AreEqual(projectId, deletionService.ProjectId);
        Assert.AreEqual(1, queueLock.AcquireCallCount);
        Assert.AreEqual(1, projectChangePublisher.PublishCallCount);
    }

    [TestMethod]
    public async Task DeleteAsync_WhenProjectIsMissing_DoesNotPublishProjectChanges()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        TrackingProjectChangePublisher projectChangePublisher = new();
        StubProjectDeletionService deletionService = new(deleted: false);
        ProjectIntakeService service = CreateService(
            dbContextFactory,
            projectChangePublisher,
            deletionService: deletionService);

        bool deleted = await service.DeleteAsync(Guid.NewGuid(), TestContext.CancellationToken);

        Assert.IsFalse(deleted);
        Assert.AreEqual(0, projectChangePublisher.PublishCallCount);
    }

    private static ProjectIntakeService CreateService(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        IProjectChangePublisher projectChangePublisher,
        IProjectUploadService? uploadService = null,
        IProjectQueueService? queueService = null,
        IProjectDeletionService? deletionService = null,
        IProjectExecutionLeaseRegistry? executionLeaseRegistry = null,
        IProjectExecutionQueueLock? queueLock = null) =>
        new(
            dbContextFactory,
            projectChangePublisher,
            uploadService ?? new StubProjectUploadService(),
            queueService ?? new StubProjectQueueService(),
            deletionService ?? new StubProjectDeletionService(),
            new ProjectProjectionMapper(new ProjectStatusMapper()),
            executionLeaseRegistry ?? new StubProjectExecutionLeaseRegistry(Guid.Empty),
            queueLock ?? new TrackingProjectExecutionQueueLock(),
            NullLogger<ProjectIntakeService>.Instance);

    private static FormFile CreateFormFile(string fileName, string content)
    {
        MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "file", fileName);
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

    private sealed class TrackingProjectExecutionQueueLock : IProjectExecutionQueueLock
    {
        public int AcquireCallCount { get; private set; }

        public Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IDisposable>(Acquire());

        private IDisposable Acquire()
        {
            AcquireCallCount++;
            return new NoopDisposable();
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class StubProjectUploadService : IProjectUploadService
    {
        public ProjectUploadArtifact Artifact { get; } = new("repo.zip", 123, "stored.zip", "uploads/stored.zip");

        public int DeleteCallCount { get; private set; }

        public ProjectUploadArtifact? DeletedArtifact { get; private set; }

        public Task<ProjectUploadArtifact> StoreAsync(Guid projectId, IFormFile zipFile, CancellationToken cancellationToken) =>
            Task.FromResult(Artifact);

        public void TryDeleteStoredFile(ProjectUploadArtifact artifact)
        {
            DeleteCallCount++;
            DeletedArtifact = artifact;
        }
    }

    private sealed class StubProjectQueueService(Exception? exception = null) : IProjectQueueService
    {
        public ProjectUploadResult Result { get; } = new()
        {
            ProjectId = Guid.NewGuid(),
            OriginalFileName = "repo.zip",
            Status = CodeSnifferDog.Server.Shared.Projects.ProjectStatus.Queued,
            FileSizeBytes = 123,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            QueueTimestampUtc = DateTimeOffset.UtcNow,
        };

        public ProjectQueueRequest? Request { get; private set; }

        public Task<ProjectUploadResult> QueueAsync(ProjectQueueRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return exception is null
                ? Task.FromResult(Result)
                : Task.FromException<ProjectUploadResult>(exception);
        }
    }

    private sealed class StubProjectDeletionService(bool deleted = false) : IProjectDeletionService
    {
        public Guid? ProjectId { get; private set; }

        public Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(Delete(projectId));

        private bool Delete(Guid projectId)
        {
            ProjectId = projectId;
            return deleted;
        }
    }
}
