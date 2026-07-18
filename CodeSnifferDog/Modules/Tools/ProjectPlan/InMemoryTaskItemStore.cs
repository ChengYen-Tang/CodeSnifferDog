using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Attempts;
using CodeSnifferDog.Modules.Tools.ProjectPlan.State;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

/// <summary>
/// Stores project-plan task items in memory with retry-safe rollback support.
/// </summary>
public sealed class InMemoryTaskItemStore : ITaskItemStore
{
    private readonly TaskItemStateStore _stateStore = new();
    private readonly AttemptWriteGuard _writeGuard = new();
    private readonly Lock _syncRoot = new();

    /// <inheritdoc />
    public ValueTask<StoredTaskItem> AddAsync(TaskItem taskItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taskItem);
        StoredTaskItem storedTaskItem = TaskItemStateStore.CreateStoredTaskItem(
            taskItem,
            Guid.CreateVersion7().ToString("N"));

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite())
                return ValueTask.FromResult(storedTaskItem);

            return ValueTask.FromResult(_stateStore.Add(storedTaskItem));
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<StoredTaskItem>> AddRangeAsync(
        IReadOnlyList<TaskItem> taskItems,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taskItems);

        if (taskItems.Count == 0)
            throw new ArgumentException("At least one project plan task item is required.", nameof(taskItems));

        List<StoredTaskItem> storedTaskItems = [];

        foreach (TaskItem taskItem in taskItems)
            storedTaskItems.Add(await AddAsync(taskItem, cancellationToken).ConfigureAwait(false));

        return storedTaskItems;
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(string projectPlanTaskItemId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPlanTaskItemId);

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite())
                return ValueTask.FromResult(false);

            return ValueTask.FromResult(_stateStore.Delete(projectPlanTaskItemId));
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<StoredTaskItem>> ListAsync(CancellationToken cancellationToken)
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
            IReadOnlyList<StoredTaskItem> snapshot = _stateStore.Clone();
            return _writeGuard.BeginAttempt(attemptId, () => _stateStore.Restore(snapshot));
        }
    }
}
