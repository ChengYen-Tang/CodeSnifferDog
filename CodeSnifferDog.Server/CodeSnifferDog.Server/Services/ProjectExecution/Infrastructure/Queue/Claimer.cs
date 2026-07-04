using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Execution;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

/// <summary>
/// Claims queued projects from the database and transitions them into the reviewing state.
/// </summary>
internal sealed class Claimer(
    IServiceScopeFactory serviceScopeFactory,
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    ILeaseRegistry leaseRegistry,
    IStateService StateService,
    ILogger<Claimer>? logger = null) : IClaimer
{
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly ILeaseRegistry _leaseRegistry = leaseRegistry;
    private readonly IStateService _StateService = StateService;
    private readonly ILogger<Claimer> _logger = logger ?? NullLogger<Claimer>.Instance;

    /// <inheritdoc />
    public async Task<Claim?> TryClaimNextAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
        IProjectChangePublisher projectChangePublisher = scope.ServiceProvider.GetRequiredService<IProjectChangePublisher>();
        ClaimData? claimData = await TryClaimNextProjectFromDatabaseAsync(cancellationToken);
        if (claimData is null)
            return null;

        Lease executionLease = _leaseRegistry.Register(claimData.ProjectId, cancellationToken);

        try
        {
            await _StateService.PublishStatusUpdateAsync(
                claimData.ProjectId,
                ProjectProcessingStatus.Reviewing,
                CancellationToken.None);
            await projectChangePublisher.PublishProjectsChangedAsync(CancellationToken.None);

            _logger.LogInformation("Project {ProjectId} was claimed for execution.", claimData.ProjectId);

            return new Claim(
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

    /// <summary>
    /// Claims the next queued project record from the database.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the database operation.</param>
    /// <returns>The claimed project data, or <see langword="null"/> when the queue is empty.</returns>
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

    /// <summary>
    /// Holds the database fields required to start project execution.
    /// </summary>
    /// <param name="ProjectId">Project identifier.</param>
    /// <param name="StoredZipRelativePath">Relative path to the uploaded archive.</param>
    private sealed record ClaimData(Guid ProjectId, string StoredZipRelativePath);
}
