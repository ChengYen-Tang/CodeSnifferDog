using CodeSnifferDog.Models.ProjectPlan;
using CodeSnifferDog.Models.ProjectPlan.Tools;
using CodeSnifferDog.Models.ProjectPlan.Tools.Listing;
using CodeSnifferDog.Modules.Tools.ProjectPlan.Listing;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

/// <summary>
/// Validates project-plan tool arguments and delegates storage operations to <see cref="ITaskItemStore" />.
/// </summary>
internal sealed class TaskItemToolService(ITaskItemStore taskItemStore)
{
    private readonly ITaskItemStore _taskItemStore = taskItemStore;

    /// <summary>
    /// Adds one project-plan task item.
    /// </summary>
    public async ValueTask<AddProjectPlanTaskItemResult> AddProjectPlanTaskItemAsync(
        AddProjectPlanTaskItemArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ValidateFiles(args.Files, nameof(args));

        StoredTaskItem storedTaskItem = await _taskItemStore.AddAsync(
            CreateTaskItem(args),
            cancellationToken).ConfigureAwait(false);

        return new AddProjectPlanTaskItemResult
        {
            ProjectPlanTaskItemId = storedTaskItem.ProjectPlanTaskItemId,
        };
    }

    /// <summary>
    /// Adds multiple project-plan task items.
    /// </summary>
    public async ValueTask<AddProjectPlanTaskItemsResult> AddProjectPlanTaskItemsAsync(
        AddProjectPlanTaskItemsArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.TaskItems.Count == 0)
            throw new ArgumentException("At least one project plan task item is required.", nameof(args));

        foreach (AddProjectPlanTaskItemArgs taskItem in args.TaskItems)
            ValidateFiles(taskItem.Files, nameof(args));

        IReadOnlyList<StoredTaskItem> storedTaskItems = await _taskItemStore.AddRangeAsync(
            [.. args.TaskItems.Select(CreateTaskItem)],
            cancellationToken).ConfigureAwait(false);

        return new AddProjectPlanTaskItemsResult
        {
            ProjectPlanTaskItemIds = [.. storedTaskItems.Select(taskItem => taskItem.ProjectPlanTaskItemId)],
        };
    }

    /// <summary>
    /// Deletes one stored project-plan task item.
    /// </summary>
    public ValueTask<bool> DeleteProjectPlanTaskItemAsync(
        DeleteProjectPlanTaskItemArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectPlanTaskItemId);
        return _taskItemStore.DeleteAsync(args.ProjectPlanTaskItemId.Trim(), cancellationToken);
    }

    /// <summary>
    /// Lists one bounded page of project-plan task item indexes.
    /// </summary>
    public async ValueTask<TaskItemPage> ListProjectPlanTaskItemsAsync(
        ListTaskItemsArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        int pageSize = args.PageSize ?? TaskItemPage.DefaultPageSize;
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, TaskItemPage.MaxPageSize);

        string? cursor = string.IsNullOrWhiteSpace(args.Cursor)
            ? null
            : args.Cursor.Trim();
        IReadOnlyList<StoredTaskItem> storedTaskItems = await _taskItemStore.ListPageAsync(
            cursor,
            pageSize + 1,
            cancellationToken).ConfigureAwait(false);

        return TaskItemPageFactory.Create(storedTaskItems, pageSize);
    }

    /// <summary>
    /// Lists one bounded page of file indexes for a selected project-plan task item.
    /// </summary>
    public async ValueTask<FilePage> ListProjectPlanTaskItemFilesAsync(
        ListFilesArgs args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentException.ThrowIfNullOrWhiteSpace(args.ProjectPlanTaskItemId);

        int offset = args.Offset ?? 0;
        ArgumentOutOfRangeException.ThrowIfLessThan(offset, 0);
        int pageSize = args.PageSize ?? FilePage.DefaultPageSize;
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, FilePage.MaxPageSize);

        StoredTaskItem taskItem = await _taskItemStore.GetAsync(
            args.ProjectPlanTaskItemId.Trim(),
            cancellationToken).ConfigureAwait(false);
        return FilePageFactory.Create(taskItem, offset, pageSize);
    }

    /// <summary>
    /// Creates a normalized task item from tool arguments.
    /// </summary>
    private static TaskItem CreateTaskItem(AddProjectPlanTaskItemArgs args) =>
        new()
        {
            Files = [.. args.Files.Select(CreateNormalizedFile)],
        };

    /// <summary>
    /// Creates a normalized plan file from tool arguments.
    /// </summary>
    private static PlanFile CreateNormalizedFile(PlanFile file)
        =>
        new()
        {
            FilePath = file.FilePath.Trim(),
            TotalLines = file.TotalLines,
        };

    /// <summary>
    /// Validates one plan-file list.
    /// </summary>
    private static void ValidateFiles(IReadOnlyList<PlanFile> files, string paramName)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0)
            throw new ArgumentException("At least one project plan file is required.", paramName);

        foreach (PlanFile file in files)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentException.ThrowIfNullOrWhiteSpace(file.FilePath);

            if (file.TotalLines <= 0)
                throw new ArgumentOutOfRangeException(paramName, "Total lines must be greater than zero.");
        }
    }
}
