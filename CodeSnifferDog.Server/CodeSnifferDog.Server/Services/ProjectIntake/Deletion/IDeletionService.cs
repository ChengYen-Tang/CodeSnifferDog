namespace CodeSnifferDog.Server.Services.ProjectIntake.Deletion;

/// <summary>
/// Deletes persisted projects and their temporary storage.
/// </summary>
internal interface IDeletionService
{
    /// <summary>
    /// Deletes one project when it exists.
    /// </summary>
    /// <param name="projectId">Project identifier to delete.</param>
    /// <param name="cancellationToken">Cancels deletion.</param>
    /// <returns><see langword="true" /> when the project existed and was deleted; otherwise <see langword="false" />.</returns>
    Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken);
}
