using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Modules.Tools.Attempts;
using CodeSnifferDog.Modules.Tools.ProjectPlan.State;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

public sealed class InMemoryProjectPlanTaskItemStore : IProjectPlanTaskItemStore
{
    private readonly ProjectPlanTaskItemStateStore _stateStore = new();
    private readonly AttemptWriteGuard _writeGuard = new();
    private readonly Lock _syncRoot = new();

    public ValueTask<StoredProjectPlanTaskItem> AddAsync(ProjectPlanTaskItem taskItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taskItem);
        StoredProjectPlanTaskItem storedTaskItem = ProjectPlanTaskItemStateStore.CreateStoredTaskItem(
            taskItem,
            Guid.NewGuid().ToString("N"));

        lock (_syncRoot)
        {
            if (!_writeGuard.CanWrite())
                return ValueTask.FromResult(storedTaskItem);

            return ValueTask.FromResult(_stateStore.Add(storedTaskItem));
        }
    }

    public async ValueTask<IReadOnlyList<StoredProjectPlanTaskItem>> AddRangeAsync(
        IReadOnlyList<ProjectPlanTaskItem> taskItems,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taskItems);

        if (taskItems.Count == 0)
            throw new ArgumentException("At least one project plan task item is required.", nameof(taskItems));

        List<StoredProjectPlanTaskItem> storedTaskItems = [];

        foreach (ProjectPlanTaskItem taskItem in taskItems)
            storedTaskItems.Add(await AddAsync(taskItem, cancellationToken).ConfigureAwait(false));

        return storedTaskItems;
    }

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

    public ValueTask<IReadOnlyList<StoredProjectPlanTaskItem>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
            return ValueTask.FromResult(_stateStore.List());
    }

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

    public IAgentAttemptLease BeginAttempt(Guid attemptId)
    {
        lock (_syncRoot)
        {
            IReadOnlyList<StoredProjectPlanTaskItem> snapshot = _stateStore.Clone();
            return _writeGuard.BeginAttempt(attemptId, () => _stateStore.Restore(snapshot));
        }
    }
}
