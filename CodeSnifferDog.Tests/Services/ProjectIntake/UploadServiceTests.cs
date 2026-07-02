using CodeSnifferDog.Server.Services.ProjectIntake.Upload;
using CodeSnifferDog.Server.Services.ProjectStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeSnifferDog.Tests.Services.ProjectIntake;

[TestClass]
public sealed class UploadServiceTests
{
    public required TestContext TestContext { get; init; }

    [TestMethod]
    public async Task StoreAsync_StoresZipAndReturnsArtifact()
    {
        UploadService service = CreateService();
        Guid projectId = Guid.NewGuid();
        FormFile zipFile = CreateFormFile("repo.zip", "content");

        Artifact artifact = await service.StoreAsync(projectId, zipFile, TestContext.CancellationToken);

        Assert.AreEqual("repo.zip", artifact.OriginalFileName);
        Assert.AreEqual(zipFile.Length, artifact.FileSizeBytes);
        Assert.IsTrue(File.Exists(artifact.StoredFilePath));
        Assert.AreEqual($"uploads/{projectId:N}.zip", artifact.StoredZipRelativePath);

        service.TryDeleteStoredFile(artifact);
    }

    [TestMethod]
    public async Task StoreAsync_WhenFileIsEmpty_ThrowsOriginalException()
    {
        UploadService service = CreateService();
        FormFile zipFile = CreateFormFile("repo.zip", string.Empty);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.StoreAsync(Guid.NewGuid(), zipFile, TestContext.CancellationToken));

        Assert.AreEqual("The uploaded zip file is empty.", exception.Message);
    }

    [TestMethod]
    public async Task StoreAsync_WhenExtensionIsNotZip_ThrowsOriginalException()
    {
        UploadService service = CreateService();
        FormFile zipFile = CreateFormFile("repo.txt", "content");

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.StoreAsync(Guid.NewGuid(), zipFile, TestContext.CancellationToken));

        Assert.AreEqual("Only .zip uploads are supported.", exception.Message);
    }

    [TestMethod]
    public async Task StoreAsync_WhenCopyFails_DeletesPartialFile()
    {
        UploadService service = CreateService();
        Guid projectId = Guid.NewGuid();
        ProjectTemporaryStoragePaths storagePaths = new();
        string storedFilePath = storagePaths.ResolveUploadedZipPath(projectId);

        await Assert.ThrowsExactlyAsync<IOException>(
            () => service.StoreAsync(projectId, new ThrowingFormFile("repo.zip", length: 10), TestContext.CancellationToken));

        Assert.IsFalse(File.Exists(storedFilePath));
    }

    private static UploadService CreateService() =>
        new(new ProjectTemporaryStoragePaths(), NullLogger<UploadService>.Instance);

    private static FormFile CreateFormFile(string fileName, string content)
    {
        MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(content));
        return new FormFile(stream, 0, stream.Length, "file", fileName);
    }

    private sealed class ThrowingFormFile(string fileName, long length) : IFormFile
    {
        public string ContentType => "application/zip";

        public string ContentDisposition => string.Empty;

        public IHeaderDictionary Headers { get; } = new HeaderDictionary();

        public long Length { get; } = length;

        public string Name => "file";

        public string FileName { get; } = fileName;

        public void CopyTo(Stream target) => throw new IOException("copy failed");

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default) =>
            throw new IOException("copy failed");

        public Stream OpenReadStream() => throw new IOException("copy failed");
    }
}
