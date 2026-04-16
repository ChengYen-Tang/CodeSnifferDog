using CodeSnifferDog.Models.ProjectPlan;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

public sealed class InMemoryProjectPlanTaskItemStore : IProjectPlanTaskItemStore
{
    private readonly List<StoredProjectPlanTaskItem> _taskItems = [];
    private readonly Lock _syncRoot = new();

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
            _taskItems.Add(storedTaskItem);

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
            _taskItems.Clear();

        return ValueTask.CompletedTask;
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
}
