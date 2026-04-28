using System.Collections.Concurrent;

namespace CodeSnifferDog.Server.Services.ProjectExecution;

public sealed class ProjectExecutionLeaseRegistry : IProjectExecutionLeaseRegistry
{
    private readonly ConcurrentDictionary<Guid, ProjectExecutionLease> _leases = [];

    public ProjectExecutionLease Register(Guid projectId, CancellationToken cancellationToken)
    {
        CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ProjectExecutionLease lease = new(projectId, cancellationTokenSource, Remove);

        if (_leases.TryAdd(projectId, lease))
            return lease;

        cancellationTokenSource.Dispose();
        throw new InvalidOperationException($"Project {projectId} is already running.");
    }

    public async Task<bool> CancelAndWaitAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!TryCancel(projectId, out Task? completion))
            return false;

        await completion!.WaitAsync(cancellationToken);
        return true;
    }

    public bool TryCancel(Guid projectId, out Task? completion)
    {
        completion = null;

        if (!_leases.TryGetValue(projectId, out ProjectExecutionLease? lease))
            return false;

        if (!lease.TryCancel())
            return false;

        completion = lease.Completion;
        return true;
    }

    private void Remove(Guid projectId) => _leases.TryRemove(projectId, out _);
}
