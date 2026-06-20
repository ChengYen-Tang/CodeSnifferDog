using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectIntake.Deletion;
using CodeSnifferDog.Server.Services.ProjectIntake.Queue;
using CodeSnifferDog.Server.Services.ProjectIntake.Upload;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Services.Projects.Projection;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectIntake;

internal sealed class ProjectIntakeService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IProjectChangePublisher projectChangePublisher,
    IProjectUploadService projectUploadService,
    IProjectQueueService projectQueueService,
    IProjectDeletionService projectDeletionService,
    IProjectProjectionMapper projectionMapper,
    IProjectExecutionLeaseRegistry executionLeaseRegistry,
    IProjectExecutionQueueLock queueLock,
    ILogger<ProjectIntakeService> logger) : IProjectIntakeService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly IProjectChangePublisher _projectChangePublisher = projectChangePublisher;
    private readonly IProjectUploadService _projectUploadService = projectUploadService;
    private readonly IProjectQueueService _projectQueueService = projectQueueService;
    private readonly IProjectDeletionService _projectDeletionService = projectDeletionService;
    private readonly IProjectProjectionMapper _projectionMapper = projectionMapper;
    private readonly IProjectExecutionLeaseRegistry _executionLeaseRegistry = executionLeaseRegistry;
    private readonly IProjectExecutionQueueLock _queueLock = queueLock;
    private readonly ILogger<ProjectIntakeService> _logger = logger;

    public async Task<ProjectUploadResult> UploadAsync(IFormFile zipFile, CancellationToken cancellationToken = default)
    {
        Guid projectId = Guid.NewGuid();
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        ProjectUploadArtifact artifact = await _projectUploadService
            .StoreAsync(projectId, zipFile, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            using IDisposable queueLease = await _queueLock.AcquireAsync(cancellationToken);
            ProjectUploadResult result = await _projectQueueService.QueueAsync(
                new ProjectQueueRequest(
                    projectId,
                    artifact.OriginalFileName,
                    artifact.FileSizeBytes,
                    artifact.StoredZipRelativePath,
                    nowUtc),
                cancellationToken);

            await _projectChangePublisher.PublishProjectsChangedAsync(CancellationToken.None);
            return result;
        }
        catch
        {
            _projectUploadService.TryDeleteStoredFile(artifact);
            throw;
        }
    }

    public async Task<IReadOnlyList<ProjectListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<ProjectRecord> projects = await dbContext.Projects
            .AsNoTracking()
            .OrderBy(project =>
                project.Status == ProjectProcessingStatus.Queued ? 0 :
                project.Status == ProjectProcessingStatus.Reviewing ? 1 :
                project.Status == ProjectProcessingStatus.Failed ? 2 :
                project.Status == ProjectProcessingStatus.Completed ? 3 : 4)
            .ThenBy(project =>
                project.Status == ProjectProcessingStatus.Queued ||
                project.Status == ProjectProcessingStatus.Reviewing
                    ? project.QueueTimestampUtc
                    : project.FinishedAtUtc ?? project.UpdatedAtUtc)
            .ThenBy(project => project.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return projects
            .Select(_projectionMapper.MapListItem)
            .ToList();
    }

    public async Task<ProjectSummaryDto?> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectRecord? project = await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

        return project is null ? null : _projectionMapper.MapSummary(project);
    }

    public async Task<bool> CancelAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using IDisposable queueLease = await _queueLock.AcquireAsync(cancellationToken);

        await using CodeSnifferDogServerDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        ProjectRecord? project = await dbContext.Projects
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

        if (project is null)
            return false;

        if (project.Status != ProjectProcessingStatus.Reviewing)
            throw new InvalidOperationException("Only reviewing projects can be canceled.");

        if (_executionLeaseRegistry.TryCancel(projectId, out _))
            _logger.LogInformation("Project {ProjectId} cancellation was requested.", projectId);
        else
            throw new InvalidOperationException("The reviewing project is not actively running.");

        return true;
    }

    public async Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using IDisposable queueLease = await _queueLock.AcquireAsync(cancellationToken);
        bool deleted = await _projectDeletionService.DeleteAsync(projectId, CancellationToken.None);
        if (deleted)
            await _projectChangePublisher.PublishProjectsChangedAsync(CancellationToken.None);

        return deleted;
    }
}
