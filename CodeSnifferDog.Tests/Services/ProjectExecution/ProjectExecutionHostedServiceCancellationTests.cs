using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectExecution.Analysis;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Artifacts;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Services.ProjectStorage;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ProjectExecutionHostedServiceCancellationTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task RunClaimedProjectAsync_UserCancel_UpdatesDatabaseToCanceled_AndPublishesChange()
    {
        Guid projectId = Guid.NewGuid();
        TestProjectChangePublisher projectChangePublisher = new();
        TestProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using ServiceProvider services = CreateServices(projectChangePublisher, liveUpdateNotifier, new CancelAwareAnalysisRunner());
        await SeedReviewingProjectAsync(services, projectId);
        EnsureExtractedProjectDirectory(projectId);

        using CancellationTokenSource hostStoppingTokenSource = new();
        using ProjectExecutionLease lease = new(projectId, hostStoppingTokenSource.Token, static _ => { });
        lease.TryCancel(ProjectExecutionCancellationSource.UserRequest);

        ClaimExecutor claimExecutor = CreateClaimExecutor(services);

        await claimExecutor.ExecuteAsync(1, CreateClaim(projectId, $@"extracted/{projectId:N}", lease), CancellationToken.None);

        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        ProjectRecord project = await dbContext.Projects.SingleAsync(project => project.Id == projectId, TestContext.CancellationToken);

        Assert.AreEqual(ProjectProcessingStatus.Canceled, project.Status);
        Assert.IsNotNull(project.FinishedAtUtc);
        Assert.AreEqual(1, projectChangePublisher.PublishCallCount);
        Assert.AreEqual(ProjectStatus.Canceled, liveUpdateNotifier.Updates.Last().ProjectStatus!.Status);
        Assert.IsFalse(Directory.Exists(GetStoragePaths().ResolveExtractedProjectPath(projectId)));
    }

    [TestMethod]
    public async Task RunClaimedProjectAsync_HostShutdown_PreservesReviewingState_AndDoesNotPublishChange()
    {
        Guid projectId = Guid.NewGuid();
        TestProjectChangePublisher projectChangePublisher = new();
        TestProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using ServiceProvider services = CreateServices(projectChangePublisher, liveUpdateNotifier, new CancelAwareAnalysisRunner());
        await SeedReviewingProjectAsync(services, projectId);
        EnsureExtractedProjectDirectory(projectId);

        using CancellationTokenSource hostStoppingTokenSource = new();
        using ProjectExecutionLease lease = new(projectId, hostStoppingTokenSource.Token, static _ => { });
        hostStoppingTokenSource.Cancel();

        ClaimExecutor claimExecutor = CreateClaimExecutor(services);

        await claimExecutor.ExecuteAsync(1, CreateClaim(projectId, $@"extracted/{projectId:N}", lease), CancellationToken.None);

        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync(TestContext.CancellationToken);
        ProjectRecord project = await dbContext.Projects.SingleAsync(project => project.Id == projectId, TestContext.CancellationToken);

        Assert.AreEqual(ProjectProcessingStatus.Reviewing, project.Status);
        Assert.IsNull(project.FinishedAtUtc);
        Assert.AreEqual(0, projectChangePublisher.PublishCallCount);
        Assert.IsEmpty(liveUpdateNotifier.Updates);
        Assert.IsTrue(Directory.Exists(GetStoragePaths().ResolveExtractedProjectPath(projectId)));
    }

    [TestMethod]
    public async Task RunClaimedProjectAsync_Success_PublishesCompletedStatusUpdate()
    {
        Guid projectId = Guid.NewGuid();
        TestProjectChangePublisher projectChangePublisher = new();
        TestProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using ServiceProvider services = CreateServices(projectChangePublisher, liveUpdateNotifier, new SuccessfulAnalysisRunner());
        await SeedReviewingProjectAsync(services, projectId);
        EnsureExtractedProjectDirectory(projectId);

        using CancellationTokenSource hostStoppingTokenSource = new();
        using ProjectExecutionLease lease = new(projectId, hostStoppingTokenSource.Token, static _ => { });

        ClaimExecutor claimExecutor = CreateClaimExecutor(services);

        await claimExecutor.ExecuteAsync(1, CreateClaim(projectId, $@"extracted/{projectId:N}", lease), CancellationToken.None);

        Assert.IsTrue(liveUpdateNotifier.Updates.Any(update =>
            update.Kind == ProjectAgentLiveUpdateKind.ProjectStatusChanged &&
            update.ProjectId == projectId &&
            update.ProjectStatus?.Status == ProjectStatus.Completed));
    }

    [TestMethod]
    public async Task RunClaimedProjectAsync_Failure_PublishesFailedStatusUpdate()
    {
        Guid projectId = Guid.NewGuid();
        TestProjectChangePublisher projectChangePublisher = new();
        TestProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        using ServiceProvider services = CreateServices(projectChangePublisher, liveUpdateNotifier, new FailingAnalysisRunner());
        await SeedReviewingProjectAsync(services, projectId);
        EnsureExtractedProjectDirectory(projectId);

        using CancellationTokenSource hostStoppingTokenSource = new();
        using ProjectExecutionLease lease = new(projectId, hostStoppingTokenSource.Token, static _ => { });

        ClaimExecutor claimExecutor = CreateClaimExecutor(services);

        await claimExecutor.ExecuteAsync(1, CreateClaim(projectId, $@"extracted/{projectId:N}", lease), CancellationToken.None);

        Assert.IsTrue(liveUpdateNotifier.Updates.Any(update =>
            update.Kind == ProjectAgentLiveUpdateKind.ProjectStatusChanged &&
            update.ProjectId == projectId &&
            update.ProjectStatus?.Status == ProjectStatus.Failed));
    }

    [TestMethod]
    public async Task RunClaimedProjectAsync_WhenProjectIsNoLongerReviewing_CleansArtifactsAndDoesNotRunAnalysis()
    {
        Guid projectId = Guid.NewGuid();
        TestProjectChangePublisher projectChangePublisher = new();
        TestProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier = new();
        TrackingAnalysisRunner analysisRunner = new();
        using ServiceProvider services = CreateServices(projectChangePublisher, liveUpdateNotifier, analysisRunner);
        await SeedProjectAsync(services, projectId, ProjectProcessingStatus.Queued);
        EnsureExtractedProjectDirectory(projectId);

        using ProjectExecutionLease lease = new(projectId, CancellationToken.None, static _ => { });
        ClaimExecutor claimExecutor = CreateClaimExecutor(services);

        await claimExecutor.ExecuteAsync(1, CreateClaim(projectId, $@"extracted/{projectId:N}", lease), CancellationToken.None);

        Assert.IsFalse(analysisRunner.WasCalled);
        Assert.IsFalse(Directory.Exists(GetStoragePaths().ResolveExtractedProjectPath(projectId)));
        Assert.AreEqual(0, projectChangePublisher.PublishCallCount);
        Assert.IsEmpty(liveUpdateNotifier.Updates);
    }

    private static ClaimExecutor CreateClaimExecutor(ServiceProvider services) =>
        new(
            services.GetRequiredService<IServiceScopeFactory>(),
            new ExecutionArtifactStore(GetStoragePaths(), NullLogger<ExecutionArtifactStore>.Instance),
            new ExecutionStateService(
                services.GetRequiredService<IServiceScopeFactory>(),
                services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>(),
                services.GetRequiredService<IProjectAgentStatusLiveUpdateNotifier>(),
                new ProjectStatusMapper()),
            NullLogger<ClaimExecutor>.Instance);

    private static ProjectExecutionClaim CreateClaim(
        Guid projectId,
        string storedZipRelativePath,
        ProjectExecutionLease lease) =>
        new(projectId, storedZipRelativePath, lease);

    private static ServiceProvider CreateServices(
        TestProjectChangePublisher projectChangePublisher,
        TestProjectAgentStatusLiveUpdateNotifier liveUpdateNotifier,
        IProjectAnalysisRunner analysisRunner)
    {
        InMemoryDatabaseRoot databaseRoot = new();
        string databaseName = Guid.NewGuid().ToString("N");
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));
        services.AddSingleton<ProjectTemporaryStoragePaths>(_ => GetStoragePaths());
        services.AddScoped<IProjectChangePublisher>(_ => projectChangePublisher);
        services.AddScoped<IProjectAnalysisRunner>(_ => analysisRunner);
        services.AddSingleton<IProjectAgentStatusLiveUpdateNotifier>(liveUpdateNotifier);
        services.AddSingleton<IProjectExecutionLeaseRegistry, ProjectExecutionLeaseRegistry>();
        services.AddSingleton<IProjectExecutionQueueLock, ImmediateProjectExecutionQueueLock>();
        return services.BuildServiceProvider();
    }

    private static async Task SeedReviewingProjectAsync(ServiceProvider services, Guid projectId)
    {
        await SeedProjectAsync(services, projectId, ProjectProcessingStatus.Reviewing);
    }

    private static async Task SeedProjectAsync(
        ServiceProvider services,
        Guid projectId,
        ProjectProcessingStatus status)
    {
        IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory = services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>();
        await using CodeSnifferDogServerDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
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
            ProcessingStartedAtUtc = status == ProjectProcessingStatus.Reviewing ? nowUtc : null,
        });
        await dbContext.SaveChangesAsync();
    }

    private static void EnsureExtractedProjectDirectory(Guid projectId)
    {
        string extractedProjectPath = GetStoragePaths().ResolveExtractedProjectPath(projectId);
        if (Directory.Exists(extractedProjectPath))
            Directory.Delete(extractedProjectPath, recursive: true);

        Directory.CreateDirectory(extractedProjectPath);
        File.WriteAllText(Path.Combine(extractedProjectPath, "Program.cs"), "class Program {}");
    }

    private static ProjectTemporaryStoragePaths GetStoragePaths()
    {
        ProjectTemporaryStoragePaths storagePaths = new();
        storagePaths.EnsureStorageDirectories();
        return storagePaths;
    }

    private sealed class CancelAwareAnalysisRunner : IProjectAnalysisRunner
    {
        public bool IsReady => true;

        public async Task RunAsync(ProjectAnalysisContext context, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class SuccessfulAnalysisRunner : IProjectAnalysisRunner
    {
        public bool IsReady => true;

        public Task RunAsync(ProjectAnalysisContext context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FailingAnalysisRunner : IProjectAnalysisRunner
    {
        public bool IsReady => true;

        public Task RunAsync(ProjectAnalysisContext context, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Analysis failed.");
    }

    private sealed class TrackingAnalysisRunner : IProjectAnalysisRunner
    {
        public bool IsReady => true;

        public bool WasCalled { get; private set; }

        public Task RunAsync(ProjectAnalysisContext context, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
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

    private sealed class ImmediateProjectExecutionQueueLock : IProjectExecutionQueueLock
    {
        public Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IDisposable>(new NoopDisposable());
    }

    private sealed class TestProjectAgentStatusLiveUpdateNotifier : IProjectAgentStatusLiveUpdateNotifier
    {
        public List<ProjectAgentLiveUpdateDto> Updates { get; } = [];

        public Task NotifyAsync(ProjectAgentLiveUpdateDto update, CancellationToken cancellationToken = default)
        {
            Updates.Add(update);
            return Task.CompletedTask;
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
