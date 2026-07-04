using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;
using CodeSnifferDog.Server.Services.ProjectStorage;
using System.IO.Compression;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Artifacts;

/// <summary>
/// Prepares and cleans up repository artifacts used by project execution workers.
/// </summary>
internal sealed class ExecutionArtifactStore(
    ProjectTemporaryStoragePaths storagePaths,
    ILogger<ExecutionArtifactStore> logger) : IExecutionArtifactStore
{
    private readonly ProjectTemporaryStoragePaths _storagePaths = storagePaths;
    private readonly ILogger<ExecutionArtifactStore> _logger = logger;

    /// <inheritdoc />
    public string PrepareRepository(Claim claim)
    {
        string uploadedZipPath = _storagePaths.ResolveStoredZipPath(claim.StoredZipRelativePath);
        string extractedProjectPath = _storagePaths.ResolveExtractedProjectPath(claim.ProjectId);

        if (File.Exists(uploadedZipPath))
        {
            DeleteDirectoryIfExists(extractedProjectPath);
            Directory.CreateDirectory(extractedProjectPath);
            ZipFile.ExtractToDirectory(uploadedZipPath, extractedProjectPath);
            File.Delete(uploadedZipPath);
            return extractedProjectPath;
        }

        if (!Directory.Exists(extractedProjectPath))
            throw new FileNotFoundException("Project upload zip and extracted repository were not found.", uploadedZipPath);

        return extractedProjectPath;
    }

    /// <inheritdoc />
    public bool StoredZipExists(string storedZipRelativePath) =>
        File.Exists(_storagePaths.ResolveStoredZipPath(storedZipRelativePath));

    /// <inheritdoc />
    public bool ExtractedProjectExists(Guid projectId) =>
        Directory.Exists(_storagePaths.ResolveExtractedProjectPath(projectId));

    /// <inheritdoc />
    public void TryDeleteExtractedProjectDirectory(Guid projectId)
    {
        try
        {
            TryDeleteDirectory(_storagePaths.ResolveExtractedProjectPath(projectId), projectId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete extracted directory for project {ProjectId}.", projectId);
        }
    }

    /// <inheritdoc />
    public void TryDeleteUploadedZipFile(string storedZipRelativePath, Guid projectId)
    {
        try
        {
            TryDeleteFile(_storagePaths.ResolveStoredZipPath(storedZipRelativePath), projectId);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete uploaded zip for project {ProjectId}.", projectId);
        }
    }

    /// <summary>
    /// Deletes a directory when it exists.
    /// </summary>
    /// <param name="directoryPath">Directory path to delete.</param>
    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive: true);
    }

    /// <summary>
    /// Attempts to delete an extracted repository directory and logs failures.
    /// </summary>
    /// <param name="directoryPath">Directory path to delete.</param>
    /// <param name="projectId">Project identifier used for logging.</param>
    private void TryDeleteDirectory(string directoryPath, Guid projectId)
    {
        try
        {
            DeleteDirectoryIfExists(directoryPath);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete extracted directory for project {ProjectId}.", projectId);
        }
    }

    /// <summary>
    /// Attempts to delete an uploaded archive and logs failures.
    /// </summary>
    /// <param name="filePath">File path to delete.</param>
    /// <param name="projectId">Project identifier used for logging.</param>
    private void TryDeleteFile(string filePath, Guid projectId)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete uploaded zip for project {ProjectId}.", projectId);
        }
    }
}
