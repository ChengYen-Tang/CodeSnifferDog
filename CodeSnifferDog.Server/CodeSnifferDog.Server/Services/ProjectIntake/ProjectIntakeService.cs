using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.ProjectExecution;
using CodeSnifferDog.Server.Services.ProjectStorage;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CodeSnifferDog.Server.Services.ProjectIntake;

public sealed class ProjectIntakeService(
    CodeSnifferDogServerDbContext dbContext,
    IProjectChangePublisher projectChangePublisher,
    ProjectTemporaryStoragePaths storagePaths,
    IProjectExecutionLeaseRegistry executionLeaseRegistry,
    IProjectExecutionQueueLock queueLock,
    IOptions<ProjectExecutionOptions> projectExecutionOptions,
    ILogger<ProjectIntakeService> logger) : IProjectIntakeService
{
    private readonly CodeSnifferDogServerDbContext _dbContext = dbContext;
    private readonly IProjectChangePublisher _projectChangePublisher = projectChangePublisher;
    private readonly ProjectTemporaryStoragePaths _storagePaths = storagePaths;
    private readonly IProjectExecutionLeaseRegistry _executionLeaseRegistry = executionLeaseRegistry;
    private readonly IProjectExecutionQueueLock _queueLock = queueLock;
    private readonly ProjectExecutionOptions _projectExecutionOptions = projectExecutionOptions.Value;
    private readonly ILogger<ProjectIntakeService> _logger = logger;

    public async Task<ProjectUploadResult> UploadAsync(IFormFile zipFile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zipFile);

        if (zipFile.Length <= 0)
            throw new InvalidOperationException("The uploaded zip file is empty.");

        if (!string.Equals(Path.GetExtension(zipFile.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only .zip uploads are supported.");

        Guid projectId = Guid.NewGuid();
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        string storedFilePath = _storagePaths.ResolveUploadedZipPath(projectId);
        string storedZipRelativePath = _storagePaths.ResolveUploadedZipRelativePath(projectId);

        _storagePaths.EnsureStorageDirectories();

        try
        {
            await using (FileStream stream = new(
                storedFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                options: FileOptions.Asynchronous))
            {
                await zipFile.CopyToAsync(stream, cancellationToken);
            }

            using IDisposable queueLease = await _queueLock.AcquireAsync(cancellationToken);
            ProjectUploadResult result = await QueueProjectAsync(
                projectId,
                zipFile.FileName,
                zipFile.Length,
                storedZipRelativePath,
                nowUtc,
                cancellationToken);

            await _projectChangePublisher.PublishProjectsChangedAsync(CancellationToken.None);
            return result;
        }
        catch
        {
            TryDeleteFileIfExists(storedFilePath);
            throw;
        }
    }

    private async Task<ProjectUploadResult> QueueProjectAsync(
        Guid projectId,
        string originalFileName,
        long fileSizeBytes,
        string storedZipRelativePath,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (_projectExecutionOptions.MaxQueuedProjects <= 0)
            throw new InvalidOperationException("ProjectExecution:MaxQueuedProjects must be greater than zero.");

        int queuedProjects = await _dbContext.Projects
            .CountAsync(project => project.Status == ProjectProcessingStatus.Queued, cancellationToken);

        if (queuedProjects >= _projectExecutionOptions.MaxQueuedProjects)
            throw new InvalidOperationException("The project queue is full.");

        ProjectRecord project = new()
        {
            Id = projectId,
            OriginalFileName = Path.GetFileName(originalFileName),
            StoredZipRelativePath = storedZipRelativePath.Replace('\\', '/'),
            Status = ProjectProcessingStatus.Queued,
            FileSizeBytes = fileSizeBytes,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            QueueTimestampUtc = nowUtc,
        };

        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProjectUploadResult
        {
            ProjectId = project.Id,
            OriginalFileName = project.OriginalFileName,
            Status = MapStatus(project.Status),
            FileSizeBytes = project.FileSizeBytes,
            CreatedAtUtc = project.CreatedAtUtc,
            QueueTimestampUtc = project.QueueTimestampUtc,
        };
    }

    public async Task<IReadOnlyList<ProjectListItemDto>> ListAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Projects
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
            .Select(project => MapListItem(project))
            .ToListAsync(cancellationToken);

    public async Task<ProjectSummaryDto?> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        ProjectRecord? project = await _dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

        return project is null ? null : Map(project);
    }

    public async Task<bool> CancelAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using IDisposable queueLease = await _queueLock.AcquireAsync(cancellationToken);

        ProjectRecord? project = await _dbContext.Projects
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
        return await DeleteStoredProjectAsync(projectId, CancellationToken.None);
    }

    private async Task<bool> DeleteStoredProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        ProjectRecord? project = await _dbContext.Projects
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

        if (project is null)
            return false;

        if (project.Status == ProjectProcessingStatus.Reviewing)
            throw new InvalidOperationException("Reviewing projects must be canceled before deletion.");

        string uploadedZipPath = _storagePaths.ResolveStoredZipPath(project.StoredZipRelativePath);
        string extractedProjectPath = _storagePaths.ResolveExtractedProjectPath(project.Id);

        DeleteFileIfExists(uploadedZipPath);
        DeleteDirectoryIfExists(extractedProjectPath);

        _dbContext.Projects.Remove(project);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _projectChangePublisher.PublishProjectsChangedAsync(CancellationToken.None);
        return true;
    }

    private static ProjectSummaryDto Map(ProjectRecord project) => new()
    {
        ProjectId = project.Id,
        OriginalFileName = project.OriginalFileName,
        Status = MapStatus(project.Status),
        FileSizeBytes = project.FileSizeBytes,
        CreatedAtUtc = project.CreatedAtUtc,
        UpdatedAtUtc = project.UpdatedAtUtc,
        QueueTimestampUtc = project.QueueTimestampUtc,
        ProcessingStartedAtUtc = project.ProcessingStartedAtUtc,
        FinishedAtUtc = project.FinishedAtUtc,
        FailureReason = project.FailureReason,
    };

    private static ProjectListItemDto MapListItem(ProjectRecord project) => new()
    {
        ProjectId = project.Id,
        OriginalFileName = project.OriginalFileName,
        Status = MapStatus(project.Status),
        CreatedAtUtc = project.CreatedAtUtc,
    };

    private static ProjectStatus MapStatus(ProjectProcessingStatus status) => status switch
    {
        ProjectProcessingStatus.Queued => ProjectStatus.Queued,
        ProjectProcessingStatus.Reviewing => ProjectStatus.Reviewing,
        ProjectProcessingStatus.Completed => ProjectStatus.Completed,
        ProjectProcessingStatus.Failed => ProjectStatus.Failed,
        ProjectProcessingStatus.Canceled => ProjectStatus.Canceled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported project status."),
    };

    private void DeleteFileIfExists(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive: true);
    }

    private void TryDeleteFileIfExists(string filePath)
    {
        try
        {
            DeleteFileIfExists(filePath);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete project temporary file {FilePath}.", filePath);
        }
    }
}
