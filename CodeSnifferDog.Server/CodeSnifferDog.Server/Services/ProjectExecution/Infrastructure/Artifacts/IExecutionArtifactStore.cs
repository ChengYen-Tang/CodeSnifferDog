using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Artifacts;

internal interface IExecutionArtifactStore
{
    string PrepareRepository(ProjectExecutionClaim claim);

    bool StoredZipExists(string storedZipRelativePath);

    bool ExtractedProjectExists(Guid projectId);

    void TryDeleteExtractedProjectDirectory(Guid projectId);

    void TryDeleteUploadedZipFile(string storedZipRelativePath, Guid projectId);
}
