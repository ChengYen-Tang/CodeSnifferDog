using CodeSnifferDog.Models.ProjectPlan;

namespace CodeSnifferDog.Modules.Tools.ProjectPlan;

/// <summary>
/// Stores project-plan task items for one planning workflow run.
/// </summary>
public interface ITaskItemStore : CodeSnifferDog.Workflows.Common.IRetrySafeAgentStore
{
    /// <summary>
    /// Adds one project-plan task item.
    /// </summary>
    ValueTask<StoredTaskItem> AddAsync(TaskItem taskItem, CancellationToken cancellationToken);

    /// <summary>
    /// Adds multiple project-plan task items.
    /// </summary>
    ValueTask<IReadOnlyList<StoredTaskItem>> AddRangeAsync(
        IReadOnlyList<TaskItem> taskItems,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one stored task item by identifier.
    /// </summary>
    ValueTask<StoredTaskItem> GetAsync(string projectPlanTaskItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one stored task item.
    /// </summary>
    ValueTask<bool> DeleteAsync(string projectPlanTaskItemId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists all stored task items for internal workflow aggregation and result creation.
    /// This operation is not exposed as an agent tool.
    /// </summary>
    ValueTask<IReadOnlyList<StoredTaskItem>> ListAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lists at most <paramref name="take"/> stored task items after <paramref name="cursor"/>.
    /// </summary>
    ValueTask<IReadOnlyList<StoredTaskItem>> ListPageAsync(
        string? cursor,
        int take,
        CancellationToken cancellationToken);

    /// <summary>
    /// Clears all stored task items.
    /// </summary>
    ValueTask ClearAsync(CancellationToken cancellationToken);
}
