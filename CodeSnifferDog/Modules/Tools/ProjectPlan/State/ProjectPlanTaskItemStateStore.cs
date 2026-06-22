using CodeSnifferDog.Models.ProjectPlan;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan.State;

internal sealed class ProjectPlanTaskItemStateStore
{
    private readonly List<StoredProjectPlanTaskItem> _taskItems = [];

    public StoredProjectPlanTaskItem Add(StoredProjectPlanTaskItem storedTaskItem)
    {
        StoredProjectPlanTaskItem? existingTaskItem = _taskItems
            .FirstOrDefault(candidate => HaveEquivalentFiles(candidate.Files, storedTaskItem.Files));
        if (existingTaskItem is not null)
            return existingTaskItem;

        _taskItems.Add(storedTaskItem);
        return storedTaskItem;
    }

    public bool Delete(string projectPlanTaskItemId)
    {
        StoredProjectPlanTaskItem? existingTaskItem =
            _taskItems.FirstOrDefault(taskItem => taskItem.ProjectPlanTaskItemId == projectPlanTaskItemId);

        if (existingTaskItem is null)
            return false;

        _taskItems.Remove(existingTaskItem);
        return true;
    }

    public IReadOnlyList<StoredProjectPlanTaskItem> List() =>
        [.. _taskItems];

    public void Clear() =>
        _taskItems.Clear();

    public IReadOnlyList<StoredProjectPlanTaskItem> Clone() =>
        [.. _taskItems.Select(CloneStoredTaskItem)];

    public void Restore(IReadOnlyList<StoredProjectPlanTaskItem> snapshot)
    {
        _taskItems.Clear();
        _taskItems.AddRange(snapshot.Select(CloneStoredTaskItem));
    }

    public static StoredProjectPlanTaskItem CreateStoredTaskItem(ProjectPlanTaskItem taskItem, string projectPlanTaskItemId)
    {
        Validate(taskItem);

        return new StoredProjectPlanTaskItem
        {
            ProjectPlanTaskItemId = projectPlanTaskItemId,
            Files = [.. taskItem.Files.Select(file => new ProjectPlanFile
            {
                FilePath = file.FilePath.Trim(),
                TotalLines = file.TotalLines,
            })],
        };
    }

    private static void Validate(ProjectPlanTaskItem taskItem)
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
