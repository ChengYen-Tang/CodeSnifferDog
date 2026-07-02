namespace CodeSnifferDog.Server.Services.ProjectIntake.Upload;

internal interface IUploadService
{
    Task<Artifact> StoreAsync(Guid projectId, IFormFile zipFile, CancellationToken cancellationToken);

    void TryDeleteStoredFile(Artifact artifact);
}
