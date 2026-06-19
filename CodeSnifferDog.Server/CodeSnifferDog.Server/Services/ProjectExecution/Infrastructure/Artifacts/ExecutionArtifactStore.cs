using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;
using CodeSnifferDog.Server.Services.ProjectStorage;
using System.IO.Compression;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Artifacts;

internal sealed class ExecutionArtifactStore(
    ProjectTemporaryStoragePaths storagePaths,
    ILogger<ExecutionArtifactStore> logger) : IExecutionArtifactStore
{
    private readonly ProjectTemporaryStoragePaths _storagePaths = storagePaths;
    private readonly ILogger<ExecutionArtifactStore> _logger = logger;

    public string PrepareRepository(ProjectExecutionClaim claim)
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

    public bool StoredZipExists(string storedZipRelativePath) =>
        File.Exists(_storagePaths.ResolveStoredZipPath(storedZipRelativePath));

    public bool ExtractedProjectExists(Guid projectId) =>
        Directory.Exists(_storagePaths.ResolveExtractedProjectPath(projectId));

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

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
            Directory.Delete(directoryPath, recursive: true);
    }

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
