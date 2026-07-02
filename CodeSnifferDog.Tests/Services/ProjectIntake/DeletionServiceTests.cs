using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectIntake.Deletion;
using CodeSnifferDog.Server.Services.ProjectStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CodeSnifferDog.Tests.Services.ProjectIntake;

[TestClass]
public sealed class DeletionServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task DeleteAsync_RemovesProjectAndArtifacts()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        ProjectTemporaryStoragePaths storagePaths = new();
        Guid projectId = Guid.NewGuid();
        string uploadedZipPath = storagePaths.ResolveUploadedZipPath(projectId);
        string extractedProjectPath = storagePaths.ResolveExtractedProjectPath(projectId);
        storagePaths.EnsureStorageDirectories();
        await File.WriteAllTextAsync(uploadedZipPath, "zip", TestContext.CancellationToken);
        Directory.CreateDirectory(extractedProjectPath);
        await File.WriteAllTextAsync(Path.Combine(extractedProjectPath, "file.txt"), "content", TestContext.CancellationToken);
        await SeedProjectAsync(
            dbContextFactory,
            projectId,
            storagePaths.ResolveUploadedZipRelativePath(projectId),
            ProjectProcessingStatus.Completed);
        DeletionService service = new(dbContextFactory, storagePaths);

        bool deleted = await service.DeleteAsync(projectId, TestContext.CancellationToken);

        Assert.IsTrue(deleted);
        Assert.IsFalse(File.Exists(uploadedZipPath));
        Assert.IsFalse(Directory.Exists(extractedProjectPath));
        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        Assert.AreEqual(0, await dbContext.Projects.CountAsync(TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task DeleteAsync_WhenProjectIsMissing_ReturnsFalse()
    {
        DeletionService service = new(CreateDbContextFactory(), new ProjectTemporaryStoragePaths());

        bool deleted = await service.DeleteAsync(Guid.NewGuid(), TestContext.CancellationToken);

        Assert.IsFalse(deleted);
    }

    [TestMethod]
    public async Task DeleteAsync_WhenProjectIsReviewing_ThrowsOriginalException()
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = CreateDbContextFactory();
        Guid projectId = Guid.NewGuid();
        await SeedProjectAsync(dbContextFactory, projectId, "uploads/reviewing.zip", ProjectProcessingStatus.Reviewing);
        DeletionService service = new(dbContextFactory, new ProjectTemporaryStoragePaths());

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.DeleteAsync(projectId, TestContext.CancellationToken));

        Assert.AreEqual("Reviewing projects must be canceled before deletion.", exception.Message);
    }

    private async Task SeedProjectAsync(
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
        Guid projectId,
        string storedZipRelativePath,
        ProjectProcessingStatus status)
    {
        await using CodeSnifferDogServerDbContext dbContext =
            await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        dbContext.Projects.Add(new ProjectRecord
        {
            Id = projectId,
            OriginalFileName = "repo.zip",
            StoredZipRelativePath = storedZipRelativePath,
            Status = status,
            FileSizeBytes = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            QueueTimestampUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(TestContext.CancellationToken);
    }

    private static IDbContextFactory<CodeSnifferDogServerDbContext> CreateDbContextFactory()
    {
        DbContextOptions<CodeSnifferDogServerDbContext> options = new DbContextOptionsBuilder<CodeSnifferDogServerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new PooledDbContextFactory<CodeSnifferDogServerDbContext>(options);
    }
}
