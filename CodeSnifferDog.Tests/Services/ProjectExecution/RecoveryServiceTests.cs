using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Artifacts;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Recovery;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.ProjectStorage;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class RecoveryServiceTests
{
    [TestMethod]
    public async Task RecoverAsync_WhenReviewingProjectHasExtractedRepository_RequeuesProject()
    {
        TestProjectChangePublisher projectChangePublisher = new();
        ProjectTemporaryStoragePaths storagePaths = CreateStoragePaths();
        using ServiceProvider services = CreateServices(projectChangePublisher);
        Guid projectId = await SeedReviewingProjectAsync(services);
        Directory.CreateDirectory(storagePaths.ResolveExtractedProjectPath(projectId));
        Service service = CreateService(services, storagePaths);

        await service.RecoverAsync(CancellationToken.None);

        ProjectRecord project = await LoadProjectAsync(services, projectId);
        Assert.AreEqual(ProjectProcessingStatus.Queued, project.Status);
        Assert.IsNull(project.ProcessingStartedAtUtc);
        Assert.IsNull(project.FinishedAtUtc);
        Assert.IsNull(project.FailureReason);
        Assert.AreEqual(1, projectChangePublisher.PublishCallCount);
    }

    [TestMethod]
    public async Task RecoverAsync_WhenReviewingProjectArtifactsAreMissing_FailsProject()
    {
        TestProjectChangePublisher projectChangePublisher = new();
        ProjectTemporaryStoragePaths storagePaths = CreateStoragePaths();
        using ServiceProvider services = CreateServices(projectChangePublisher);
        Guid projectId = await SeedReviewingProjectAsync(services);
        Service service = CreateService(services, storagePaths);

        await service.RecoverAsync(CancellationToken.None);

        ProjectRecord project = await LoadProjectAsync(services, projectId);
        Assert.AreEqual(ProjectProcessingStatus.Failed, project.Status);
        Assert.AreEqual("Project artifacts were lost before recovery could restart analysis.", project.FailureReason);
        Assert.IsNotNull(project.FinishedAtUtc);
        Assert.AreEqual(1, projectChangePublisher.PublishCallCount);
    }

    [TestMethod]
    public async Task RecoverAsync_WhenStoredZipExists_DeletesStaleExtractedDirectory()
    {
        TestProjectChangePublisher projectChangePublisher = new();
        ProjectTemporaryStoragePaths storagePaths = CreateStoragePaths();
        using ServiceProvider services = CreateServices(projectChangePublisher);
        Guid projectId = await SeedReviewingProjectAsync(services);
        File.WriteAllText(storagePaths.ResolveUploadedZipPath(projectId), "zip-placeholder");
        Directory.CreateDirectory(storagePaths.ResolveExtractedProjectPath(projectId));
        Service service = CreateService(services, storagePaths);

        await service.RecoverAsync(CancellationToken.None);

        ProjectRecord project = await LoadProjectAsync(services, projectId);
        Assert.AreEqual(ProjectProcessingStatus.Queued, project.Status);
        Assert.IsFalse(Directory.Exists(storagePaths.ResolveExtractedProjectPath(projectId)));
    }

    private static Service CreateService(
        ServiceProvider services,
        ProjectTemporaryStoragePaths storagePaths) =>
        new(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>(),
            new ExecutionArtifactStore(storagePaths, NullLogger<ExecutionArtifactStore>.Instance));

    private static ServiceProvider CreateServices(TestProjectChangePublisher projectChangePublisher)
    {
        InMemoryDatabaseRoot databaseRoot = new();
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(Guid.CreateVersion7().ToString("N"), databaseRoot));
        services.AddScoped<IProjectChangePublisher>(_ => projectChangePublisher);
        return services.BuildServiceProvider();
    }

    private static ProjectTemporaryStoragePaths CreateStoragePaths()
    {
        ProjectTemporaryStoragePaths storagePaths = new();
        storagePaths.EnsureStorageDirectories();
        return storagePaths;
    }

    private static async Task<Guid> SeedReviewingProjectAsync(ServiceProvider services)
    {
        Guid projectId = Guid.CreateVersion7();
        await using CodeSnifferDogServerDbContext dbContext = await services
            .GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>()
            .CreateDbContextAsync();
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
            ProcessingStartedAtUtc = nowUtc,
        });
        await dbContext.SaveChangesAsync();
        return projectId;
    }

    private static async Task<ProjectRecord> LoadProjectAsync(ServiceProvider services, Guid projectId)
    {
        await using CodeSnifferDogServerDbContext dbContext = await services
            .GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>()
            .CreateDbContextAsync();
        return await dbContext.Projects.SingleAsync(project => project.Id == projectId);
    }

    private sealed class TestProjectChangePublisher : IProjectChangePublisher
    {
        public int PublishCallCount { get; private set; }

        public Task PublishProjectsChangedAsync(CancellationToken cancellationToken = default)
        {
            PublishCallCount++;
            return Task.CompletedTask;
        }
    }
}
