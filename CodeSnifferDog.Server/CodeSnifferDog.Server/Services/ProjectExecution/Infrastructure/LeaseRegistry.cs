using System.Collections.Concurrent;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

public sealed class LeaseRegistry : ILeaseRegistry
{
    private readonly ConcurrentDictionary<Guid, Lease> _leases = [];

    public Lease Register(Guid projectId, CancellationToken cancellationToken)
    {
        Lease lease = new(projectId, cancellationToken, Remove);

        if (_leases.TryAdd(projectId, lease))
            return lease;

        lease.Dispose();
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

        if (!_leases.TryGetValue(projectId, out Lease? lease))
            return false;

        if (!lease.TryCancel(Source.UserRequest))
            return false;

        completion = lease.Completion;
        return true;
    }

    private void Remove(Guid projectId) => _leases.TryRemove(projectId, out _);
}
