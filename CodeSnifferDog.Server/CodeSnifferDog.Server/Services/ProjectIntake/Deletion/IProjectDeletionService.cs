namespace CodeSnifferDog.Server.Services.ProjectIntake.Deletion;

internal interface IProjectDeletionService
{
    Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken);
}
