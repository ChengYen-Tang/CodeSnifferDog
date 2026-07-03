namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

public interface ILeaseRegistry
{
    Lease Register(Guid projectId, CancellationToken cancellationToken);

    Task<bool> CancelAndWaitAsync(Guid projectId, CancellationToken cancellationToken = default);

    bool TryCancel(Guid projectId, out Task? completion);
}
