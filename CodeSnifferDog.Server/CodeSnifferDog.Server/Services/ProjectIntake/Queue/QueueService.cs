using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.Projects.Projection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSnifferDog.Server.Services.ProjectIntake.Queue;

internal sealed class QueueService(
    IDbContextFactory<CodeSnifferDogServerDbContext> dbContextFactory,
    IOptions<ProjectExecutionOptions> projectExecutionOptions,
    IProjectProjectionMapper projectionMapper,
    ILogger<QueueService>? logger = null) : IQueueService
{
    private readonly IDbContextFactory<CodeSnifferDogServerDbContext> _dbContextFactory = dbContextFactory;
    private readonly ProjectExecutionOptions _projectExecutionOptions = projectExecutionOptions.Value;
    private readonly IProjectProjectionMapper _projectionMapper = projectionMapper;
    private readonly ILogger<QueueService> _logger = logger ?? NullLogger<QueueService>.Instance;

    public async Task<ProjectUploadResult> QueueAsync(Request request, CancellationToken cancellationToken)
    {
        if (_projectExecutionOptions.MaxQueuedProjects <= 0)
            throw new InvalidOperationException("ProjectExecution:MaxQueuedProjects must be greater than zero.");

        await using CodeSnifferDogServerDbContext dbContext =
            await _dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        int queuedProjects = await dbContext.Projects
            .CountAsync(project => project.Status == ProjectProcessingStatus.Queued, cancellationToken);

        if (queuedProjects >= _projectExecutionOptions.MaxQueuedProjects)
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
