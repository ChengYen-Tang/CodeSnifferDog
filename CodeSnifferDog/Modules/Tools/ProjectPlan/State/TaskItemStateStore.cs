using CodeSnifferDog.Models.ProjectPlan;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan.State;

/// <summary>
/// Stores project-plan task items for the current workflow run and supports snapshot rollback.
/// </summary>
internal sealed class TaskItemStateStore
{
    private readonly List<StoredTaskItem> _taskItems = [];

    /// <summary>
    /// Adds a stored task item unless an equivalent item already exists.
    /// </summary>
    public StoredTaskItem Add(StoredTaskItem storedTaskItem)
    {
        StoredTaskItem? existingTaskItem = _taskItems
            .FirstOrDefault(candidate => HaveEquivalentFiles(candidate.Files, storedTaskItem.Files));
        if (existingTaskItem is not null)
            return existingTaskItem;

        _taskItems.Insert(FindInsertionIndex(storedTaskItem.ProjectPlanTaskItemId), storedTaskItem);
        return storedTaskItem;
    }

    /// <summary>
    /// Gets one stored task item by its identifier.
    /// </summary>
    public StoredTaskItem Get(string projectPlanTaskItemId)
    {
        int index = FindIndex(projectPlanTaskItemId.Trim());

        return index >= 0
            ? _taskItems[index]
            : throw new KeyNotFoundException($"Project plan task item was not found: {projectPlanTaskItemId}");
    }

    /// <summary>
    /// Deletes a stored task item by identifier.
    /// </summary>
    public bool Delete(string projectPlanTaskItemId)
    {
        int index = FindIndex(projectPlanTaskItemId.Trim());

        if (index < 0)
            return false;

        _taskItems.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Lists all stored task items for internal workflow aggregation.
    /// </summary>
    public IReadOnlyList<StoredTaskItem> ListAll() =>
        [.. _taskItems];

    /// <summary>
    /// Lists at most <paramref name="take"/> stored task items after <paramref name="cursor"/>.
    /// </summary>
    public IReadOnlyList<StoredTaskItem> ListPage(string? cursor, int take)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);

        int startIndex = string.IsNullOrWhiteSpace(cursor)
            ? 0
            : FindFirstAfter(cursor.Trim());
        int count = Math.Min(take, _taskItems.Count - startIndex);

        return count == 0
            ? []
            : _taskItems.GetRange(startIndex, count);
    }

    /// <summary>
    /// Clears all stored task items.
    /// </summary>
    public void Clear() =>
        _taskItems.Clear();

    /// <summary>
    /// Creates a snapshot clone of all stored task items.
    /// </summary>
    public IReadOnlyList<StoredTaskItem> Clone() =>
        [.. _taskItems.Select(CloneStoredTaskItem)];

    /// <summary>
    /// Restores the store from a cloned snapshot.
    /// </summary>
    public void Restore(IReadOnlyList<StoredTaskItem> snapshot)
    {
        _taskItems.Clear();
        _taskItems.AddRange(snapshot.Select(CloneStoredTaskItem));
        _taskItems.Sort(static (left, right) => string.CompareOrdinal(
            left.ProjectPlanTaskItemId,
            right.ProjectPlanTaskItemId));
    }

    /// <summary>
    /// Creates a stored task item from a task item and generated identifier.
    /// </summary>
    public static StoredTaskItem CreateStoredTaskItem(TaskItem taskItem, string projectPlanTaskItemId)
    {
        Validate(taskItem);

        return new StoredTaskItem
        {
            ProjectPlanTaskItemId = projectPlanTaskItemId,
            Files = [.. taskItem.Files.Select(file => new PlanFile
            {
                FilePath = file.FilePath.Trim(),
                TotalLines = file.TotalLines,
            })],
        };
    }

    /// <summary>
    /// Validates one task item.
    /// </summary>
    private static void Validate(TaskItem taskItem)
    {
        if (taskItem.Files.Count == 0)
            throw new ArgumentException("At least one project plan file is required.", nameof(taskItem));

        foreach (PlanFile file in taskItem.Files)
            ValidateFile(file);
    }

    /// <summary>
    /// Validates one plan file.
    /// </summary>
    private static void ValidateFile(PlanFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(file.FilePath);

        if (file.TotalLines <= 0)
            throw new ArgumentOutOfRangeException(nameof(file), "Total lines must be greater than zero.");
    }

    /// <summary>
    /// Determines whether two plan-file lists are equivalent.
    /// </summary>
    private static bool HaveEquivalentFiles(
        IReadOnlyList<PlanFile> left,
        IReadOnlyList<PlanFile> right)
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

    /// <summary>
    /// Clones one stored task item.
    /// </summary>
    private static StoredTaskItem CloneStoredTaskItem(StoredTaskItem taskItem) =>
        new()
        {
            ProjectPlanTaskItemId = taskItem.ProjectPlanTaskItemId,
            Files = [.. taskItem.Files.Select(static file => new PlanFile
            {
                FilePath = file.FilePath,
                TotalLines = file.TotalLines,
            })],
        };

    /// <summary>
    /// Finds the index of the specified task item identifier.
    /// </summary>
    private int FindIndex(string projectPlanTaskItemId)
    {
        int index = FindInsertionIndex(projectPlanTaskItemId);
        return index < _taskItems.Count && string.Equals(
            _taskItems[index].ProjectPlanTaskItemId,
            projectPlanTaskItemId,
            StringComparison.Ordinal)
            ? index
            : -1;
    }

    /// <summary>
    /// Finds the first insertion position for a task item identifier.
    /// </summary>
    private int FindInsertionIndex(string projectPlanTaskItemId)
    {
        int low = 0;
        int high = _taskItems.Count;

        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (string.CompareOrdinal(_taskItems[middle].ProjectPlanTaskItemId, projectPlanTaskItemId) < 0)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }

    /// <summary>
    /// Finds the first task item whose identifier sorts after the supplied cursor.
    /// </summary>
    private int FindFirstAfter(string cursor)
    {
        int low = 0;
        int high = _taskItems.Count;

        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (string.CompareOrdinal(_taskItems[middle].ProjectPlanTaskItemId, cursor) <= 0)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }
}
