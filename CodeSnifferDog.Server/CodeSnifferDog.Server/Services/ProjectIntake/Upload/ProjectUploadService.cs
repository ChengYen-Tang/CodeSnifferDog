using CodeSnifferDog.Server.Services.ProjectStorage;

namespace CodeSnifferDog.Server.Services.ProjectIntake.Upload;

internal sealed class ProjectUploadService(
    ProjectTemporaryStoragePaths storagePaths,
    ILogger<ProjectUploadService> logger) : IProjectUploadService
{
    private readonly ProjectTemporaryStoragePaths _storagePaths = storagePaths;
    private readonly ILogger<ProjectUploadService> _logger = logger;

    public async Task<ProjectUploadArtifact> StoreAsync(
        Guid projectId,
        IFormFile zipFile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(zipFile);

        if (zipFile.Length <= 0)
            throw new InvalidOperationException("The uploaded zip file is empty.");

        if (!string.Equals(Path.GetExtension(zipFile.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only .zip uploads are supported.");

        string storedFilePath = _storagePaths.ResolveUploadedZipPath(projectId);
        string storedZipRelativePath = _storagePaths.ResolveUploadedZipRelativePath(projectId);

        _storagePaths.EnsureStorageDirectories();

        ProjectUploadArtifact artifact = new(
            zipFile.FileName,
            zipFile.Length,
            storedFilePath,
            storedZipRelativePath);

        try
        {
            await using FileStream stream = new(
                storedFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                options: FileOptions.Asynchronous);

            await zipFile.CopyToAsync(stream, cancellationToken);
            return artifact;
        }
        catch
        {
            TryDeleteStoredFile(artifact);
            throw;
        }
    }

    public void TryDeleteStoredFile(ProjectUploadArtifact artifact)
    {
        try
        {
            if (File.Exists(artifact.StoredFilePath))
                File.Delete(artifact.StoredFilePath);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete project temporary file {FilePath}.", artifact.StoredFilePath);
        }
    }
}
