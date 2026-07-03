using CodeSnifferDog.Models.ProjectPlan;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan.State;

internal sealed class TaskItemStateStore
{
    private readonly List<StoredTaskItem> _taskItems = [];

    public StoredTaskItem Add(StoredTaskItem storedTaskItem)
    {
        StoredTaskItem? existingTaskItem = _taskItems
            .FirstOrDefault(candidate => HaveEquivalentFiles(candidate.Files, storedTaskItem.Files));
        if (existingTaskItem is not null)
            return existingTaskItem;

        _taskItems.Add(storedTaskItem);
        return storedTaskItem;
    }

    public bool Delete(string projectPlanTaskItemId)
    {
        StoredTaskItem? existingTaskItem =
            _taskItems.FirstOrDefault(taskItem => taskItem.ProjectPlanTaskItemId == projectPlanTaskItemId);

        if (existingTaskItem is null)
            return false;

        _taskItems.Remove(existingTaskItem);
        return true;
    }

    public IReadOnlyList<StoredTaskItem> List() =>
        [.. _taskItems];

    public void Clear() =>
        _taskItems.Clear();

    public IReadOnlyList<StoredTaskItem> Clone() =>
        [.. _taskItems.Select(CloneStoredTaskItem)];

    public void Restore(IReadOnlyList<StoredTaskItem> snapshot)
    {
        _taskItems.Clear();
        _taskItems.AddRange(snapshot.Select(CloneStoredTaskItem));
    }

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

    private static void Validate(TaskItem taskItem)
    {
        if (taskItem.Files.Count == 0)
            throw new ArgumentException("At least one project plan file is required.", nameof(taskItem));

        foreach (PlanFile file in taskItem.Files)
            ValidateFile(file);
    }

    private static void ValidateFile(PlanFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(file.FilePath);

        if (file.TotalLines <= 0)
            throw new ArgumentOutOfRangeException(nameof(file), "Total lines must be greater than zero.");
    }

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
