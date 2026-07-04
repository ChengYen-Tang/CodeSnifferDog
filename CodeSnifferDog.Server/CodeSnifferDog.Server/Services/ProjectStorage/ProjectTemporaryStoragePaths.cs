namespace CodeSnifferDog.Server.Services.ProjectStorage;

/// <summary>
/// Resolves temporary storage paths used for uploaded archives and extracted project contents.
/// </summary>
public sealed class ProjectTemporaryStoragePaths
{
    private const string TemporaryStorageDirectoryName = "TemporaryStorage";
    private const string UploadedZipDirectoryName = "uploads";
    private const string ExtractedProjectDirectoryName = "extracted";

    /// <summary>
    /// Gets the root directory used for temporary project storage.
    /// </summary>
    public string RootPath => Path.Combine(AppContext.BaseDirectory, TemporaryStorageDirectoryName);

    /// <summary>
    /// Gets the directory that stores uploaded project archives.
    /// </summary>
    public string UploadedZipDirectoryPath => Path.Combine(RootPath, UploadedZipDirectoryName);

    /// <summary>
    /// Gets the directory that stores extracted project contents.
    /// </summary>
    public string ExtractedProjectsDirectoryPath => Path.Combine(RootPath, ExtractedProjectDirectoryName);

    /// <summary>
    /// Resolves the archive path used for one uploaded project.
    /// </summary>
    /// <param name="projectId">Project identifier whose archive path should be resolved.</param>
    /// <returns>The absolute archive path for the project.</returns>
    public string ResolveUploadedZipPath(Guid projectId) =>
        Path.Combine(UploadedZipDirectoryPath, $"{projectId:N}.zip");

    /// <summary>
    /// Resolves the root-relative archive path stored for one uploaded project.
    /// </summary>
    /// <param name="projectId">Project identifier whose stored archive path should be resolved.</param>
    /// <returns>The root-relative archive path using forward slashes.</returns>
    public string ResolveUploadedZipRelativePath(Guid projectId) =>
        Path.GetRelativePath(RootPath, ResolveUploadedZipPath(projectId)).Replace('\\', '/');

    /// <summary>
    /// Resolves an absolute archive path from a stored root-relative archive path.
    /// </summary>
    /// <param name="storedZipRelativePath">Stored root-relative archive path.</param>
    /// <returns>The absolute archive path.</returns>
    public string ResolveStoredZipPath(string storedZipRelativePath) =>
        Path.Combine(RootPath, storedZipRelativePath.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// Resolves the extraction directory used for one uploaded project.
    /// </summary>
    /// <param name="projectId">Project identifier whose extraction path should be resolved.</param>
    /// <returns>The absolute extraction directory path for the project.</returns>
    public string ResolveExtractedProjectPath(Guid projectId) =>
        Path.Combine(ExtractedProjectsDirectoryPath, projectId.ToString("N"));

    /// <summary>
    /// Ensures that the temporary storage directory structure exists.
    /// </summary>
    public void EnsureStorageDirectories()
    {
        Directory.CreateDirectory(UploadedZipDirectoryPath);
        Directory.CreateDirectory(ExtractedProjectsDirectoryPath);
    }
}
