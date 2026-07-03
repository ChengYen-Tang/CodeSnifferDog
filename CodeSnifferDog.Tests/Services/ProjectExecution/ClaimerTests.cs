using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Notifications;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Projection;
using CodeSnifferDog.Server.Services.ProjectAgentStatus.Snapshots;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.AgentStatus;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ClaimerTests
{
    [TestMethod]
    public async Task TryClaimNextAsync_ClaimsOldestQueuedProject_AndPublishesReviewingUpdate()
    {
        TestProjectChangePublisher projectChangePublisher = new();
        TestLiveUpdateNotifier liveUpdateNotifier = new();
        TestLeaseRegistry leaseRegistry = new();
        using ServiceProvider services = CreateServices(projectChangePublisher, liveUpdateNotifier);
        Guid newerProjectId = await SeedQueuedProjectAsync(services, DateTimeOffset.UtcNow.AddMinutes(1));
        Guid olderProjectId = await SeedQueuedProjectAsync(services, DateTimeOffset.UtcNow);
        Claimer claimer = CreateClaimer(services, leaseRegistry);

        Claim? claim = await claimer.TryClaimNextAsync(CancellationToken.None);

        Assert.IsNotNull(claim);
        Assert.AreEqual(olderProjectId, claim.ProjectId);
        Assert.AreEqual(olderProjectId, leaseRegistry.RegisteredProjectIds.Single());
        Assert.AreEqual(ProjectStatus.Reviewing, liveUpdateNotifier.Updates.Single().ProjectStatus!.Status);
        Assert.AreEqual(1, projectChangePublisher.PublishCallCount);

        await using CodeSnifferDogServerDbContext dbContext = await services
            .GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>()
            .CreateDbContextAsync();
        Assert.AreEqual(ProjectProcessingStatus.Reviewing, (await dbContext.Projects.FindAsync(olderProjectId))!.Status);
        Assert.AreEqual(ProjectProcessingStatus.Queued, (await dbContext.Projects.FindAsync(newerProjectId))!.Status);
    }

    [TestMethod]
    public async Task TryClaimNextAsync_WhenPublishFails_DisposesRegisteredLease()
    {
        TestProjectChangePublisher projectChangePublisher = new();
        TestLiveUpdateNotifier liveUpdateNotifier = new() { ThrowOnNotify = true };
        TestLeaseRegistry leaseRegistry = new();
        using ServiceProvider services = CreateServices(projectChangePublisher, liveUpdateNotifier);
        Guid projectId = await SeedQueuedProjectAsync(services, DateTimeOffset.UtcNow);
        Claimer claimer = CreateClaimer(services, leaseRegistry);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => claimer.TryClaimNextAsync(CancellationToken.None));

        Assert.AreEqual(projectId, leaseRegistry.DisposedProjectIds.Single());
        Assert.AreEqual(0, projectChangePublisher.PublishCallCount);
    }

    private static Claimer CreateClaimer(ServiceProvider services, TestLeaseRegistry leaseRegistry) =>
        new(
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>(),
            leaseRegistry,
            new StateService(
                services.GetRequiredService<IServiceScopeFactory>(),
                services.GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>(),
                services.GetRequiredService<ILiveUpdateNotifier>(),
                new ProjectStatusMapper()));

    private static ServiceProvider CreateServices(
        TestProjectChangePublisher projectChangePublisher,
        TestLiveUpdateNotifier liveUpdateNotifier)
    {
        InMemoryDatabaseRoot databaseRoot = new();
        ServiceCollection services = [];
        services.AddPooledDbContextFactory<CodeSnifferDogServerDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N"), databaseRoot));
        services.AddScoped<IProjectChangePublisher>(_ => projectChangePublisher);
        services.AddSingleton<ILiveUpdateNotifier>(liveUpdateNotifier);
        return services.BuildServiceProvider();
    }

    private static async Task<Guid> SeedQueuedProjectAsync(ServiceProvider services, DateTimeOffset queueTimestampUtc)
    {
        Guid projectId = Guid.NewGuid();
        await using CodeSnifferDogServerDbContext dbContext = await services
            .GetRequiredService<IDbContextFactory<CodeSnifferDogServerDbContext>>()
            .CreateDbContextAsync();
        dbContext.Projects.Add(new ProjectRecord
        {
            Id = projectId,
            OriginalFileName = "repo.zip",
            StoredZipRelativePath = $"uploads/{projectId:N}.zip",
            Status = ProjectProcessingStatus.Queued,
            FileSizeBytes = 10,
            CreatedAtUtc = queueTimestampUtc,
            UpdatedAtUtc = queueTimestampUtc,
            QueueTimestampUtc = queueTimestampUtc,
        });
        await dbContext.SaveChangesAsync();
        return projectId;
    }

    private sealed class TestLeaseRegistry : ILeaseRegistry
    {
        public List<Guid> RegisteredProjectIds { get; } = [];

        public List<Guid> DisposedProjectIds { get; } = [];

        public Lease Register(Guid projectId, CancellationToken cancellationToken)
        {
            RegisteredProjectIds.Add(projectId);
            return new Lease(projectId, cancellationToken, DisposedProjectIds.Add);
        }

        public Task<bool> CancelAndWaitAsync(Guid projectId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public bool TryCancel(Guid projectId, out Task? completion) =>
            throw new NotSupportedException();
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

    private sealed class TestLiveUpdateNotifier : ILiveUpdateNotifier
    {
        public bool ThrowOnNotify { get; init; }

        public List<LiveUpdateDto> Updates { get; } = [];

        public Task NotifyAsync(LiveUpdateDto update, CancellationToken cancellationToken = default)
        {
            if (ThrowOnNotify)
                throw new InvalidOperationException("Notify failed.");

            Updates.Add(update);
            return Task.CompletedTask;
        }
    }
}
