using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.Projects.Projection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSnifferDog.Server.Services.ProjectIntake.Queue;

/// <summary>
/// Adds uploaded projects to the persisted processing queue.
/// </summary>
/// <param name="dbContextFactory">Factory used to create database contexts for queue persistence.</param>
/// <param name="Settings">Execution settings that define queue limits.</param>
/// <param name="projectionMapper">Mapper used to convert persisted rows into shared DTOs.</param>
/// <param name="logger">Optional logger used for queue diagnostics.</param>
internal sealed class QueueService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IOptions<Settings> Settings,
    IProjectProjectionMapper projectionMapper,
    ILogger<QueueService>? logger = null) : IQueueService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly Settings _Settings = Settings.Value;
    private readonly IProjectProjectionMapper _projectionMapper = projectionMapper;
    private readonly ILogger<QueueService> _logger = logger ?? NullLogger<QueueService>.Instance;

    /// <inheritdoc />
    public async Task<ProjectUploadResult> QueueAsync(Request request, CancellationToken cancellationToken)
    {
        if (_Settings.MaxQueuedProjects <= 0)
            throw new InvalidOperationException("ProjectExecution:MaxQueuedProjects must be greater than zero.");

        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        int queuedProjects = await dbContext.Projects
            .CountAsync(project => project.Status == ProjectProcessingStatus.Queued, cancellationToken);

        if (queuedProjects >= _Settings.MaxQueuedProjects)
            throw new InvalidOperationException("The project queue is full.");

        ProjectRecord project = new()
        {
            Id = request.ProjectId,
            OriginalFileName = Path.GetFileName(request.OriginalFileName),
            StoredZipRelativePath = request.StoredZipRelativePath.Replace('\\', '/'),
            Status = ProjectProcessingStatus.Queued,
            FileSizeBytes = request.FileSizeBytes,
            CreatedAtUtc = request.NowUtc,
            UpdatedAtUtc = request.NowUtc,
            QueueTimestampUtc = request.NowUtc,
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Project {ProjectId} queued from {OriginalFileName}. File size: {FileSizeBytes} bytes.",
            project.Id,
            project.OriginalFileName,
            project.FileSizeBytes);

        return new ProjectUploadResult
        {
            ProjectId = project.Id,
            OriginalFileName = project.OriginalFileName,
            Status = _projectionMapper.MapStatus(project.Status),
            FileSizeBytes = project.FileSizeBytes,
            CreatedAtUtc = project.CreatedAtUtc,
            QueueTimestampUtc = project.QueueTimestampUtc,
        };
    }
}
