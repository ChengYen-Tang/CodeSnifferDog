namespace CodeSnifferDog.Server.Services.ProjectIntake.Upload;

internal interface IProjectUploadService
{
    Task<ProjectUploadArtifact> StoreAsync(Guid projectId, IFormFile zipFile, CancellationToken cancellationToken);

    void TryDeleteStoredFile(ProjectUploadArtifact artifact);
}
