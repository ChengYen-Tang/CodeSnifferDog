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

        _taskItems.Add(storedTaskItem);
        return storedTaskItem;
    }

    /// <summary>
    /// Deletes a stored task item by identifier.
    /// </summary>
    public bool Delete(string projectPlanTaskItemId)
    {
        StoredTaskItem? existingTaskItem =
            _taskItems.FirstOrDefault(taskItem => taskItem.ProjectPlanTaskItemId == projectPlanTaskItemId);

        if (existingTaskItem is null)
            return false;

        _taskItems.Remove(existingTaskItem);
        return true;
    }

    /// <summary>
    /// Lists the stored task items in insertion order.
    /// </summary>
    public IReadOnlyList<StoredTaskItem> List() =>
        [.. _taskItems];

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
}
