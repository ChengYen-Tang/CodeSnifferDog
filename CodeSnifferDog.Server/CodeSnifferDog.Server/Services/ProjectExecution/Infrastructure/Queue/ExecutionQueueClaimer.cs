using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

internal sealed class ExecutionQueueClaimer(
    IServiceScopeFactory serviceScopeFactory,
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IProjectExecutionLeaseRegistry leaseRegistry,
    IExecutionStateService executionStateService,
    ILogger<ExecutionQueueClaimer>? logger = null) : IExecutionQueueClaimer
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IProjectExecutionLeaseRegistry _leaseRegistry = leaseRegistry;
    private readonly IExecutionStateService _executionStateService = executionStateService;
    private readonly ILogger<ExecutionQueueClaimer> _logger = logger ?? NullLogger<ExecutionQueueClaimer>.Instance;

    public async Task<ProjectExecutionClaim?> TryClaimNextAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
        IProjectChangePublisher projectChangePublisher = scope.ServiceProvider.GetRequiredService<IProjectChangePublisher>();
        ClaimData? claimData = await TryClaimNextProjectFromDatabaseAsync(cancellationToken);
        if (claimData is null)
            return null;

        ProjectExecutionLease executionLease = _leaseRegistry.Register(claimData.ProjectId, cancellationToken);

        try
        {
            await _executionStateService.PublishStatusUpdateAsync(
                claimData.ProjectId,
                ProjectProcessingStatus.Reviewing,
                CancellationToken.None);
            await projectChangePublisher.PublishProjectsChangedAsync(CancellationToken.None);

            _logger.LogInformation("Project {ProjectId} was claimed for execution.", claimData.ProjectId);

            return new ProjectExecutionClaim(
                claimData.ProjectId,
                claimData.StoredZipRelativePath,
                executionLease);
        }
        catch
        {
            executionLease.Dispose();
            throw;
        }
    }

    private async Task<ClaimData?> TryClaimNextProjectFromDatabaseAsync(CancellationToken cancellationToken)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        ProjectRecord? project = await dbContext.Projects
            .OrderBy(project => project.Status == ProjectProcessingStatus.Queued ? 0 : 1)
            .ThenBy(project => project.QueueTimestampUtc)
            .ThenBy(project => project.CreatedAtUtc)
            .FirstOrDefaultAsync(project => project.Status == ProjectProcessingStatus.Queued, cancellationToken);

        if (project is null)
            return null;

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        project.Status = ProjectProcessingStatus.Reviewing;
        project.ProcessingStartedAtUtc = nowUtc;
        project.FinishedAtUtc = null;
        project.UpdatedAtUtc = nowUtc;
        project.FailureReason = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ClaimData(project.Id, project.StoredZipRelativePath);
    }

    private sealed record ClaimData(Guid ProjectId, string StoredZipRelativePath);
}
