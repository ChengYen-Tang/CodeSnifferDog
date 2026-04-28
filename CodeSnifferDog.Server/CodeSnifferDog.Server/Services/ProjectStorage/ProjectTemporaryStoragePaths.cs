namespace CodeSnifferDog.Server.Services.ProjectStorage;

public sealed class ProjectTemporaryStoragePaths
{
    private const string TemporaryStorageDirectoryName = "TemporaryStorage";
    private const string UploadedZipDirectoryName = "uploads";
    private const string ExtractedProjectDirectoryName = "extracted";

    public string RootPath => Path.Combine(AppContext.BaseDirectory, TemporaryStorageDirectoryName);

    public string UploadedZipDirectoryPath => Path.Combine(RootPath, UploadedZipDirectoryName);

    public string ExtractedProjectsDirectoryPath => Path.Combine(RootPath, ExtractedProjectDirectoryName);

    public string ResolveUploadedZipPath(Guid projectId) =>
        Path.Combine(UploadedZipDirectoryPath, $"{projectId:N}.zip");

    public string ResolveUploadedZipRelativePath(Guid projectId) =>
        Path.GetRelativePath(RootPath, ResolveUploadedZipPath(projectId)).Replace('\\', '/');

    public string ResolveStoredZipPath(string storedZipRelativePath) =>
        Path.Combine(RootPath, storedZipRelativePath.Replace('/', Path.DirectorySeparatorChar));

    public string ResolveExtractedProjectPath(Guid projectId) =>
        Path.Combine(ExtractedProjectsDirectoryPath, projectId.ToString("N"));

    public void EnsureStorageDirectories()
    {
        Directory.CreateDirectory(UploadedZipDirectoryPath);
        Directory.CreateDirectory(ExtractedProjectsDirectoryPath);
    }
}
