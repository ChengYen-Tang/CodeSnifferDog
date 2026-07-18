using CodeSnifferDog.Models.Scan;
using CodeSnifferDog.Modules.Tools.Attempts;
using CodeSnifferDog.Modules.Tools.Scan.State;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.Scan;

/// <summary>
/// Stores scan projects in memory with retry-safe rollback support.
/// </summary>
public sealed class InMemoryScanProjectStore : IScanProjectStore
{
    private readonly ScanProjectStateStore _stateStore = new();
    private readonly AttemptWriteGuard _writeGuard = new();
    private readonly Lock _syncRoot = new();

    /// <inheritdoc />
    public ValueTask<StoredScanProject> AddAsync(ScanProject project, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);
        StoredScanProject storedProject = ScanProjectStateStore.CreateStoredProject(
            project,
            Guid.CreateVersion7().ToString("N"));

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite())
                return ValueTask.FromResult(storedProject);

            return ValueTask.FromResult(_stateStore.Add(storedProject));
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<StoredScanProject>> AddRangeAsync(
        IReadOnlyList<ScanProject> projects,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projects);

        if (projects.Count == 0)
            throw new ArgumentException("At least one scan project is required.", nameof(projects));

        List<StoredScanProject> storedProjects = [];

        foreach (ScanProject project in projects)
            storedProjects.Add(await AddAsync(project, cancellationToken).ConfigureAwait(false));

        return storedProjects;
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(string scanProjectId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scanProjectId);

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite())
                return ValueTask.FromResult(false);

            return ValueTask.FromResult(_stateStore.Delete(scanProjectId));
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<StoredScanProject>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(_stateStore.List());
    }

    /// <inheritdoc />
    public ValueTask ClearAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite())
                return ValueTask.CompletedTask;

            _stateStore.Clear();
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public IAgentAttemptLease BeginAttempt(Guid attemptId)
    {
        lock (_syncRoot)
        {
            IReadOnlyList<StoredScanProject> snapshot = _stateStore.Clone();
            return _writeGuard.BeginAttempt(attemptId, () => _stateStore.Restore(snapshot));
        }
    }
}
