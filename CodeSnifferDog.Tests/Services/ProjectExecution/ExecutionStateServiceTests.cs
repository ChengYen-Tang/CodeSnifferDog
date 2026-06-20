using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ExecutionStateServiceTests
{
    [TestMethod]
    public async Task CompleteAsync_WhenProjectExists_UpdatesDatabaseThenPublishesUpdates()
    {
        TestProjectChangePublisher projectChangePublisher = new();
        TestLiveUpdateNotifier liveUpdateNotifier = new();
        using ServiceProvider services = CreateServices(projectChangePublisher, liveUpdateNotifier);
        Guid projectId = await SeedProjectAsync(services, ProjectProcessingStatus.Reviewing);
        ExecutionStateService service = CreateService(services);

        await service.CompleteAsync(projectId, ProjectProcessingStatus.Failed, "failed", CancellationToken.None);

        await using CodeSnifferDogServerDbContext dbContext = await services
            .GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>()
            .CreateDbContextAsync();
        ProjectRecord project = await dbContext.Projects.SingleAsync(project => project.Id == projectId);
        Assert.AreEqual(ProjectProcessingStatus.Failed, project.Status);
        Assert.AreEqual("failed", project.FailureReason);
        Assert.IsNotNull(project.FinishedAtUtc);
        Assert.AreEqual(1, liveUpdateNotifier.Updates.Count);
        Assert.AreEqual(ProjectStatus.Failed, liveUpdateNotifier.Updates.Single().ProjectStatus!.Status);
        Assert.AreEqual(1, projectChangePublisher.PublishCallCount);
    }

    [TestMethod]
    public async Task CanStartExecutionAsync_ReturnsTrueOnlyForReviewingProject()
    {
        using ServiceProvider services = CreateServices(new TestProjectChangePublisher(), new TestLiveUpdateNotifier());
        Guid reviewingProjectId = await SeedProjectAsync(services, ProjectProcessingStatus.Reviewing);
        Guid queuedProjectId = await SeedProjectAsync(services, ProjectProcessingStatus.Queued);
        ExecutionStateService service = CreateService(services);

        Assert.IsTrue(await service.CanStartExecutionAsync(reviewingProjectId, CancellationToken.None));
        Assert.IsFalse(await service.CanStartExecutionAsync(queuedProjectId, CancellationToken.None));
    }

    [TestMethod]
    public void MapProjectStatus_WhenStatusIsUnsupported_ThrowsOriginalException()
    {
        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => ExecutionStateService.MapProjectStatus((ProjectProcessingStatus)999));

        StringAssert.Contains(exception.Message, "Unsupported project status");
    }

    private static ExecutionStateService CreateService(ServiceProvider services) =>
        new(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>(),
            services.GetRequiredService<IProjectAgentStatusLiveUpdateNotifier>());

    private static ServiceProvider CreateServices(
        TestProjectChangePublisher projectChangePublisher,
        TestLiveUpdateNotifier liveUpdateNotifier)
    {
        InMemoryDatabaseRoot databaseRoot = new();
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"), databaseRoot));
        services.AddScoped<IProjectChangePublisher>(_ => projectChangePublisher);
        services.AddSingleton<IProjectAgentStatusLiveUpdateNotifier>(liveUpdateNotifier);
        return services.BuildServiceProvider();
    }

    private static async Task<Guid> SeedProjectAsync(ServiceProvider services, ProjectProcessingStatus status)
    {
        Guid projectId = Guid.NewGuid();
        await using CodeSnifferDogServerDbContext dbContext = await services
            .GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>()
            .CreateDbContextAsync();
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        dbContext.Projects.Add(new ProjectRecord
        {
            Id = projectId,
            OriginalFileName = "repo.zip",
            StoredZipRelativePath = $"uploads/{projectId:N}.zip",
            Status = status,
            FileSizeBytes = 10,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            QueueTimestampUtc = nowUtc,
        });
        await dbContext.SaveChangesAsync();
        return projectId;
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

    private sealed class TestLiveUpdateNotifier : IProjectAgentStatusLiveUpdateNotifier
    {
        public List<ProjectAgentLiveUpdateDto> Updates { get; } = [];

        public Task NotifyAsync(ProjectAgentLiveUpdateDto update, CancellationToken cancellationToken = default)
        {
            Updates.Add(update);
            return Task.CompletedTask;
        }
    }
}
