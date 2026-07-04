using System.Collections.Concurrent;
using CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure.Cancellation;

namespace CodeSnifferDog.Server.Services.ProjectExecution.Infrastructure;

/// <summary>
/// Tracks active execution leases for projects currently being processed.
/// </summary>
public sealed class LeaseRegistry : ILeaseRegistry
{
    private readonly ConcurrentDictionary<Guid, Lease> _leases = [];

    /// <inheritdoc />
    public Lease Register(Guid projectId, CancellationToken cancellationToken)
    {
        Lease lease = new(projectId, cancellationToken, Remove);

        if (_leases.TryAdd(projectId, lease))
            return lease;

        lease.Dispose();
        throw new InvalidOperationException($"Project {projectId} is already running.");
    }

    /// <inheritdoc />
    public async Task<bool> CancelAndWaitAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!TryCancel(projectId, out Task? completion))
            return false;

        await completion!.WaitAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
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

    /// <summary>
    /// Removes a lease after execution completes.
    /// </summary>
    /// <param name="projectId">Project identifier to remove.</param>
    private void Remove(Guid projectId) => _leases.TryRemove(projectId, out _);
}
