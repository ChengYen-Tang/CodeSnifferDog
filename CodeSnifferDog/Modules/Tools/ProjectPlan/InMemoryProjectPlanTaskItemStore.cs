using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Workflows.Common;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

public sealed class InMemoryProjectPlanTaskItemStore : IProjectPlanTaskItemStore
{
    private readonly List<StoredProjectPlanTaskItem> _taskItems = [];
    private readonly Lock _syncRoot = new();
    private Guid? _activeAttemptId;

    public ValueTask<StoredProjectPlanTaskItem> AddAsync(ProjectPlanTaskItem taskItem, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(taskItem);
        ValidateTaskItem(taskItem);

        StoredProjectPlanTaskItem storedTaskItem = new()
        {
            ProjectPlanTaskItemId = Guid.NewGuid().ToString("N"),
            Files = [.. taskItem.Files.Select(file => new ProjectPlanFile
            {
                FilePath = file.FilePath.Trim(),
                TotalLines = file.TotalLines,
            })],
        };

        lock (_syncRoot)
        {
            if (!CanWrite())
                return ValueTask.FromResult(storedTaskItem);

            StoredProjectPlanTaskItem? existingTaskItem = _taskItems.FirstOrDefault(candidate => HaveEquivalentFiles(candidate.Files, storedTaskItem.Files));
            if (existingTaskItem is not null)
                return ValueTask.FromResult(existingTaskItem);

            _taskItems.Add(storedTaskItem);
        }

        return ValueTask.FromResult(storedTaskItem);
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
            if (!CanWrite())
                return ValueTask.FromResult(false);

            StoredProjectPlanTaskItem? existingTaskItem =
                _taskItems.FirstOrDefault(taskItem => taskItem.ProjectPlanTaskItemId == projectPlanTaskItemId);

            if (existingTaskItem is null)
                return ValueTask.FromResult(false);

            _taskItems.Remove(existingTaskItem);
            return ValueTask.FromResult(true);
        }
    }

    public ValueTask<IReadOnlyList<StoredProjectPlanTaskItem>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
            return ValueTask.FromResult<IReadOnlyList<StoredProjectPlanTaskItem>>([.. _taskItems]);
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            if (!CanWrite())
                return ValueTask.CompletedTask;

            _taskItems.Clear();
        }

        return ValueTask.CompletedTask;
    }

    public IAgentAttemptLease BeginAttempt(Guid attemptId)
    {
        lock (_syncRoot)
        {
            Guid staleWriteBlockerAttemptId = Guid.NewGuid();
            List<StoredProjectPlanTaskItem> snapshot = [.. _taskItems.Select(CloneStoredTaskItem)];
            _activeAttemptId = attemptId;

            return new AgentAttemptLease(() =>
            {
                lock (_syncRoot)
                {
                    _activeAttemptId = staleWriteBlockerAttemptId;
                    _taskItems.Clear();
                    _taskItems.AddRange(snapshot.Select(CloneStoredTaskItem));
                }
            });
        }
    }

    private static void ValidateTaskItem(ProjectPlanTaskItem taskItem)
    {
        if (taskItem.Files.Count == 0)
            throw new ArgumentException("At least one project plan file is required.", nameof(taskItem));

        foreach (ProjectPlanFile file in taskItem.Files)
            ValidateFile(file);
    }

    private static void ValidateFile(ProjectPlanFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(file.FilePath);

        if (file.TotalLines <= 0)
            throw new ArgumentOutOfRangeException(nameof(file), "Total lines must be greater than zero.");
    }

    private static bool HaveEquivalentFiles(
        IReadOnlyList<ProjectPlanFile> left,
        IReadOnlyList<ProjectPlanFile> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].FilePath, right[index].FilePath, StringComparison.Ordinal) ||
                left[index].TotalLines != right[index].TotalLines)
                return false;
        }

        return true;
    }

    private bool CanWrite()
    {
        Guid? currentAttemptId = AgentRunAttemptContext.CurrentAttemptId;
        return currentAttemptId is null || _activeAttemptId is null || currentAttemptId == _activeAttemptId;
    }

    private static StoredProjectPlanTaskItem CloneStoredTaskItem(StoredProjectPlanTaskItem taskItem) =>
        new()
        {
            ProjectPlanTaskItemId = taskItem.ProjectPlanTaskItemId,
            Files = [.. taskItem.Files.Select(static file => new ProjectPlanFile
            {
                FilePath = file.FilePath,
                TotalLines = file.TotalLines,
            })],
        };
}
