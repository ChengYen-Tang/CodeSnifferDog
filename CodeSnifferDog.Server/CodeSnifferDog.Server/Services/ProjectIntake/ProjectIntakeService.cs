using CodeSnifferDog.Server.Data;
using CodeSnifferDog.Server.Data.Entities;
using CodeSnifferDog.Server.Services.Projects;
using CodeSnifferDog.Server.Shared.Projects;
using Microsoft.EntityFrameworkCore;

namespace CodeSnifferDog.Server.Services.ProjectIntake;

public sealed class ProjectIntakeService(
    CodeSnifferDogServerDbContext dbContext,
    IProjectChangePublisher projectChangePublisher) : IProjectIntakeService
{
    private const string TemporaryStorageDirectoryName = "TemporaryStorage";
    private const string UploadedZipDirectoryName = "uploads";
    private const string ExtractedProjectDirectoryName = "extracted";

    private readonly CodeSnifferDogServerDbContext _dbContext = dbContext;
    private readonly IProjectChangePublisher _projectChangePublisher = projectChangePublisher;

    public async Task<ProjectUploadResult> UploadAsync(IFormFile zipFile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zipFile);

        if (zipFile.Length <= 0)
            throw new InvalidOperationException("The uploaded zip file is empty.");

        if (!string.Equals(Path.GetExtension(zipFile.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only .zip uploads are supported.");

        Guid projectId = Guid.NewGuid();
        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
        string storageRootPath = ResolveTemporaryStorageRootPath();
        string uploadedZipDirectoryPath = Path.Combine(storageRootPath, UploadedZipDirectoryName);
        string storedFileName = $"{projectId:N}.zip";
        string storedFilePath = Path.Combine(uploadedZipDirectoryPath, storedFileName);
        string storedZipRelativePath = Path.GetRelativePath(storageRootPath, storedFilePath);

        Directory.CreateDirectory(uploadedZipDirectoryPath);
        Directory.CreateDirectory(Path.Combine(storageRootPath, ExtractedProjectDirectoryName));

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

            ProjectRecord project = new()
            {
                Id = projectId,
                OriginalFileName = Path.GetFileName(zipFile.FileName),
                StoredZipRelativePath = storedZipRelativePath.Replace('\\', '/'),
                Status = ProjectProcessingStatus.Queued,
                FileSizeBytes = zipFile.Length,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                QueueTimestampUtc = nowUtc,
            };

            _dbContext.Projects.Add(project);
            await _dbContext.SaveChangesAsync(cancellationToken);

            ProjectUploadResult result = new()
            {
                ProjectId = project.Id,
                OriginalFileName = project.OriginalFileName,
                Status = MapStatus(project.Status),
                FileSizeBytes = project.FileSizeBytes,
                CreatedAtUtc = project.CreatedAtUtc,
                QueueTimestampUtc = project.QueueTimestampUtc,
            };

            await _projectChangePublisher.PublishProjectsChangedAsync(CancellationToken.None);
            return result;
        }
        catch
        {
            TryDeleteFile(storedFilePath);
            throw;
        }
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

    private static string ResolveTemporaryStorageRootPath() =>
        Path.Combine(AppContext.BaseDirectory, TemporaryStorageDirectoryName);

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

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
        }
    }

}
