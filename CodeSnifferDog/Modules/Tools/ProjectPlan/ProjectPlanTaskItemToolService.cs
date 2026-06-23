using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

internal sealed class ProjectPlanTaskItemToolService(IProjectPlanTaskItemStore taskItemStore)
{
    private readonly IProjectPlanTaskItemStore _taskItemStore = taskItemStore;

    public async ValueTask<AddProjectPlanTaskItemResult> AddProjectPlanTaskItemAsync(
        AddProjectPlanTaskItemArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ValidateFiles(args.Files, nameof(args));

        StoredProjectPlanTaskItem storedTaskItem = await _taskItemStore.AddAsync(
            CreateTaskItem(args),
            cancellationToken).ConfigureAwait(false);

        return new AddProjectPlanTaskItemResult
        {
            ProjectPlanTaskItemId = storedTaskItem.ProjectPlanTaskItemId,
        };
    }

    public async ValueTask<AddProjectPlanTaskItemsResult> AddProjectPlanTaskItemsAsync(
        AddProjectPlanTaskItemsArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.TaskItems.Count == 0)
            throw new ArgumentException("At least one project plan task item is required.", nameof(args));

        foreach (AddProjectPlanTaskItemArgs taskItem in args.TaskItems)
            ValidateFiles(taskItem.Files, nameof(args));

        IReadOnlyList<StoredProjectPlanTaskItem> storedTaskItems = await _taskItemStore.AddRangeAsync(
            [.. args.TaskItems.Select(CreateTaskItem)],
            cancellationToken).ConfigureAwait(false);

        return new AddProjectPlanTaskItemsResult
        {
            ProjectPlanTaskItemIds = [.. storedTaskItems.Select(taskItem => taskItem.ProjectPlanTaskItemId)],
        };
    }

    public ValueTask<bool> DeleteProjectPlanTaskItemAsync(
        DeleteProjectPlanTaskItemArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectPlanTaskItemId);
        return _taskItemStore.DeleteAsync(args.ProjectPlanTaskItemId.Trim(), cancellationToken);
    }

    public ValueTask<IReadOnlyList<StoredProjectPlanTaskItem>> ListProjectPlanTaskItemsAsync(CancellationToken cancellationToken)
        =>
        _taskItemStore.ListAsync(cancellationToken);

    private static ProjectPlanTaskItem CreateTaskItem(AddProjectPlanTaskItemArgs args) =>
        new()
        {
            Files = [.. args.Files.Select(CreateNormalizedFile)],
        };

    private static ProjectPlanFile CreateNormalizedFile(ProjectPlanFile file)
        =>
        new()
        {
            FilePath = file.FilePath.Trim(),
            TotalLines = file.TotalLines,
        };

    private static void ValidateFiles(IReadOnlyList<ProjectPlanFile> files, string paramName)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0)
            throw new ArgumentException("At least one project plan file is required.", paramName);

        foreach (ProjectPlanFile file in files)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentException.ThrowIfNullOrWhiteSpace(file.FilePath);

            if (file.TotalLines <= 0)
                throw new ArgumentOutOfRangeException(paramName, "Total lines must be greater than zero.");
        }
    }
}
