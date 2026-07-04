using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Queue;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Artifacts;

/// <summary>
/// Manages uploaded and extracted project artifacts used during execution.
/// </summary>
internal interface IExecutionArtifactStore
{
    /// <summary>
    /// Ensures the repository for a claimed project is available on disk and returns its root path.
    /// </summary>
    /// <param name="claim">Claim that identifies the project and uploaded archive.</param>
    /// <returns>The extracted repository root path.</returns>
    /// <exception cref="FileNotFoundException">Thrown when neither the uploaded archive nor the extracted repository exists.</exception>
    string PrepareRepository(Claim claim);

    /// <summary>
    /// Checks whether the uploaded zip still exists for a project.
    /// </summary>
    /// <param name="storedZipRelativePath">Relative path to the stored upload archive.</param>
    /// <returns><see langword="true"/> when the uploaded zip exists; otherwise, <see langword="false"/>.</returns>
    bool StoredZipExists(string storedZipRelativePath);

    /// <summary>
    /// Checks whether the extracted repository directory exists for a project.
    /// </summary>
    /// <param name="projectId">Project identifier.</param>
    /// <returns><see langword="true"/> when the extracted repository exists; otherwise, <see langword="false"/>.</returns>
    bool ExtractedProjectExists(Guid projectId);

    /// <summary>
    /// Attempts to delete the extracted repository directory for a project.
    /// </summary>
    /// <param name="projectId">Project identifier.</param>
    void TryDeleteExtractedProjectDirectory(Guid projectId);

    /// <summary>
    /// Attempts to delete the uploaded zip file for a project.
    /// </summary>
    /// <param name="storedZipRelativePath">Relative path to the stored upload archive.</param>
    /// <param name="projectId">Project identifier used for logging.</param>
    void TryDeleteUploadedZipFile(string storedZipRelativePath, Guid projectId);
}
