namespace CodeSnifferDog.Server.Services.ProjectIntake.Deletion;

internal interface IDeletionService
{
    Task<bool> DeleteAsync(Guid projectId, CancellationToken cancellationToken);
}
