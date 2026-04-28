namespace CodeSnifferDog.Server.Services.ProjectExecution;

public interface IProjectExecutionLeaseRegistry
{
    ProjectExecutionLease Register(Guid projectId, CancellationToken cancellationToken);

    Task<bool> CancelAndWaitAsync(Guid projectId, CancellationToken cancellationToken = default);

    bool TryCancel(Guid projectId, out Task? completion);
}
