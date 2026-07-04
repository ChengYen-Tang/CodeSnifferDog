namespace CodeSnifferDog.Server.Services.ProjectIntake.Upload;

/// <summary>
/// Stores uploaded project zip files in temporary storage.
/// </summary>
internal interface IUploadService
{
    /// <summary>
    /// Stores one uploaded zip file and returns its artifact description.
    /// </summary>
    /// <param name="projectId">Project identifier used to determine storage paths.</param>
    /// <param name="zipFile">Uploaded zip file.</param>
    /// <param name="cancellationToken">Cancels file storage.</param>
    /// <returns>The stored artifact description.</returns>
    Task<Artifact> StoreAsync(Guid projectId, IFormFile zipFile, CancellationToken cancellationToken);

    /// <summary>
    /// Tries to delete a previously stored artifact file.
    /// </summary>
    /// <param name="artifact">Artifact whose stored file should be deleted.</param>
    void TryDeleteStoredFile(Artifact artifact);
}
