using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Artifacts;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;
using CodeSnifferDog.Server.Services.ProjectStorage;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Compression;

namespace CodeSnifferDog.Tests.Services.ProjectExecution;

[TestClass]
public sealed class ExecutionArtifactStoreTests
{
    [TestMethod]
    public void PrepareRepository_WhenUploadedZipExists_ExtractsRepositoryAndDeletesZip()
    {
        ProjectTemporaryStoragePaths storagePaths = CreateStoragePaths();
        ExecutionArtifactStore store = CreateStore(storagePaths);
        Guid projectId = Guid.NewGuid();
        string storedZipRelativePath = storagePaths.ResolveUploadedZipRelativePath(projectId);
        string uploadedZipPath = storagePaths.ResolveStoredZipPath(storedZipRelativePath);
        CreateZip(uploadedZipPath, "Program.cs", "class Program {}");

        string repositoryPath = store.PrepareRepository(CreateClaim(projectId, storedZipRelativePath));

        Assert.AreEqual(storagePaths.ResolveExtractedProjectPath(projectId), repositoryPath);
        Assert.IsTrue(File.Exists(Path.Combine(repositoryPath, "Program.cs")));
        Assert.IsFalse(File.Exists(uploadedZipPath));
    }

    [TestMethod]
    public void PrepareRepository_WhenExtractedRepositoryExists_ReturnsExistingPath()
    {
        ProjectTemporaryStoragePaths storagePaths = CreateStoragePaths();
        ExecutionArtifactStore store = CreateStore(storagePaths);
        Guid projectId = Guid.NewGuid();
        string extractedPath = storagePaths.ResolveExtractedProjectPath(projectId);
        Directory.CreateDirectory(extractedPath);

        string repositoryPath = store.PrepareRepository(CreateClaim(
            projectId,
            storagePaths.ResolveUploadedZipRelativePath(projectId)));

        Assert.AreEqual(extractedPath, repositoryPath);
    }

    [TestMethod]
    public void PrepareRepository_WhenArtifactsAreMissing_ThrowsOriginalFileNotFoundException()
    {
        ProjectTemporaryStoragePaths storagePaths = CreateStoragePaths();
        ExecutionArtifactStore store = CreateStore(storagePaths);
        Guid projectId = Guid.NewGuid();

        Assert.ThrowsExactly<FileNotFoundException>(() => store.PrepareRepository(CreateClaim(
            projectId,
            storagePaths.ResolveUploadedZipRelativePath(projectId))));
    }

    [TestMethod]
    public void DeleteMethods_WhenArtifactDoesNotExist_DoNotThrow()
    {
        ProjectTemporaryStoragePaths storagePaths = CreateStoragePaths();
        ExecutionArtifactStore store = CreateStore(storagePaths);
        Guid projectId = Guid.NewGuid();

        store.TryDeleteUploadedZipFile(storagePaths.ResolveUploadedZipRelativePath(projectId), projectId);
        store.TryDeleteExtractedProjectDirectory(projectId);
    }

    private static ExecutionArtifactStore CreateStore(ProjectTemporaryStoragePaths storagePaths) =>
        new(storagePaths, NullLogger<ExecutionArtifactStore>.Instance);

    private static ProjectTemporaryStoragePaths CreateStoragePaths()
    {
        ProjectTemporaryStoragePaths storagePaths = new();
        storagePaths.EnsureStorageDirectories();
        return storagePaths;
    }

    private static Claim CreateClaim(Guid projectId, string storedZipRelativePath) =>
        new(projectId, storedZipRelativePath, new Lease(projectId, CancellationToken.None, static _ => { }));

    private static void CreateZip(string zipPath, string entryName, string content)
    {
        string sourceDirectory = Path.Combine(Path.GetTempPath(), $"codesnifferdog-zip-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceDirectory);
        try
        {
            File.WriteAllText(Path.Combine(sourceDirectory, entryName), content);
            ZipFile.CreateFromDirectory(sourceDirectory, zipPath);
        }
        finally
        {
            if (Directory.Exists(sourceDirectory))
                Directory.Delete(sourceDirectory, recursive: true);
        }
    }
}
